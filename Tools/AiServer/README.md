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
| `ollama_url` | `http://localhost:11434` | Ollama address |
| `model_classifier`, `model_actor`, `model_judge` | `qwen3.5:4b` | model per role |
| `temperature_classifier` / `_actor` / `_judge` | 0 / 0.7 / 0 | temperature per role |
| `keep_alive` | `30m` | how long Ollama keeps the model in memory |
| `request_timeout_seconds` | 120 | timeout per model call |
| `port` | 8765 | server port (listens on 127.0.0.1 only) |
| `stt_model` | `small.en` | faster-whisper model |
| `stt_try_gpu` | true | try CUDA first; falls back to CPU int8 when CUDA/cuDNN is missing |
| `stt_cpu_threads`, `stt_beam_size` | 8, 5 | CPU speech recognition settings |
| `stt_min_words` | 2 | shorter transcripts return `{"retry": true}` |
| `tts_voice` | `en_US-amy-medium` | Piper voice (same voice as Sarah's recorded lines in `Stemmen/`), downloaded when missing |
| `tts_length_scale_by_band` | 0.85 / 0.9 / 1.0 / 1.1 | speaking speed per band (lower = faster) |
| `models_dir` | `models` | where Whisper models and Piper voices are stored |

### Speech recognition on the GPU

On Windows, faster-whisper needs the CUDA 12 cuBLAS and cuDNN 9 DLLs for the GPU. If they are
missing, the server logs which ones and uses the CPU (int8). `/health` shows the device in use.

## API

| method | path | body | returns |
| --- | --- | --- | --- |
| GET | `/health` | | `{ok, models, sessions}` |
| POST | `/session/start` | `{scenario_id}` | `{session_id, opening: {reply, emotion, animation, pose, anger, trust, band, phase, audio_wav_base64, tts_ms}}` |
| POST | `/turn/text` | `{session_id, text}` | one turn (see below) |
| POST | `/turn/audio` | multipart: `session_id`, `audio` (wav, 16 kHz mono) | one turn + `transcript` + `audio_wav_base64`, or `{"retry": true}` |
| POST | `/session/{id}/end` | | saves `sessions/<id>.json`, returns `{saved, end_reason, turns}` |

A turn returns: `reply, emotion, animation, pose, facts_used, labels, evidence_quote,
off_topic, unsafe, before {anger, trust, band, phase}, after {...}, anger, trust, band, phase,
ended (null | "resolved" | "time_up"), fallback_used, notes [...], timings_ms {classify_ms,
state_ms, actor_ms, total_ms}`.

For `/turn/audio`, `timings_ms` also has `stt_ms`, `tts_ms` and `request_total_ms`, and these
are saved in the session log too (the audio itself is not). The uploaded wav is only processed in
memory and never written to disk. If the transcript is empty or shorter than `stt_min_words`, no
turn happens and the answer is `{"retry": true, "transcript": ..., "timings_ms": {"stt_ms": ...}}`.

Errors: `400` the upload is not a readable wav, `404` unknown session or scenario, `409` session already ended,
`503 {"error": "llm_unavailable", "detail": ...}` when Ollama is not running or the model is missing.

## Scenarios

Everything about a scenario is in `scenarios/<id>.json`: persona, opening, facts and when they
unlock, phases and their conditions, labels with their anger/trust effect, bands with acting
instructions and allowed emotions, rules (caps, thresholds), fallback lines and the debrief rubric.
A new scenario is a new JSON file; no code change is needed.

Phase conditions (all given keys must hold): `anger_lt`, `trust_gte`, `labels_ever_all`,
`labels_ever_any`, `labels_this_turn_any`. A phase with `"terminal": true` ends the conversation.

## Other model providers

All model calls go through `LlmClient.chat_json(system, messages, schema, role) -> dict`
in `llm_client.py`. To use another provider, write a class with the same method and pass it to
`Engine` in `server.py`.
