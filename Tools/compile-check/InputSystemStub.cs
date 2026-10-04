// Minimal stand-in for the parts of com.unity.inputsystem that GameInput uses, so the
// Input System code path can be compile-checked without the package.
namespace UnityEngine.InputSystem
{
    public enum Key { None, Space, Enter, Tab, Backquote, LeftShift, RightShift, LeftCtrl, RightCtrl, Escape, A, Digit1 }

    namespace Controls
    {
        public class ButtonControl { public bool isPressed; public bool wasPressedThisFrame; public bool wasReleasedThisFrame; }
        public class KeyControl : ButtonControl { }
        public class Vector2Control { public Vector2 ReadValue() => default; }
    }

    public class Keyboard
    {
        public static Keyboard current;
        public Controls.KeyControl this[Key key] => null;
    }

    public class Mouse
    {
        public static Mouse current;
        public Controls.ButtonControl leftButton, rightButton, middleButton;
        public Controls.Vector2Control delta, scroll, position;
    }
}
