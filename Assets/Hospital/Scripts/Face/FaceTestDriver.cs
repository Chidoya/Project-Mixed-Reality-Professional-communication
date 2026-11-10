using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ProCommVR.Hospital.Face
{
    /// <summary>
    /// Testscene liam_face_test: toetsen 1-8 zetten de uitdrukking op iedereen, B laat iedereen
    /// knipperen, L zet HeadLookAt aan/uit, A speelt de Animator-state Angry af (test FaceFromAnimator),
    /// I speelt Idle af.
    /// </summary>
    public class FaceTestDriver : MonoBehaviour
    {
        [SerializeField] private float _blendTime = 0.3f;

        private FaceController[] _faces;
        private HeadLookAt[] _looks;
        private Animator[] _animators;
        private string _status = "1-8 uitdrukking, B knipper, L kijken, A Angry-state, I Idle";

        private void Start()
        {
            _faces = FindObjectsByType<FaceController>(FindObjectsSortMode.None);
            _looks = FindObjectsByType<HeadLookAt>(FindObjectsSortMode.None);
            _animators = FindObjectsByType<Animator>(FindObjectsSortMode.None);
        }

        public void SetAll(FaceExpression expression)
        {
            foreach (var f in _faces)
                f.SetExpression(expression, 1f, _blendTime);
            _status = $"Uitdrukking: {expression}";
        }

        public void BlinkAll()
        {
            foreach (var f in _faces)
                f.Blink();
        }

        public void ToggleLook()
        {
            var on = _looks.Length > 0 && !_looks[0].enabled;
            foreach (var l in _looks)
                l.enabled = on;
            _status = $"HeadLookAt {(on ? "aan" : "uit")}";
        }

        public void PlayState(string state)
        {
            foreach (var a in _animators)
                if (a.runtimeAnimatorController != null)
                    a.CrossFadeInFixedTime(state, 0.25f);
            _status = $"Animator-state: {state}";
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
                return;
            Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8 };
            for (var i = 0; i < digits.Length; i++)
                if (kb[digits[i]].wasPressedThisFrame)
                    SetAll((FaceExpression)i);
            if (kb.bKey.wasPressedThisFrame)
                BlinkAll();
            if (kb.lKey.wasPressedThisFrame)
                ToggleLook();
            if (kb.aKey.wasPressedThisFrame)
                PlayState("Angry");
            if (kb.iKey.wasPressedThisFrame)
                PlayState("Idle");
#endif
        }

        private void OnGUI()
        {
            GUI.Label(new Rect(12, 12, 900, 24), _status);
        }
    }
}
