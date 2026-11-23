"""Runs tests/disappointed_patient.jsonl with the real model (Ollama) and reports which sentences it labels wrong.

    .venv\\Scripts\\python tests\\run_label_tests.py [file.jsonl]

Each line: {"text", "expect": [labels that must be there], "allow": [also fine], "forbid": [must not be there],
"unsafe": true (must be flagged unsafe)}. Every sentence is the first turn of a fresh session, so the labels pass the
same filters as in a conversation (drop_if_any, upgrade_to, max_labels_per_turn). Exit code 1 if any line is wrong.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(HERE))

from engine import Engine  # noqa: E402
from llm_client import LlmClient  # noqa: E402


def main() -> None:
    path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).with_name("disappointed_patient.jsonl")
    scenario = path.stem
    cfg = json.loads((HERE / "config.json").read_text(encoding="utf-8"))
    eng = Engine(LlmClient(cfg), HERE / "scenarios")
    cases = [json.loads(line) for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    wrong = []
    for i, case in enumerate(cases, 1):
        session, _ = eng.start_session(scenario)
        r = eng.run_turn(session, case["text"])
        got = set(r["labels"])
        expect, allow, forbid = set(case.get("expect", [])), set(case.get("allow", [])), set(case.get("forbid", []))
        problems = []
        if expect - got:
            problems.append(f"missing {sorted(expect - got)}")
        extra = got - expect - allow
        if extra:
            problems.append(f"unexpected {sorted(extra)}")
        if got & forbid:
            problems.append(f"forbidden {sorted(got & forbid)}")
        if case.get("unsafe") and not r["unsafe"]:
            problems.append("not flagged unsafe")
        ok = not problems
        flags = " unsafe" if r["unsafe"] else ""
        print(f"{'OK  ' if ok else 'WRONG'} {i:2d}. {case['text']}\n       got {sorted(got)}{flags}"
              + ("" if ok else f"  ({'; '.join(problems)})  [{case.get('note', '')}]"))
        if not ok:
            wrong.append((i, case, sorted(got), problems))
    print(f"\n{len(cases) - len(wrong)}/{len(cases)} correct")
    for i, case, got, problems in wrong:
        print(f"  wrong {i}: {case['text']!r}: {'; '.join(problems)}")
    sys.exit(1 if wrong else 0)


if __name__ == "__main__":
    main()
