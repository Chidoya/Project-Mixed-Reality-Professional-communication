using System;
using System.Text;
using ProCommVR.Hospital.AiTalk;
using UnityEngine;

namespace ProCommVR.AiTalk
{
    /// <summary>
    /// The briefing of the AI scenario for the nurse: the "case" (who the patient is, the task, the goal list) and the
    /// "guide" (hints per patient state) from the scenario file (Tools/AiServer/scenarios/&lt;id&gt;.json), so the text
    /// is data that other scenarios can fill in, not code. Loaded from the AI server at the start
    /// (GET /scenarios/{id}/meta), so the clipboard shows it in the lobby before any conversation; a newer meta from
    /// the opening of a conversation replaces it.
    /// </summary>
    public class ScenarioBriefing : MonoBehaviour
    {
        [SerializeField] private AiNpc _npc;
        [Tooltip("Seconds to keep trying while the AI server starts (the first start takes a while).")]
        [SerializeField, Min(5f)] private float _maxWaitSeconds = 300f;
        [TextArea, SerializeField] private string _loadingText = "Loading the case... (the AI server is starting)";
        [TextArea, SerializeField] private string _guideIntro =
            "<b>Look at him through his eyes (Y). What do you see?</b>";

        public AiScenarioMeta Meta { get; private set; }
        public bool IsLoaded => Meta != null;
        public event Action<ScenarioBriefing> Loaded;

        public string CaseTitle => Meta?.@case != null && !string.IsNullOrEmpty(Meta.@case.title) ? Meta.@case.title : "CASE";

        public string CaseText => Meta?.@case != null && !string.IsNullOrEmpty(Meta.@case.text) ? Meta.@case.text : _loadingText;

        public int GuideCount => Meta?.guide?.Length ?? 0;

        /// <summary>All hints, each with the sign to look for in the patient view (the nurse decides which one applies).</summary>
        public string GuideText
        {
            get
            {
                if (GuideCount == 0)
                    return IsLoaded ? "" : _loadingText;
                var text = new StringBuilder(_guideIntro).Append("\n\n");
                foreach (var hint in Meta.guide)
                    text.Append($"<b>{hint.title}</b>\n{hint.text}\n\n");
                return text.ToString().TrimEnd();
            }
        }

        private void Awake()
        {
            if (_npc == null)
                _npc = FindAnyObjectByType<AiNpc>();
        }

        private void OnEnable()
        {
            if (_npc != null)
                _npc.TurnApplied += OnTurn;
        }

        private void OnDisable()
        {
            if (_npc != null)
                _npc.TurnApplied -= OnTurn;
        }

        private async void Start()
        {
            if (_npc == null || _npc.Settings == null)
            {
                Debug.LogWarning("[ProCommVR] Scenario briefing: no AI patient or settings, no case.", this);
                return;
            }
            var client = new AiTalkClient(_npc.Settings, _npc.ScenarioId);
            var until = Time.realtimeSinceStartup + _maxWaitSeconds;
            while (!IsLoaded && Time.realtimeSinceStartup < until)
            {
                var meta = await client.GetScenarioMeta();
                if (this == null)
                    return;
                if (meta != null && !IsLoaded)
                {
                    Set(meta);
                    return;
                }
                await Awaitable.WaitForSecondsAsync(2f);
                if (this == null)
                    return;
            }
        }

        private void OnTurn(AiTurn turn)
        {
            if (_npc.Meta != null && _npc.Meta != Meta && _npc.Meta.progress != null && _npc.Meta.progress.Present)
                Set(_npc.Meta);
        }

        private void Set(AiScenarioMeta meta)
        {
            Debug.Log($"[ProCommVR] Scenario briefing loaded: case '{meta.@case?.title}', {meta.guide?.Length ?? 0} guide hints.", this);
            Meta = meta;
            Loaded?.Invoke(this);
        }
    }
}
