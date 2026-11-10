"""Debrief: the judge model scores the conversation with the scenario's rubric.

- The JSON schema has one fixed field per rubric item (item_1 .. item_11), so the model must give every
  item exactly once.
- The judge runs `judge_runs` times (default 2), in parallel. Run 2 sees the rubric in reverse order, so it
  is a real second opinion even at temperature 0. Per item the lowest score wins; if the runs disagree the
  item is marked uncertain.
- Python checks every quote (it must be literal from the STUDENT) and applies the score rules:
  "do" items (e.g. names the emotion): a 1 or 2 needs a literal quote, otherwise 0 (it never happened).
  "avoid" items (e.g. not defensive): a 2 is allowed without quote (the bad behaviour never happened);
  the quote then shows the bad sentence if there was one.
  "if_needed" items (set boundaries): a 2 is allowed without quote (it was not needed), a 1 needs a quote.
- feedback must be Dutch and better_phrase English; if one run got the language wrong, the other run's
  text is used.
- The effect of the key moments is computed from the log, not taken from the model.
"""
from __future__ import annotations

import json
import re
import time
from concurrent.futures import ThreadPoolExecutor
from typing import Any, Literal, Optional

from pydantic import BaseModel, ConfigDict, Field, ValidationError, create_model

from llm_client import LlmClient, LlmOutputError


class DebriefItem(BaseModel):
    model_config = ConfigDict(extra="forbid")
    score: Literal[0, 1, 2]
    evidence_quote: str
    feedback: str = Field(min_length=1)
    better_phrase: str = Field(min_length=1)


class KeyMoment(BaseModel):
    model_config = ConfigDict(extra="forbid")
    turn_index: int
    what_happened: str = Field(min_length=1)
    effect: str


def make_output_model(rubric_ids: list[int]) -> type[BaseModel]:
    fields: dict[str, Any] = {f"item_{i}": (DebriefItem, ...) for i in rubric_ids}
    fields["key_moments"] = (list[KeyMoment], Field(max_length=3))
    fields["summary"] = (str, Field(min_length=1))
    return create_model("DebriefOutput", __config__=ConfigDict(extra="forbid"), **fields)


_DUTCH = re.compile(r"\b(ik|u|uw|je|jij|jouw|het|een|de|niet|nodig|dat|zijn|heeft|wordt|en|wat|voor|met|"
                    r"zonder|nu|dus|kom|terug|bel|wil|er|naar|over|ook|heel|eerst|om|bij|van|op|maar|geen|"
                    r"hebt|bent|kan|moet|uur|vader|nee|ja)\b", re.I)
_ENGLISH = re.compile(r"\b(i|you|your|the|a|an|to|is|are|and|of|for|can|will|me|it|that|this|what|now|"
                      r"please|let|we|my|be|have|with|about|i'm|you're|i'll|he|his|feel|know)\b", re.I)


def _is_dutch(text: str) -> bool:
    return len(_DUTCH.findall(text)) > len(_ENGLISH.findall(text))


class _Translations(BaseModel):
    model_config = ConfigDict(extra="forbid")
    english: list[str]


def _translate_to_english(llm: LlmClient, phrases: list[str]) -> list[str]:
    """One short call for all better_phrases that came out in Dutch."""
    numbered = "\n".join(f"{i + 1}. {p}" for i, p in enumerate(phrases))
    raw = llm.chat_json(
        "Translate each Dutch sentence into natural spoken English, as words a nurse says. "
        "Keep the same order and the same number of sentences.",
        [{"role": "user", "content": numbered}], _Translations.model_json_schema(), role="judge")
    out = _Translations.model_validate(raw).english
    if len(out) != len(phrases):
        raise LlmOutputError(f"translated {len(out)} of {len(phrases)} phrases")
    return out


def _norm(text: str) -> str:
    return re.sub(r"\s+", " ", re.sub(r"[^\w\s']", " ", text.lower())).strip()


def build_log(session) -> list[dict[str, Any]]:
    """The facts per turn that the judge gets (and that Unity draws)."""
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
            "reply": t.get("reply", ""),
        })
    return rows


def _prompt(session, log: list[dict], reverse: bool) -> tuple[str, str]:
    sc = session.scenario
    rubric = list(sc.data["rubric"])
    if reverse:
        rubric = rubric[::-1]
    rubric_text = "\n".join(
        f'item_{r["id"]} ({r.get("kind", "do")}): {r["criterion"]} | 0: {r["levels"]["0"]} | '
        f'1: {r["levels"]["1"]} | 2: {r["levels"]["2"]}'
        for r in rubric
    )
    name = sc.data["persona"]["name"]
    system = (
        "You are a strict but fair communication trainer for nursing students. Score the STUDENT in this "
        f"conversation with {name} ({sc.data['persona']['description'][0]})\n"
        f"Rubric:\n{rubric_text}\n\n"
        "Rules:\n"
        f"- evidence_quote: words copied exactly from a STUDENT line. Never quote {name}.\n"
        "- 'do' items: score 1 or 2 only with an evidence_quote of the student doing it. "
        "If the student never did it: score 0 and evidence_quote \"\".\n"
        "- 'avoid' items (bad behaviour to avoid): if the student did the bad thing, quote that sentence "
        "and give 0 or 1. If it never happened, give 2 with evidence_quote \"\".\n"
        "- 'if_needed' items: 2 with evidence_quote \"\" if it was not needed; otherwise like a 'do' item.\n"
        "- The labels per turn come from an automatic classifier: a hint only, check the student's words "
        "yourself. quote_fallback means the classifier was unsure.\n"
        "- feedback: one short sentence in DUTCH (Nederlands) to the student, max 15 words. Use 'je', "
        "never 'u'. Example: \"Je benoemde haar angst, daardoor voelde ze zich gehoord.\"\n"
        "- better_phrase: always one short ENGLISH sentence with the exact words the nurse says to "
        f"{name} (not an instruction), also when the score is 2. It must be NEW: not a sentence the student "
        f"or {name} already said. Example: \"I can see you're scared for your father.\"\n"
        "- The evidence_quote must really show THIS item (e.g. item 7 needs an open question).\n"
        "- key_moments: at most 3 student turns that changed the conversation most (turn_index = turn "
        "number), what_happened in Dutch (one sentence), effect like \"anger +10\".\n"
        "- summary: exactly 2 sentences in Dutch, encouraging and honest."
    )
    lines = [f'Turn 0. {name}: "{sc.data["opening"]["reply"]}"']
    for r in log:
        extra = ", quote_fallback" if r["quote_fallback"] else ""
        extra += ", off_topic_or_unsafe" if r["off_topic_or_unsafe"] else ""
        lines.append(
            f'Turn {r["turn"]}. STUDENT: "{r["student"]}"\n'
            f'   labels: {", ".join(r["labels"]) or "-"}{extra}; anger {r["anger"][0]}->{r["anger"][1]}, '
            f'trust {r["trust"][0]}->{r["trust"][1]}\n'
            f'   {name}: "{r["reply"]}"'
        )
    end = session.ended or "stopped"
    user = "Conversation (end: " + end + "):\n" + "\n".join(lines) + "\n\nScore the student now."
    return system, user


def _one_run(llm: LlmClient, session, log, reverse: bool) -> tuple[Optional[BaseModel], dict]:
    system, user = _prompt(session, log, reverse)
    ids = [r["id"] for r in session.scenario.data["rubric"]]
    model = make_output_model(ids)
    info = {"reverse": reverse, "ms": 0, "in": 0, "out": 0, "attempts": 0, "errors": []}
    t = time.perf_counter()
    for attempt in (1, 2):
        info["attempts"] = attempt
        try:
            raw = llm.chat_json(system, [{"role": "user", "content": user}], model.model_json_schema(), role="judge")
            usage = llm.last_usage()
            info["in"] += usage.get("in", 0)
            info["out"] += usage.get("out", 0)
            out = model.model_validate(raw)
            info["ms"] = int((time.perf_counter() - t) * 1000)
            return out, info
        except (ValidationError, LlmOutputError) as exc:
            info["errors"].append(str(exc).splitlines()[0])
    info["ms"] = int((time.perf_counter() - t) * 1000)
    return None, info


def _check_item(rid: int, kind: str, item: DebriefItem, student_texts: list[str], all_texts: list[str],
                notes: list[str]) -> dict:
    d = item.model_dump()
    quote = d["evidence_quote"].strip().strip("\"'“”‘’").strip()
    if quote and not any(_norm(quote) in _norm(s) for s in student_texts if s):
        notes.append(f"item {rid}: quote is not from the student, removed: {quote!r}")
        quote = ""
    if ((kind == "do" and d["score"] > 0) or (kind == "if_needed" and d["score"] == 1)) and not quote:
        notes.append(f"item {rid}: score {d['score']} without a student quote, set to 0")
        d["score"] = 0
    d["evidence_quote"] = quote
    # better_phrase must be English and new (not a line of the student or the character)
    d["better_phrase_dutch"] = _is_dutch(d["better_phrase"])
    d["better_phrase_copied"] = any(_norm(d["better_phrase"]) == _norm(s) for s in all_texts if s)
    d["feedback_dutch"] = _is_dutch(d["feedback"])
    d["feedback_formal"] = bool(re.search(r"\b(u|uw)\b", d["feedback"], re.I))  # we want 'je', not 'u'
    return d


def _cross_check_labels(items: list[dict], rubric: dict, log: list[dict], notes: list[str]) -> None:
    """The classifier labels per turn as a second opinion (a help, not a replacement).

    do / if_needed items: points from the judge but the related label never fell -> uncertain, one point
    less; the quote's turn has none of the labels (but another turn does) -> uncertain; the label fell but
    the judge gave 0 -> uncertain. avoid items (the labels are the bad behaviour): judge says it never
    happened (2) but the classifier saw it -> uncertain, one point less; judge saw it but the classifier
    never did -> uncertain.
    """
    for item in items:
        labels = set(rubric[item["id"]].get("evidence_labels", []))
        if not labels:
            continue
        turns_with = [r for r in log if labels & set(r["labels"])]
        quote_turn = next((r for r in log if item["evidence_quote"]
                           and _norm(item["evidence_quote"]) in _norm(r["student"])), None)
        reason = None
        if item["kind"] == "avoid":
            if item["score"] == 2 and turns_with:
                reason = f"classifier saw {sorted(labels & set(turns_with[0]['labels']))} in turn {turns_with[0]['turn']}"
                item["score"] = 1
            elif item["score"] < 2 and not turns_with:
                reason = "classifier never saw this bad behaviour"
        else:
            if item["score"] > 0 and not turns_with:
                if item["kind"] == "if_needed" and item["score"] == 2 and not item["evidence_quote"]:
                    continue  # not needed: nothing to check
                reason = f"classifier never saw {sorted(labels)}"
                item["score"] -= 1
            elif item["score"] > 0 and quote_turn is not None and not labels & set(quote_turn["labels"]):
                reason = f"quote is from turn {quote_turn['turn']}, classifier saw it in turn {turns_with[0]['turn']}"
            elif item["score"] == 0 and turns_with:
                reason = f"classifier saw {sorted(labels & set(turns_with[0]['labels']))} in turn {turns_with[0]['turn']}"
        if reason:
            item["uncertain"] = True
            item["label_check"] = reason
            notes.append(f"item {item['id']}: {reason} -> uncertain, score {item['score']}")


def run_debrief(llm: LlmClient, session, runs: int = 2) -> dict[str, Any]:
    t_start = time.perf_counter()
    log = build_log(session)
    sc = session.scenario
    student_texts = [r["student"] for r in log]
    all_texts = student_texts + [r["reply"] for r in log] + [sc.data["opening"]["reply"]]
    rubric = {r["id"]: r for r in sc.data["rubric"]}

    with ThreadPoolExecutor(max_workers=max(1, runs)) as pool:
        futures = [pool.submit(_one_run, llm, session, log, i % 2 == 1) for i in range(runs)]
        results = [f.result() for f in futures]

    valid = [out for out, _ in results if out is not None]
    run_info = [info for _, info in results]
    if not valid:
        raise LlmOutputError("judge failed in every run: " + "; ".join(e for i in run_info for e in i["errors"]))

    notes: list[str] = []
    if len(valid) < runs:
        notes.append(f"only {len(valid)} of {runs} judge runs were valid: no uncertainty check")
    checked = [{rid: _check_item(rid, rubric[rid].get("kind", "do"), getattr(out, f"item_{rid}"), student_texts, all_texts, notes)
                for rid in rubric} for out in valid]
    items = []
    for rid, r in rubric.items():
        candidates = [c[rid] for c in checked]
        lowest = min(candidates, key=lambda c: c["score"])
        # a better phrase that is English and new, from any run (Dutch ones are translated below)
        better = lowest["better_phrase"]
        if lowest["better_phrase_dutch"] or lowest["better_phrase_copied"]:
            usable = [c["better_phrase"] for c in candidates
                      if not c["better_phrase_dutch"] and not c["better_phrase_copied"]]
            english = [c["better_phrase"] for c in candidates if not c["better_phrase_dutch"]]
            better = (usable or english or [better])[0]
        feedback = lowest["feedback"]
        if not lowest["feedback_dutch"] or lowest["feedback_formal"]:
            same = [c for c in candidates if c["feedback_dutch"] and c["score"] == lowest["score"]]
            informal = [c["feedback"] for c in same if not c["feedback_formal"]]
            dutch = [c["feedback"] for c in same]
            feedback = (informal or dutch or [feedback])[0]
            if not _is_dutch(feedback):
                notes.append(f"item {rid}: feedback is not Dutch")
        items.append({
            "id": rid,
            "kind": r.get("kind", "do"),
            "criterion": r["criterion"],
            "criterion_nl": r.get("criterion_nl", r["criterion"]),
            "score": lowest["score"],
            "scores": [c["score"] for c in candidates],
            "uncertain": len({c["score"] for c in candidates}) > 1,
            "evidence_quote": lowest["evidence_quote"],
            "feedback": feedback,
            "better_phrase": better,
        })

    _cross_check_labels(items, rubric, log, notes)

    # better phrases that are still Dutch: translate them in one extra call
    dutch = [i for i in items if _is_dutch(i["better_phrase"])]
    if dutch:
        try:
            for item, english in zip(dutch, _translate_to_english(llm, [i["better_phrase"] for i in dutch])):
                notes.append(f"item {item['id']}: better_phrase translated: {item['better_phrase']!r} -> {english!r}")
                item["better_phrase"] = english
        except (ValidationError, LlmOutputError) as exc:
            notes.append(f"translation of better phrases failed: {exc}")

    # key moments from the first valid run; effect and anger from the log, not from the model
    by_turn = {r["turn"]: r for r in log}
    moments = []
    for m in valid[0].key_moments:
        r = by_turn.get(m.turn_index)
        if r is None or any(x["turn_index"] == m.turn_index for x in moments):
            notes.append(f"key moment with unknown or double turn {m.turn_index} dropped")
            continue
        da, dt = r["anger"][1] - r["anger"][0], r["trust"][1] - r["trust"][0]
        moments.append({"turn_index": m.turn_index, "what_happened": m.what_happened,
                        "effect": f"anger {da:+d}, trust {dt:+d}", "model_effect": m.effect,
                        "anger_after": r["anger"][1]})

    opening = sc.data["opening"]
    return {
        "session_id": session.id,
        "scenario_id": sc.id,
        "end_reason": session.ended or "stopped",
        "summary": valid[0].summary,
        "items": items,
        "total_score": sum(i["score"] for i in items),
        "max_score": 2 * len(items),
        "key_moments": moments,
        "anger_curve": [opening["anger"]] + [r["anger"][1] for r in log],
        "trust_curve": [opening["trust"]] + [r["trust"][1] for r in log],
        "notes": notes,
        "runs": run_info,
        "timings_ms": {"total_ms": int((time.perf_counter() - t_start) * 1000),
                       "runs_ms": [i["ms"] for i in run_info]},
    }


if __name__ == "__main__":  # print the saved debrief of a session: python debrief.py sessions/<id>.json
    import sys
    print(json.dumps(json.load(open(sys.argv[1], encoding="utf-8")).get("debrief"), indent=2, ensure_ascii=False))
