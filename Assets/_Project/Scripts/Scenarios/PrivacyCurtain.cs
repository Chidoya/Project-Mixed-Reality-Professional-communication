using System;
using System.Collections;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// A privacy curtain between two beds that the player can draw by choosing a place to talk (a location of the
    /// scenario, e.g. "curtain"). The curtain is stretched from its rail end at the wall (this object's pivot) along its
    /// forward: open it is bunched up at the wall, drawn it reaches its full length. While it is being drawn the
    /// presenter holds the options. It opens again when a scenario starts.
    /// </summary>
    public class PrivacyCurtain : MonoBehaviour
    {
        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("Id of the place in the scenario (Locations) that draws the curtain.")]
        [SerializeField] private string _locationId = "curtain";

        [Tooltip("Length of the bunched-up curtain, as a fraction of its full length.")]
        [SerializeField, Range(0.02f, 1f)] private float _openFraction = 0.12f;

        [Tooltip("Seconds to draw the curtain.")]
        [SerializeField, Min(0f)] private float _drawTime = 1.5f;

        [Tooltip("Optional. Plays when the curtain is drawn (the rings sliding along the rail).")]
        [SerializeField] private AudioSource _sound;

        private Coroutine _draw;

        /// <summary>
        /// True once the curtain is fully drawn.
        /// </summary>
        public bool IsDrawn { get; private set; }

        public event Action<PrivacyCurtain> Drawn;

        private void Awake()
        {
            SetFraction(_openFraction);
        }

        private void OnEnable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted += OnScenarioStarted;
            _presenter.LocationChosen += OnLocationChosen;
        }

        private void OnDisable()
        {
            if (_presenter == null)
                return;
            _presenter.ScenarioStarted -= OnScenarioStarted;
            _presenter.LocationChosen -= OnLocationChosen;
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            if (_draw != null)
                StopCoroutine(_draw);
            _draw = null;
            IsDrawn = false;
            SetFraction(_openFraction);
        }

        private void OnLocationChosen(Scenario scenario, ConversationLocation location)
        {
            if (location.Id != _locationId || IsDrawn || _draw != null)
                return;
            _draw = StartCoroutine(Draw());
        }

        private IEnumerator Draw()
        {
            _presenter.HoldChoices = true;
            if (_sound != null)
                _sound.Play();
            for (var t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(_drawTime, 0.0001f))
            {
                SetFraction(Mathf.Lerp(_openFraction, 1f, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            SetFraction(1f);
            _draw = null;
            IsDrawn = true;
            _presenter.HoldChoices = false;
            Drawn?.Invoke(this);
        }

        private void SetFraction(float fraction)
        {
            var scale = transform.localScale;
            scale.z = fraction;
            transform.localScale = scale;
        }
    }
}
