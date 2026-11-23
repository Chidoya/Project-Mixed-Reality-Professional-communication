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

        [Tooltip("Timeout per serveroproep. Een beurt duurt normaal 2-5 s, maar met Unity Play mode / Quest Link " +
                 "op dezelfde GPU soms 20 s of meer.")]
        [Range(5f, 120f)] public float requestTimeoutSeconds = 60f;

        [Tooltip("Timeout voor de nabespreking (de beoordelaar draait 2 keer, 20 tot 40 s).")]
        [Range(30f, 600f)] public float debriefTimeoutSeconds = 180f;

        [Tooltip("v2: de naam van de student (bv. Jalil). De patiënt spreekt de student enkel zo aan en de spraakherkenning " +
                 "verwacht deze naam. Leeg: de naam uit 'I'm ...' in het gesprek.")]
        public string nurseName = "";

        [Tooltip("Lokaal antwoord bij een fout of als de server de opname niet verstond.")]
        public string fallbackReply = "Sorry, what did you say?";

        [Tooltip("Klein paneel naast het personage met transcriptie, labels en wachttijden (alleen voor testen).")]
        public bool showDebugPanel = true;

        [Tooltip("Alleen in de Editor: maximum fps in Play mode (0 = geen limiet). Zonder limiet neemt de Editor de GPU in " +
                 "en wordt de AI-server (Ollama, Whisper) op dezelfde laptop tot 2x trager.")]
        public int editorTargetFrameRate = 72;

        [Tooltip("Alleen in de Editor: start Ollama en Tools/AiServer/server.py automatisch bij Play als ze nog niet draaien. " +
                 "De server blijft draaien tussen Play-sessies en stopt als Unity sluit.")]
        public bool autoStartServerInEditor = true;

        [Tooltip("Alleen in de Editor: hoe lang de eerste sessie wacht tot de server klaar is met opwarmen.")]
        [Range(10f, 300f)] public float serverStartWaitSeconds = 120f;
    }
}
