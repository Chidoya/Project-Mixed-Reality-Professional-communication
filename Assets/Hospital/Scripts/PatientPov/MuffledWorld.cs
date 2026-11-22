using System;
using System.Collections.Generic;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// In the patient view, with Sadness dominant, the world (corridor, mood sound, the nurse's voice lines) sounds dull,
    /// as if under water: a low-pass filter on the scene's audio sources, cutoff from 22000 Hz down to 900 Hz with the
    /// strength. Not on the patient's own sounds (his thoughts, the heartbeat) or the room's emotion sound. Back in the
    /// nurse view the filters are switched off again (exactly as before: a disabled filter does nothing).
    /// The project has no AudioMixer, so this works per source.
    /// </summary>
    public class MuffledWorld : MonoBehaviour
    {
        private const float OpenCutoff = 22000f;

        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PovController _pov;
        [SerializeField] private PatientWorldController _world;
        [Tooltip("Sources that keep sounding clear (the patient's thoughts, the heartbeat, the clock).")]
        [SerializeField] private AudioSource[] _exclude = Array.Empty<AudioSource>();
        [SerializeField] private float _muffledCutoff = 900f;
        [Tooltip("Only sources within this distance of the patient (or 2D sources). 0: all.")]
        [SerializeField, Min(0f)] private float _radius = 12f;
        [SerializeField] private Transform _patient;

        private readonly List<AudioLowPassFilter> _filters = new List<AudioLowPassFilter>();
        private float _fullStrengthAt = PatientPovMath.DefaultFullStrengthAt;
        private Emotion? _shown;
        private float _weight;

        public bool IsMuffled { get; private set; }
        public float Cutoff { get; private set; } = OpenCutoff;
        public int FilterCount => _filters.Count;

        private void Start()
        {
            _fullStrengthAt = PatientPovMath.ReadFullStrengthAt(_world);
            var worldSource = _world != null ? _world.GetComponent<AudioSource>() : null;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source == worldSource || Array.IndexOf(_exclude, source) >= 0)
                    continue;
                if (_radius > 0f && _patient != null && source.spatialBlend > 0f &&
                    Vector3.Distance(source.transform.position, _patient.position) > _radius)
                    continue;
                if (source.GetComponent<AudioLowPassFilter>() != null)
                    continue; // someone else's filter: leave it alone
                var filter = source.gameObject.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = OpenCutoff;
                filter.enabled = false;
                _filters.Add(filter);
            }
        }

        private void OnEnable()
        {
            if (_pov != null)
                _pov.PovChanged += OnPovChanged;
        }

        private void OnDisable()
        {
            if (_pov != null)
                _pov.PovChanged -= OnPovChanged;
            Restore();
        }

        private void OnPovChanged(PovMode mode)
        {
            if (mode == PovMode.Nurse)
                Restore();
        }

        private void Update()
        {
            if (_pov == null || _pov.Mode != PovMode.Patient)
                return;
            var strength = PatientPovMath.DominantStrength(_holder, _fullStrengthAt, out var dominant);
            var wanted = dominant == Emotion.Sadness ? (Emotion?)Emotion.Sadness : null;
            PatientPovMath.StepExclusive(ref _shown, ref _weight, wanted, wanted.HasValue ? strength : 0f, Time.deltaTime / 2f);
            var w = _shown == Emotion.Sadness ? Mathf.SmoothStep(0f, 1f, _weight) : 0f;
            if (w <= 0f)
            {
                if (IsMuffled)
                    Restore();
                return;
            }
            // Perceived pitch is logarithmic: blend the cutoff on a log scale.
            Cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(OpenCutoff), Mathf.Log(_muffledCutoff), w));
            for (var i = 0; i < _filters.Count; i++)
            {
                var filter = _filters[i];
                if (filter == null)
                    continue;
                filter.cutoffFrequency = Cutoff;
                filter.enabled = true;
            }
            IsMuffled = true;
        }

        private void Restore()
        {
            for (var i = 0; i < _filters.Count; i++)
            {
                var filter = _filters[i];
                if (filter == null)
                    continue;
                filter.cutoffFrequency = OpenCutoff;
                filter.enabled = false;
            }
            Cutoff = OpenCutoff;
            IsMuffled = false;
            _shown = null;
            _weight = 0f;
        }
    }
}
