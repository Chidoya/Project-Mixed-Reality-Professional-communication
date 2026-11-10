# AI server (free conversation with a scenario character)

A small local server that lets a student talk freely (in English) with a character such as
Sarah, the angry daughter. It runs next to Unity on the same PC and uses a local model through
[Ollama](https://ollama.com), speech recognition with
[faster-whisper](https://github.com/SYSTRAN/faster-whisper) and speech with
[Piper](https://github.com/OHF-Voice/piper1-gpl). Nothing is sent to the internet (only the
models are downloaded once).

Per turn: **classify** the student's sentence into labels (model, temperature 0) ->
**update anger/trust/phase** (plain Python, rules from the scenario file) ->
**actor** writes Sarah's reply (model, temperature 0.7) -> **log**.
All model output is forced to JSON with Ollama's `format` (a Pydantic JSON schema) and then
validated with Pydantic. Invalid output is retried once, after that a fallback line is used.

## Install (once)

1. Install Ollama and pull the model:
   ```
   ollama pull qwen3.5:4b
   ```
2. Python 3.11+ (tested with 3.13). In this folder:
   ```
   python -m venv .venv
   .venv\Scripts\pip install -r requirements.txt
   ```
3. The first start downloads the Whisper model `small.en` (about 460 MB) and the Piper voice
   (about 60 MB) into `models/` (not in git).

## Start

1. Make sure Ollama is running (the Ollama app in the tray, or `ollama serve`).
2. Start the server (it warms up the model, the first time takes a few seconds):
   ```
   .venv\Scripts\python server.py
   ```
3. In a second terminal, chat in the terminal:
   ```
   .venv\Scripts\python chat.py
   ```
   Type in English. An empty line counts as silence. `/quit` ends and saves the session.
   After every turn you see the labels, the quote, anger/trust before and after, band, phase and
   the reply.
4. Or talk with your microphone:
   ```
   .venv\Scripts\python test_audio.py
   ```
   Press Enter to start recording, Enter to stop. The reply is played and the waiting time per
   step is printed. `--wav a.wav b.wav` sends existing files instead, `--no-play` stays silent.

Transcripts are saved in `sessions/<id>.json` (not in git).

## Configuration (`config.json`)

| key | default | meaning |
| --- | --- | --- |
| `ollama_url` | `http://127.0.0.1:11434` | Ollama address (use 127.0.0.1, not localhost: on Windows localhost first tries IPv6 and every new connection then loses about 2 s) |
| `model_classifier`, `model_actor`, `model_judge` | `qwen3.5:4b` | model per role |
| `temperature_classifier` / `_actor` / `_judge` | 0 / 0.7 / 0 | temperature per role |
| `keep_alive` | `30m` | how long Ollama keeps the model in memory |
| `request_timeout_seconds` | 120 | timeout per model call |
| `num_predict_classifier` / `_actor` / `_judge` | 200 / 300 / 600 | max output tokens per role |
| `port` | 8765 | server port (listens on 127.0.0.1 only) |
| `stt_model` | `small.en` | faster-whisper model |
| `stt_try_gpu` | true | try CUDA first; falls back to CPU int8 when CUDA/cuDNN is missing |
| `stt_cpu_threads`, `stt_beam_size` | 8, 5 | CPU speech recognition settings |
| `stt_min_words` | 2 | shorter transcripts return `{"retry": true}` |
| `tts_voice` | `en_US-amy-medium` | Piper voice (same voice as Sarah's recorded lines in `Stemmen/`), downloaded when missing |
| `tts_length_scale_by_band` | 0.85 / 0.9 / 1.0 / 1.1 | speaking speed per band (lower = faster) |
| `models_dir` | `models` | where Whisper models and Piper voices are stored |

### Speech recognition on the GPU

On Windows, faster-whisper needs the CUDA 12 cuBLAS and cuDNN 9 DLLs for the GPU. Install them
in the venv (about 1.5 GB, NVIDIA GPU only):
```
.venv\Scripts\pip install -r requirements-gpu.txt
```
The server finds them automatically. If they are missing, it logs which ones and uses the CPU
(int8). `/health` shows the device in use. On an RTX 4070 Laptop: about 0.2-0.6 s per sentence
on the GPU, about 1.9 s on the CPU.

## API

| method | path | body | returns |
| --- | --- | --- | --- |
| GET | `/health` | | `{ok, models, sessions}` |
| POST | `/session/start` | `{scenario_id}` | `{session_id, opening: {reply, emotion, animation, pose, anger, trust, band, phase, audio_wav_base64, tts_ms}}` |
| POST | `/turn/text` | `{session_id, text}` | one turn (see below) |
| POST | `/turn/audio` | multipart: `session_id`, `audio` (wav, 16 kHz mono) | one turn + `transcript` + `audio_wav_base64`, or `{"retry": true}` |
| POST | `/session/{id}/end` | optional `{reason}` (e.g. `time_up`) | saves `sessions/<id>.json`, returns `{saved, end_reason, turns}`. The session stays in memory for `/debrief` |
| POST | `/session/{id}/debrief` | (`?force=true` to redo) | rubric scores, key moments, anger/trust curve; also saved in `sessions/<id>.json` |
| POST | `/session/{id}/event` | `{event}` (e.g. `curtain_closed`) | effect of a non-verbal action from the scenario's `events`, logged like a turn |
| GET | `/scenarios` | | id, title, number, character model and tension name of every scenario |

### Debrief (`debrief.py`)

The scores are computed in Python from the labels per turn, no model. Every rubric item in the scenario has
a `scoring` rule:
- `positive` labels: 2 if one of them fell (the first label in the list is the preferred evidence);
  `partial` labels: 1 if only those fell;
- `avoid` labels: 2 if they never fell, 0 if they fell (the quote of that turn is the evidence);
  with both positive and avoid labels and neither fell: `score_if_neither`;
- extras: `two_if_anger_never_above` (boundaries not needed), `positive_in_last_turns`,
  `two_if_phase_reached`, `one_if_positive_earlier` (closing agreement).

The quote is the classifier's `evidence_quote` of that turn (the whole sentence if the turn had several
labels). If that turn used the quote fallback, the item is `uncertain` (grey in Unity). Feedback for score
0, 1 and 2 and the better phrase are fixed texts in the scenario (`feedback`, `better_phrase`), to be checked
by a nurse. Key moments: the 3 turns with the biggest change in anger, described with `moment_nl` per label.

With `summary_mode: "model"` only the 2-sentence summary comes from the model (`model_actor`, `summary_num_predict` 150, temperature 0.3).
Python picks the strength (first item with 2 that was really done) and the point to work on (first 0), the
model only phrases them. Not Dutch, not 'je', not 2 sentences: one retry; failed or slower than
`summary_timeout_seconds` (8): a fixed sentence from `summary_fallback`. No strength at all: the fixed
sentence directly. `summary_mode` is `"template"` by default: the summary is built from the fixed feedback of the strength and the point to work on, without a model (instant). Set it to `"model"` to let the actor model phrase it.

Response: `{summary, summary_source (model | fallback | template), items: [{id, criterion_nl, score,
uncertain, evidence_quote, evidence_turn, feedback, better_phrase, why}], total_score, max_score,
key_moments: [{turn_index, what_happened, effect}], anger_curve, trust_curve, notes, timings_ms}`.
Time: 1-2.5 s (the summary), also with the Unity Editor in Play mode.

A turn returns: `reply, emotion, animation, pose, facts_used, labels, evidence_quote,
off_topic, unsafe, before {anger, trust, band, phase}, after {...}, anger, trust, band, phase,
ended (null | "resolved" | "time_up"), fallback_used, notes [...], timings_ms {classify_ms,
state_ms, actor_ms, total_ms}, tokens {classify: {in, out, calls}, actor: {in, out, calls}}`.

For `/turn/audio`, `timings_ms` also has `stt_ms`, `tts_ms` and `request_total_ms`, and these
are saved in the session log too (the audio itself is not). The uploaded wav is only processed in
memory and never written to disk. If the transcript is empty or shorter than `stt_min_words`, no
turn happens and the answer is `{"retry": true, "transcript": ..., "timings_ms": {"stt_ms": ...}}`.

Errors: `400` the upload is not a readable wav, `404` unknown session or scenario, `409` session already ended,
`503 {"error": "llm_unavailable", "detail": ...}` when Ollama is not running or the model is missing.

## Scenarios

Every scenario is a JSON file in `scenarios/` (1 `disappointed_patient`, 2 `angry_daughter`,
3 `doctor_and_family`, 4 `poor_prognosis`, 5 `aggressive_patient`); `GET /scenarios` lists them. Everything
about a scenario is in that file, no code change is needed for a new one:

- `persona`, `student_role`, `opening` (`tension`, `trust`, emotion, animation, `rest_animation` such as
  `Laying` or `Sitting`), `facts` and `fact_tiers`, `facts_instruction`, `fallback_lines`;
- `tension_name` (anger, hopelessness, impatience, distress...) and `tension_name_nl`: the general state of the
  character. Towards Unity the field stays `anger`;
- `bands` (`min_tension`, acting `instruction`, `allowed_emotions`, `animations`): the code corrects the actor's
  emotion and animation to the band (a patient in bed only has `Laying`);
- `voice`: Piper voice and speaking speed per band (downloaded at server start);
- `phases` with conditions (`tension_lt`, `trust_gte`, `labels_ever_any/all`, `labels_this_turn_any`); a
  phase with `terminal: true` ends the conversation as `resolved`;
- `labels`: common labels from `labels_common.json` (only name and effect `tension`/`trust` needed; description,
  example or `moment_nl` may be overridden) and the scenario's own labels (with description);
- `critical: true` on a label: the scenario ends at once (`critical_error`), the character says
  `critical_reaction.reply`, and the debrief shows `critical_title` and `critical_feedback` on top. The label
  only counts if one of `critical_requires_any` is really in the sentence (whole words);
- `events`: non-verbal actions for `POST /session/{id}/event` (effect and optional label);
- `rubric` (`scoring`, Dutch `feedback` per score, English `better_phrase`) and `debrief` settings.

Checks on every actor reply (in Python, one retry): the character gets its last 3 lines with the rule not to repeat
them or the nurse's question; a reply (or one of its sentences) that is more than 80 % similar to an earlier line
of the character or to the nurse's last sentence is refused. Times, numbers and day names (`two`, `4`, `tomorrow`,
`o'clock`...) may only appear if they are in the unlocked facts or the nurse said them; if the retry still invents
one, a fallback line is used. Names: every name in a reply must be in the scenario's `known_names` or said by the
nurse; the nurse's own name (from "I'm Tom") may only be used to address the nurse ("Thank you, Tom"), never for
someone else. Otherwise one retry, then a fallback line. Sentences are not split after Mr., Mrs., Ms. or Dr.

The scenarios 1, 3, 4 and 5 are drafts based on Jalil's scenario texts; a nursing teacher should review them.
`chat.py <scenario_id> --debrief` plays a scenario in the terminal and prints the debrief.

## Other model providers

All model calls go through `LlmClient.chat_json(system, messages, schema, role) -> dict`
in `llm_client.py`. To use another provider, write a class with the same method and pass it to
`Engine` in `server.py`.
