"""Local speech: speech-to-text (faster-whisper) and text-to-speech (Piper).

Audio is handled in memory only. Incoming audio is never written to disk.
"""
from __future__ import annotations

import ctypes
import io
import logging
import os
import sys
import threading
import time
import wave
from pathlib import Path
from typing import Any

import numpy as np

log = logging.getLogger("aiserver")
logging.getLogger("faster_whisper").setLevel(logging.WARNING)
os.environ.setdefault("HF_HUB_DISABLE_SYMLINKS_WARNING", "1")  # Windows without developer mode

WHISPER_RATE = 16000

# DLLs that CTranslate2 (faster-whisper) needs for CUDA on Windows. If one is missing, the
# process can crash hard during transcription instead of raising a Python error, so we check
# them before we even try the GPU.
CUDA_DLLS_WINDOWS = ("cublas64_12.dll", "cublasLt64_12.dll", "cudnn64_9.dll", "cudnn_ops64_9.dll")


def _add_nvidia_dll_dirs() -> None:
    """Make the DLLs from the nvidia-cublas-cu12 / nvidia-cudnn-cu12 pip packages findable."""
    try:
        import nvidia  # namespace package from the pip packages (requirements-gpu.txt)
    except ImportError:
        return
    for base in getattr(nvidia, "__path__", []):
        for bin_dir in Path(base).glob("*/bin"):
            if str(bin_dir) not in os.environ.get("PATH", ""):
                os.environ["PATH"] = str(bin_dir) + os.pathsep + os.environ.get("PATH", "")
                os.add_dll_directory(str(bin_dir))


def _missing_cuda_dlls() -> list[str]:
    if sys.platform != "win32":
        return []
    _add_nvidia_dll_dirs()
    missing = []
    for dll in CUDA_DLLS_WINDOWS:
        try:
            ctypes.WinDLL(dll)
        except OSError:
            missing.append(dll)
    return missing


def read_wav_mono_16k(data: bytes) -> np.ndarray:
    """Decode a PCM wav (bytes) to float32 mono at 16 kHz."""
    with wave.open(io.BytesIO(data), "rb") as w:
        channels, width, rate = w.getnchannels(), w.getsampwidth(), w.getframerate()
        frames = w.readframes(w.getnframes())
    if width == 2:
        audio = np.frombuffer(frames, dtype="<i2").astype(np.float32) / 32768.0
    elif width == 4:
        audio = np.frombuffer(frames, dtype="<i4").astype(np.float32) / 2147483648.0
    elif width == 1:
        audio = (np.frombuffer(frames, dtype=np.uint8).astype(np.float32) - 128.0) / 128.0
    else:
        raise ValueError(f"unsupported wav sample width {width}")
    if channels > 1:
        audio = audio.reshape(-1, channels).mean(axis=1)
    if rate != WHISPER_RATE and len(audio):
        n = int(round(len(audio) * WHISPER_RATE / rate))
        audio = np.interp(np.linspace(0, len(audio) - 1, n), np.arange(len(audio)), audio).astype(np.float32)
    return audio


class SpeechToText:
    def __init__(self, config: dict[str, Any], models_dir: Path):
        from faster_whisper import WhisperModel

        self.model_name = config.get("stt_model", "small.en")
        self.language = config.get("stt_language", "en")
        self.beam_size = config.get("stt_beam_size", 5)
        root = str(models_dir / "whisper")
        self._lock = threading.Lock()
        self.device = "cpu"
        self.compute_type = "int8"
        self.fallback_reason = ""

        model = None
        if config.get("stt_try_gpu", True):
            missing = _missing_cuda_dlls()
            if missing:
                self.fallback_reason = f"CUDA/cuDNN files missing: {', '.join(missing)}"
            else:
                try:
                    model = WhisperModel(self.model_name, device="cuda", compute_type="float16",
                                         download_root=root)
                    # run the encoder once: missing libraries often only show up here
                    list(model.transcribe(np.zeros(WHISPER_RATE, dtype=np.float32), language=self.language)[0])
                    self.device, self.compute_type = "cuda", "float16"
                except Exception as exc:  # noqa: BLE001 - any CUDA problem means: use the CPU
                    self.fallback_reason = f"GPU failed: {exc}"
                    model = None
        if model is None:
            if self.fallback_reason:
                log.warning("Speech recognition: %s. Falling back to CPU (int8).", self.fallback_reason)
            model = WhisperModel(self.model_name, device="cpu", compute_type="int8", download_root=root,
                                 cpu_threads=config.get("stt_cpu_threads", 8))
        self.model = model
        log.info("Speech recognition ready: %s on %s (%s)", self.model_name, self.device, self.compute_type)

    def transcribe(self, wav_bytes: bytes) -> str:
        audio = read_wav_mono_16k(wav_bytes)
        if len(audio) < WHISPER_RATE // 4:  # shorter than 0.25 s
            return ""
        with self._lock:
            segments, _ = self.model.transcribe(
                audio, language=self.language, vad_filter=True, beam_size=self.beam_size,
                condition_on_previous_text=False,
            )
            return " ".join(s.text.strip() for s in segments).strip()


class TextToSpeech:
    """Piper voices, one per character. The default voice comes from config.json; a scenario can name its own
    ("voice": {"piper": ..., "length_scale_by_band": {...}}). Missing voices are downloaded once."""

    def __init__(self, config: dict[str, Any], models_dir: Path):
        self.voice_name = config.get("tts_voice", "en_US-amy-medium")
        self.length_scale_by_band: dict[str, float] = config.get("tts_length_scale_by_band", {})
        self.voices_dir = models_dir / "piper"
        self.voices_dir.mkdir(parents=True, exist_ok=True)
        self._voices: dict[str, Any] = {}
        self._lock = threading.Lock()
        self.voice = self.load(self.voice_name)

    def load(self, name: str):
        """Load (and if needed download) a Piper voice; cached."""
        from piper import PiperVoice
        from piper.download_voices import download_voice

        if name in self._voices:
            return self._voices[name]
        model_path = self.voices_dir / f"{name}.onnx"
        if not model_path.exists() or not (self.voices_dir / f"{name}.onnx.json").exists():
            log.info("Downloading Piper voice %s ...", name)
            download_voice(name, self.voices_dir)
        voice = PiperVoice.load(model_path)
        self._voices[name] = voice
        log.info("Text to speech ready: Piper %s (%d Hz)", name, voice.config.sample_rate)
        return voice

    def synthesize(self, text: str, band: str | None = None, voice: str | None = None,
                   length_scale_by_band: dict[str, float] | None = None) -> bytes:
        """Return the spoken text as wav bytes (mono, 16-bit, the voice's sample rate)."""
        from piper import SynthesisConfig

        scales = length_scale_by_band if length_scale_by_band is not None else self.length_scale_by_band
        length_scale = scales.get(band or "", 1.0)
        buf = io.BytesIO()
        with self._lock:
            piper_voice = self.load(voice or self.voice_name)
            with wave.open(buf, "wb") as w:
                piper_voice.synthesize_wav(text, w, syn_config=SynthesisConfig(length_scale=length_scale))
        return buf.getvalue()


def timed(fn, *args, **kwargs) -> tuple[Any, int]:
    t = time.perf_counter()
    out = fn(*args, **kwargs)
    return out, int((time.perf_counter() - t) * 1000)
