using System;
using System.Collections;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Shows the NPC's reaction to the chosen option in the world: moves the NPC into the option's
    /// <see cref="NpcPose"/>, plays the pose's Animator state (or fires an Animator trigger with the pose name, if the
    /// controller has one) and plays the option's voice clip. Returns to the start pose when a scenario starts.
    /// The start pose is kept relative to the body's parent, so the NPC can move together with its parent
    /// (e.g. <see cref="ConversationLocationMover"/>), with pose targets that are children of that parent.
    /// </summary>
    public class NpcReactor : MonoBehaviour
    {
        [Serializable]
        private class PoseTarget
        {
            public NpcPose Pose;

            [Tooltip("Where the NPC body moves to for this pose (position and rotation). Empty: the NPC stays where it is.")]
            public Transform Target;

            [Tooltip("Optional. Animator state to play in this pose, e.g. \"Angry\" or \"Talking\".")]
            public string AnimatorState;
        }

        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("The NPC body that moves into the pose. Leave empty to use this object.")]
        [SerializeField] private Transform _body;

        [Tooltip("One entry per pose. A pose without an entry or target keeps the NPC where it is.")]
        [SerializeField] private PoseTarget[] _poses = Array.Empty<PoseTarget>();

        [Tooltip("Optional. Plays the pose's Animator State, or gets a trigger with the pose name (e.g. \"TurnsAway\") if it has one.")]
        [SerializeField] private Animator _animator;

        [Tooltip("Optional. Plays the option's voice clip.")]
        [SerializeField] private AudioSource _voice;

        [Tooltip("Time to move into a pose, in seconds.")]
        [SerializeField, Min(0f)] private float _poseDuration = 0.8f;

        private Vector3 _startPosition;
        private Quaternion _startRotation;
        private Coroutine _move;

        private Transform Body => _body != null ? _body : transform;

        private void Awake()
        {
            _startPosition = Body.localPosition;
            _startRotation = Body.localRotation;
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

        private void OnScenarioStarted(Scenario scenario)
        {
            if (_move != null)
                StopCoroutine(_move);
            _move = null;
            Body.SetLocalPositionAndRotation(_startPosition, _startRotation);
            if (_voice != null)
                _voice.Stop();
        }

        private void OnOptionChosen(Scenario scenario, DialogueOption option)
        {
            var pose = FindPose(option.NpcPose);
            if (pose != null && pose.Target != null)
            {
                if (_move != null)
                    StopCoroutine(_move);
                _move = StartCoroutine(MoveTo(pose.Target.position, pose.Target.rotation));
            }

            if (_animator != null)
                Animate(option.NpcPose, pose);

            if (_voice != null && option.NpcVoice != null)
                _voice.PlayOneShot(option.NpcVoice);
        }

        private void Animate(NpcPose pose, PoseTarget target)
        {
            if (target != null && !string.IsNullOrEmpty(target.AnimatorState))
            {
                var state = Animator.StringToHash(target.AnimatorState);
                if (_animator.HasState(0, state))
                    _animator.CrossFade(state, 0.25f);
                else
                    Debug.LogWarning($"[ProCommVR] Animator of '{_animator.name}' has no state '{target.AnimatorState}'.", this);
                return;
            }

            if (pose == NpcPose.Unchanged)
                return;
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == pose.ToString())
                {
                    _animator.SetTrigger(parameter.name);
                    return;
                }
            }
        }

        private PoseTarget FindPose(NpcPose pose)
        {
            foreach (var poseTarget in _poses)
            {
                if (poseTarget != null && poseTarget.Pose == pose)
                    return poseTarget;
            }
            return null;
        }

        private IEnumerator MoveTo(Vector3 position, Quaternion rotation)
        {
            var fromPosition = Body.position;
            var fromRotation = Body.rotation;
            for (var t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(_poseDuration, 0.0001f))
            {
                var eased = Mathf.SmoothStep(0f, 1f, t);
                Body.SetPositionAndRotation(Vector3.Lerp(fromPosition, position, eased),
                    Quaternion.Slerp(fromRotation, rotation, eased));
                yield return null;
            }
            Body.SetPositionAndRotation(position, rotation);
            _move = null;
        }
    }
}
