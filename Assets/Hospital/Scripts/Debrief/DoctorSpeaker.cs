using System.Collections;
using ProCommVR.Hospital.Face;
using ProCommVR.Scenarios;
using UnityEngine;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// The head doctor speaks: a voice clip from her head (3D, but loud enough to hear across the table), her mouth follows
    /// the voice (<see cref="FaceController.TalkLevel"/>, like AiNpc does for the patient), the Animator goes to Talking
    /// and back to Idle, and the line is shown as a subtitle bubble (the existing DialogueBubble). Without a clip the
    /// subtitle stays for the reading time (text length * <see cref="_secondsPerCharacter"/>). A skips to the end of the
    /// line. Nothing is allocated per frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class DoctorSpeaker : MonoBehaviour
    {
        [SerializeField] private FaceController _face;
        [SerializeField] private HeadLookAt _look;
        [SerializeField] private Animator _animator;
        [Tooltip("Empty: a 3D AudioSource on the Head bone.")]
        [SerializeField] private AudioSource _voice;
        [Tooltip("The subtitle (an instance of the DialogueBubble prefab). Empty: no subtitle.")]
        [SerializeField] private DialogueBubble _subtitle;
        [Tooltip("Where the subtitle hangs. Empty: under her head, toward the player.")]
        [SerializeField] private Transform _subtitleAnchor;
        [SerializeField, Min(0.01f)] private float _secondsPerCharacter = 0.06f;
        [SerializeField, Min(0f)] private float _pauseAfterLine = 0.35f;
        [SerializeField] private bool _skipWithA = true;

        [Header("Mouth")]
        [SerializeField] private float _mouthGain = 9f;
        [SerializeField] private float _mouthOpenSpeed = 30f;
        [SerializeField] private float _mouthCloseSpeed = 9f;

        [Header("Animator states")]
        [SerializeField] private string _talkState = "Talking";
        [SerializeField] private string _idleState = "Idle";

        private readonly float[] _samples = new float[256];
        private float _mouth;
        private Transform _head;
        private bool _skip;

        /// <summary>A line is being said right now.</summary>
        public bool IsSpeaking { get; private set; }
        /// <summary>The text of the line being said (or the last one).</summary>
        public string CurrentText { get; private set; } = "";
        /// <summary>The key of the line being said (or the last one).</summary>
        public string CurrentKey { get; private set; } = "";
        /// <summary>How many lines were said (for tests).</summary>
        public int LinesSaid { get; private set; }
        public AudioSource Voice => _voice;
        public DialogueBubble Subtitle => _subtitle;
        public HeadLookAt Look => _look;
        public FaceController Face => _face;
        public Transform Head => _head != null ? _head : transform;

        /// <summary>Tests only: every line lasts this long (0 = the real length).</summary>
        public static float TestLineSeconds;

        public Transform SubtitleAnchor
        {
            get => _subtitleAnchor;
            set => _subtitleAnchor = value;
        }

        private void Awake()
        {
            if (_face == null) _face = GetComponentInChildren<FaceController>();
            if (_look == null) _look = GetComponentInChildren<HeadLookAt>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            _head = _animator != null && _animator.isHuman ? _animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (_voice == null)
            {
                var go = new GameObject("Doctor Voice");
                go.transform.SetParent(_head != null ? _head : transform, false);
                if (_head == null)
                    go.transform.localPosition = Vector3.up * 1.6f;
                _voice = go.AddComponent<AudioSource>();
                _voice.playOnAwake = false;
                _voice.spatialBlend = 0.85f;
                _voice.rolloffMode = AudioRolloffMode.Linear;
                _voice.minDistance = 3f;
                _voice.maxDistance = 15f;
                _voice.dopplerLevel = 0f;
            }
            if (_subtitle != null)
                _subtitle.gameObject.SetActive(false);
            PlayState(_idleState);
        }

        /// <summary>Says a line (clip and text). Yield on it in a coroutine; ends when the clip ends or A is pressed.</summary>
        public IEnumerator Say(DebriefLine line)
        {
            if (line == null)
                yield break;
            yield return Say(line.Clip, line.Text, line.Key);
        }

        /// <summary>Says a text with an optional clip (no clip: the subtitle stays for the reading time).</summary>
        public IEnumerator Say(AudioClip clip, string text, string key = "")
        {
            if (clip == null && string.IsNullOrEmpty(text))
                yield break;
            IsSpeaking = true;
            CurrentText = text ?? "";
            CurrentKey = key ?? "";
            LinesSaid++;
            _skip = false;
            ShowSubtitle(text);
            PlayState(_talkState);
            if (_look != null && _look.Mode != HeadLookAt.LookMode.LookAtPlayer)
                _look.Mode = HeadLookAt.LookMode.LookAtPlayer;
            var duration = clip != null ? clip.length : Mathf.Max(1.2f, (text ?? "").Length * _secondsPerCharacter);
            if (TestLineSeconds > 0f)
                duration = TestLineSeconds;
            if (clip != null)
            {
                _voice.Stop();
                _voice.clip = clip;
                _voice.Play();
            }
            var elapsed = 0f;
            while (elapsed < duration && !_skip)
            {
                if (_skipWithA && elapsed > 0.15f && DebriefInput.A)
                    break;
                if (clip != null && !_voice.isPlaying && elapsed > 0.2f && TestLineSeconds <= 0f)
                    break;
                elapsed += Time.deltaTime;
                yield return null;
            }
            _voice.Stop();
            PlayState(_idleState);
            IsSpeaking = false;
            var pause = 0f;
            while (pause < _pauseAfterLine && !_skip)
            {
                pause += Time.deltaTime;
                yield return null;
            }
            HideSubtitle();
        }

        /// <summary>Stops the current line at once (e.g. when the debrief is aborted).</summary>
        public void Stop()
        {
            _skip = true;
            if (_voice != null)
                _voice.Stop();
            IsSpeaking = false;
            PlayState(_idleState);
            HideSubtitle();
        }

        /// <summary>Skips to the end of the current line (as A does).</summary>
        public void Skip() => _skip = true;

        private void ShowSubtitle(string text)
        {
            if (_subtitle == null)
                return;
            _subtitle.gameObject.SetActive(true);
            _subtitle.SetClickable(false);
            _subtitle.SetText(text);
            PlaceSubtitle();
        }

        private void HideSubtitle()
        {
            if (_subtitle != null)
                _subtitle.gameObject.SetActive(false);
        }

        private void PlaceSubtitle()
        {
            var camera = Camera.main;
            var t = _subtitle.transform;
            if (_subtitleAnchor != null)
                t.position = _subtitleAnchor.position;
            else
            {
                // under her head, a little toward the player, so her body never hides it
                var toward = camera != null ? camera.transform.position - Head.position : transform.forward;
                toward.y = 0f;
                t.position = Head.position + Vector3.down * 0.42f + (toward.sqrMagnitude > 0.0001f ? toward.normalized * 0.45f : Vector3.zero);
            }
            if (camera != null)
            {
                var away = t.position - camera.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude > 0.0001f)
                    t.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            }
        }

        private void LateUpdate()
        {
            var target = 0f;
            if (_voice != null && _voice.isPlaying)
            {
                _voice.GetOutputData(_samples, 0);
                var sum = 0f;
                for (var i = 0; i < _samples.Length; i++)
                    sum += _samples[i] * _samples[i];
                target = Mathf.Clamp01(Mathf.Sqrt(sum / _samples.Length) * _mouthGain);
            }
            var speed = target > _mouth ? _mouthOpenSpeed : _mouthCloseSpeed;
            _mouth = Mathf.MoveTowards(_mouth, target, speed * Time.deltaTime);
            if (_face != null)
                _face.TalkLevel = _mouth;
            if (_subtitle != null && _subtitle.gameObject.activeSelf)
                PlaceSubtitle();
        }

        private void PlayState(string state)
        {
            if (_animator == null || string.IsNullOrEmpty(state) || _animator.runtimeAnimatorController == null || !_animator.isActiveAndEnabled)
                return;
            var hash = Animator.StringToHash(state);
            if (_animator.HasState(0, hash))
                _animator.CrossFadeInFixedTime(hash, 0.25f, 0);
        }
    }
}
