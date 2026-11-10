"""Small local AI server for the VR conversation training.

Start:  .venv\\Scripts\\python server.py      (see README.md)
"""
from __future__ import annotations

import base64
import json
import logging
import time
from contextlib import asynccontextmanager
from pathlib import Path
from typing import Optional

import uvicorn
from fastapi import FastAPI, File, Form, HTTPException, Request, UploadFile
from fastapi.responses import JSONResponse
from pydantic import BaseModel

from engine import Engine, Session, SessionEndedError
from debrief import run_debrief
from llm_client import LlmClient, LlmOutputError, LlmUnavailableError
from speech import SpeechToText, TextToSpeech, timed

HERE = Path(__file__).resolve().parent
CONFIG = json.loads((HERE / "config.json").read_text(encoding="utf-8"))
SESSIONS_DIR = HERE / "sessions"
MODELS_DIR = HERE / CONFIG.get("models_dir", "models")
MAX_AUDIO_BYTES = 10 * 1024 * 1024  # about 5 minutes of 16 kHz mono 16-bit

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
logging.getLogger("httpx").setLevel(logging.WARNING)
log = logging.getLogger("aiserver")

llm = LlmClient(CONFIG)
engine = Engine(llm, HERE / "scenarios")
sessions: dict[str, Session] = {}
stt: Optional[SpeechToText] = None
tts: Optional[TextToSpeech] = None


@asynccontextmanager
async def lifespan(app: FastAPI):
    global stt, tts
    stt = SpeechToText(CONFIG, MODELS_DIR)  # loaded once
    tts = TextToSpeech(CONFIG, MODELS_DIR)
    _, ms = timed(tts.synthesize, "Hello.")
    log.info("warm-up text to speech done in %d ms", ms)
    try:
        for line in engine.warm_up():
            log.info(line)
    except LlmUnavailableError as exc:
        log.error("WARM-UP FAILED: %s", exc)
        log.error("The server keeps running, but every turn will fail until Ollama is available.")
    yield


app = FastAPI(title="ProCommVR AI server", lifespan=lifespan)


@app.exception_handler(LlmUnavailableError)
async def llm_unavailable(_: Request, exc: LlmUnavailableError):
    log.error("%s", exc)
    return JSONResponse(status_code=503, content={"error": "llm_unavailable", "detail": str(exc)})


class StartRequest(BaseModel):
    scenario_id: str


class TurnRequest(BaseModel):
    session_id: str
    text: str


def get_session(session_id: str) -> Session:
    session = sessions.get(session_id)
    if session is None:
        raise HTTPException(404, f"unknown session {session_id}")
    return session


def speak(text: str, band: str) -> tuple[str, int]:
    wav, ms = timed(tts.synthesize, text, band)
    return base64.b64encode(wav).decode("ascii"), ms


@app.get("/health")
def health():
    return {
        "ok": True,
        "models": llm.models,
        "stt": {"model": stt.model_name, "device": stt.device, "compute_type": stt.compute_type,
                "fallback_reason": stt.fallback_reason},
        "tts": {"voice": tts.voice_name},
        "sessions": len(sessions),
    }


@app.post("/session/start")
def session_start(req: StartRequest):
    try:
        session, opening = engine.start_session(req.scenario_id)
    except FileNotFoundError:
        raise HTTPException(404, f"unknown scenario {req.scenario_id}")
    opening["audio_wav_base64"], opening["tts_ms"] = speak(opening["reply"], opening["band"])
    opening["time_limit_seconds"] = session.scenario.data.get("time_limit_seconds", 0)
    opening["time_up_line"] = session.scenario.data.get("time_up_line", "")
    sessions[session.id] = session
    log.info("session %s started (%s)", session.id, req.scenario_id)
    return {"session_id": session.id, "opening": opening}


@app.post("/turn/text")
def turn_text(req: TurnRequest):
    session = get_session(req.session_id)
    with session.lock:
        try:
            result = engine.run_turn(session, req.text)
        except SessionEndedError as exc:
            raise HTTPException(409, str(exc))
    result["input"] = "text"
    return result


@app.post("/turn/audio")
def turn_audio(session_id: str = Form(...), audio: UploadFile = File(...)):
    """wav -> text -> the same turn as /turn/text -> reply as wav.

    The uploaded wav is only kept in memory and dropped at the end of this request.
    """
    t_start = time.perf_counter()
    session = get_session(session_id)
    wav_bytes = audio.file.read(MAX_AUDIO_BYTES + 1)
    if len(wav_bytes) > MAX_AUDIO_BYTES:
        raise HTTPException(413, "audio too long")
    try:
        transcript, stt_ms = timed(stt.transcribe, wav_bytes)
    except Exception as exc:  # noqa: BLE001 - bad or non-wav upload
        raise HTTPException(400, f"could not read the wav: {exc}")
    finally:
        del wav_bytes  # never stored
        audio.file.close()

    if len(transcript.split()) < CONFIG.get("stt_min_words", 2):
        log.info("audio turn %s: transcript too short (%r), asking for retry", session_id, transcript)
        return {"retry": True, "transcript": transcript, "timings_ms": {"stt_ms": stt_ms}}

    with session.lock:
        try:
            result = engine.run_turn(session, transcript)
        except SessionEndedError as exc:
            raise HTTPException(409, str(exc))
        result["input"] = "audio"
        result["transcript"] = transcript
        audio_b64, tts_ms = speak(result["reply"], result["band"])
        # result is the same dict that is stored in the session log, so the log gets these too
        timings = result["timings_ms"]
        timings["stt_ms"] = stt_ms
        timings["tts_ms"] = tts_ms
        timings["request_total_ms"] = int((time.perf_counter() - t_start) * 1000)
    log.info("audio turn %d %s: stt %d ms, turn %d ms, tts %d ms, total %d ms", result["turn"],
             session_id, stt_ms, timings["total_ms"], tts_ms, timings["request_total_ms"])
    response = dict(result)  # audio only in the response, not in the session log
    response["audio_wav_base64"] = audio_b64
    return response


class EndRequest(BaseModel):
    reason: Optional[str] = None  # e.g. "time_up" when the client's timer ran out


def save_session(session: Session) -> Path:
    SESSIONS_DIR.mkdir(exist_ok=True)
    path = SESSIONS_DIR / f"{session.id}.json"
    path.write_text(json.dumps(engine.transcript(session), indent=2, ensure_ascii=False), encoding="utf-8")
    return path


@app.post("/session/{session_id}/end")
def session_end(session_id: str, req: Optional[EndRequest] = None):
    """Ends and saves the session. It stays in memory, so /debrief still works afterwards."""
    session = get_session(session_id)
    with session.lock:
        if session.ended is None:
            session.ended = (req.reason if req and req.reason else None) or "stopped"
        path = save_session(session)
    log.info("session %s ended (%s), saved to %s", session_id, session.ended, path)
    return {"saved": str(path), "end_reason": session.ended, "turns": len(session.turns)}


@app.post("/session/{session_id}/debrief")
def session_debrief(session_id: str, force: bool = False):
    """Scores the conversation with the rubric (judge model, judge_runs times). Ends the session if needed."""
    session = get_session(session_id)
    with session.lock:
        if session.debrief is not None and not force:
            return session.debrief
        if session.ended is None:
            session.ended = "stopped"
        try:
            result = run_debrief(llm, session, CONFIG.get("judge_runs", 2))
        except LlmOutputError as exc:
            raise HTTPException(502, f"debrief failed: {exc}")
        session.debrief = result
        path = save_session(session)
    log.info("debrief %s: %d/%d in %d ms (runs %s), saved to %s", session_id, result["total_score"],
             result["max_score"], result["timings_ms"]["total_ms"], result["timings_ms"]["runs_ms"], path)
    return result


if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=CONFIG["port"])
