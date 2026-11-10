using System;

namespace ProCommVR.Hospital.AiTalk
{
    // JSON van Tools/AiServer (zie Tools/AiServer/README.md). Veldnamen = JSON-sleutels, voor JsonUtility.
    // Ontbrekende velden blijven op hun standaardwaarde; "null" wordt een lege string.

    [Serializable]
    public class AiStateSnapshot
    {
        public int anger;
        public int trust;
        public string band;
        public string phase;
    }

    [Serializable]
    public class AiTimings
    {
        public int stt_ms;
        public int classify_ms;
        public int state_ms;
        public int actor_ms;
        public int tts_ms;
        public int total_ms;
        public int request_total_ms;
    }

    /// <summary>Eén beurt (antwoord van /turn/audio) of de openingszin (opening van /session/start).</summary>
    [Serializable]
    public class AiTurn
    {
        public bool retry;
        public string transcript;
        public string reply;
        public string emotion;
        public string animation;
        public string pose;
        public string[] labels;
        public string evidence_quote;
        public bool off_topic;
        public bool @unsafe;
        public bool fallback_used;
        public int anger;
        public int trust;
        public string band;
        public string phase;
        public string ended;
        public AiStateSnapshot before;
        public AiStateSnapshot after;
        public AiTimings timings_ms;
        public int tts_ms;
        public string audio_wav_base64;

        // Alleen in Unity, niet van de server
        [NonSerialized] public bool localFallback;
        [NonSerialized] public string error;
        [NonSerialized] public int roundTripMs;

        public bool HasEnded => !string.IsNullOrEmpty(ended);
    }

    [Serializable]
    public class AiStartResponse
    {
        public string session_id;
        public AiTurn opening;
    }

    [Serializable]
    internal class AiStartRequest
    {
        public string scenario_id;
    }
}
