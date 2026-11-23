using System;
using System.Collections;
using System.Collections.Generic;
using ProCommVR.AiTalk;
using ProCommVR.Emotions;
using ProCommVR.Hospital.AiTalk;
using ProCommVR.Hospital.Face;
using ProCommVR.Player;
using ProCommVR.Pov;
using ProCommVR.Scenarios;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// Plays one moment of the conversation back in the real room 1A, as a memory: grey and a little dark (one extra global
    /// Volume: Color Adjustments and Vignette, no Bloom, no Depth of Field), the clock stands still, a white figure of the
    /// student stands where the student stood, the student's own recorded voice comes from it, then the patient answers with
    /// his real reply and the face of then. The doctor stands next to the player: "Look at his face. What did he hear?",
    /// then she says the better phrase and his face softens (only the face and our emotions, no new AI turn, no score).
    /// Afterwards everything is restored. No extra camera or render texture; only fades move the view.
    /// </summary>
    public class MemoryReplay : MonoBehaviour
    {
        [SerializeField] private AiNpc _patient;
        [SerializeField] private EmotionHolder _holder;
        [SerializeField] private PlayerAvatar _avatar;
        [SerializeField] private Material _ghostMaterial;
        [SerializeField] private VolumeProfile _memoryProfile;
        [SerializeField] private GameObject _bubblePrefab;
        [Tooltip("Where the student stood when there is no recorded pose (the conversation spot).")]
        [SerializeField] private Transform _fallbackSpot;
        [SerializeField, Min(0.1f)] private float _fadeSeconds = 0.6f;
        [SerializeField, Min(0.1f)] private float _volumeSeconds = 0.6f;
        [SerializeField, Min(0f)] private float _lookPause = 2f;
        [SerializeField, Min(0.3f)] private float _sideOffset = 1f;

        private Volume _volume;
        private GameObject _ghost;
        private AudioSource _ghostVoice;
        private DialogueBubble _ghostBubble;
        private DialogueBubble _patientBubble;
        private readonly List<Behaviour> _stoppedClocks = new();

        // restore state
        private bool _active;
        private EmotionState _savedEmotion;
        private FaceExpression _savedExpression;
        private FaceExpression _savedRest;
        private HeadLookAt.LookMode _savedLook;
        private bool _savedPost;
        private UniversalAdditionalCameraData _cameraData;
        private Vector3 _doctorPosition;
        private Quaternion _doctorRotation;
        private Transform _doctor;

        public bool IsPlaying => _active;
        public Volume Volume => _volume;
        public GameObject Ghost => _ghost;
        /// <summary>The moment that plays now (or the last one).</summary>
        public RecordedTurn CurrentTurn { get; private set; }
        public int Played { get; private set; }
        public bool StudentClipPlayed { get; private set; }
        public bool PatientClipPlayed { get; private set; }
        public float VolumeWeight => _volume != null ? _volume.weight : 0f;

        public event Action<bool> PlayingChanged;

        private void Awake()
        {
            if (_memoryProfile == null)
                return;
            var go = new GameObject("Memory Volume");
            go.transform.SetParent(transform, false);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 200f;
            _volume.weight = 0f;
            _volume.sharedProfile = _memoryProfile;
            _volume.enabled = false;
        }

        /// <summary>
        /// Plays the memory of <paramref name="moment"/>. <paramref name="speaker"/> is the head doctor (she stands next to
        /// the player), <paramref name="betterPhrase"/> the line she says at the end (with clip). The caller moves the player
        /// back afterwards (<paramref name="returnTo"/> is called while the view is black).
        /// </summary>
        public IEnumerator Play(RecordedTurn turn, string fallbackStudentText, DoctorSpeaker speaker, DebriefScript script,
            DebriefLine betterPhrase, XROrigin origin, ScreenFade fade, PrototypeFlow flow, Action returnTo)
        {
            if (_patient == null || origin == null)
                yield break;
            CurrentTurn = turn;
            StudentClipPlayed = PatientClipPlayed = false;
            DebriefInput.ClearFake();
            if (fade != null)
                yield return fade.FadeTo(1f, _fadeSeconds);

            Begin(turn, speaker, origin, flow);
            if (fade != null)
                StartCoroutine(fade.FadeTo(0f, _fadeSeconds));
            yield return BlendVolume(1f);

            var skipped = false;
            bool Skip() => skipped || (skipped = DebriefInput.B);

            // 1. the student, from the white figure
            var studentText = turn != null && !string.IsNullOrEmpty(turn.StudentText) ? turn.StudentText : fallbackStudentText;
            if (!Skip())
            {
                if (turn == null || !turn.HasStudentClip)
                    yield return speaker.Say(script.Line("memory_no_audio"));
                ShowBubble(_ghostBubble, $"<b>You:</b> {studentText}");
                if (turn != null && turn.HasStudentClip)
                {
                    _ghostVoice.clip = turn.StudentClip;
                    _ghostVoice.Play();
                    StudentClipPlayed = true;
                }
                yield return Wait(turn != null && turn.HasStudentClip ? turn.StudentClip.length : Reading(studentText), Skip);
                _ghostVoice.Stop();
                HideBubble(_ghostBubble);
            }

            // 2. the patient, with his face of then
            if (!Skip() && turn != null)
            {
                ShowBubble(_patientBubble, turn.PatientText);
                if (turn.HasPatientClip)
                {
                    _patient.PlayReplay(turn.PatientClip);
                    PatientClipPlayed = true;
                }
                yield return Wait(turn.HasPatientClip ? turn.PatientClip.length : Reading(turn.PatientText), Skip);
                _patient.PlayReplay(null);
                HideBubble(_patientBubble);
            }

            // 3. the doctor: look at his face; then the better phrase, and his face softens
            if (!Skip())
            {
                yield return speaker.Say(script.Line("memory_look"));
                yield return Wait(_lookPause, Skip);
            }
            if (!Skip() && betterPhrase != null)
            {
                yield return speaker.Say(script.Line("memory_better"));
                var ownFace = !_patient.FaceFollowsEmotions; // else the EmotionHolder drives the face (EmotionFaceDriver)
                if (ownFace && _patient.Face != null)
                    _patient.Face.SetExpression(script.BetterAimExpression, script.BetterAimIntensity, 2f);
                if (ownFace && _patient.FaceFromAnimator != null)
                    _patient.FaceFromAnimator.RestExpression = script.BetterAimExpression;
                if (_holder != null)
                    _holder.SetTarget(script.BetterAim);
                yield return speaker.Say(betterPhrase);
                yield return Wait(1.2f, Skip);
            }
            if (!Skip())
                yield return speaker.Say(script.Line("memory_back"));

            // back: fade, restore, the caller moves the player, fade in
            if (fade != null)
                yield return fade.FadeTo(1f, _fadeSeconds);
            Restore();
            returnTo?.Invoke();
            yield return null;
            if (fade != null)
                yield return fade.FadeTo(0f, _fadeSeconds);
            Played++;
        }

        private void Begin(RecordedTurn turn, DoctorSpeaker speaker, XROrigin origin, PrototypeFlow flow)
        {
            _active = true;
            PlayingChanged?.Invoke(true);
            var patient = _patient.transform;

            // where the student stood (recorded pose, else the conversation spot)
            Vector3 head;
            float yaw;
            if (turn != null && turn.HasPose)
            {
                head = patient.TransformPoint(turn.HeadLocalPosition);
                yaw = patient.eulerAngles.y + turn.HeadLocalYaw;
            }
            else if (_fallbackSpot != null)
            {
                head = _fallbackSpot.position + Vector3.up * 1.6f;
                yaw = _fallbackSpot.eulerAngles.y;
            }
            else
            {
                head = patient.position + patient.forward * 1.5f + Vector3.up * 1.6f;
                yaw = Quaternion.LookRotation(-patient.forward).eulerAngles.y;
            }
            var floor = FloorBelow(head, patient.position.y);
            var ghostFloor = new Vector3(head.x, floor, head.z);
            var facing = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            SpawnGhost(ghostFloor, facing, head.y - floor);

            // the player next to the ghost, looking between the ghost and the patient
            var right = Vector3.Cross(Vector3.up, facing).normalized;
            var playerFloor = FreeSpot(ghostFloor, new[] { right, -right, -facing + right * 0.5f, -facing - right * 0.5f }, _sideOffset);
            var patientHead = _patient.VoiceSource != null ? _patient.VoiceSource.transform.position : patient.position + Vector3.up;
            var look = Flat((ghostFloor + Flat(patientHead) * 1f) / 2f - Flat(playerFloor));
            if (look.sqrMagnitude < 0.01f)
                look = facing;
            var spot = new GameObject("Memory Spot").transform;
            spot.SetPositionAndRotation(playerFloor, Quaternion.LookRotation(look.normalized));
            if (flow != null)
                flow.Teleport(spot);
            else
                DebriefDirector.TeleportRig(origin, spot);
            Destroy(spot.gameObject);

            // the doctor next to the player
            _doctor = speaker != null ? speaker.transform : null;
            if (_doctor != null)
            {
                _doctorPosition = _doctor.position;
                _doctorRotation = _doctor.rotation;
                var away = Flat(playerFloor - ghostFloor).normalized;
                var doctorFloor = FreeSpot(playerFloor, new[] { away, -look.normalized * 0.8f + away * 0.6f, -look.normalized }, 0.9f);
                _doctor.SetPositionAndRotation(doctorFloor, Quaternion.LookRotation(Flat(patientHead - doctorFloor).normalized));
            }

            // the patient's face and our emotions of then (visual only)
            if (_holder != null)
            {
                _savedEmotion = _holder.Target;
                if (turn != null)
                    _holder.SetImmediate(turn.Emotion);
            }
            var ownFace = !_patient.FaceFollowsEmotions;
            if (ownFace && _patient.Face != null)
            {
                _savedExpression = _patient.Face.Expression;
                if (turn != null && Enum.TryParse(turn.AiEmotion, true, out FaceExpression expression))
                    _patient.Face.SetExpression(expression, Mathf.Max(0.4f, turn.Anger / 100f), 0.05f);
            }
            if (ownFace && _patient.FaceFromAnimator != null)
            {
                _savedRest = _patient.FaceFromAnimator.RestExpression;
                if (turn != null && Enum.TryParse(turn.AiEmotion, true, out FaceExpression rest))
                    _patient.FaceFromAnimator.RestExpression = rest;
            }
            if (ownFace && _patient.HeadLook != null)
            {
                _savedLook = _patient.HeadLook.Mode;
                _patient.HeadLook.Mode = HeadLookAt.LookMode.LookAtPlayer;
            }

            // the clock stands still
            _stoppedClocks.Clear();
            foreach (var clock in FindObjectsByType<WallClock>(FindObjectsSortMode.None))
                if (clock.enabled && Vector3.Distance(clock.transform.position, patient.position) < 8f)
                {
                    clock.enabled = false;
                    _stoppedClocks.Add(clock);
                }

            // post-processing must be on for the Volume (the nurse view keeps it off)
            var camera = origin.Camera;
            _cameraData = camera != null ? camera.GetComponent<UniversalAdditionalCameraData>() : null;
            if (_cameraData != null)
            {
                _savedPost = _cameraData.renderPostProcessing;
                _cameraData.renderPostProcessing = true;
            }
            if (_volume != null)
            {
                _volume.enabled = true;
                _volume.weight = 0f;
            }

            // subtitles for the ghost and the patient
            _ghostBubble = Bubble("Memory Bubble (you)", head + Vector3.up * 0.35f);
            _patientBubble = Bubble("Memory Bubble (patient)", patientHead + Vector3.up * 0.5f);
        }

        /// <summary>Puts everything back as before the memory (also when the debrief is aborted).</summary>
        public void Restore()
        {
            if (!_active)
                return;
            _active = false;
            StopAllCoroutines();
            if (_ghost != null)
                Destroy(_ghost);
            _ghost = null;
            if (_ghostBubble != null)
                Destroy(_ghostBubble.gameObject);
            if (_patientBubble != null)
                Destroy(_patientBubble.gameObject);
            _patient.PlayReplay(null);
            if (_volume != null)
            {
                _volume.weight = 0f;
                _volume.enabled = false;
            }
            if (_cameraData != null)
                _cameraData.renderPostProcessing = _savedPost;
            if (_holder != null)
                _holder.SetImmediate(_savedEmotion);
            if (!_patient.FaceFollowsEmotions)
            {
                if (_patient.Face != null)
                    _patient.Face.SetExpression(_savedExpression, _patient.Face.Intensity, 0.05f);
                if (_patient.FaceFromAnimator != null)
                    _patient.FaceFromAnimator.RestExpression = _savedRest;
                if (_patient.HeadLook != null)
                    _patient.HeadLook.Mode = _savedLook;
            }
            foreach (var clock in _stoppedClocks)
                if (clock != null)
                    clock.enabled = true;
            _stoppedClocks.Clear();
            if (_doctor != null)
                _doctor.SetPositionAndRotation(_doctorPosition, _doctorRotation);
            PlayingChanged?.Invoke(false);
        }

        private void OnDisable() => Restore();

        private IEnumerator BlendVolume(float to)
        {
            if (_volume == null)
                yield break;
            var from = _volume.weight;
            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / _volumeSeconds;
                _volume.weight = Mathf.Lerp(from, to, Mathf.Clamp01(t));
                yield return null;
            }
        }

        private void SpawnGhost(Vector3 floor, Vector3 facing, float eyeHeight)
        {
            var holder = new GameObject("Memory Ghost (you)");
            holder.transform.SetPositionAndRotation(floor, Quaternion.LookRotation(facing));
            if (_avatar != null)
            {
                // Instantiate under an inactive parent: the copy's PlayerAvatar never wakes up and is removed.
                var parking = new GameObject("Ghost Parking");
                parking.SetActive(false);
                var copy = Instantiate(_avatar.gameObject, parking.transform);
                foreach (var avatar in copy.GetComponentsInChildren<PlayerAvatar>(true))
                    DestroyImmediate(avatar);
                foreach (var aura in copy.GetComponentsInChildren<ReputationAura>(true))
                    DestroyImmediate(aura);
                copy.transform.SetParent(holder.transform, false);
                copy.transform.localPosition = Vector3.zero;
                copy.transform.localRotation = Quaternion.identity;
                copy.transform.localScale = _avatar.transform.lossyScale;
                Destroy(parking);
                foreach (var renderer in copy.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = true;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    if (_ghostMaterial != null)
                    {
                        var materials = renderer.sharedMaterials;
                        for (var i = 0; i < materials.Length; i++)
                            materials[i] = _ghostMaterial;
                        renderer.sharedMaterials = materials;
                    }
                }
                var animator = copy.GetComponentInChildren<Animator>();
                if (animator != null && animator.isHuman)
                {
                    var headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                    if (headBone != null && headBone.localScale.x < 0.01f)
                        headBone.localScale = Vector3.one;
                }
                copy.SetActive(true);
            }
            else
            {
                var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                DestroyImmediate(capsule.GetComponent<Collider>());
                capsule.transform.SetParent(holder.transform, false);
                capsule.transform.localPosition = Vector3.up * 0.85f;
                capsule.transform.localScale = new Vector3(0.45f, 0.85f, 0.3f);
                if (_ghostMaterial != null)
                    capsule.GetComponent<Renderer>().sharedMaterial = _ghostMaterial;
            }
            var voice = new GameObject("Your Voice");
            voice.transform.SetParent(holder.transform, false);
            voice.transform.localPosition = Vector3.up * Mathf.Clamp(eyeHeight, 1.2f, 1.9f);
            _ghostVoice = voice.AddComponent<AudioSource>();
            _ghostVoice.playOnAwake = false;
            _ghostVoice.spatialBlend = 0.8f;
            _ghostVoice.minDistance = 2f;
            _ghostVoice.maxDistance = 10f;
            _ghostVoice.rolloffMode = AudioRolloffMode.Linear;
            _ghostVoice.dopplerLevel = 0f;
            _ghost = holder;
        }

        private DialogueBubble Bubble(string name, Vector3 position)
        {
            if (_bubblePrefab == null)
                return null;
            var go = Instantiate(_bubblePrefab, position, Quaternion.identity);
            go.name = name;
            var bubble = go.GetComponent<DialogueBubble>();
            bubble.SetClickable(false);
            go.SetActive(false);
            return bubble;
        }

        private static void ShowBubble(DialogueBubble bubble, string text)
        {
            if (bubble == null)
                return;
            bubble.gameObject.SetActive(true);
            bubble.SetText(text);
            var camera = Camera.main;
            if (camera == null)
                return;
            var away = Flat(bubble.transform.position - camera.transform.position);
            if (away.sqrMagnitude > 0.0001f)
                bubble.transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        private static void HideBubble(DialogueBubble bubble)
        {
            if (bubble != null)
                bubble.gameObject.SetActive(false);
        }

        private static IEnumerator Wait(float seconds, Func<bool> skip)
        {
            var t = 0f;
            while (t < seconds && !skip())
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        private static float Reading(string text) => Mathf.Clamp((text ?? "").Length * 0.06f, 1.5f, 8f);

        /// <summary>The first of the directions (times the distance) where a person fits (no wall or furniture).</summary>
        private static Vector3 FreeSpot(Vector3 from, Vector3[] directions, float distance)
        {
            foreach (var direction in directions)
            {
                var flat = Flat(direction);
                if (flat.sqrMagnitude < 0.0001f)
                    continue;
                var candidate = from + flat.normalized * distance;
                var bottom = candidate + Vector3.up * 0.35f;
                var top = candidate + Vector3.up * 1.6f;
                if (!Physics.CheckCapsule(bottom, top, 0.22f, ~0, QueryTriggerInteraction.Ignore)
                    && !Physics.Linecast(from + Vector3.up * 1f, candidate + Vector3.up * 1f, ~0, QueryTriggerInteraction.Ignore))
                    return candidate;
            }
            return from + Flat(directions[0]).normalized * distance;
        }

        private static float FloorBelow(Vector3 head, float fallback)
        {
            if (Physics.Raycast(head, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }

        private static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);
    }
}
