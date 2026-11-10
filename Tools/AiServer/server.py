"""Small local AI server for the VR conversation training.

Start:  .venv\\Scripts\\python server.py      (see README.md)
"""
from __future__ import annotations

import json
import logging
import time
from contextlib import asynccontextmanager
from pathlib import Path

import uvicorn
from fastapi import FastAPI, HTTPException, Request
from fastapi.responses import JSONResponse
from pydantic import BaseModel

from engine import Engine, Session, SessionEndedError
from llm_client import LlmClient, LlmUnavailableError

HERE = Path(__file__).resolve().parent
CONFIG = json.loads((HERE / "config.json").read_text(encoding="utf-8"))
SESSIONS_DIR = HERE / "sessions"

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(message)s")
log = logging.getLogger("aiserver")

llm = LlmClient(CONFIG)
engine = Engine(llm, HERE / "scenarios")
sessions: dict[str, Session] = {}


def warm_up() -> None:
    """Load the model(s) into memory with a tiny request, so the first turn is fast."""
    schema = {"type": "object", "properties": {"ok": {"type": "boolean"}}, "required": ["ok"]}
    warmed: set[str] = set()
    for role in ("classifier", "actor", "judge"):
        if llm.models[role] in warmed:
            continue  # same model is already loaded
        warmed.add(llm.models[role])
        t = time.perf_counter()
        llm.chat_json("Answer with JSON.", [{"role": "user", "content": "Say ok."}], schema, role=role)
        log.info("warm-up %s (%s) done in %d ms", role, llm.models[role], (time.perf_counter() - t) * 1000)


@asynccontextmanager
async def lifespan(app: FastAPI):
    try:
        warm_up()
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


@app.get("/health")
def health():
    return {"ok": True, "models": llm.models, "sessions": len(sessions)}


@app.post("/session/start")
def session_start(req: StartRequest):
    try:
        session, opening = engine.start_session(req.scenario_id)
    except FileNotFoundError:
        raise HTTPException(404, f"unknown scenario {req.scenario_id}")
    sessions[session.id] = session
    log.info("session %s started (%s)", session.id, req.scenario_id)
    return {"session_id": session.id, "opening": opening}


@app.post("/turn/text")
def turn_text(req: TurnRequest):
    session = get_session(req.session_id)
    with session.lock:
        try:
            return engine.run_turn(session, req.text)
        except SessionEndedError as exc:
            raise HTTPException(409, str(exc))


@app.post("/session/{session_id}/end")
def session_end(session_id: str):
    session = get_session(session_id)
    with session.lock:
        if session.ended is None:
            session.ended = "stopped"
        SESSIONS_DIR.mkdir(exist_ok=True)
        path = SESSIONS_DIR / f"{session.id}.json"
        path.write_text(json.dumps(engine.transcript(session), indent=2, ensure_ascii=False),
                        encoding="utf-8")
    sessions.pop(session_id, None)
    log.info("session %s saved to %s", session_id, path)
    return {"saved": str(path), "end_reason": session.ended, "turns": len(session.turns)}


if __name__ == "__main__":
    uvicorn.run(app, host="127.0.0.1", port=CONFIG["port"])
