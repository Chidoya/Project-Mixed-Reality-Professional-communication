using UnityEngine.InputSystem;

namespace ProCommVR.Hospital.Debrief
{
    /// <summary>
    /// The two buttons of the debrief, on the right controller (no laser needed): A skips the doctor's line (and means
    /// Try again at the choice), B skips a memory (and means Finish at the choice). Space and Backspace in the Editor.
    /// Polled, not event based, so steps can wait for them in a coroutine. Tests can press them with <see cref="Press"/>.
    /// </summary>
    public static class DebriefInput
    {
        private static InputAction _a;
        private static InputAction _b;
        private static bool _fakeA, _fakeB;

        private static void Ensure()
        {
            if (_a != null)
                return;
            _a = new InputAction("DebriefA", InputActionType.Button);
            _a.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            _a.AddBinding("<Keyboard>/space");
            _b = new InputAction("DebriefB", InputActionType.Button);
            _b.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            _b.AddBinding("<Keyboard>/backspace");
            _a.Enable();
            _b.Enable();
        }

        /// <summary>A was pressed this frame.</summary>
        public static bool A
        {
            get
            {
                Ensure();
                if (_fakeA)
                {
                    _fakeA = false;
                    return true;
                }
                return _a.WasPressedThisFrame();
            }
        }

        /// <summary>B was pressed this frame.</summary>
        public static bool B
        {
            get
            {
                Ensure();
                if (_fakeB)
                {
                    _fakeB = false;
                    return true;
                }
                return _b.WasPressedThisFrame();
            }
        }

        /// <summary>For tests: the next read of A (or B) is true.</summary>
        public static void Press(bool a)
        {
            if (a)
                _fakeA = true;
            else
                _fakeB = true;
        }

        /// <summary>Forgets presses of tests that nobody read.</summary>
        public static void ClearFake() => _fakeA = _fakeB = false;
    }
}
