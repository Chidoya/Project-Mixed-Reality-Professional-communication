using UnityEngine;

namespace ProCommVR.Hospital
{
    public enum Difficulty
    {
        Easy,
        Medium,
        Hard,
    }

    public enum StartPose
    {
        Laying,
        Sitting,
    }

    /// <summary>Eén antwoord dat de speler kan kiezen, met de score en de feedback die daarna verschijnt.</summary>
    [System.Serializable]
    public class DialogueOption
    {
        [SerializeField] private string _title;
        [SerializeField, TextArea(2, 6)] private string _text;
        [SerializeField] private int _score;
        [SerializeField, TextArea(2, 6)] private string _feedback;

        /// <summary>Naam van de aanpak uit het scenariodocument, bv. "Professional Empathy". Niet zichtbaar tijdens het gesprek.</summary>
        public string Title => _title;
        public string Text => _text;
        public int Score => _score;
        public string Feedback => _feedback;
    }

    /// <summary>
    /// Eén patiënt achter één deur: wat op de patiëntkaart staat, welk personage in welke pose ligt of zit,
    /// wat de patiënt zegt en de 4 antwoordopties. Een nieuw scenario toevoegen = een nieuw asset maken
    /// (Create > ProCommVR > Patient Case) en de doorLabel invullen; geen code nodig.
    /// </summary>
    [CreateAssetMenu(menuName = "ProCommVR/Patient Case", fileName = "Case_")]
    public class PatientCase : ScriptableObject
    {
        public const int OptionCount = 4;
        public const int ScoreBestPractice = 3;
        public const int ScoreSubOptimal = 1;
        public const int ScorePoor = 0;
        public const int ScoreCriticalFailure = -2;

        [Header("Deur en patiëntkaart")]
        [SerializeField] private Difficulty _difficulty;
        [Tooltip("Deur waar deze case achter zit, bv. 1A (vloer 1, eerste deur).")]
        [SerializeField] private string _doorLabel;
        [SerializeField] private string _patientName;
        [SerializeField] private int _age;
        [SerializeField] private string _room;
        [Tooltip("Twee zinnen: wat er aan de hand is.")]
        [SerializeField, TextArea(2, 4)] private string _situation;
        [Tooltip("Eén zin: waar moet de student op letten.")]
        [SerializeField, TextArea(1, 3)] private string _watchOut;

        [Header("Patiënt in de kamer")]
        [SerializeField] private GameObject _patientPrefab;
        [SerializeField] private StartPose _startPose;

        [Header("Gesprek")]
        [SerializeField, TextArea(2, 5)] private string _npcLine;
        [SerializeField] private DialogueOption[] _options = new DialogueOption[OptionCount];

        public Difficulty Difficulty => _difficulty;
        public string DoorLabel => _doorLabel;
        public string PatientName => _patientName;
        public int Age => _age;
        public string Room => _room;
        public string Situation => _situation;
        public string WatchOut => _watchOut;
        public GameObject PatientPrefab => _patientPrefab;
        public StartPose StartPose => _startPose;
        public string NpcLine => _npcLine;
        public DialogueOption[] Options => _options;

        private void OnValidate()
        {
            if (_options == null || _options.Length != OptionCount)
                System.Array.Resize(ref _options, OptionCount);
        }
    }
}
