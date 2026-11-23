"""Renders every line of the head doctor in the debrief (and the pager, the footsteps and the granddaughter) to WAV.

No live TTS during the debrief: all doctor lines are fixed texts, so they are rendered once, here.
- Fixed lines and the reveal lines: Assets/Hospital/Data/Debrief/DebriefLines_Scenario1.json
- Feedback per rubric item and score (feedback_en), better phrases and the critical error (critical_feedback_en):
  Tools/AiServer/scenarios/<scenario_id>.json
Output: Assets/Hospital/Audio/Debrief/Doctor/<key>.wav (key = a stable name, e.g. s1_item2_score0) and a manifest
Assets/Hospital/Data/Debrief/DoctorVoice_<prefix>.json ({key: text}) that Unity imports into the voice library.

Run from Tools/AiServer:  .venv/Scripts/python.exe tools/gen_doctor_lines.py [--only-missing]
Then in Unity: ProCommVR > Liam > Debrief > Import Doctor Lines.
"""
from __future__ import annotations

import argparse
import io
import json
import math
import random
import sys
import wave
from pathlib import Path

import numpy as np

HERE = Path(__file__).resolve().parent
SERVER = HERE.parent
PROJECT = SERVER.parent.parent
LINES = PROJECT / "Assets/Hospital/Data/Debrief/DebriefLines_Scenario1.json"
OUT = PROJECT / "Assets/Hospital/Audio/Debrief/Doctor"
SFX = PROJECT / "Assets/Hospital/Audio/Debrief"
EMOTIONS = ["Joy", "Anger", "Fear", "Sadness"]

sys.path.insert(0, str(SERVER))


def load_voice(name: str):
    from piper import PiperVoice
    path = SERVER / "models" / "piper" / f"{name}.onnx"
    if not path.exists():
        raise SystemExit(f"Piper voice {path} is missing (the AI server downloads its voices into models/piper).")
    return PiperVoice.load(str(path))


def speak(voice, text: str, length_scale: float) -> tuple[np.ndarray, int]:
    from piper import SynthesisConfig
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        voice.synthesize_wav(text, w, syn_config=SynthesisConfig(length_scale=length_scale))
    buf.seek(0)
    with wave.open(buf, "rb") as r:
        rate = r.getframerate()
        audio = np.frombuffer(r.readframes(r.getnframes()), dtype="<i2").astype(np.float32) / 32768.0
    return audio, rate


TARGET_RATE = 16000  # speech stays clear, the files stay small (no Git LFS in this repo yet)


def write(path: Path, audio: np.ndarray, rate: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if rate > TARGET_RATE:
        n = int(round(len(audio) * TARGET_RATE / rate))
        audio = np.interp(np.linspace(0, len(audio) - 1, n), np.arange(len(audio)), audio).astype(np.float32)
        rate = TARGET_RATE
    pcm = (np.clip(audio, -1.0, 1.0) * 32767.0).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm.tobytes())


def trim(audio: np.ndarray, rate: int, pad: float = 0.08) -> np.ndarray:
    """Cuts the silence Piper leaves at the start and the end (keeps a short pad)."""
    loud = np.where(np.abs(audio) > 0.01)[0]
    if len(loud) == 0:
        return audio
    a = max(0, loud[0] - int(pad * rate))
    b = min(len(audio), loud[-1] + int(pad * rate))
    return audio[a:b]


def pager_sound(voice_audio: np.ndarray, rate: int) -> np.ndarray:
    """Three short pager beeps, then the line as through a small speaker (band pass)."""
    t = np.arange(int(0.09 * rate)) / rate
    beep = 0.35 * np.sin(2 * math.pi * 2900 * t) * np.minimum(1, np.minimum(t, t[::-1]) * 200)
    gap = np.zeros(int(0.07 * rate), np.float32)
    beeps = np.concatenate([beep, gap, beep, gap, beep, np.zeros(int(0.15 * rate), np.float32)])
    # small speaker: high pass ~300 Hz, low pass ~3.4 kHz (one-pole filters)
    x = voice_audio.copy()
    hp = np.zeros_like(x)
    a = math.exp(-2 * math.pi * 300 / rate)
    for i in range(1, len(x)):
        hp[i] = a * (hp[i - 1] + x[i] - x[i - 1])
    lp = np.zeros_like(hp)
    b = math.exp(-2 * math.pi * 3400 / rate)
    for i in range(1, len(hp)):
        lp[i] = (1 - b) * hp[i] + b * lp[i - 1]
    lp *= 0.8 / max(1e-4, np.max(np.abs(lp)))
    return np.concatenate([beeps, lp]).astype(np.float32)


def footsteps(rate: int = 22050, seconds: float = 4.0) -> np.ndarray:
    """Soft footsteps on a hospital floor: low filtered thumps with a little scuff, about 1.8 steps per second."""
    rng = random.Random(7)
    out = np.zeros(int(seconds * rate), np.float32)
    step = 0.15
    while step < seconds - 0.3:
        n = int(0.16 * rate)
        noise = np.array([rng.uniform(-1, 1) for _ in range(n)], np.float32)
        env = np.exp(-np.arange(n) / (0.025 * rate))
        lp = np.zeros(n, np.float32)
        for i in range(1, n):
            lp[i] = 0.12 * noise[i] + 0.88 * lp[i - 1]
        thump = 0.9 * np.sin(2 * math.pi * 70 * np.arange(n) / rate) * np.exp(-np.arange(n) / (0.02 * rate))
        sound = (lp * 3.0 + thump) * env * rng.uniform(0.7, 1.0)
        s = int(step * rate)
        out[s:s + n] += sound[: len(out) - s]
        step += rng.uniform(0.5, 0.6)
    fade = np.minimum(1, np.minimum(np.arange(len(out)), np.arange(len(out))[::-1]) / (0.3 * rate))
    out *= fade
    return (0.5 * out / max(1e-4, np.max(np.abs(out)))).astype(np.float32)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--only-missing", action="store_true", help="render only WAVs that do not exist yet")
    args = parser.parse_args()

    cfg = json.loads(LINES.read_text(encoding="utf-8"))
    prefix = cfg["scenario_prefix"]
    scenario = json.loads((SERVER / "scenarios" / f"{cfg['scenario_id']}.json").read_text(encoding="utf-8"))
    lines: dict[str, str] = {}

    for key, text in cfg["lines"].items():
        if key == "consequence_good_granddaughter":
            continue
        lines[key] = text
    rv = cfg["reveal"]
    for picked in EMOTIONS:
        for real in EMOTIONS:
            template = rv["same"] if picked == real else rv["different"]
            lines[f"reveal_{picked}_{real}"] = template.format(picked=rv["picked_words"][picked], real=rv["words"][real])
        lines[f"reveal_{picked}_None"] = rv["neutral"].format(picked=rv["picked_words"][picked])
    for item in scenario["rubric"]:
        for score, text in item.get("feedback_en", {}).items():
            lines[f"{prefix}_item{item['id']}_score{score}"] = text
        lines[f"{prefix}_item{item['id']}_better"] = item["better_phrase"]
    for label in scenario["labels"]:
        if label.get("critical") and label.get("critical_feedback_en"):
            lines[f"{prefix}_critical_{label['name']}"] = label["critical_feedback_en"]

    doctor = load_voice(cfg["doctor_voice"])
    scale = float(cfg.get("doctor_length_scale", 1.0))
    made = 0
    for key, text in lines.items():
        path = OUT / f"{key}.wav"
        if args.only_missing and path.exists():
            continue
        audio, rate = speak(doctor, text, scale)
        audio = trim(audio, rate)
        if key == "pager":
            write(SFX / "Pager.wav", pager_sound(audio, rate), rate)
            continue
        write(path, audio, rate)
        made += 1
    lines.pop("pager", None)

    # the granddaughter (phase 6): a different, younger voice, a little faster and higher
    girl_text = cfg["lines"].get("consequence_good_granddaughter")
    if girl_text:
        girl = load_voice("en_US-amy-medium")
        audio, rate = speak(girl, girl_text, 0.9)
        audio = trim(audio, rate)
        idx = np.arange(0, len(audio) - 1, 1.12)  # about two semitones higher
        audio = np.interp(idx, np.arange(len(audio)), audio).astype(np.float32)
        write(SFX / "Granddaughter.wav", audio, rate)

    write(SFX / "Footsteps.wav", footsteps(), 22050)

    manifest = PROJECT / f"Assets/Hospital/Data/Debrief/DoctorVoice_{prefix}.json"
    manifest.write_text(json.dumps({"voice": cfg["doctor_voice"], "lines": lines}, indent=2, ensure_ascii=False) + "\n",
                        encoding="utf-8")
    print(f"{made} doctor lines rendered ({len(lines)} keys), pager, footsteps, granddaughter. Manifest: {manifest}")


if __name__ == "__main__":
    main()
