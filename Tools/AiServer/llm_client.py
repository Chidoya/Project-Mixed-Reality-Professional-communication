"""All calls to the language model go through LlmClient.chat_json.

To plug in another provider later, write a class with the same chat_json method
(system, messages, schema, role) -> dict and pass it to the Engine instead.
"""
from __future__ import annotations

import json
from typing import Any

import httpx
import ollama


class LlmUnavailableError(RuntimeError):
    """The model server cannot be reached (Ollama not running, wrong URL, model missing)."""


class LlmOutputError(RuntimeError):
    """The model answered, but not with parseable JSON."""


class LlmClient:
    ROLES = ("classifier", "actor", "judge")

    def __init__(self, config: dict[str, Any]):
        self.url = config["ollama_url"]
        self.keep_alive = config.get("keep_alive", "30m")
        self.models = {role: config[f"model_{role}"] for role in self.ROLES}
        self.temperatures = {
            "classifier": config.get("temperature_classifier", 0),
            "actor": config.get("temperature_actor", 0.7),
            "judge": config.get("temperature_judge", 0),
        }
        self._client = ollama.Client(host=self.url, timeout=config.get("request_timeout_seconds", 120))

    def chat_json(
        self,
        system: str,
        messages: list[dict[str, str]],
        schema: dict[str, Any],
        role: str = "actor",
    ) -> dict:
        """Send one chat request and return the JSON object the model produced.

        `schema` is a JSON schema (from a Pydantic model); Ollama constrains the output to it.
        `role` picks model and temperature from config.json.
        """
        if role not in self.ROLES:
            raise ValueError(f"unknown role {role!r}")
        model = self.models[role]
        try:
            response = self._client.chat(
                model=model,
                messages=[{"role": "system", "content": system}, *messages],
                format=schema,
                think=False,
                keep_alive=self.keep_alive,
                options={"temperature": self.temperatures[role]},
            )
        except (httpx.ConnectError, httpx.ConnectTimeout, ConnectionError) as exc:
            raise LlmUnavailableError(
                f"Cannot reach Ollama at {self.url}. Is Ollama running? Start the Ollama app "
                f"or run 'ollama serve'. ({exc})"
            ) from exc
        except ollama.ResponseError as exc:
            if exc.status_code == 404:
                raise LlmUnavailableError(
                    f"Model '{model}' is not installed in Ollama. Run: ollama pull {model}"
                ) from exc
            raise LlmUnavailableError(f"Ollama returned an error: {exc}") from exc

        content = response.message.content or ""
        try:
            data = json.loads(content)
        except json.JSONDecodeError as exc:
            raise LlmOutputError(f"model did not return valid JSON: {content[:200]!r}") from exc
        if not isinstance(data, dict):
            raise LlmOutputError(f"model returned JSON that is not an object: {content[:200]!r}")
        return data
