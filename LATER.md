# Later (ideas, not built)

Ideas from the overnight run of 2026-11-23 (feature/jalil/learnable-conversation). Deliberately not built, to keep the
scope tight.

## Gameplay
- **Walking with the patient** (out of bed, to the window, the corridor) as a later step of the scenario. Now every
  walking suggestion is ignored on purpose (`drop_if_any`).
- **Talking to people in the corridor** (other patients, visitors, colleagues) between tasks.
- **More tasks and rooms in the shift**: `AiShift` already has a task list (data). The flow only plays the first task. It
  needs a patient, a room and a conversation spot per task.
- **A critical error with consequences instead of an ending**: keep talking, and the trust collapses in the next shift
  (Jalil's earlier idea, see ARCHITECTURE 20).
- **Best attempt instead of last attempt** in the shift report and the total score.
- **Guide hints that light up** for the patient's current state (now the nurse decides which hint applies, by looking in
  the patient view; that was the request).

## Content
- Translate Liam's debrief panel and its feedback texts (`AiDebriefPanel`, the rubric `feedback`, `critical_feedback`,
  `summary_fallback`) to English, so all in-game text is English.
- Case and guide texts for Liam's other scenarios (the `case` and `guide` sections are optional).
- A real model of a computer on the reception desk instead of the grey box.

## AI
- The 4B model still labels "antibiotics through the drip" as jargon now and then (see OVERNIGHT_REPORT). Options: an
  optional `drop_if_any` for words that are never jargon, or a bigger model.
- A rude first sentence is sometimes labelled dismissive instead of unsafe (it still costs a lot).
