"""Debrief: rubric scores computed in Python from the labels per turn (no model for the scores).

Per rubric item the scenario says how to score it ("scoring"):
  positive  labels: 2 if one of them fell at least once (optionally only in the last N turns)
  partial   labels: 1 if only those fell
  avoid     labels: 2 if they NEVER fell, 0 if they fell (the quote of that turn is the evidence)
  score_if_neither: score when an item has both positive and avoid labels and neither fell
  two_if_anger_never_above: 2 if anger never went above this value (e.g. a boundary was not needed)
  two_if_phase_reached:     2 if the conversation reached this phase
  one_if_positive_earlier:  1 if a positive label fell, but not in the last N turns
The quote is the classifier's evidence_quote of the turn where the label fell; if that turn used the
quote fallback (the classifier was unsure) the item is marked uncertain.
Feedback (per score) and the better phrase are fixed texts in the scenario. Key moments are the 3 turns
with the biggest change in anger. Only the 2-sentence summary comes from the model (actor model, short,
with a timeout); if that fails, a fixed sentence from the scenario is used.
"""
from __future__ import annotations

import json
import re
import time
from concurrent.futures import ThreadPoolExecutor, TimeoutError as FutureTimeout
from typing import Any, Optional

from pydantic import BaseModel, ConfigDict, Field, ValidationError

from llm_client import LlmClient, LlmOutputError, LlmUnavailableError

_pool = ThreadPoolExecutor(max_workers=2, thread_name_prefix="summary")

_DUTCH = re.compile(r"\b(ik|u|uw|je|jij|jouw|het|een|de|niet|nodig|dat|zijn|heeft|wordt|en|wat|voor|met|"
                    r"zonder|nu|dus|kom|terug|bel|wil|er|naar|over|ook|heel|eerst|om|bij|van|op|maar|geen|"
                    r"hebt|bent|kan|moet|uur|vader|nee|ja)\b", re.I)
_ENGLISH = re.compile(r"\b(i|you|your|the|a|an|to|is|are|and|of|for|can|will|me|it|that|this|what|now|"
                      r"please|let|we|my|be|have|with|about|i'm|you're|i'll|he|his|feel|know)\b", re.I)


def _is_dutch(text: str) -> bool:
    return len(_DUTCH.findall(text)) > len(_ENGLISH.findall(text))


class _Summary(BaseModel):
    """Two fields, one short sentence each: keeps a small model from writing about the task instead."""
    model_config = ConfigDict(extra="forbid")
    zin_sterk_punt: str = Field(min_length=10, max_length=180)
    zin_werkpunt: str = Field(min_length=10, max_length=180)


def build_log(session) -> list[dict[str, Any]]:
    """The facts per turn that the scores, the key moments and Unity's graph come from."""
    rows = []
    for t in session.turns:
        rows.append({
            "turn": t["turn"],
            "student": t.get("student_text", ""),
            "labels": t.get("labels", []),
            "quote": t.get("evidence_quote", ""),
            "quote_fallback": any("using the whole sentence" in n for n in t.get("notes", [])),
            "off_topic_or_unsafe": bool(t.get("off_topic") or t.get("unsafe")),
            "anger": [t["before"]["anger"], t["after"]["anger"]],
            "trust": [t["before"]["trust"], t["after"]["trust"]],
            "phase": t["after"]["phase"],
            "reply": t.get("reply", ""),
        })
    return rows


def _first_hit(log: list[dict], labels: list[str]) -> Optional[dict]:
    """First turn with a label, in the priority order of the scenario list (the first label is the best
    evidence), so e.g. an apology is the evidence for 'shows understanding' rather than 'offered support'."""
    for label in labels:
        hit = next((r for r in log if label in r["labels"]), None)
        if hit is not None:
            return hit
    return None


def score_item(item: dict, log: list[dict], phases: list[str], opening_anger: int) -> dict:
    rule = item["scoring"]
    positive, partial, avoid = rule.get("positive", []), rule.get("partial", []), rule.get("avoid", [])
    last_n = rule.get("positive_in_last_turns")
    score, hit, why = 0, None, ""

    avoid_hit = _first_hit(log, avoid) if avoid else None
    positive_log = log[-last_n:] if last_n else log
    positive_hit = _first_hit(positive_log, positive) if positive else None
    partial_hit = _first_hit(log, partial) if partial else None

    if avoid_hit:
        score, hit = 0, avoid_hit
        why = "avoid label " + ", ".join(sorted(set(avoid) & set(avoid_hit["labels"])))
    elif positive_hit:
        score, hit = 2, positive_hit
        why = "positive label " + ", ".join(sorted(set(positive) & set(positive_hit["labels"])))
    elif partial_hit:
        score, hit = 1, partial_hit
        why = "partial label " + ", ".join(sorted(set(partial) & set(partial_hit["labels"])))
    elif "two_if_anger_never_above" in rule and max([opening_anger] + [r["anger"][1] for r in log]) <= rule["two_if_anger_never_above"]:
        score, why = 2, f"anger never above {rule['two_if_anger_never_above']} (not needed)"
    elif "two_if_phase_reached" in rule and rule["two_if_phase_reached"] in phases and any(
            r["phase"] in phases and phases.index(r["phase"]) >= phases.index(rule["two_if_phase_reached"]) for r in log):
        score, why = 2, f"phase {rule['two_if_phase_reached']} reached"
    elif rule.get("one_if_positive_earlier") and _first_hit(log, positive):
        score, hit = 1, _first_hit(log, positive)
        why = "positive label earlier, not at the end"
    elif avoid and not positive:
        score, why = 2, "avoid labels never fell"
    elif avoid and positive:
        score, why = rule.get("score_if_neither", 1), "neither positive nor avoid labels fell"
    else:
        why = "label never fell"

    # One evidence_quote per turn: with several labels in that turn it may belong to another label,
    # so then the whole sentence of the student is the evidence.
    if hit is None:
        quote = ""
    elif len(hit["labels"]) > 1 or not hit["quote"]:
        quote = hit["student"]
    else:
        quote = hit["quote"]
    return {
        "id": item["id"],
        "criterion": item["criterion"],
        "criterion_nl": item.get("criterion_nl", item["criterion"]),
        "score": score,
        "uncertain": bool(hit and hit["quote_fallback"]),
        "evidence_quote": quote,
        "evidence_turn": hit["turn"] if hit else None,
        "feedback": item["feedback"][str(score)],
        "better_phrase": item["better_phrase"],
        "why": why,
    }


def key_moments(sc, log: list[dict], count: int = 3) -> list[dict]:
    """The turns with the biggest change in anger, in conversation order."""
    texts = {l["name"]: l.get("moment_nl", l["name"]) for l in sc.data["labels"]}
    cfg = sc.data.get("debrief", {})
    changed = [r for r in log if r["anger"][1] != r["anger"][0]]
    top = sorted(changed, key=lambda r: (-abs(r["anger"][1] - r["anger"][0]), r["turn"]))[:count]
    moments = []
    for r in sorted(top, key=lambda r: r["turn"]):
        if r["off_topic_or_unsafe"]:
            text = cfg.get("moment_off_topic", "off topic")
        elif r["labels"]:
            parts = [texts.get(l, l) for l in r["labels"][:2]]
            text = parts[0] + (" en " + re.sub(r"^Je ", "", parts[1]) if len(parts) > 1 else "")
        else:
            text = cfg.get("moment_no_label", "-")
        da, dt = r["anger"][1] - r["anger"][0], r["trust"][1] - r["trust"][0]
        moments.append({"turn_index": r["turn"], "what_happened": text, "quote": r["quote"],
                        "effect": f"anger {da:+d}, trust {dt:+d}", "anger_after": r["anger"][1]})
    return moments


def _fallback_summary(sc, total: int) -> str:
    options = sorted(sc.data.get("debrief", {}).get("summary_fallback", []), key=lambda o: -o["min_score"])
    return next((o["text"] for o in options if total >= o["min_score"]), "Goed geprobeerd.")


def strength_and_weakness(items: list[dict]) -> tuple[Optional[dict], Optional[dict]]:
    """Python picks what the summary is about: the first item with 2 (that was really done, not just
    'never did the bad thing') and the first item with 0, so the model cannot invent other points."""
    done = [i for i in items if i["score"] == 2 and i["evidence_quote"]]
    strength = done[0] if done else None
    zeros = [i for i in items if i["score"] == 0]
    ones = [i for i in items if i["score"] == 1]
    weakness = (zeros or ones or [None])[0]
    return strength, weakness


def _model_summary(llm: LlmClient, sc, items: list[dict], moments: list[dict], end: str, num_predict: int) -> str:
    name = sc.data["persona"]["name"]
    strength, weakness = strength_and_weakness(items)
    total = sum(i["score"] for i in items)
    good = (f'Sterk punt: {strength["criterion_nl"]} ({strength["feedback"]})' if strength
            else "Sterk punt: geen. Zeg dat het een moeilijk gesprek was en dat oefenen helpt.")
    work = (f'Werkpunt: {weakness["criterion_nl"]} ({weakness["feedback"]})' if weakness
            else "Werkpunt: geen. Moedig aan om het zo te blijven doen.")
    system = (
        "Je bent een vriendelijke communicatietrainer voor verpleegkundestudenten. Schrijf 2 korte zinnen "
        f"in het Nederlands tegen de student over het gesprek met {name}: zin_sterk_punt over het sterke punt, "
        "zin_werkpunt over het werkpunt. Elke zin maximaal 20 woorden. Spreek de student aan met 'je'. "
        "Wees bemoedigend en eerlijk. Schrijf alleen de zinnen zelf, geen uitleg over de opdracht. "
        "Gebruik ALLEEN de twee punten hieronder, noem niets anders. Schrijf nooit 'de student'.\n"
        "Voorbeeld van de vorm (over een ander gesprek): \"Je luisterde goed naar de patiënt, daardoor durfde "
        "hij meer te vertellen. Probeer de volgende keer wat trager te praten.\""
    )
    user = f"Score: {total}/{2 * len(items)}. Einde: {end}.\n{good}\n{work}"
    problem = ""
    for _ in range(2):
        raw = llm.chat_json(system, [{"role": "user", "content": user + problem}], _Summary.model_json_schema(),
                            role="actor", num_predict=num_predict, temperature=0.3)
        out = _Summary.model_validate(raw)
        parts = [x.strip().rstrip(".!") + "." for x in (out.zin_sterk_punt, out.zin_werkpunt)]
        text = " ".join(parts)
        sentences = [s for s in re.split(r"(?<=[.!?])\s+", text) if s]
        if not _is_dutch(text):
            problem = "\n(Vorige poging was niet in het Nederlands.)"
        elif not re.search(r"\b(je|jij|jouw)\b", text, re.I) or re.search(r"\bde student\b", text, re.I):
            problem = "\n(Vorige poging sprak de student niet aan met 'je'.)"
        elif len(sentences) != 2:
            problem = "\n(Vorige poging had geen 2 zinnen.)"
        else:
            return text
    raise LlmOutputError(f"summary not usable: {text!r}")


def run_debrief(llm: LlmClient, session) -> dict[str, Any]:
    t_start = time.perf_counter()
    sc = session.scenario
    log = build_log(session)
    phases = [p["id"] for p in sc.phases]
    opening = sc.data["opening"]
    items = [score_item(r, log, phases, opening["anger"]) for r in sc.data["rubric"]]
    moments = key_moments(sc, log)
    total = sum(i["score"] for i in items)
    t_scores = int((time.perf_counter() - t_start) * 1000)

    cfg = sc.data.get("debrief", {})
    notes: list[str] = []
    t = time.perf_counter()
    if cfg.get("summary_mode", "template") == "template":
        # No model: the fixed feedback of the strongest and the weakest item
        strength, weakness = strength_and_weakness(items)
        if strength is None:
            summary = _fallback_summary(sc, total)
        else:
            first = strength["feedback"]["2"] if isinstance(strength["feedback"], dict) else strength["feedback"]
            summary = re.split(r"(?<=[.!?])\s+", first)[0]
            if weakness is not None:
                fact = re.split(r"(?<=[.!?])\s+", weakness["feedback"])[0]
                summary += " Werkpunt: " + fact[0].lower() + fact[1:]
            else:
                summary += " Ga zo door."
        return _result(session, sc, log, items, moments, total, summary, "template", notes, t_start, t_scores, t)
    if strength_and_weakness(items)[0] is None:
        # nothing was really done well: the model cannot write an honest first sentence, use the fixed one
        summary, source = _fallback_summary(sc, total), "fallback"
        notes.append("no strength to summarize, fixed sentence used")
        return _result(session, sc, log, items, moments, total, summary, source, notes, t_start, t_scores, t)
    future = _pool.submit(_model_summary, llm, sc, items, moments, session.ended or "stopped",
                          cfg.get("summary_num_predict", 150))
    try:
        summary, source = future.result(timeout=cfg.get("summary_timeout_seconds", 8)), "model"
    except FutureTimeout:
        summary, source = _fallback_summary(sc, total), "fallback"
        notes.append(f"summary took longer than {cfg.get('summary_timeout_seconds', 8)} s, fixed sentence used")
    except (LlmOutputError, LlmUnavailableError, ValidationError) as exc:
        summary, source = _fallback_summary(sc, total), "fallback"
        notes.append(f"summary failed ({str(exc).splitlines()[0]}), fixed sentence used")
    return _result(session, sc, log, items, moments, total, summary, source, notes, t_start, t_scores, t)


def _result(session, sc, log, items, moments, total, summary, source, notes, t_start, t_scores, t_summary_start):
    opening = sc.data["opening"]
    return {
        "session_id": session.id,
        "scenario_id": sc.id,
        "end_reason": session.ended or "stopped",
        "summary": summary,
        "summary_source": source,
        "items": items,
        "total_score": total,
        "max_score": 2 * len(items),
        "key_moments": moments,
        "anger_curve": [opening["anger"]] + [r["anger"][1] for r in log],
        "trust_curve": [opening["trust"]] + [r["trust"][1] for r in log],
        "notes": notes,
        "timings_ms": {"scores_ms": t_scores, "summary_ms": int((time.perf_counter() - t_summary_start) * 1000),
                       "total_ms": int((time.perf_counter() - t_start) * 1000)},
    }


if __name__ == "__main__":  # print the saved debrief of a session: python debrief.py sessions/<id>.json
    import sys
    print(json.dumps(json.load(open(sys.argv[1], encoding="utf-8")).get("debrief"), indent=2, ensure_ascii=False))
