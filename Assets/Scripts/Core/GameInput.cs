using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace UKCity.Core
{
    /// <summary>
    /// Thin input wrapper so the game works whether the project uses the old Input Manager
    /// or the new Input System package (Unity 6 templates default to the new one).
    /// </summary>
    public static class GameInput
    {
        /// <summary>Set by the UI when a text field or menu wants keyboard focus.</summary>
        public static bool KeyboardCaptured;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private static readonly Dictionary<KeyCode, Key> map = new Dictionary<KeyCode, Key>();

        private static Key ToKey(KeyCode k)
        {
            if (map.TryGetValue(k, out var key)) return key;
            string n = k.ToString();
            if (n.StartsWith("Alpha")) n = "Digit" + n.Substring(5);
            else if (n == "Return") n = "Enter";
            else if (n == "LeftControl") n = "LeftCtrl";
            else if (n == "RightControl") n = "RightCtrl";
            else if (n == "BackQuote") n = "Backquote";
            if (!System.Enum.TryParse(n, out key)) key = Key.None;
            map[k] = key;
            return key;
        }

        private static UnityEngine.InputSystem.Controls.KeyControl Ctl(KeyCode k)
        {
            var kb = Keyboard.current;
            var key = ToKey(k);
            return kb == null || key == Key.None ? null : kb[key];
        }

        public static bool KeyHeld(KeyCode k) => !KeyboardCaptured && (Ctl(k)?.isPressed ?? false);
        public static bool KeyDown(KeyCode k) => !KeyboardCaptured && (Ctl(k)?.wasPressedThisFrame ?? false);
        public static bool KeyUp(KeyCode k) => Ctl(k)?.wasReleasedThisFrame ?? false;

        private static UnityEngine.InputSystem.Controls.ButtonControl Btn(int b)
        {
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m == null) return null;
            return b == 0 ? m.leftButton : b == 1 ? m.rightButton : m.middleButton;
        }

        public static bool MouseHeld(int b) => Btn(b)?.isPressed ?? false;
        public static bool MouseDown(int b) => Btn(b)?.wasPressedThisFrame ?? false;
        public static bool MouseUp(int b) => Btn(b)?.wasReleasedThisFrame ?? false;
        public static Vector2 MouseDelta => (UnityEngine.InputSystem.Mouse.current?.delta.ReadValue() ?? Vector2.zero) * 0.1f;
        public static float Scroll => (UnityEngine.InputSystem.Mouse.current?.scroll.ReadValue().y ?? 0f) / 120f;
        public static Vector2 MousePosition => UnityEngine.InputSystem.Mouse.current?.position.ReadValue() ?? Vector2.zero;
#else
        public static bool KeyHeld(KeyCode k) => !KeyboardCaptured && Input.GetKey(k);
        public static bool KeyDown(KeyCode k) => !KeyboardCaptured && Input.GetKeyDown(k);
        public static bool KeyUp(KeyCode k) => Input.GetKeyUp(k);
        public static bool MouseHeld(int b) => Input.GetMouseButton(b);
        public static bool MouseDown(int b) => Input.GetMouseButtonDown(b);
        public static bool MouseUp(int b) => Input.GetMouseButtonUp(b);
        public static Vector2 MouseDelta => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
        public static float Scroll => Input.mouseScrollDelta.y;
        public static Vector2 MousePosition => Input.mousePosition;
#endif

        public static bool Shift => KeyHeld(KeyCode.LeftShift) || KeyHeld(KeyCode.RightShift);
        public static bool Ctrl => KeyHeld(KeyCode.LeftControl) || KeyHeld(KeyCode.RightControl);

        private static readonly List<KeyCode> digits = new List<KeyCode>
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
        };

        /// <summary>0-8 if a number key 1-9 was pressed this frame, otherwise -1.</summary>
        public static int DigitDown()
        {
            for (int i = 0; i < digits.Count; i++) if (KeyDown(digits[i])) return i;
            return -1;
        }
    }
}
