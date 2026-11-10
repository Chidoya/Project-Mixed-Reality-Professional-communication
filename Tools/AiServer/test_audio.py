"""Talk to the scenario character with your microphone through /turn/audio.

    .venv\\Scripts\\python test_audio.py                 record from the default microphone
    .venv\\Scripts\\python test_audio.py --wav a.wav b.wav   send existing wav files instead
    options: --scenario angry_daughter   --no-play (do not play the replies)
             --pause 6 (with --wav: wait between files, like a real conversation)

Microphone mode: press Enter to start recording, Enter again to stop. Type q + Enter to quit.
The recording only exists in memory; it is sent to the local server and then dropped.
"""
from __future__ import annotations

import argparse
import base64
import io
import json
import sys
import threading
import time
import wave
from pathlib import Path

import httpx
import numpy as np
import sounddevice as sd

HERE = Path(__file__).resolve().parent
PORT = json.loads((HERE / "config.json").read_text(encoding="utf-8"))["port"]
BASE = f"http://127.0.0.1:{PORT}"
RATE = 16000


def check(r: httpx.Response) -> dict:
    if r.status_code == 503:
        sys.exit("The AI server cannot reach Ollama: " + r.json().get("detail", ""))
    if r.status_code >= 400:
        sys.exit(f"Server error {r.status_code}: {r.text}")
    return r.json()


def play_wav_b64(data: str, enabled: bool) -> None:
    if not enabled or not data:
        return
    with wave.open(io.BytesIO(base64.b64decode(data)), "rb") as w:
        rate = w.getframerate()
        audio = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2")
    sd.play(audio, rate)
    sd.wait()


def record_until_enter() -> bytes:
    """Record 16 kHz mono from the default microphone until Enter; return wav bytes."""
    chunks: list[np.ndarray] = []
    stop = threading.Event()

    def callback(indata, frames, t, status):  # noqa: ARG001
        chunks.append(indata.copy())

    with sd.InputStream(samplerate=RATE, channels=1, dtype="int16", callback=callback):
        print("  recording... press Enter to stop", flush=True)
        threading.Thread(target=lambda: (input(), stop.set()), daemon=True).start()
        stop.wait()
    audio = np.concatenate(chunks) if chunks else np.zeros((0, 1), dtype=np.int16)
    print(f"  recorded {len(audio) / RATE:.1f} s")
    return to_wav(audio)


def to_wav(audio: np.ndarray) -> bytes:
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(audio.astype("<i2").tobytes())
    return buf.getvalue()


def show_turn(t: dict, client_ms: int) -> None:
    b, a = t["before"], t["after"]
    tm = t["timings_ms"]
    print(f"  heard  : {t['transcript']!r}")
    print(f"  labels : {', '.join(t['labels']) or '-'}   quote: {t['evidence_quote']!r}")
    print(f"  anger  : {b['anger']} -> {a['anger']}   trust: {b['trust']} -> {a['trust']}   "
          f"band: {a['band']}   phase: {a['phase']}")
    print(f"SARAH [{t['emotion']}/{t['animation']}/{t['pose']}]: {t['reply']}")
    print(f"  timing : speech recognition {tm.get('stt_ms', 0)} ms | classification "
          f"{tm.get('classify_ms', 0)} ms | actor {tm.get('actor_ms', 0)} ms | speech "
          f"{tm.get('tts_ms', 0)} ms | server total {tm.get('request_total_ms', 0)} ms | "
          f"round trip {client_ms} ms")
    if t.get("tokens"):
        print("  tokens : " + "   ".join(f"{k} in {v['in']} / out {v['out']} ({v['calls']}x)"
                                     for k, v in t["tokens"].items()))
    for note in t.get("notes", []):
        print(f"  note   : {note}")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--scenario", default="angry_daughter")
    ap.add_argument("--wav", nargs="*", help="send these wav files instead of recording")
    ap.add_argument("--no-play", action="store_true")
    ap.add_argument("--pause", type=float, default=0, help="seconds to wait between wav files")
    args = ap.parse_args()
    play = not args.no_play

    with httpx.Client(base_url=BASE, timeout=300) as client:
        try:
            start = check(client.post("/session/start", json={"scenario_id": args.scenario}))
        except httpx.ConnectError:
            sys.exit(f"Cannot reach the AI server at {BASE}. Start it first: .venv\\Scripts\\python server.py")
        sid, op = start["session_id"], start["opening"]
        print(f"session {sid}")
        print(f"SARAH [{op['emotion']}]: {op['reply']}   (speech {op['tts_ms']} ms)")
        play_wav_b64(op["audio_wav_base64"], play)

        files = list(args.wav) if args.wav is not None else None
        try:
            while True:
                if files is not None:
                    if not files:
                        break
                    path = files.pop(0)
                    time.sleep(args.pause)  # like a student listening and thinking
                    print(f"\nYOU (file {path})")
                    wav = Path(path).read_bytes()
                else:
                    cmd = input("\nPress Enter to speak (q + Enter to quit): ").strip().lower()
                    if cmd == "q":
                        break
                    wav = record_until_enter()

                t = time.perf_counter()
                r = check(client.post("/turn/audio", data={"session_id": sid},
                                      files={"audio": ("speech.wav", wav, "audio/wav")}))
                client_ms = int((time.perf_counter() - t) * 1000)
                del wav
                if r.get("retry"):
                    print(f"  (not understood: {r.get('transcript')!r}, please say it again; "
                          f"speech recognition {r['timings_ms']['stt_ms']} ms)")
                    continue
                show_turn(r, client_ms)
                play_wav_b64(r["audio_wav_base64"], play)
                if r["ended"]:
                    print(f"*** conversation ended: {r['ended']} ***")
                    break
        except KeyboardInterrupt:
            print()
        end = check(client.post(f"/session/{sid}/end"))
        print(f"\nsaved {end['turns']} turns to {end['saved']} (end reason: {end['end_reason']})")


if __name__ == "__main__":
    main()
