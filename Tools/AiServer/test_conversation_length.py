"""Plays text conversations with the disappointed patient (no Unity, no speech) to check how long they take:
a good one should need 10 to 14 turns before "resolved", a rude one should end soon with "escalated", and a false
discharge promise should move the meters and end at once.
Needs Ollama running. Run from Tools/AiServer:  .venv/Scripts/python.exe test_conversation_length.py
Exit code 1 when one of these expectations fails.
"""
import json, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent; sys.path.insert(0, str(HERE))
from engine import Engine
from llm_client import LlmClient
cfg = json.loads((HERE / "config.json").read_text(encoding="utf-8"))
eng = Engine(LlmClient(cfg), HERE / "scenarios")
FAILED = []

def play(name, lines):
    s, opening = eng.start_session("disappointed_patient")
    print(f"=== {name}: tension {s.anger} trust {s.trust}")
    i = 0
    for i, text in enumerate(lines, 1):
        r = eng.run_turn(s, text)
        print(f"{i}. \"{text}\"\n   labels {r.get('labels')} unsafe {r.get('unsafe')} -> tension {s.anger} trust {s.trust} phase {s.phase} ended {s.ended}\n   patient ({r.get('emotion')}): {r.get('reply')}")
        for n in r.get("notes", []):
            if "phase" in n or "dropped" in n or "max" in n:
                print(f"   note: {n}")
        if s.ended: break
    print(f"=== {name}: {i} turns, ended {s.ended}\n")
    return s, i

def expect(ok, what):
    print(("PASS " if ok else "FAIL ") + what)
    if not ok: FAILED.append(what)

# A good nurse: introduces, names the feeling, asks, summarizes, explains, offers support, an alternative and a small
# step in bed. More lines than needed: the conversation should end on its own between turn 10 and 14.
GOOD = [
 "Hello, I'm Jalil, one of the nurses on this ward. How are you feeling today?",
 "I can see you're really sad and disappointed. That must be hard.",
 "It sounds like today hurts a lot. What is on your mind?",
 "What is it about the birthday that matters most to you?",
 "So missing her party feels like letting her down. Did I get that right?",
 "You have to stay because the infection in your leg still needs the antibiotics through the drip. Only the doctor decides about going home.",
 "The doctor comes by this evening, you can ask her about it then.",
 "I'm here for you. We'll find something that helps you today.",
 "What if we set up a video call with your granddaughter during the party?",
 "And shall I help you sit up a little, so you're ready for the call?",
 "We could also get you something to drink first. How does that sound?",
 "Then let's do it: I help you sit up and we call her this afternoon.",
 "Great. I'll bring the tablet and we'll call her together.",
 "I'm glad you want to try. Shall we sit you up now?",
 "Okay, I'll help you sit up and then we call her.",
 "Here we go, slowly. Tell me if it hurts.",
]
s, n = play("good", GOOD)
expect(s.ended == "resolved" and 10 <= n <= 14, f"good conversation resolves in 10-14 turns ({n} turns, {s.ended})")

s, n = play("playtest 2026-11-23: harmless lines do not escalate, dismissive ones may", [
 "Can you hear me this test?",
 "Right, alright man, listen.",
 "Okay, so listen, everything will be alright, okay?",
 "Don't worry, there will be other birthdays.",
])
expect(n >= 3, f"the two harmless lines do not end the conversation (ended after {n} turns, {s.ended})")

s, n = play("rude", [
 "Hi.",
 "Stop whining, nobody cares about your stupid party.",
 "You are just lazy, get out of bed.",
 "Honestly, you're pathetic.",
])
expect(s.ended == "escalated", f"a rude conversation escalates ({n} turns, {s.ended})")

s, n = play("false discharge promise", [
 "Hello, I'm Jalil, your nurse.",
 "Don't worry, I'll ask the doctor to discharge you early so you can go to the party.",
])
expect(s.ended == "critical_error", f"a false discharge promise ends the conversation ({s.ended})")
t = s.turns[-1]
expect(t["after"]["trust"] < t["before"]["trust"] and t["after"]["anger"] > t["before"]["anger"],
       f"the meters move before the black screen (trust {t['before']['trust']}->{t['after']['trust']}, tension {t['before']['anger']}->{t['after']['anger']})")

s, n = play("walking suggestion", [
 "Hello, I'm Jalil, your nurse.",
 "How about a short walk together in the corridor?",
])
good = {"motivated_small_step", "offered_alternative_connection", "offered_support"}
expect(not good & set(s.turns[-1]["labels"]), f"a walking suggestion does not score ({s.turns[-1]['labels']})")

print(f"\n{'ALL PASSED' if not FAILED else str(len(FAILED)) + ' FAILED: ' + '; '.join(FAILED)}")
sys.exit(1 if FAILED else 0)
