using System;
using System.Collections;
using UnityEngine;

namespace ProCommVR.Scenarios
{
    /// <summary>
    /// Shows the NPC's reaction to the chosen option in the world: moves the NPC into the option's
    /// <see cref="NpcPose"/>, fires an Animator trigger with the pose name (once there is a real character)
    /// and plays the option's voice clip. Returns to the start pose when a scenario starts.
    /// </summary>
    public class NpcReactor : MonoBehaviour
    {
        [Serializable]
        private class PoseTarget
        {
            public NpcPose Pose;

            [Tooltip("Where the NPC body moves to for this pose (position and rotation).")]
            public Transform Target;
        }

        [SerializeField] private ScenarioPresenter _presenter;

        [Tooltip("The NPC body that moves into the pose. Leave empty to use this object.")]
        [SerializeField] private Transform _body;

        [Tooltip("One target per pose. A pose without a target keeps the NPC where it is.")]
        [SerializeField] private PoseTarget[] _poses = Array.Empty<PoseTarget>();

        [Tooltip("Optional. Gets a trigger with the pose name, e.g. \"TurnsAway\".")]
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
            _startPosition = Body.position;
            _startRotation = Body.rotation;
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
            Body.SetPositionAndRotation(_startPosition, _startRotation);
            if (_voice != null)
                _voice.Stop();
        }

        private void OnOptionChosen(Scenario scenario, DialogueOption option)
        {
            var target = FindTarget(option.NpcPose);
            if (target != null)
            {
                if (_move != null)
                    StopCoroutine(_move);
                _move = StartCoroutine(MoveTo(target.position, target.rotation));
            }

            if (_animator != null && option.NpcPose != NpcPose.Unchanged)
                _animator.SetTrigger(option.NpcPose.ToString());

            if (_voice != null && option.NpcVoice != null)
                _voice.PlayOneShot(option.NpcVoice);
        }

        private Transform FindTarget(NpcPose pose)
        {
            foreach (var poseTarget in _poses)
            {
                if (poseTarget != null && poseTarget.Pose == pose)
                    return poseTarget.Target;
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
