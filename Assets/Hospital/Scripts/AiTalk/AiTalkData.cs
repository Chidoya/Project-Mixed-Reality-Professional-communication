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
        public int classify_calls; // aantal AI-oproepen (2 = eerste antwoord afgekeurd en opnieuw gevraagd)
        public int actor_calls;
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
        /// <summary>De kritieke fout die het gesprek in deze beurt beëindigde (anders leeg).</summary>
        public AiCritical critical;
        public AiStateSnapshot before;
        public AiStateSnapshot after;
        public AiTimings timings_ms;
        public int tts_ms;
        public string audio_wav_base64;
        // Alleen in de opening (van /session/start)
        public float time_limit_seconds;
        public string time_up_line;
        public string rest_animation;
        public string tension_name;
        public string character_name;
        /// <summary>Optioneel (alleen in de opening): case, guide en de grenzen van het gesprek uit het scenario.</summary>
        public AiScenarioMeta meta;
        /// <summary>Nummer van de beurt (1, 2, ...); 0 in de opening.</summary>
        public int turn;

        // Alleen in Unity, niet van de server
        [NonSerialized] public bool localFallback;
        [NonSerialized] public string error;
        [NonSerialized] public int roundTripMs;

        public bool HasEnded => !string.IsNullOrEmpty(ended);
    }

    /// <summary>Grenzen van het gesprek uit het scenario (meta.progress): fasen, einde en escalatie.</summary>
    [Serializable]
    public class AiProgressMeta
    {
        public string[] phases;
        public float resolve_trust;
        public float resolve_tension;
        public float escalate_tension;
        public float escalate_trust;
        public int min_turns_total;

        public bool Present => phases != null && phases.Length > 0;
    }

    /// <summary>De briefing van het scenario (meta.case), voor het klembord.</summary>
    [Serializable]
    public class AiCaseMeta
    {
        public string title;
        public string text;
    }

    /// <summary>Een tip voor de verpleegkundige bij een toestand van de patiënt (meta.guide).</summary>
    [Serializable]
    public class AiGuideHint
    {
        public string when;
        public string title;
        public string text;
    }

    /// <summary>Wat Unity over het scenario mag tonen naast het gesprek zelf (meta in de opening).</summary>
    [Serializable]
    public class AiScenarioMeta
    {
        public AiProgressMeta progress;
        public AiCaseMeta @case;
        public AiGuideHint[] guide;
    }

    [Serializable]
    public class AiStartResponse
    {
        public string session_id;
        public AiTurn opening;
    }

    // --- Nabespreking (/session/{id}/debrief) ------------------------------------------------

    [Serializable]
    public class AiDebriefItem
    {
        public int id;
        public string kind;
        public string criterion;
        public string criterion_nl;
        public int score;
        public int[] scores;
        public bool uncertain;
        public string evidence_quote;
        public string feedback;
        public string better_phrase;
    }

    [Serializable]
    public class AiKeyMoment
    {
        public int turn_index;
        public string what_happened;
        public string effect;
        public int anger_after;
    }

    [Serializable]
    public class AiDebriefTimings
    {
        public int total_ms;
        public int[] runs_ms;
    }

    /// <summary>De kritieke fout die het scenario beëindigde (leeg als er geen was).</summary>
    [Serializable]
    public class AiCritical
    {
        public string label;
        public int turn;
        public string quote;
        public string title;
        public string feedback;

        public bool Present => !string.IsNullOrEmpty(label);
    }

    [Serializable]
    public class AiDebrief
    {
        public string session_id;
        public string end_reason;
        public string summary;
        public AiDebriefItem[] items;
        public int total_score;
        public int max_score;
        public AiKeyMoment[] key_moments;
        public int[] anger_curve;
        public int[] trust_curve;
        public AiDebriefTimings timings_ms;
        public string tension_name_nl;
        public AiCritical critical;

        // Alleen in Unity
        [NonSerialized] public string error;
        [NonSerialized] public int roundTripMs;
        public bool Failed => !string.IsNullOrEmpty(error);
    }

    [Serializable]
    internal class AiEndRequest
    {
        public string reason;
    }

    [Serializable]
    internal class AiStartRequest
    {
        public string scenario_id;
    }
}
