using System;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Emotions
{
    /// <summary>
    /// The emotions of one patient (data only: it does not change anything in the scene itself). Readers such as the
    /// patient-view HUD and room effects listen to <see cref="EmotionChanged"/>.
    /// <para>
    /// <see cref="Target"/> is where the emotions are going; <see cref="Current"/> follows it smoothly in Blend Seconds,
    /// so effects never snap. <see cref="EmotionChanged"/> is raised every frame <see cref="Current"/> moves.
    /// </para>
    /// <para>
    /// With a presenter assigned: <see cref="ScenarioPresenter.ScenarioStarted"/> sets the scenario's Initial Emotion
    /// at once, and <see cref="ScenarioPresenter.OptionChosen"/> adds the option's Emotion Delta to the target.
    /// In Play mode you can drag the Target sliders in the inspector to test every effect by hand.
    /// </para>
    /// Independent of the Mood system: this component never reads or changes a <see cref="Mood"/>.
    /// </summary>
    public class EmotionHolder : MonoBehaviour
    {
        [Tooltip("Optional. Follows the emotions of every scenario this presenter starts and every option chosen in it.")]
        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("Seconds Current takes to reach a new Target.")]
        [SerializeField, Min(0.01f)] private float _blendSeconds = 1.5f;

        [Tooltip("Below this value no emotion is dominant.")]
        [SerializeField, Range(0f, 1f)] private float _dominantThreshold = EmotionState.DefaultDominantThreshold;

        [Tooltip("Where the emotions are going. Drag the sliders in Play mode to test the effects by hand.")]
        [SerializeField, EmotionRange(0f, 1f)] private EmotionState _target;

        [Tooltip("What the patient feels right now (follows Target smoothly). Read only.")]
        [SerializeField, EmotionRange(0f, 1f, true)] private EmotionState _current;

        private EmotionState _blendFrom;
        private EmotionState _blendTo;
        private float _blendElapsed;

        /// <summary>
        /// Raised with the previous and the new <see cref="Current"/> whenever it changes.
        /// </summary>
        public event Action<EmotionState, EmotionState> EmotionChanged;

        public EmotionState Current => _current;
        public EmotionState Target => _target;
        public float BlendSeconds => _blendSeconds;

        /// <summary>
        /// The strongest current emotion, or null when none reaches the threshold.
        /// </summary>
        public Emotion? Dominant => _current.GetDominant(_dominantThreshold);

        public ScenarioPresenter Presenter => _presenter;

        private void Awake()
        {
            _target = _target.Clamped();
            _current = _target;
            _blendFrom = _blendTo = _target;
            _blendElapsed = _blendSeconds;
        }

        private void OnEnable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted += OnScenarioStarted;
            _presenter.OptionChosen += OnOptionChosen;
        }

        private void OnDisable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted -= OnScenarioStarted;
            _presenter.OptionChosen -= OnOptionChosen;
        }

        private void Update()
        {
            // A new target (from Apply or from the inspector sliders): blend from where we are now.
            if (_target != _blendTo)
            {
                _blendFrom = _current;
                _blendTo = _target;
                _blendElapsed = 0f;
            }
            if (_current == _blendTo)
                return;

            _blendElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(_blendElapsed / _blendSeconds);
            SetCurrent(t >= 1f ? _blendTo : EmotionState.Lerp(_blendFrom, _blendTo, Mathf.SmoothStep(0f, 1f, t)));
        }

        /// <summary>
        /// Adds a delta to the target (clamped to 0..1); <see cref="Current"/> follows smoothly.
        /// </summary>
        public void Apply(EmotionState delta)
        {
            _target = _target.Add(delta);
        }

        /// <summary>
        /// Sets a new target (clamped to 0..1); <see cref="Current"/> follows smoothly. For conversations that give a
        /// whole emotional state per turn instead of a delta (e.g. the AI conversation).
        /// </summary>
        public void SetTarget(EmotionState state)
        {
            _target = state.Clamped();
        }

        /// <summary>
        /// Sets target and current at once, without blending (e.g. at the start of a conversation).
        /// </summary>
        public void SetImmediate(EmotionState state)
        {
            _target = state.Clamped();
            _blendFrom = _blendTo = _target;
            _blendElapsed = _blendSeconds;
            SetCurrent(_target);
        }

        private void SetCurrent(EmotionState state)
        {
            if (state == _current)
                return;
            var previous = _current;
            _current = state;
            EmotionChanged?.Invoke(previous, _current);
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            SetImmediate(scenario.InitialEmotion);
        }

        private void OnOptionChosen(Scenario scenario, DialogueOption option)
        {
            Apply(option.EmotionDelta);
        }

        private void OnValidate()
        {
            // Outside Play mode the current state simply shows the target.
            if (!Application.isPlaying)
                _current = _target;
        }
    }
}
