using UnityEngine;

namespace ProCommVR.Hospital.AiTalk
{
    /// <summary>
    /// Instellingen voor het vrije gesprek met de lokale AI-server (Tools/AiServer).
    /// Asset: Assets/Hospital/Data/AiTalkSettings.asset.
    /// </summary>
    [CreateAssetMenu(menuName = "ProCommVR/AI Talk Settings", fileName = "AiTalkSettings")]
    public class AiTalkSettings : ScriptableObject
    {
        [Tooltip("Adres van Tools/AiServer. Gebruik 127.0.0.1, niet localhost: op Windows probeert localhost eerst IPv6 en dat kost 2 s per verbinding.")]
        public string serverUrl = "http://127.0.0.1:8765";

        [Tooltip("Bestandsnaam (zonder .json) in Tools/AiServer/scenarios.")]
        public string scenarioId = "angry_daughter";

        [Tooltip("Langste opname per beurt.")]
        [Range(2f, 60f)] public float maxRecordSeconds = 15f;

        [Tooltip("Kortere opnames worden genegeerd (per ongeluk op de knop gedrukt).")]
        [Range(0.1f, 2f)] public float minRecordSeconds = 0.4f;

        [Tooltip("Timeout per serveroproep.")]
        [Range(5f, 60f)] public float requestTimeoutSeconds = 20f;

        [Tooltip("Lokaal antwoord bij een fout of als de server de opname niet verstond.")]
        public string fallbackReply = "Sorry, what did you say?";

        [Tooltip("Klein paneel naast het personage met transcriptie, labels en wachttijden (alleen voor testen).")]
        public bool showDebugPanel = true;

        [Tooltip("Alleen in de Editor: maximum fps in Play mode (0 = geen limiet). Zonder limiet neemt de Editor de GPU in " +
                 "en wordt de AI-server (Ollama, Whisper) op dezelfde laptop tot 2x trager.")]
        public int editorTargetFrameRate = 72;
    }
}
