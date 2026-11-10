"""Conversation engine: scenario loading, one turn (classify -> state -> actor), session log.

Everything scenario-specific (persona, labels, effects, phases, bands, facts, lines) lives in
scenarios/<id>.json. This file only contains the generic rules.
"""
from __future__ import annotations

import json
import logging
import random
import re
import threading
import time
import uuid
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Literal, Optional

from pydantic import BaseModel, ConfigDict, Field, ValidationError, create_model

from llm_client import LlmClient, LlmOutputError

log = logging.getLogger("aiserver")

Emotion = Literal["Neutral", "Happy", "Sad", "Angry", "Worried", "Surprised", "Pain", "Crying"]
Animation = Literal["Idle", "Talking", "Angry", "Sad", "Crying"]
Pose = Literal["Engaged", "TurnsAway", "Withdrawn", "Unchanged"]

CONTEXT_TURNS = 4  # classifier sees the last 4 turns (student line + reply = 1 turn)


class ActorOutput(BaseModel):
    model_config = ConfigDict(extra="forbid")
    reply: str = Field(min_length=1)
    emotion: Emotion
    animation: Animation
    pose: Pose
    facts_used: list[str]


def make_classifier_model(label_names: list[str]) -> type[BaseModel]:
    """Build the strict classification model with the scenario's labels as a Literal."""
    LabelLiteral = Literal[tuple(label_names)]  # type: ignore[valid-type]
    return create_model(
        "ClassifierOutput",
        __config__=ConfigDict(extra="forbid"),
        labels=(list[LabelLiteral], ...),
        evidence_quote=(str, ...),
        off_topic=(bool, ...),
        unsafe=(bool, ...),
    )


# ---------------------------------------------------------------- scenario

class Scenario:
    def __init__(self, data: dict[str, Any]):
        self.data = data
        self.id: str = data["id"]
        self.labels: dict[str, dict] = {l["name"]: l for l in data["labels"]}
        self.phases: list[dict] = data["phases"]
        self.bands: list[dict] = sorted(data["bands"], key=lambda b: -b["min_anger"])
        self.rules: dict = data["rules"]
        self.classifier_model = make_classifier_model(list(self.labels))

    @classmethod
    def load(cls, folder: Path, scenario_id: str) -> "Scenario":
        if not re.fullmatch(r"[A-Za-z0-9_\-]+", scenario_id):
            raise FileNotFoundError(scenario_id)
        path = folder / f"{scenario_id}.json"
        with path.open(encoding="utf-8") as f:
            return cls(json.load(f))

    def band_for(self, anger: int) -> dict:
        for band in self.bands:
            if anger >= band["min_anger"]:
                return band
        return self.bands[-1]

    def phase(self, phase_id: str) -> dict:
        return next(p for p in self.phases if p["id"] == phase_id)

    def fallback_line(self) -> str:
        return random.choice(self.data["fallback_lines"])


# ---------------------------------------------------------------- session

@dataclass
class Session:
    id: str
    scenario: Scenario
    anger: int
    trust: int
    phase: str
    started_at: float = field(default_factory=time.monotonic)
    started_iso: str = field(default_factory=lambda: datetime.now(timezone.utc).isoformat())
    labels_ever: set[str] = field(default_factory=set)
    unlocked_facts: list[str] = field(default_factory=list)
    history: list[dict[str, str]] = field(default_factory=list)  # {"speaker": "student"|"character", "text"}
    turns: list[dict[str, Any]] = field(default_factory=list)
    ended: Optional[str] = None  # None, "resolved", "time_up", "stopped"
    lock: threading.Lock = field(default_factory=threading.Lock)

    def snapshot(self) -> dict[str, Any]:
        return {
            "anger": self.anger,
            "trust": self.trust,
            "band": self.scenario.band_for(self.anger)["name"],
            "phase": self.phase,
        }

    def elapsed(self) -> float:
        return time.monotonic() - self.started_at


class SessionEndedError(RuntimeError):
    pass


def _clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def _norm(text: str) -> str:
    return re.sub(r"\s+", " ", re.sub(r"[^\w\s']", " ", text.lower())).strip()


def _limit_sentences(text: str, max_sentences: int = 2) -> str:
    parts = re.split(r"(?<=[.!?])\s+", text.strip())
    return " ".join(parts[:max_sentences]).strip()


# ---------------------------------------------------------------- engine

class Engine:
    def __init__(self, llm: LlmClient, scenario_folder: Path):
        self.llm = llm
        self.scenario_folder = scenario_folder
        self._scenarios: dict[str, Scenario] = {}

    def scenario(self, scenario_id: str) -> Scenario:
        if scenario_id not in self._scenarios:
            self._scenarios[scenario_id] = Scenario.load(self.scenario_folder, scenario_id)
        return self._scenarios[scenario_id]

    # -- session start

    def start_session(self, scenario_id: str) -> tuple[Session, dict[str, Any]]:
        sc = self.scenario(scenario_id)
        op = sc.data["opening"]
        session = Session(
            id=uuid.uuid4().hex[:12],
            scenario=sc,
            anger=op["anger"],
            trust=op["trust"],
            phase=sc.phases[0]["id"],
        )
        self._unlock_facts(session)
        session.history.append({"speaker": "character", "text": op["reply"]})
        opening = {
            "reply": op["reply"],
            "emotion": op["emotion"],
            "animation": op.get("animation", sc.data["emotion_to_animation"][op["emotion"]]),
            "pose": op.get("pose", "Engaged"),
            "anger": session.anger,
            "trust": session.trust,
            "band": sc.band_for(session.anger)["name"],
            "phase": session.phase,
        }
        return session, opening

    # -- one turn

    def run_turn(self, session: Session, text: str) -> dict[str, Any]:
        if session.ended:
            raise SessionEndedError(f"session already ended ({session.ended})")
        sc = session.scenario
        t_start = time.perf_counter()
        timings: dict[str, int] = {}
        text = (text or "").strip()
        before = session.snapshot()
        result: dict[str, Any] = {
            "turn": len(session.turns) + 1,
            "time": datetime.now(timezone.utc).isoformat(),
            "elapsed_s": round(session.elapsed(), 1),
            "student_text": text,
            "labels": [],
            "evidence_quote": "",
            "off_topic": False,
            "unsafe": False,
            "before": before,
            "fallback_used": False,
            "notes": [],
        }

        # 0. scenario time limit
        if session.elapsed() > sc.data["time_limit_seconds"]:
            session.ended = "time_up"
            session.history.append({"speaker": "student", "text": text})
            return self._finish(session, result, timings, t_start,
                                reply=sc.data["time_up_line"], fallback=True)

        session.history.append({"speaker": "student", "text": text})

        # 1. classification
        t = time.perf_counter()
        if not text:
            cls = {"labels": ["no_response"], "evidence_quote": "", "off_topic": False, "unsafe": False}
            result["notes"].append("empty input: labelled no_response without the model")
        else:
            cls = self._classify(session, text, result["notes"])
        timings["classify_ms"] = int((time.perf_counter() - t) * 1000)

        if cls is None:  # classifier failed twice
            return self._finish(session, result, timings, t_start,
                                reply=sc.fallback_line(), fallback=True)

        result.update({k: cls[k] for k in ("labels", "evidence_quote", "off_topic", "unsafe")})

        # 2. off topic / unsafe: no AI reply, fallback line, anger +5
        if cls["off_topic"] or cls["unsafe"]:
            t = time.perf_counter()
            eff = sc.rules["off_topic_or_unsafe_effect"]
            session.anger = int(_clamp(session.anger + eff["anger"], 0, 100))
            session.trust = int(_clamp(session.trust + eff["trust"], 0, 100))
            timings["state_ms"] = int((time.perf_counter() - t) * 1000)
            return self._finish(session, result, timings, t_start,
                                reply=sc.fallback_line(), fallback=True)

        # 3. state update (pure Python)
        t = time.perf_counter()
        self._apply_labels(session, cls["labels"], result["notes"])
        self._advance_phase(session, cls["labels"], result["notes"])
        self._unlock_facts(session)
        timings["state_ms"] = int((time.perf_counter() - t) * 1000)

        # 4. actor
        t = time.perf_counter()
        actor = self._act(session, result["notes"])
        timings["actor_ms"] = int((time.perf_counter() - t) * 1000)
        if actor is None:
            return self._finish(session, result, timings, t_start,
                                reply=sc.fallback_line(), fallback=True)
        return self._finish(session, result, timings, t_start, actor=actor)

    # -- step 1

    def _classify(self, session: Session, text: str, notes: list[str]) -> Optional[dict]:
        sc = session.scenario
        label_lines = "\n".join(
            f'- {l["name"]}: {l["description"]} Example: "{l["example"]}"' for l in sc.data["labels"]
        )
        system = (
            "You analyse what a nursing student says in a communication training. "
            f"The student is {sc.data['student_role']}. "
            f"The other person is {sc.data['persona']['name']}: "
            f"{sc.data['persona']['description'][0]}\n\n"
            "Pick ONLY the labels that are really present in the student's NEW sentence. "
            "Do not label earlier sentences. An empty list is fine.\n"
            f"Labels:\n{label_lines}\n\n"
            "evidence_quote: copy the most important words literally from the NEW sentence "
            "(an exact substring), or \"\" when there are no labels.\n"
            "off_topic: true only if the sentence has nothing to do with this conversation "
            "(e.g. the weather, football, games, asking about AI).\n"
            "unsafe: true only for threats, insults, sexual or hateful content.\n"
            "Answer with JSON only."
        )
        context = session.history[-(CONTEXT_TURNS * 2 + 1):-1]
        ctx_text = "\n".join(
            f'{"Student" if h["speaker"] == "student" else sc.data["persona"]["name"]}: {h["text"]}'
            for h in context
        ) or "(no earlier conversation)"
        user = (f"Earlier conversation (context only, do NOT label it):\n{ctx_text}\n\n"
                f"NEW sentence of the student (label only this):\n\"{text}\"")
        messages = [{"role": "user", "content": user}]
        for attempt in (1, 2):
            out = self._call_validated(system, messages, sc.classifier_model, "classifier", notes)
            if out is None:
                return None
            data = out.model_dump()
            data["labels"] = list(dict.fromkeys(data["labels"]))  # dedupe, keep order
            quote = data["evidence_quote"].strip().strip("\"'“”‘’").strip()
            if not quote or _norm(quote) in _norm(text):
                break
            # quote comes from somewhere else (usually an earlier sentence): labels are suspect
            if attempt == 1:
                notes.append(f"evidence_quote not literal, retrying: {quote!r}")
                messages = messages + [
                    {"role": "assistant", "content": json.dumps(data)},
                    {"role": "user", "content": (
                        f"Wrong: {quote!r} is not part of the NEW sentence. Label ONLY the NEW "
                        f"sentence \"{text}\" and quote only from it.")},
                ]
            else:
                notes.append(f"evidence_quote still not literal, cleared: {quote!r}")
                quote = ""
        data["evidence_quote"] = quote
        if (data["off_topic"] or data["unsafe"]) and data["labels"]:
            notes.append(f"off_topic/unsafe: ignored labels {data['labels']}")
            data["labels"] = []
        return data

    # -- step 3

    def _apply_labels(self, session: Session, labels: list[str], notes: list[str]) -> None:
        sc, rules = session.scenario, session.scenario.rules
        session.labels_ever.update(labels)
        for stat in ("anger", "trust"):
            delta = sum(sc.labels[l][stat] for l in labels)
            capped = int(_clamp(delta, -rules["max_decrease_per_turn"], rules["max_increase_per_turn"]))
            if capped != delta:
                notes.append(f"{stat} delta {delta:+d} capped to {capped:+d}")
            old = getattr(session, stat)
            new = old + capped
            for th in rules.get("thresholds", []):
                if th["stat"] == stat and th["until_label_ever"] not in session.labels_ever:
                    floor = min(th["floor"], old)  # a floor never pushes a value up
                    if new < floor:
                        notes.append(f"{stat} held at floor {floor} (no {th['until_label_ever']} yet)")
                        new = floor
            setattr(session, stat, int(_clamp(new, 0, 100)))

    def _advance_phase(self, session: Session, labels: list[str], notes: list[str]) -> None:
        phase = session.scenario.phase(session.phase)
        cond = phase.get("condition")
        if phase.get("terminal") or not cond:
            return
        ok = True
        if "anger_lt" in cond:
            ok &= session.anger < cond["anger_lt"]
        if "trust_gte" in cond:
            ok &= session.trust >= cond["trust_gte"]
        if "labels_ever_all" in cond:
            ok &= all(l in session.labels_ever for l in cond["labels_ever_all"])
        if "labels_ever_any" in cond:
            ok &= any(l in session.labels_ever for l in cond["labels_ever_any"])
        if "labels_this_turn_any" in cond:
            ok &= any(l in labels for l in cond["labels_this_turn_any"])
        if ok:
            notes.append(f"phase {session.phase} -> {phase['next']}")
            session.phase = phase["next"]
            new_phase = session.scenario.phase(session.phase)
            if new_phase.get("terminal"):
                session.ended = new_phase.get("end_reason", "resolved")

    def _unlock_facts(self, session: Session) -> None:
        sc = session.scenario
        for fact in sc.data["facts"]:
            if fact["text"] in session.unlocked_facts:
                continue
            tier = sc.data["fact_tiers"][fact["tier"]]
            unlocked = (
                tier.get("always", False)
                or ("trust_min" in tier and session.trust >= tier["trust_min"])
                or any(l in session.labels_ever for l in tier.get("unlock_when_label_ever", []))
            )
            if unlocked:
                session.unlocked_facts.append(fact["text"])

    # -- step 4

    def _act(self, session: Session, notes: list[str]) -> Optional[ActorOutput]:
        sc = session.scenario
        persona = sc.data["persona"]
        band = sc.band_for(session.anger)
        phase = sc.phase(session.phase)
        facts = "\n".join(f"- {f}" for f in session.unlocked_facts)
        system = (
            "\n".join(persona["description"])
            + f"\n\nYou are talking to {sc.data['student_role']}.\n"
            f"Things you know and may say (do not mention anything else about your family or "
            f"your father's health):\n{facts}\n\n"
            f"Where you are in the conversation: {phase['actor_goal']}\n"
            f"Your anger right now: {band['name']} ({session.anger}/100). How you speak now: "
            f"{band['instruction']}.\n"
            f"Your trust in this nurse: {session.trust}/100.\n\n"
            "Answer as JSON:\n"
            "- reply: what you say out loud, at most 2 short sentences, spoken English, no swearing.\n"
            f"- emotion: one of {', '.join(band['allowed_emotions'])}.\n"
            "- animation: Idle, Talking, Angry, Sad or Crying.\n"
            "- pose: Engaged, TurnsAway, Withdrawn or Unchanged.\n"
            "- facts_used: the facts from the list above that you used in your reply "
            "(copy them exactly), or an empty list."
        )
        messages = [
            {"role": "user" if h["speaker"] == "student" else "assistant", "content": h["text"]}
            for h in session.history
        ]
        out = self._call_validated(system, messages, ActorOutput, "actor", notes)
        if out is None:
            return None

        # post-checks in Python
        reply = _limit_sentences(out.reply, 2)
        if reply != out.reply.strip():
            notes.append("reply trimmed to 2 sentences")
        out.reply = reply
        if out.emotion not in band["allowed_emotions"]:
            fixed = band["allowed_emotions"][0]
            notes.append(f"emotion {out.emotion} does not fit band {band['name']}, corrected to {fixed}")
            out.emotion = fixed
            out.animation = sc.data["emotion_to_animation"][fixed]
        unknown = [f for f in out.facts_used if f not in session.unlocked_facts]
        if unknown:
            notes.append(f"facts_used not unlocked, removed: {unknown}")
            out.facts_used = [f for f in out.facts_used if f in session.unlocked_facts]
        return out

    # -- step 6: validation with one retry

    def _call_validated(self, system: str, messages: list[dict], model: type[BaseModel],
                        role: str, notes: list[str]) -> Optional[BaseModel]:
        schema = model.model_json_schema()
        for attempt in (1, 2):
            try:
                raw = self.llm.chat_json(system, messages, schema, role=role)
                return model.model_validate(raw)
            except (ValidationError, LlmOutputError) as exc:
                msg = str(exc).splitlines()[0]
                notes.append(f"{role} output invalid (attempt {attempt}): {msg}")
                log.warning("%s output invalid (attempt %d): %s", role, attempt, exc)
        notes.append(f"{role} failed twice, using fallback line")
        return None

    # -- step 5: log + response

    def _finish(self, session: Session, result: dict[str, Any], timings: dict[str, int],
                t_start: float, reply: str = "", fallback: bool = False,
                actor: Optional[ActorOutput] = None) -> dict[str, Any]:
        sc = session.scenario
        band = sc.band_for(session.anger)
        if actor is not None:
            result.update(actor.model_dump())
        else:
            emotion = "Angry" if "Angry" in band["allowed_emotions"] else band["allowed_emotions"][0]
            result.update({
                "reply": reply,
                "emotion": emotion,
                "animation": sc.data["emotion_to_animation"][emotion],
                "pose": "Unchanged",
                "facts_used": [],
            })
            result["fallback_used"] = fallback
        session.history.append({"speaker": "character", "text": result["reply"]})
        timings["total_ms"] = int((time.perf_counter() - t_start) * 1000)
        result["after"] = session.snapshot()
        result["phase"] = session.phase
        result["band"] = result["after"]["band"]
        result["anger"] = session.anger
        result["trust"] = session.trust
        result["ended"] = session.ended
        result["timings_ms"] = timings
        session.turns.append(result)
        log.info("turn %d %s labels=%s anger %d->%d trust %d->%d phase=%s %dms",
                 result["turn"], session.id, result["labels"], result["before"]["anger"],
                 session.anger, result["before"]["trust"], session.trust, session.phase,
                 timings["total_ms"])
        return result

    # -- transcript

    def transcript(self, session: Session) -> dict[str, Any]:
        return {
            "session_id": session.id,
            "scenario_id": session.scenario.id,
            "started": session.started_iso,
            "saved": datetime.now(timezone.utc).isoformat(),
            "duration_s": round(session.elapsed(), 1),
            "end_reason": session.ended,
            "final_state": session.snapshot(),
            "labels_ever": sorted(session.labels_ever),
            "unlocked_facts": session.unlocked_facts,
            "opening": session.scenario.data["opening"]["reply"],
            "turns": session.turns,
        }
