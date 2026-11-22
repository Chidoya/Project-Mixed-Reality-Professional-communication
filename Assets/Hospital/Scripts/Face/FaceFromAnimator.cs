using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProCommVR.Hospital.Face
{
    /// <summary>
    /// Zet de uitdrukking automatisch volgens de Animator-state (laag 0): Angry, Sad en Crying krijgen
    /// hun gezicht, andere states de rustuitdrukking. Zo krijgt een personage een boos gezicht zodra
    /// iemand (bv. NpcReactor) de state Angry afspeelt, zonder extra code.
    /// Een SetExpression van buitenaf wint 5 s lang, of tot de state verandert.
    /// </summary>
    [DisallowMultipleComponent]
    public class FaceFromAnimator : MonoBehaviour
    {
        [Serializable]
        public struct StateExpression
        {
            public string State;
            public FaceExpression Expression;

            public StateExpression(string state, FaceExpression expression)
            {
                State = state;
                Expression = expression;
            }
        }

        [SerializeField] private List<StateExpression> _map = new()
        {
            new StateExpression("Angry", FaceExpression.Angry),
            new StateExpression("Sad", FaceExpression.Sad),
            new StateExpression("Crying", FaceExpression.Crying),
        };

        [Tooltip("Uitdrukking voor Idle, Talking, Sitting, Laying en andere states zonder koppeling.")]
        [SerializeField] private FaceExpression _restExpression = FaceExpression.Neutral;
        [SerializeField] private float _overrideSeconds = 5f;

        private Animator _animator;
        private FaceController _face;
        private readonly Dictionary<int, FaceExpression> _hashes = new();
        private float _seenExternal = float.NegativeInfinity;
        private int _stateAtOverride;

        public FaceExpression RestExpression
        {
            get => _restExpression;
            set => _restExpression = value;
        }

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            _face = GetComponent<FaceController>() ?? GetComponentInChildren<FaceController>();
            foreach (var m in _map)
                if (!string.IsNullOrEmpty(m.State))
                    _hashes[Animator.StringToHash(m.State)] = m.Expression;
        }

        private void Update()
        {
            if (_animator == null || _face == null || _animator.runtimeAnimatorController == null || !_animator.isActiveAndEnabled)
                return;

            var info = _animator.IsInTransition(0) ? _animator.GetNextAnimatorStateInfo(0) : _animator.GetCurrentAnimatorStateInfo(0);
            var hash = info.shortNameHash;

            if (!Mathf.Approximately(_face.LastExternalSetTime, _seenExternal))
            {
                _seenExternal = _face.LastExternalSetTime;
                _stateAtOverride = hash;
            }
            var overridden = Time.time - _seenExternal < _overrideSeconds && hash == _stateAtOverride;
            if (overridden)
                return;

            var want = _hashes.TryGetValue(hash, out var e) ? e : _restExpression;
            if (want != _face.Expression)
                _face.ApplyAutomatic(want);
        }
    }
}
