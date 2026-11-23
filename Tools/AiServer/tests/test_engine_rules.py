"""Fast checks of the optional engine rules, without Ollama (a fake model answers).

    .venv\\Scripts\\python tests\\test_engine_rules.py

Checks: every scenario still loads (Liam's ones too) and sends meta; max_labels_per_turn keeps critical and costly
labels first; drop_if_any drops a label (a walk is not a small step); min_turns holds a phase; the defaults (no
max_labels_per_turn, no min_turns) behave as before; a false discharge promise moves the meters before it ends.
"""
from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HERE))

from engine import Engine  # noqa: E402

FAILED: list[str] = []


def check(ok: bool, what: str) -> None:
    print(("PASS " if ok else "FAIL ") + what)
    if not ok:
        FAILED.append(what)


class FakeLlm:
    """Classifier: the labels queued for the next sentence. Actor: a new neutral line every time."""

    def __init__(self):
        self.next_labels: list[str] = []
        self.n = 0

    def chat_json(self, system, messages, schema, role="actor"):
        if role == "classifier":
            text = messages[0]["content"].split("NEW sentence:\n")[-1].strip().strip('"')
            labels, self.next_labels = self.next_labels, []
            return {"labels": labels, "evidence_quote": text if labels else "", "off_topic": False, "unsafe": False}
        self.n += 1
        words = ["Hm", "Maybe", "Okay", "Well", "I see", "Right", "Fine", "Perhaps", "Alright", "Mm", "Yes", "Sure",
                 "Huh", "Oh", "So", "True"]
        return {"reply": f"{words[self.n % len(words)]}, line number {'abcdefghijklmnopqrstuvwxyz'[self.n % 26]}.",
                "emotion": "Sad", "animation": "Sad", "pose": "Unchanged", "facts_used": []}

    def last_usage(self):
        return {}


def main() -> None:
    llm = FakeLlm()
    eng = Engine(llm, HERE / "scenarios")

    # 1. every scenario loads and sends meta
    for path in sorted((HERE / "scenarios").glob("*.json")):
        s, opening = eng.start_session(path.stem)
        meta = opening.get("meta", {})
        check(bool(meta.get("progress", {}).get("phases")), f"{path.stem} loads and sends meta.progress")
        check(isinstance(meta.get("guide"), list) and "case" in meta, f"{path.stem} sends meta.case and meta.guide")

    # 2. defaults: Liam's angry_daughter has no max_labels_per_turn and no min_turns
    sc = eng.scenario("angry_daughter")
    check("max_labels_per_turn" not in sc.rules and all("min_turns" not in p for p in sc.phases),
          "angry_daughter uses the defaults (no label limit, no min_turns)")
    s, _ = eng.start_session("angry_daughter")
    many = [n for n in ("named_emotion", "asked_open_question", "summarized", "apologized_sincerely") if n in sc.labels]
    llm.next_labels = list(many)
    r = eng.run_turn(s, "I can see you're worried, what happened? So nobody told you, I'm sorry.")
    check(r["labels"] == many, f"without max_labels_per_turn every label counts ({r['labels']})")

    # 3. disappointed_patient: max 2 labels, costly first
    dp = eng.scenario("disappointed_patient")
    check(dp.rules.get("max_labels_per_turn") == 2, "disappointed_patient keeps at most 2 labels per turn")
    s, opening = eng.start_session("disappointed_patient")
    llm.next_labels = ["named_emotion", "asked_open_question", "summarized", "dismissive"]
    r = eng.run_turn(s, "I see you're sad, what matters to you? So you miss her. Anyway, there will be other birthdays.")
    check(r["labels"] == ["named_emotion", "dismissive"], f"max 2: the costly label stays ({r['labels']})")

    # 4. drop_if_any: a walk is not a small step
    s, _ = eng.start_session("disappointed_patient")
    llm.next_labels = ["motivated_small_step"]
    r = eng.run_turn(s, "How about a short walk together in the corridor?")
    check(r["labels"] == [], f"a walking suggestion does not score ({r['labels']})")
    llm.next_labels = ["motivated_small_step"]
    r = eng.run_turn(s, "Shall I help you sit up a little and get you something to drink?")
    check(r["labels"] == ["motivated_small_step"], f"a small step in bed scores ({r['labels']})")

    # 5. min_turns: B1 needs 3 turns even when the condition is met at once
    s, _ = eng.start_session("disappointed_patient")
    s.anger, s.trust = 40, 60
    llm.next_labels = ["named_emotion"]
    eng.run_turn(s, "I can see how sad you are.")
    check(s.phase == "B1_withdrawn", f"turn 1: still B1 because of min_turns ({s.phase})")
    eng.run_turn(s, "Mm.")
    check(s.phase == "B1_withdrawn", f"turn 2: still B1 ({s.phase})")
    llm.next_labels = ["asked_open_question"]
    eng.run_turn(s, "What would help you today?")
    check(s.phase == "B2_opening_up", f"turn 3: B1 -> B2 ({s.phase})")

    # 6. false discharge promise: the meters move, then the scenario ends
    s, _ = eng.start_session("disappointed_patient")
    t0, tr0 = s.anger, s.trust
    llm.next_labels = ["false_discharge_promise"]
    r = eng.run_turn(s, "I'll ask the doctor to discharge you early so you can go to the party.")
    check(r["ended"] == "critical_error" and s.anger > t0 and s.trust < tr0,
          f"false discharge promise: tension {t0}->{s.anger}, trust {tr0}->{s.trust}, ended {r['ended']}")

    check(r["emotion"] != "Happy", f"no happy face after a false discharge promise ({r['emotion']})")
    s, _ = eng.start_session("disappointed_patient")
    s.anger = 10  # LOW band: Happy is allowed there
    s.last_labels = ["dismissive"]
    out = eng._finish(s, {"turn": 1, "labels": [], "tokens": {}, "before": s.snapshot()}, {}, 0.0,
                      reply="Fine.", emotion="Happy", fallback=True)
    check(out["emotion"] != "Happy", f"a fixed happy line after lost trust is not happy ({out['emotion']})")

    # 6b. upgrade_to: a false promise about going home is the critical one
    s, _ = eng.start_session("disappointed_patient")
    llm.next_labels = ["false_promise"]
    r = eng.run_turn(s, "Don't worry, I'll ask the doctor to discharge you early so you can go to the party.")
    check(r["labels"] == ["false_discharge_promise"] and r["ended"] == "critical_error",
          f"false_promise about discharge is upgraded to the critical label ({r['labels']}, {r['ended']})")
    s, _ = eng.start_session("disappointed_patient")
    llm.next_labels = ["false_promise"]
    r = eng.run_turn(s, "I promise everything will be fine.")
    check(r["labels"] == ["false_promise"] and r["ended"] is None, f"an ordinary false promise stays ({r['labels']})")

    # 7. the doctor's visit is a timeline, not a false promise (the label exists)
    check("gave_concrete_timeline" in dp.labels and dp.labels["gave_concrete_timeline"]["trust"] > 0,
          "gave_concrete_timeline is a positive label in disappointed_patient")

    print(f"\n{'ALL PASSED' if not FAILED else f'{len(FAILED)} FAILED'}")
    sys.exit(1 if FAILED else 0)


if __name__ == "__main__":
    main()
