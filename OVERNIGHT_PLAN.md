# Overnight plan: learnable conversation (feature/jalil/learnable-conversation)

Base: `origin/jalil` at a74702f (contains fix/jalil/prototype1-playtest4, PR #30). Worktree `Project_prototype1`
(the main worktree has uncommitted work and stays untouched). One commit per step, compile check + tests before each.

1. **Scoring** (engine.py, disappointed_patient.json): `gave_concrete_timeline` + doctor fact; real penalties for
   `false_discharge_promise`; optional `rules.max_labels_per_turn` (default unlimited) and `phases[].min_turns`
   (default 0); tune steps so a good talk takes 10-14 turns; scenario sends progress thresholds (`meta.progress`) so
   ConversationProgressBar reads them instead of hard-coded numbers. Remove every walk/stroll/corridor mention.
   Tests: `test_conversation_length.py` (good finishes, rude escalates), Liam's other scenarios still load.
2. **One rule for happy**: the server's emotion (filtered by band and lost trust) is the only source; remove
   `_joyTrust` and `_neutralTension` from AiPatientBridge.
3. **Learnable**: scenario JSON gets optional `case` and `guide` sections, served in `opening.meta`; the clipboard
   shows Case (practical briefing + goal list + subtle warning) and a new Guide page (hints per patient state).
4. **Patient POV is the feedback**: no progress bar, trust or emotion in the nurse view (dev page only behind a dev
   flag, off); POV HUD gets last-change arrows; effects and aura respond within a few seconds.
5. **Tests**: `tests/disappointed_patient.jsonl` (20+ sentences) + runner with the real model; extend
   `RunPrototype1` (nurse view hides state, POV shows it, Guide page, text conversation reaches 10+ turns).
6. **Game loop**: lobby, Tasks page (one task from a small data list), start button at the reception desk, task
   In progress, door sign/map point to 1A, talk, ending card, doctor debrief, task Done/Failed + shift report,
   Try again / Finish shift (end screen with the total). Smoke test covers the loop.
7. **Finish**: ARCHITECTURE.md, OVERNIGHT_REPORT.md, LATER.md, push the feature branch once, one PR into `jalil`.
