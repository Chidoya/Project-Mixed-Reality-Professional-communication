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

COMMON_LABELS_FILE = Path(__file__).resolve().parent / "labels_common.json"


def _tension(d: dict, key: str = "tension", old: str = "anger", default: Any = 0) -> Any:
    """Scenario files use 'tension' (the general name); the first scenario used 'anger'. Both work."""
    return d.get(key, d.get(old, default))


class Scenario:
    """A scenario JSON, normalized: common labels merged in, 'anger' keys read as 'tension'.

    The tension is the general state of the character (anger, hopelessness, impatience, distress...);
    its name per scenario is 'tension_name'. Towards Unity the field stays 'anger' so AiNpc does not change.
    """

    def __init__(self, data: dict[str, Any], common: Optional[dict[str, dict]] = None):
        common = common or {}
        self.data = data
        self.id: str = data["id"]
        self.tension_name: str = data.get("tension_name", "anger")
        merged = []
        for entry in data["labels"]:
            base = dict(common.get(entry["name"], {}))
            if not base and "description" not in entry:
                raise ValueError(f"{self.id}: label {entry['name']} is not common and has no description")
            base.update(entry)
            base["tension"] = _tension(entry)
            base["trust"] = entry.get("trust", 0)
            merged.append(base)
        data["labels"] = merged
        self.labels: dict[str, dict] = {l["name"]: l for l in merged}
        self.critical_labels = [l["name"] for l in merged if l.get("critical")]
        op = data["opening"]
        op["tension"] = op["anger"] = _tension(op)  # both names, for older code paths
        self.phases: list[dict] = data["phases"]
        for band in data["bands"]:
            band["min_tension"] = _tension(band, "min_tension", "min_anger")
        self.bands: list[dict] = sorted(data["bands"], key=lambda b: -b["min_tension"])
        self.rules: dict = data["rules"]
        self.classifier_model = make_classifier_model(list(self.labels))

    @classmethod
    def load(cls, folder: Path, scenario_id: str) -> "Scenario":
        if not re.fullmatch(r"[A-Za-z0-9_\-]+", scenario_id):
            raise FileNotFoundError(scenario_id)
        path = folder / f"{scenario_id}.json"
        with path.open(encoding="utf-8") as f:
            data = json.load(f)
        common = {}
        if COMMON_LABELS_FILE.exists():
            common = {l["name"]: l for l in json.loads(COMMON_LABELS_FILE.read_text(encoding="utf-8"))["labels"]}
        return cls(data, common)

    def band_for(self, tension: int) -> dict:
        for band in self.bands:
            if tension >= band["min_tension"]:
                return band
        return self.bands[-1]

    def voice(self) -> dict:
        """Piper voice of this character: {"piper": name, "length_scale_by_band": {...}} (may be empty)."""
        return self.data.get("voice", {})

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
    ended: Optional[str] = None  # None, "resolved", "time_up", "stopped", "critical_error"
    lock: threading.Lock = field(default_factory=threading.Lock)
    debrief: Optional[dict[str, Any]] = None
    critical: Optional[dict[str, Any]] = None  # the critical error that ended the scenario

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


def _has_word(text: str, word: str) -> bool:
    """Whole-word (or whole-phrase) match, case-insensitive."""
    return re.search(r"(?<![a-z])" + re.escape(word.lower()) + r"(?![a-z])", text.lower()) is not None


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
            anger=_tension(op),
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
            "tension_name": sc.tension_name,
            "rest_animation": op.get("rest_animation", "Idle"),
            "character_name": sc.data["persona"]["name"],
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
            "tokens": {},
        }

        # 0. scenario time limit (0 = none)
        limit = sc.data.get("time_limit_seconds", 0)
        if limit and session.elapsed() > limit:
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
            cls = self._classify(session, text, result["notes"], result["tokens"])
        timings["classify_ms"] = int((time.perf_counter() - t) * 1000)

        if cls is None:  # classifier failed twice
            return self._finish(session, result, timings, t_start,
                                reply=sc.fallback_line(), fallback=True)

        result.update({k: cls[k] for k in ("labels", "evidence_quote", "off_topic", "unsafe")})

        # 2. off topic / unsafe: no AI reply, fallback line, tension +5
        if cls["off_topic"] or cls["unsafe"]:
            t = time.perf_counter()
            eff = sc.rules["off_topic_or_unsafe_effect"]
            session.anger = int(_clamp(session.anger + _tension(eff), 0, 100))
            session.trust = int(_clamp(session.trust + eff["trust"], 0, 100))
            timings["state_ms"] = int((time.perf_counter() - t) * 1000)
            return self._finish(session, result, timings, t_start,
                                reply=sc.fallback_line(), fallback=True)

        # 2b. critical error: the scenario ends at once, the character reacts with a fixed line
        critical = [l for l in cls["labels"] if l in sc.critical_labels]
        for name in list(critical):
            # A critical label ends the whole scenario: it only counts if one of its keywords is really said
            words = sc.labels[name].get("critical_requires_any", [])
            if words and not any(_has_word(text, w) for w in words):
                critical.remove(name)
                cls["labels"] = [l for l in cls["labels"] if l != name]
                result["labels"] = cls["labels"]
                result["notes"].append(f"critical label {name} dropped: none of its keywords in the sentence")
        if critical:
            t = time.perf_counter()
            self._apply_labels(session, cls["labels"], result["notes"])
            label = sc.labels[critical[0]]
            reaction = label.get("critical_reaction", {})
            session.ended = "critical_error"
            session.critical = {
                "label": label["name"],
                "turn": result["turn"],
                "quote": cls["evidence_quote"] or text,
                "student_text": text,
                "title": label.get("critical_title", label["name"]),
                "feedback": label.get("critical_feedback", ""),
            }
            result["critical"] = session.critical
            result["notes"].append(f"critical label {label['name']}: scenario ends")
            timings["state_ms"] = int((time.perf_counter() - t) * 1000)
            return self._finish(session, result, timings, t_start,
                                reply=reaction.get("reply", sc.fallback_line()),
                                emotion=reaction.get("emotion"), animation=reaction.get("animation"),
                                pose=reaction.get("pose", "TurnsAway"))

        # 3. state update (pure Python)
        t = time.perf_counter()
        self._apply_labels(session, cls["labels"], result["notes"])
        self._advance_phase(session, cls["labels"], result["notes"])
        self._unlock_facts(session)
        timings["state_ms"] = int((time.perf_counter() - t) * 1000)

        # 4. actor
        t = time.perf_counter()
        actor = self._act(session, result["notes"], result["tokens"])
        timings["actor_ms"] = int((time.perf_counter() - t) * 1000)
        if actor is None:
            return self._finish(session, result, timings, t_start,
                                reply=sc.fallback_line(), fallback=True)
        return self._finish(session, result, timings, t_start, actor=actor)

    # -- step 1

    def _classify(self, session: Session, text: str, notes: list[str],
                  tokens: dict) -> Optional[dict]:
        sc = session.scenario
        label_lines = "\n".join(
            f'- {l["name"]}: {l["description"]}' + (f' e.g. "{l["example"]}"' if l.get("example") else "")
            for l in sc.data["labels"]
        )
        system = (
            f"Classify what a nursing student says to {sc.data['persona']['name']} "
            f"({sc.data['persona']['description'][0]})\n"
            "Pick only labels clearly present in the NEW sentence; an empty list is fine.\n"
            f"Labels:\n{label_lines}\n"
            "evidence_quote: the shortest exact words (max 8) copied from the NEW sentence that show "
            "the labels; \"\" only if there are no labels.\n"
            "off_topic: nothing to do with this conversation (weather, sports, AI). "
            "unsafe: threats, insults, sexual or hateful content."
        )
        context = session.history[-(CONTEXT_TURNS * 2 + 1):-1]
        ctx_text = "\n".join(
            f'{"Student" if h["speaker"] == "student" else sc.data["persona"]["name"]}: {h["text"]}'
            for h in context
        ) or "(none)"
        user = f"Earlier (context only):\n{ctx_text}\n\nNEW sentence:\n\"{text}\""
        messages = [{"role": "user", "content": user}]
        for attempt in (1, 2):
            out = self._call_validated(system, messages, sc.classifier_model, "classifier", notes, tokens)
            if out is None:
                return None
            data = out.model_dump()
            data["labels"] = list(dict.fromkeys(data["labels"]))  # dedupe, keep order
            if (data["off_topic"] or data["unsafe"]) and data["labels"]:
                notes.append(f"off_topic/unsafe: ignored labels {data['labels']}")
                data["labels"] = []
            quote = data["evidence_quote"].strip().strip("\"'“”‘’").strip()
            if not data["labels"]:
                data["evidence_quote"] = ""
                return data
            if quote and _norm(quote) in _norm(text):
                data["evidence_quote"] = quote
                return data
            # labels without literal evidence: ask once more
            problem = "is empty" if not quote else f"{quote!r} is not in the NEW sentence"
            if attempt == 1:
                notes.append(f"evidence_quote {problem}, retrying")
                messages = messages + [
                    {"role": "assistant", "content": json.dumps(data)},
                    {"role": "user", "content": (
                        f"Wrong: evidence_quote {problem}. Label only the NEW sentence \"{text}\" "
                        "and copy the evidence_quote exactly from it.")},
                ]
        # still no literal evidence after the retry: keep the labels, quote the whole sentence
        notes.append(f"evidence_quote {problem} again, using the whole sentence")
        data["evidence_quote"] = text
        return data

    # -- step 3

    def _apply_labels(self, session: Session, labels: list[str], notes: list[str]) -> None:
        sc, rules = session.scenario, session.scenario.rules
        session.labels_ever.update(labels)
        for stat, key in (("anger", "tension"), ("trust", "trust")):  # session.anger holds the tension
            delta = sum(sc.labels[l][key] for l in labels)
            capped = int(_clamp(delta, -rules["max_decrease_per_turn"], rules["max_increase_per_turn"]))
            if capped != delta:
                notes.append(f"{key} delta {delta:+d} capped to {capped:+d}")
            old = getattr(session, stat)
            new = old + capped
            for th in rules.get("thresholds", []):
                if th["stat"] in (stat, key) and th["until_label_ever"] not in session.labels_ever:
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
        if "tension_lt" in cond or "anger_lt" in cond:
            ok &= session.anger < _tension(cond, "tension_lt", "anger_lt")
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

    def _act(self, session: Session, notes: list[str], tokens: dict) -> Optional[ActorOutput]:
        sc = session.scenario
        persona = sc.data["persona"]
        band = sc.band_for(session.anger)
        phase = sc.phase(session.phase)
        facts = "\n".join(f"- {f}" for f in session.unlocked_facts) or "- (nothing special)"
        facts_rule = sc.data.get("facts_instruction", "do not invent other personal or medical facts")
        animations = band.get("animations") or ["Idle", "Talking", "Angry", "Sad", "Crying"]
        last_student = next((h["text"] for h in reversed(session.history) if h["speaker"] == "student"), "")
        system = (
            "\n".join(persona["description"])
            + f"\n\nYou are talking to {sc.data['student_role']}.\n"
            f"Things you know and may say ({facts_rule}):\n{facts}\n\n"
            f"Where you are in the conversation: {phase['actor_goal']}\n"
            f"Your {sc.tension_name} right now: {band['name']} ({session.anger}/100). How you speak now: "
            f"{band['instruction']}.\n"
            f"Your trust in this nurse: {session.trust}/100.\n\n"
            f"The nurse just said: \"{last_student}\"\n"
            f"React only to what the nurse really said in this conversation, mostly that last "
            f"sentence. Never claim or suggest the nurse said, promised, explained or offered "
            f"something they did not say: only mention a time, a plan, a reason or a promise if "
            f"the nurse said it, in their words.\n\n"
            "Answer as JSON:\n"
            "- reply: what you say out loud, at most 2 short sentences, spoken English, no swearing.\n"
            f"- emotion: one of {', '.join(band['allowed_emotions'])}.\n"
            f"- animation: one of {', '.join(animations)}.\n"
            "- pose: Engaged, TurnsAway, Withdrawn or Unchanged.\n"
            "- facts_used: the facts from the list above that you used in your reply "
            "(copy them exactly), or an empty list."
        )
        messages = [
            {"role": "user" if h["speaker"] == "student" else "assistant", "content": h["text"]}
            for h in session.history
        ]
        out = self._call_validated(system, messages, ActorOutput, "actor", notes, tokens)
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
        if band.get("animations") and out.animation not in band["animations"]:
            wanted = sc.data["emotion_to_animation"].get(out.emotion)
            fixed = wanted if wanted in band["animations"] else band["animations"][0]
            notes.append(f"animation {out.animation} does not fit band {band['name']}, corrected to {fixed}")
            out.animation = fixed
        unknown = [f for f in out.facts_used if f not in session.unlocked_facts]
        if unknown:
            notes.append(f"facts_used not unlocked, removed: {unknown}")
            out.facts_used = [f for f in out.facts_used if f in session.unlocked_facts]
        return out

    # -- step 6: validation with one retry

    def _call_validated(self, system: str, messages: list[dict], model: type[BaseModel],
                        role: str, notes: list[str], tokens: Optional[dict] = None) -> Optional[BaseModel]:
        schema = model.model_json_schema()
        for attempt in (1, 2):
            try:
                try:
                    raw = self.llm.chat_json(system, messages, schema, role=role)
                finally:
                    if tokens is not None:  # sum over retries: tokens["classify"] = {in, out, calls}
                        usage = self.llm.last_usage() if hasattr(self.llm, "last_usage") else {}
                        key = "classify" if role == "classifier" else role
                        acc = tokens.setdefault(key, {"in": 0, "out": 0, "calls": 0})
                        acc["in"] += usage.get("in", 0)
                        acc["out"] += usage.get("out", 0)
                        acc["calls"] += 1
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
                actor: Optional[ActorOutput] = None, emotion: Optional[str] = None,
                animation: Optional[str] = None, pose: Optional[str] = None) -> dict[str, Any]:
        sc = session.scenario
        band = sc.band_for(session.anger)
        if actor is not None:
            result.update(actor.model_dump())
        else:
            emotion = emotion or band.get("fallback_emotion", band["allowed_emotions"][0])
            result.update({
                "reply": reply,
                "emotion": emotion,
                "animation": animation or sc.data["emotion_to_animation"][emotion],
                "pose": pose or "Unchanged",
                "facts_used": [],
            })
            # e.g. a patient in bed only has "Laying": never let a fixed line make him stand up
            if band.get("animations") and result["animation"] not in band["animations"]:
                result["animation"] = band["animations"][0]
            result["fallback_used"] = fallback
        session.history.append({"speaker": "character", "text": result["reply"]})
        timings["total_ms"] = int((time.perf_counter() - t_start) * 1000)
        result["after"] = session.snapshot()
        result["phase"] = session.phase
        result["band"] = result["after"]["band"]
        result["anger"] = session.anger
        result["trust"] = session.trust
        result["ended"] = session.ended
        result["tension_name"] = sc.tension_name
        result["character_name"] = sc.data["persona"]["name"]
        result["timings_ms"] = timings
        session.turns.append(result)
        log.info("turn %d %s labels=%s anger %d->%d trust %d->%d phase=%s %dms",
                 result["turn"], session.id, result["labels"], result["before"]["anger"],
                 session.anger, result["before"]["trust"], session.trust, session.phase,
                 timings["total_ms"])
        return result

    # -- non-verbal events

    def apply_event(self, session: Session, name: str) -> dict[str, Any]:
        """A non-verbal action from Unity (e.g. "curtain_closed"). Effect and optional label come from the
        scenario's "events"; it is logged like a turn (without a reply) so the debrief can count it."""
        if session.ended:
            raise SessionEndedError(f"session already ended ({session.ended})")
        sc = session.scenario
        events = sc.data.get("events", {})
        if name not in events:
            raise KeyError(name)
        ev = events[name]
        before = session.snapshot()
        session.anger = int(_clamp(session.anger + _tension(ev), 0, 100))
        session.trust = int(_clamp(session.trust + ev.get("trust", 0), 0, 100))
        labels = [ev["label"]] if ev.get("label") else []
        session.labels_ever.update(labels)
        notes: list[str] = []
        self._advance_phase(session, labels, notes)
        result = {
            "turn": len(session.turns) + 1,
            "kind": "event",
            "event": name,
            "time": datetime.now(timezone.utc).isoformat(),
            "elapsed_s": round(session.elapsed(), 1),
            "student_text": f"[{ev.get('description', name)}]",
            "labels": labels,
            "evidence_quote": f"[{ev.get('description', name)}]",
            "off_topic": False,
            "unsafe": False,
            "before": before,
            "after": session.snapshot(),
            "reply": "",
            "notes": notes,
            "anger": session.anger,
            "trust": session.trust,
            "band": sc.band_for(session.anger)["name"],
            "phase": session.phase,
            "ended": session.ended,
            "tension_name": sc.tension_name,
        }
        session.turns.append(result)
        log.info("event %s %s: %s %d->%d", name, session.id, sc.tension_name, before["anger"], session.anger)
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
            "critical": session.critical,
            "debrief": session.debrief,
        }

    # -- warm-up

    def warm_up(self) -> list[str]:
        """Load the model into memory with the real schemas (classifier per scenario, actor),
        so the first turn of the first student is as fast as the others."""
        lines = []
        jobs = [("actor", ActorOutput.model_json_schema())]
        for path in sorted(self.scenario_folder.glob("*.json")):
            jobs.insert(0, ("classifier", self.scenario(path.stem).classifier_model.model_json_schema()))
        for role, schema in jobs:
            t = time.perf_counter()
            try:
                self.llm.chat_json("Answer with valid JSON.", [{"role": "user", "content": "Hello."}],
                                   schema, role=role)
            except LlmOutputError:
                pass  # output does not matter here
            lines.append(f"warm-up {role} done in {int((time.perf_counter() - t) * 1000)} ms")
        return lines
