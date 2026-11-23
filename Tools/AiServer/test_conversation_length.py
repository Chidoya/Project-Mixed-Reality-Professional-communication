"""Plays two text conversations with the disappointed patient (no Unity, no speech) to check how long they take:
a good one should need several turns before "resolved", a rude one should end soon with "escalated".
Needs Ollama running. Run from Tools/AiServer:  .venv/Scripts/python.exe test_conversation_length.py
"""
import json, sys
from pathlib import Path
HERE = Path(__file__).resolve().parent; sys.path.insert(0, str(HERE))
from engine import Engine
from llm_client import LlmClient
cfg = json.loads((HERE / "config.json").read_text(encoding="utf-8"))
eng = Engine(LlmClient(cfg), HERE / "scenarios")

def play(name, lines):
    s, opening = eng.start_session("disappointed_patient")
    print(f"=== {name}: tension {s.anger} trust {s.trust}")
    for i, text in enumerate(lines, 1):
        r = eng.run_turn(s, text)
        print(f"{i}. \"{text}\"\n   labels {r.get('labels')} unsafe {r.get('unsafe')} -> tension {s.anger} trust {s.trust} phase {s.phase} ended {s.ended}\n   patient ({r.get('emotion')}): {r.get('reply')}")
        if s.ended: break
    print(f"=== {name}: {i} turns, ended {s.ended}\n")

play("good", [
 "Hello, I'm Jalil, your nurse. How are you feeling today?",
 "I can see you're really disappointed and sad. That must be hard.",
 "What is it about the birthday that matters most to you?",
 "So missing her party feels like letting her down. Did I get that right?",
 "You have to stay because the infection still needs treatment, I'm sorry.",
 "I'm here for you, we'll get through this together.",
 "What if we set up a video call with your granddaughter during the party?",
 "And how about a short walk together in the corridor, just five minutes?",
 "Let's start with a short walk now, I'll help you up.",
])
play("playtest 2026-11-23: no escalation on harmless lines, then two dismissive lines escalate", [
 "Can you hear me this test?",
 "Right, alright man, listen.",
 "Okay, so listen, everything will be alright, okay?",
 "Don't worry, there will be other birthdays.",
])
play("rude", [
 "Hi.",
 "Stop whining, nobody cares about your stupid party.",
 "You are just lazy, get out of bed.",
])
