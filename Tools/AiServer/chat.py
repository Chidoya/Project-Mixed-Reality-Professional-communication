"""Terminal chat with a scenario character through the AI server.

    .venv\\Scripts\\python chat.py [scenario_id]

Type in English. Empty line = silence (no_response). /quit ends and saves the session.
Input can also be piped (one line per turn), e.g. for scripted tests.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

import httpx

HERE = Path(__file__).resolve().parent
PORT = json.loads((HERE / "config.json").read_text(encoding="utf-8"))["port"]
BASE = f"http://127.0.0.1:{PORT}"


def post(client: httpx.Client, path: str, body: dict | None = None) -> dict:
    try:
        r = client.post(BASE + path, json=body or {})
    except httpx.ConnectError:
        sys.exit(f"Cannot reach the AI server at {BASE}. Start it first: .venv\\Scripts\\python server.py")
    if r.status_code == 503:
        sys.exit("The AI server cannot reach Ollama: " + r.json().get("detail", ""))
    r.raise_for_status()
    return r.json()


def show_turn(t: dict) -> None:
    b, a = t["before"], t["after"]
    flags = [f for f in ("off_topic", "unsafe", "fallback_used") if t.get(f)]
    print(f"  labels : {', '.join(t['labels']) or '-'}" + (f"   [{', '.join(flags)}]" if flags else ""))
    print(f"  quote  : {t['evidence_quote']!r}")
    print(f"  anger  : {b['anger']} -> {a['anger']}   trust: {b['trust']} -> {a['trust']}")
    print(f"  band   : {b['band']} -> {a['band']}   phase: {b['phase']} -> {a['phase']}")
    for note in t.get("notes", []):
        print(f"  note   : {note}")
    tm = t["timings_ms"]
    print("  time   : " + ", ".join(f"{k} {v}" for k, v in tm.items()) + f"   (t+{t['elapsed_s']}s)")
    print(f"SARAH [{t['emotion']}/{t['animation']}/{t['pose']}]: {t['reply']}")
    if t.get("facts_used"):
        print(f"  facts  : {t['facts_used']}")


def main() -> None:
    scenario_id = sys.argv[1] if len(sys.argv) > 1 else "angry_daughter"
    piped = not sys.stdin.isatty()
    with httpx.Client(timeout=300) as client:
        start = post(client, "/session/start", {"scenario_id": scenario_id})
        sid, op = start["session_id"], start["opening"]
        print(f"session {sid}  (type /quit to stop)\n")
        print(f"  anger {op['anger']}  trust {op['trust']}  band {op['band']}")
        print(f"SARAH [{op['emotion']}/{op['animation']}]: {op['reply']}\n")
        try:
            while True:
                try:
                    text = input("YOU: ")
                except EOFError:
                    break
                if piped:
                    print(text)
                if text.strip().lower() in ("/quit", "/exit"):
                    break
                turn = post(client, "/turn/text", {"session_id": sid, "text": text})
                show_turn(turn)
                print()
                if turn["ended"]:
                    print(f"*** conversation ended: {turn['ended']} ***")
                    break
        except KeyboardInterrupt:
            print()
        end = post(client, f"/session/{sid}/end")
        print(f"saved {end['turns']} turns to {end['saved']} (end reason: {end['end_reason']})")


if __name__ == "__main__":
    main()
