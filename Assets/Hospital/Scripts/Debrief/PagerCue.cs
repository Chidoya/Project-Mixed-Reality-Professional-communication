using System.Collections;
using ProCommVR.Hospital.AiTalk;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// The call to the debrief comes from the world, not from a menu: when the conversation ends, a small pager on the
    /// left hand beeps (the pager clip), vibrates (3 short pulses on the left controller) and shows "Dr. Peeters -
    /// Briefing room". All within the 2 s PrototypeFlow waits before it fades. During the fade soft footsteps play (2D).
    /// </summary>
    public class PagerCue : MonoBehaviour
    {
        [SerializeField] private AiNpc _npc;
        [Tooltip("The left controller (the pager is held there). Empty: no model, the beep is 2D.")]
        [SerializeField] private Transform _leftHand;
        [SerializeField] private AudioClip _pagerClip;
        [SerializeField] private AudioClip _footstepsClip;
        [TextArea, SerializeField] private string _text = "Dr. Peeters\nBriefing room";
        [SerializeField] private Vector3 _localPosition = new(-0.03f, 0.03f, -0.02f);
        [SerializeField] private Vector3 _localEuler = new(-50f, 0f, 0f);
        [Tooltip("Seconds after the end of the conversation the footsteps start (PrototypeFlow fades after 2 s).")]
        [SerializeField, Min(0f)] private float _footstepsDelay = 2.1f;
        [SerializeField, Range(0f, 1f)] private float _footstepsVolume = 0.45f;
        [SerializeField, Min(0.5f)] private float _showSeconds = 4.5f;
        [SerializeField] private Material _material;

        private GameObject _pager;
        private TextMeshPro _label;
        private AudioSource _beep;
        private AudioSource _steps;

        public bool IsShown => _pager != null && _pager.activeSelf;
        public string Text => _label != null ? _label.text : "";
        public int Pulses { get; private set; }
        public bool FootstepsPlayed { get; private set; }
        public bool BeepPlayed { get; private set; }

        public AiNpc Npc
        {
            get => _npc;
            set => _npc = value;
        }

        private void Awake()
        {
            BuildPager();
            var stepsGo = new GameObject("Footsteps (2D)");
            stepsGo.transform.SetParent(transform, false);
            _steps = stepsGo.AddComponent<AudioSource>();
            _steps.playOnAwake = false;
            _steps.spatialBlend = 0f;
            _steps.volume = _footstepsVolume;
        }

        private void OnEnable()
        {
            if (_npc != null)
                _npc.OnConversationEnded.AddListener(OnEnded);
        }

        private void OnDisable()
        {
            if (_npc != null)
                _npc.OnConversationEnded.RemoveListener(OnEnded);
        }

        private void OnEnded(string reason)
        {
            StopAllCoroutines();
            StartCoroutine(Page());
        }

        /// <summary>Beep, vibrate, show the text; then the footsteps. Also for tests.</summary>
        public IEnumerator Page()
        {
            FootstepsPlayed = BeepPlayed = false;
            Pulses = 0;
            if (_pager != null)
            {
                _pager.SetActive(true);
                _label.text = _text;
            }
            if (_pagerClip != null)
            {
                _beep.clip = _pagerClip;
                _beep.Play();
                BeepPlayed = true;
            }
            for (var i = 0; i < 3; i++)
            {
                Pulse(0.7f, 0.1f);
                Pulses++;
                yield return new WaitForSeconds(0.22f);
            }
            var waited = 0.66f;
            if (_footstepsDelay > waited)
                yield return new WaitForSeconds(_footstepsDelay - waited);
            if (_footstepsClip != null)
            {
                _steps.clip = _footstepsClip;
                _steps.Play();
                FootstepsPlayed = true;
            }
            var rest = _showSeconds - Mathf.Max(waited, _footstepsDelay);
            if (rest > 0f)
                yield return new WaitForSeconds(rest);
            if (_pager != null)
                _pager.SetActive(false);
        }

        private void Pulse(float amplitude, float seconds)
        {
            var player = _leftHand != null ? _leftHand.GetComponentInChildren<HapticImpulsePlayer>(true) : null;
            if (player != null && player.isActiveAndEnabled)
            {
                player.SendHapticImpulse(amplitude, seconds);
                return;
            }
            if (InputSystem.GetDevice<XRController>(CommonUsages.LeftHand) is XRControllerWithRumble rumble)
                rumble.SendImpulse(amplitude, seconds);
        }

        private void BuildPager()
        {
            _pager = new GameObject("Pager");
            if (_leftHand != null)
            {
                _pager.transform.SetParent(_leftHand, false);
                _pager.transform.localPosition = _localPosition;
                _pager.transform.localRotation = Quaternion.Euler(_localEuler);
            }
            else
                _pager.transform.SetParent(transform, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(body.GetComponent<Collider>());
            body.name = "Body";
            body.transform.SetParent(_pager.transform, false);
            body.transform.localScale = new Vector3(0.06f, 0.015f, 0.04f);
            var renderer = body.GetComponent<Renderer>();
            if (_material != null)
                renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.12f, 0.13f, 0.16f));
            renderer.SetPropertyBlock(block);

            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(screen.GetComponent<Collider>());
            screen.name = "Screen";
            screen.transform.SetParent(_pager.transform, false);
            screen.transform.localPosition = new Vector3(0f, 0.0076f, 0f);
            screen.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            screen.transform.localScale = new Vector3(0.052f, 0.03f, 1f);
            var screenRenderer = screen.GetComponent<Renderer>();
            if (_material != null)
                screenRenderer.sharedMaterial = _material;
            var screenBlock = new MaterialPropertyBlock();
            screenBlock.SetColor("_BaseColor", new Color(0.55f, 0.85f, 0.6f));
            screenBlock.SetColor("_EmissionColor", new Color(0.35f, 0.6f, 0.4f));
            screenRenderer.SetPropertyBlock(screenBlock);

            _label = new GameObject("Text").AddComponent<TextMeshPro>();
            _label.transform.SetParent(_pager.transform, false);
            _label.transform.localPosition = new Vector3(0f, 0.0082f, 0f);
            _label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _label.rectTransform.sizeDelta = new Vector2(0.05f, 0.028f);
            _label.enableAutoSizing = true;
            _label.fontSizeMin = 0.02f;
            _label.fontSizeMax = 0.08f;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = new Color(0.05f, 0.15f, 0.08f);
            _label.text = _text;

            _beep = _pager.AddComponent<AudioSource>();
            _beep.playOnAwake = false;
            _beep.spatialBlend = _leftHand != null ? 0.6f : 0f;
            _beep.minDistance = 1f;
            _pager.SetActive(false);
        }
    }
}
