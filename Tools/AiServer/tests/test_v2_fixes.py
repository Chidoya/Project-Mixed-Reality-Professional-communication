"""Checks of the v2 fixes from the playtest, without Ollama (a fake model answers).

    .venv\\Scripts\\python tests\\test_v2_fixes.py

1. "I may not let you leave, only the doctor may" is not a discharge promise (negation, doctor, model check).
2. The nurse's name: from Unity or from "I'm Jalil"; the patient never calls the nurse X or another name.
3. The feeling of the debrief question is what he felt MOST, and "Surprised" is no feeling.
4. Trust and mood are part of the score.
"""
from __future__ import annotations

import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HERE))

from debrief import felt_emotion, run_debrief  # noqa: E402
from engine import Engine, _nurse_names, defers_to_doctor  # noqa: E402

FAILED: list[str] = []


def check(ok: bool, what: str) -> None:
    print(("PASS " if ok else "FAIL ") + what)
    if not ok:
        FAILED.append(what)


class FakeLlm:
    def __init__(self):
        self.next_labels: list[str] = []
        self.verdict = True
        self.replies: list[str] = []
        self.emotions: list[str] = []
        self.n = 0

    def chat_json(self, system, messages, schema, role="actor", **_):
        props = schema.get("properties", {})
        if "nurse_promises_it" in props:
            return {"nurse_promises_it": self.verdict}
        if role == "classifier":
            text = messages[0]["content"].split("NEW sentence:\n")[-1].strip().strip('"')
            labels, self.next_labels = self.next_labels, []
            return {"labels": labels, "evidence_quote": text if labels else "", "off_topic": False, "unsafe": False}
        self.n += 1
        reply = self.replies.pop(0) if self.replies else f"Hm, line number {'abcdefghijklmnopqrstuvwxyz'[self.n % 26]}."
        emotion = self.emotions.pop(0) if self.emotions else "Sad"
        return {"reply": reply, "emotion": emotion, "animation": "Sad", "pose": "Unchanged", "facts_used": []}

    def last_usage(self):
        return {}


def main() -> None:
    llm = FakeLlm()
    eng = Engine(llm, HERE / "scenarios")

    # 1. leaving / discharge
    for text in ["I may not let you leave, only the doctor may decide that.",
                 "I can't let you go home, only the doctor can.",
                 "I'm not allowed to discharge you, that's the doctor's decision."]:
        s, _ = eng.start_session("disappointed_patient")
        llm.next_labels = ["false_discharge_promise"]
        r = eng.run_turn(s, text)
        check(r["ended"] is None and "false_discharge_promise" not in r["labels"]
              and "deferred_to_doctor" in r["labels"], f"not a promise: {text!r} -> {r['labels']}, ended {r['ended']}")

    s, _ = eng.start_session("disappointed_patient")
    llm.next_labels = ["false_discharge_promise"]
    llm.verdict = False
    r = eng.run_turn(s, "I don't know yet, maybe we can let you go to the party later.")
    check(r["ended"] is None, f"the model check can stop a wrong critical label ({r['labels']})")
    llm.verdict = True

    s, _ = eng.start_session("disappointed_patient")
    llm.next_labels = ["false_discharge_promise"]
    r = eng.run_turn(s, "I'll make sure we discharge you early so you can go to the party.")
    check(r["ended"] == "critical_error", "a real discharge promise still ends the scenario")
    check(r["emotion"] == "Sad", f"his reaction to it is sad, not surprised ({r['emotion']})")

    llm.next_labels = []
    s, _ = eng.start_session("disappointed_patient")
    llm.next_labels = ["false_promise"]
    r = eng.run_turn(s, "I can't promise you it will be fine, but I'll stay with you.")
    check("false_promise" not in r["labels"], f"'I can't promise' is no false promise ({r['labels']})")
    check(defers_to_doctor("Only the doctor can decide when you go home."), "deferring to the doctor is seen")
    check(not defers_to_doctor("I'll get you home for the party."), "a promise is not deferring")

    # 2. names
    check(_nurse_names(["hi i'm jalil, i'm your nurse today"]) == {"Jalil"}, "lower case 'i'm jalil' is a name")
    check(_nurse_names(["I'm sorry, I'm here for you"]) == set(), "'I'm sorry' is no name")
    s, _ = eng.start_session("disappointed_patient", nurse_name="Jalil")
    check(s.nurse_name == "Jalil" and "Jalil" in eng.stt_hints(s), "the name from Unity is kept and given to Whisper")
    check(eng._content_problem(s, "Thank you, X.", [], "hello") != "", "the patient never calls the nurse X")
    check(eng._content_problem(s, "Thank you, Jaleel.", [], "hello") != "", "nor by a wrong name")
    check(eng._content_problem(s, "Thank you, Jalil.", [], "hello") == "", "the right name is fine")
    check(eng._content_problem(s, "Mr. X is just tired.", [], "hello") == "", "Mr. X (himself) is fine")

    # 3. feeling: mostly sad, the last reply worried
    s, _ = eng.start_session("disappointed_patient")
    llm.emotions = ["Sad", "Sad", "Worried"]
    for text in ["How are you feeling?", "What is on your mind?", "Tell me more."]:
        eng.run_turn(s, text)
    felt = felt_emotion(s)
    check(felt["emotion"] == "Sadness", f"mostly sad over the conversation, even when the last reply is worried ({felt})")
    s.turns[-1]["emotion"] = "Surprised"
    check(felt_emotion(s)["emotion"] == "Sadness", "Surprised is no feeling")

    # 4. trust and mood in the score
    s, _ = eng.start_session("disappointed_patient")
    llm.next_labels = ["named_emotion"]
    eng.run_turn(s, "I can see how sad you are about missing her birthday.")
    llm.next_labels = ["asked_open_question"]
    eng.run_turn(s, "What would you like to do for her?")
    llm.next_labels = ["offered_alternative_connection", "motivated_small_step"]
    eng.run_turn(s, "Shall I help you sit up and we set up a video call?")
    d = run_debrief(llm, s)
    items = {i["id"]: i for i in d["items"]}
    check(8 in items and 9 in items, "the debrief has a trust item and a mood item")
    check(items[8]["kind"] == "trust" and items[8]["end_value"] == d["final_trust"], f"trust item uses the final trust ({items[8]['why']})")
    check(items[9]["kind"] == "tension" and items[9]["end_value"] == d["final_tension"], f"mood item uses the final tension ({items[9]['why']})")
    check(d["max_score"] == 18, f"max score 18 ({d['max_score']})")
    check(items[8]["score"] >= 1 and items[9]["score"] >= 1, f"a good conversation scores on trust and mood ({items[8]['score']}, {items[9]['score']})")
    s2, _ = eng.start_session("disappointed_patient")
    llm.next_labels = ["dismissive"]
    eng.run_turn(s2, "There will be other birthdays.")
    d2 = run_debrief(llm, s2)
    items2 = {i["id"]: i for i in d2["items"]}
    check(items2[8]["score"] == 0 and items2[9]["score"] == 0, "a dismissive conversation scores 0 on trust and mood")
    check(d2["felt_emotion"]["emotion"] == "Sadness", "the debrief sends the felt emotion")

    print("\nALL PASSED" if not FAILED else f"\n{len(FAILED)} FAILED")
    sys.exit(1 if FAILED else 0)


if __name__ == "__main__":
    main()
