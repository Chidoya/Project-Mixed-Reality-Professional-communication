"""Makes the ambience of people talking in the hospital (Assets/_Project/Audio/Ambience/HospitalBabble_*.wav).

Not a real conversation: several Piper voices (the ones of Tools/AiServer, LibriVox based: kristin and norman) say short
everyday hospital sentences at the same time, each a little faster or slower and higher or lower, so it sounds like
different people (men and women: a wide pitch range). Each person walks closer and farther away (a slow change of
loudness), there are quieter moments, and short replies ("Okay, thanks.") mix with longer sentences, so it does not
sound like one repeating loop. The mix is muffled (low pass) and gets some room echo, so single words are hard to
follow. The end fades into the start, so each clip loops without a click. Several clips (other people, other timing)
are made, so the sources in the ward never play the same thing.

Run with the Python of the AI server (it has Piper and numpy):
    Tools/AiServer/.venv/Scripts/python.exe Tools/Ambience/make_babble.py
"""
import io
import random
import wave
from pathlib import Path

import numpy as np
from piper import PiperVoice, SynthesisConfig

ROOT = Path(__file__).resolve().parents[2]
VOICES = ROOT / "Tools" / "AiServer" / "models" / "piper"
OUT_DIR = ROOT / "Assets" / "_Project" / "Audio" / "Ambience"
RATE = 11025       # muffled anyway (low pass at 1.4 kHz), so a low rate keeps the files small
SECONDS = 60
STREAMS = 5
CLIPS = {"HospitalBabble_A.wav": 11, "HospitalBabble_B.wav": 23, "HospitalBabble_C.wav": 37}  # file: seed

SENTENCES = [
    "Could you check the chart for room twelve?", "I'll be right back with the forms.",
    "The coffee machine is broken again.", "Did the lab call back yet?", "She went home this morning.",
    "Can you sign here, please?", "We are waiting for the doctor.", "The results look fine so far.",
    "I'll bring an extra blanket.", "Visiting hours end at eight.", "He's been asking for you.",
    "Let me find a wheelchair.", "The pharmacy sent the wrong box.", "Is the meeting still at two?",
    "Thank you so much for your help.", "We'll take a look in a minute.", "The elevator is slow today.",
    "Have you had lunch yet?", "I think it's on the second floor.", "Just a moment, please.",
    "Her daughter is on the way.", "The night shift starts soon.", "Who has the keys to the cabinet?",
    "That's what the nurse told me.", "Please take a seat over there.", "I can't find my glasses.",
    "We moved him to another room.", "The parking was full again.", "It should be ready tomorrow.",
    "Let's go over it together.", "Okay, thanks.", "Yes, that's fine.", "Mm-hm.", "Sure, no problem.",
    "Where did you put it?", "I'll ask her.", "Not today, I think.", "See you tomorrow.", "Good morning!",
    "Is someone helping you?", "One moment.", "Room fourteen, on the left.", "That was quick.",
    "I don't know, maybe after lunch.", "He's sleeping now.", "Right, I'll call them.", "Oh, that's nice.",
]


def synth(voice: PiperVoice, text: str, length_scale: float) -> np.ndarray:
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        voice.synthesize_wav(text, w, syn_config=SynthesisConfig(length_scale=length_scale))
    buf.seek(0)
    with wave.open(buf, "rb") as w:
        rate = w.getframerate()
        data = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768.0
    return resample(data, rate, RATE)


def resample(x: np.ndarray, src: float, dst: float) -> np.ndarray:
    n = int(len(x) * dst / src)
    return np.interp(np.linspace(0, len(x) - 1, n), np.arange(len(x)), x).astype(np.float32)


def low_pass(x: np.ndarray, cutoff: float) -> np.ndarray:
    a = np.exp(-2 * np.pi * cutoff / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i, v in enumerate(x):
        acc = (1 - a) * v + a * acc
        y[i] = acc
    return y


def make(voices: list, seed: int, out: Path) -> None:
    random.seed(seed)
    total = SECONDS * RATE
    mix = np.zeros(total + RATE * 4, dtype=np.float32)
    for stream in range(STREAMS):
        voice = voices[stream % len(voices)]
        pitch = random.uniform(0.78, 1.28)   # a deeper or higher voice (resampled)
        speed = random.uniform(0.85, 1.2)
        gain = random.uniform(0.3, 0.8)       # near and far people
        # This person walks closer and farther: loudness goes slowly up and down.
        period = random.uniform(9.0, 25.0)
        phase = random.uniform(0, 2 * np.pi)
        t = random.randint(0, RATE * 5)
        while t < total:
            line = synth(voice, random.choice(SENTENCES), speed * random.uniform(0.95, 1.05))
            line = resample(line, RATE, RATE / pitch)
            end = min(len(mix), t + len(line))
            times = np.arange(t, end) / RATE
            walk = 0.35 + 0.65 * (0.5 + 0.5 * np.sin(2 * np.pi * times / period + phase))
            mix[t:end] += line[: end - t] * gain * walk
            t = end + random.randint(int(RATE * 0.6), int(RATE * 5.0))  # a pause, then the next sentence

    # Loop: the part after the end fades into the start.
    tail = mix[total: total + RATE * 4]
    fade = np.linspace(1.0, 0.0, len(tail), dtype=np.float32)
    mix = mix[:total]
    mix[: len(tail)] = mix[: len(tail)] * (1 - fade) + tail * fade

    mix = low_pass(mix, 1400.0)                      # muffled, as through a room
    room = mix.copy()
    for delay, amount in ((0.031, 0.35), (0.067, 0.25), (0.113, 0.18), (0.171, 0.12)):
        d = int(delay * RATE)
        room += np.roll(mix, d) * amount             # np.roll wraps around: the echo loops too
    room /= np.max(np.abs(room)) + 1e-6
    room *= 0.5                                      # leave room; the volume is set on the AudioSource

    out.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(out), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((room * 32767).astype(np.int16).tobytes())
    print(f"written {out} ({SECONDS} s, {RATE} Hz mono)")


def main() -> None:
    voices = [PiperVoice.load(VOICES / f"{name}.onnx") for name in ("en_US-kristin-medium", "en_US-norman-medium")]
    for name, seed in CLIPS.items():
        make(voices, seed, OUT_DIR / name)


if __name__ == "__main__":
    main()
