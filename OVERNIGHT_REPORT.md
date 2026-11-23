# Overnight report: learnable conversation (2026-11-23)

Developer: Jalil (from the task). Branch `feature/jalil/learnable-conversation`, from `origin/jalil` (a74702f, contains
playtest4 / PR #30). Worked in the worktree `Project_prototype1`. The main worktree has uncommitted work and was not touched.
All 7 steps done, nothing reverted. Details per step: ARCHITECTURE.md 20.1 to 20.6.

## Summary

| Step | Commit | What |
| --- | --- | --- |
| 1 Scoring | dd843b3 | Optional engine rules `rules.max_labels_per_turn`, `phases[].min_turns`, `labels[].drop_if_any`, `labels[].upgrade_to` (default = old behaviour). `gave_concrete_timeline` + doctor fact ("comes by this evening"). `false_discharge_promise` costs tension +10 / trust -15 before the end. Smaller steps, max 2 labels, min turns 3/4/3: a good conversation takes 10+ turns. Progress bar reads its limits from the scenario (`meta.progress`). Walking removed everywhere. |
| 2 Happy | eb0b9f3 | The server's emotion is the only rule: `_joyTrust` 70 and `_neutralTension` 55 removed from `AiPatientBridge`. Fixed lines (critical reaction, fallback) after lost trust are never Happy either; the false-discharge reaction is now doubtful, not "thank you". |
| 3 Learnable | 65f9205 | Optional `case` and `guide` in the scenario JSON, served in the opening `meta` and `GET /scenarios/{id}/meta`. Clipboard Case page: practical briefing + GOAL list + "Only the doctor can decide about discharge. Be careful with promises." New Guide page: hints per patient state, titled by what you see in the patient view. Door sign rewritten. |
| 4 POV feedback | 9ee85f0 | Nurse view: no progress bar, no Dev page (dev flags, off). Patient view HUD: arrows for the last change (trust "▼ -10", emotion ▲/▼). Aura grows within its tier (`Edge Strength` 0.35), emotion strength range 0.2 to 1. |
| 5 Tests | 74f083e | `tests/disappointed_patient.jsonl` (24 sentences) + `run_label_tests.py` with the real model; Unity text turns (`AiNpc.SendStudentText`); smoke test: typed good conversation resolves after 10+ turns. |
| 6 Game loop | e11b25a | `AiShift` + `AiShiftStartButton`: lobby, Tasks page (one task, Waiting), start at the reception computer, In progress, door and map point to 1A, every ending on a black screen with the reason, Done/Failed + shift report, Try again / Finish shift (end screen with the total score). |
| 7 Finish | (this commit) | ARCHITECTURE.md, this report, LATER.md. |

## Tests that pass

- `Tools/AiServer/tests/test_engine_rules.py` (no model): all pass. All 5 scenarios load (Liam's 4 as well), the defaults
  behave as before, plus the label limit, `drop_if_any`, `upgrade_to`, `min_turns`, the false discharge and no Happy after
  lost trust.
- `Tools/AiServer/test_conversation_length.py` (real model, qwen3.5:4b): all pass. Good conversation `resolved` after **10**
  turns (every run this night: 4 in Python, 3 in Unity). Rude: escalated after 2 turns. False discharge: critical error, trust 28 to 13 and tension 70
  to 80 first. Walking suggestion: no labels.
- `tests/run_label_tests.py` (real model): **23/24**.
- Unity batchmode compile check: no errors (before every commit).
- `PlayModeSmokeTest.RunPrototype1` (real AI server): passes. The full loop: lobby, nothing before the start, start
  button, In progress, door sign and map, conversation, hands-free sentence, nurse view without bar, trust or emotion, patient
  view with HUD, arrows and aura depth, Case/Guide/Tasks, finish (CONVERSATION ENDED, Done, report), Try again, typed good
  conversation (10 turns, CONVERSATION FINISHED, Done, score 12/14), Try again, critical error (CRITICAL ERROR, Failed),
  Finish shift (SHIFT FINISHED, total score).
- The 20 other smoke tests (EmotionHud, EmotionWorld, PatientViewTest, EmotionModel, PlayerAvatar, PovSwitch, Scenario01
  PatientPov (+Shift), Scenario02 to 05 good/poor/fail/timeup, ShiftLoop, ShiftTimeout, Scenario01Mood): all pass (run after
  step 5). Step 6 only changed Prototype1 classes and one optional field of `AiDebriefPanel`, so they were not run again.

## What the 4B model gets wrong (label test)

- "Stop whining, nobody cares about your stupid party." as the **first** sentence: `dismissive` + `invalidated_feeling`
  instead of `unsafe`. It still costs a lot (tension +18, trust -18). After "Hi." it was flagged unsafe and escalated.
- Not in the label set, seen in the conversations: "...needs the antibiotics through the drip" gets `jargon` next to
  `explained_why_staying` (the jargon description now says these are everyday words; the model still does it). The debrief
  then says "Werkpunt: je gebruikte vakjargon" for a good sentence.
- Found and fixed during the night: the model labelled the patient's own sentence ("I just want to stay in bed and go home")
  as a false discharge promise, and mixed up `false_promise` and `false_discharge_promise`. Fixed with narrower keywords and
  `upgrade_to`.

## Not tested

- **Not on the Quest 3 and not via Quest Link.** Only the editor in batchmode (simulated HMD). Not tested: the real
  microphone, the laser on the start computer (the test calls `Press()`), button labels on the headset, 72 fps, the
  readability of the clipboard and the shift report in the headset.
- The AI server only runs on the laptop: Prototype1 works only in the editor or via Quest Link.
- A real escalated conversation through the whole loop in Unity (tested in Python, and the card in the earlier test).

## Changes to Liam's code and assets (all additive, nothing breaking)

- `AiTalkData.cs`: `meta` and `turn` fields, meta classes, `AiTextRequest`.
- `AiNpc.cs`: `Meta`, `SendStudentText`. `AiTalkClient.cs`: `GetScenarioMeta`, `SendText`.
- `AiDebriefPanel.cs`: optional `_retryLabel` / `_doneLabel` (empty = his Dutch labels).
- `Hospital/Data/Case_1A.asset`: in the best option "a short walk together" became "help you sit up a little and get you
  something to drink" (text only).
- `server.py`: new `GET /scenarios/{id}/meta`. `engine.py`: the optional rules above. His other scenario files are unchanged.

## Open questions

1. **Language:** the in-game texts I wrote are English, but Liam's debrief panel and the rubric feedback are still Dutch
   ("Nabespreking", "Werkpunt..."). Should all of it be translated? (In LATER.md.) I kept my edits to existing Dutch feedback
   lines in Dutch.
2. Is 10 turns the right minimum, or should B2 be shorter (min_turns 3/4/3 = 10)? A good conversation now always takes exactly
   10 turns; a weaker one takes longer.
3. The walk words are still in the scenario, but only as filter words ("Walking does NOT count", `drop_if_any`). They make sure
   a walk never scores. Is that OK, or should the classifier prompt not mention it at all?
4. Shift score: last attempt (now) or best attempt?
5. The content (case, guide, label values, replies) was written by Claude: still to be checked by the content expert or a
   nursing lecturer.
6. Should CLAUDE.md "Status" and "Beslissingen" (and the Notion decision log) get these decisions? Not done, as CLAUDE.md asks
   to propose it first.
7. The clipboard listens to the briefing via `Update` because a C# event from `ScenarioBriefing` did not arrive in the test.
   Cause not found (harmless, but odd).

## Push / PR

The feature branch is pushed once and one PR into `jalil` is opened (see the PR). Nothing merged, nothing pushed to `main`,
`jalil` or `liam`.
