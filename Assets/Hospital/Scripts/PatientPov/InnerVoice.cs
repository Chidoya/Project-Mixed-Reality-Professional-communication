using System.Collections;
using ProCommVR.Emotions;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using TMPro;
using UnityEngine;

namespace ProCommVR.Hospital.PatientPov
{
    /// <summary>
    /// When the player steps into the patient's head (PovChanged(Patient)), the patient whispers one thought for his
    /// dominant emotion, after the fade: 2D, a little quieter than the dialogue, with an italic subtitle under the emotion
    /// HUD that fades after the clip. Each thought plays at most once per conversation (a new conversation resets it).
    /// </summary>
    public class InnerVoice : MonoBehaviour
    {
        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PovController _pov;
        [SerializeField] private InnerVoiceLines _lines;
        [SerializeField] private AudioSource _source;

        [Header("Subtitle")]
        [SerializeField] private CanvasGroup _subtitle;
        [SerializeField] private TMP_Text _subtitleText;
        [Tooltip("The emotion HUD the subtitle hangs under (it follows it). Empty: in front of the camera.")]
        [SerializeField] private Transform _hud;
        [SerializeField] private Vector3 _offsetFromHud = new Vector3(0f, -0.24f, 0f);
        [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.4f;

        private readonly bool[] _played = new bool[4];
        private ScenarioPresenter _presenter;
        private Coroutine _routine;

        public int PlayedCount { get; private set; }
        public InnerVoiceLines.Line LastLine { get; private set; }
        public bool IsSpeaking => _source != null && _source.isPlaying;
        public bool SubtitleShown => _subtitle != null && _subtitle.alpha > 0.01f;

        private void Awake()
        {
            if (_subtitle != null)
            {
                _subtitle.alpha = 0f;
                _subtitle.gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (_pov != null)
                _pov.PovChanged += OnPovChanged;
            _presenter = _holder != null ? _holder.Presenter : null;
            if (_presenter != null)
                _presenter.ScenarioStarted += OnScenarioStarted;
        }

        private void OnDisable()
        {
            if (_pov != null)
                _pov.PovChanged -= OnPovChanged;
            if (_presenter != null)
                _presenter.ScenarioStarted -= OnScenarioStarted;
            StopThought();
        }

        private void OnScenarioStarted(Scenario scenario)
        {
            for (var i = 0; i < _played.Length; i++)
                _played[i] = false;
        }

        private void OnPovChanged(PovMode mode)
        {
            StopThought();
            if (mode == PovMode.Patient)
                _routine = StartCoroutine(Think());
        }

        private IEnumerator Think()
        {
            while (_pov.IsSwitching)
                yield return null;
            var delay = _lines != null ? _lines.Delay : 0.4f;
            for (var t = 0f; t < delay; t += Time.deltaTime)
                yield return null;
            if (_pov.Mode != PovMode.Patient || _holder == null || _lines == null)
                yield break;
            var dominant = _holder.Dominant;
            if (!dominant.HasValue || _played[(int)dominant.Value])
                yield break;
            var line = _lines.Get(dominant.Value);
            if (line == null)
                yield break;
            _played[(int)dominant.Value] = true;
            PlayedCount++;
            LastLine = line;

            var length = 2.5f;
            if (_source != null && line.Clip != null)
            {
                _source.clip = line.Clip;
                _source.volume = _lines.Volume;
                _source.Play();
                length = line.Clip.length;
            }
            if (_subtitle != null)
            {
                _subtitleText.SetText(line.Text);
                _subtitle.gameObject.SetActive(true);
                for (var t = 0f; t < _fadeSeconds; t += Time.deltaTime)
                {
                    _subtitle.alpha = t / _fadeSeconds;
                    yield return null;
                }
                _subtitle.alpha = 1f;
            }
            for (var t = 0f; t < length + _lines.SubtitleHold; t += Time.deltaTime)
                yield return null;
            if (_subtitle != null)
            {
                for (var t = 0f; t < _fadeSeconds; t += Time.deltaTime)
                {
                    _subtitle.alpha = 1f - t / _fadeSeconds;
                    yield return null;
                }
                _subtitle.alpha = 0f;
                _subtitle.gameObject.SetActive(false);
            }
            _routine = null;
        }

        private void LateUpdate()
        {
            if (_subtitle == null || !_subtitle.gameObject.activeSelf)
                return;
            // Follow the HUD (it eases after the head), just under it.
            var t = _subtitle.transform;
            if (_hud != null)
                t.SetPositionAndRotation(_hud.position + _hud.rotation * _offsetFromHud, _hud.rotation);
            else if (Camera.main != null)
            {
                var cam = Camera.main.transform;
                var forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                t.SetPositionAndRotation(cam.position + forward * 1.6f - Vector3.up * 0.35f, Quaternion.LookRotation(forward));
            }
        }

        private void StopThought()
        {
            if (_routine != null)
                StopCoroutine(_routine);
            _routine = null;
            if (_source != null && _source.isPlaying)
                _source.Stop();
            if (_subtitle != null)
            {
                _subtitle.alpha = 0f;
                _subtitle.gameObject.SetActive(false);
            }
        }
    }
}
