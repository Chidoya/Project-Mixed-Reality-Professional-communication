"""Three spoken student sentences for the live debrief smoke test (RunDebriefLive): Piper voice ryan, 16 kHz mono WAV in
Tools/AiServer/tests/audio/debrief_<n>.wav. Run from Tools/AiServer: .venv/Scripts/python.exe tools/gen_test_sentences.py"""
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
from gen_doctor_lines import load_voice, speak, trim, write  # noqa: E402

SENTENCES = [
    "Hello, I'm your nurse today. I can see you're really sad about missing the party.",
    "What is on your mind right now?",
    "You have to stay because the infection in your leg still needs the antibiotics.",
]

if __name__ == "__main__":
    voice = load_voice("en_US-ryan-high")
    out = Path(__file__).resolve().parents[1] / "tests" / "audio"
    for i, text in enumerate(SENTENCES, 1):
        audio, rate = speak(voice, text, 1.0)
        write(out / f"debrief_{i}.wav", trim(audio, rate, 0.3), rate)
    print(len(SENTENCES), "sentences in", out)
