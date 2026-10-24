using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace IVSim
{
    public enum IVKey
    {
        W, A, S, D, Q, E, F, G, H, R, X, C, Space, LeftShift, Escape, Tab,
        Alpha1, Alpha2, Alpha3, UpArrow, DownArrow, Enter
    }

    /// <summary>
    /// Keyboard/mouse wrapper that works with either the new Input System (default in Unity 6)
    /// or the old Input Manager, so the project compiles whichever one is enabled.
    /// </summary>
    public static class IVInput
    {
#if ENABLE_INPUT_SYSTEM
        static Key Map(IVKey k)
        {
            switch (k)
            {
                case IVKey.W: return Key.W;
                case IVKey.A: return Key.A;
                case IVKey.S: return Key.S;
                case IVKey.D: return Key.D;
                case IVKey.Q: return Key.Q;
                case IVKey.E: return Key.E;
                case IVKey.F: return Key.F;
                case IVKey.G: return Key.G;
                case IVKey.H: return Key.H;
                case IVKey.R: return Key.R;
                case IVKey.X: return Key.X;
                case IVKey.C: return Key.C;
                case IVKey.Space: return Key.Space;
                case IVKey.LeftShift: return Key.LeftShift;
                case IVKey.Escape: return Key.Escape;
                case IVKey.Tab: return Key.Tab;
                case IVKey.Alpha1: return Key.Digit1;
                case IVKey.Alpha2: return Key.Digit2;
                case IVKey.Alpha3: return Key.Digit3;
                case IVKey.UpArrow: return Key.UpArrow;
                case IVKey.DownArrow: return Key.DownArrow;
                default: return Key.Enter;
            }
        }

        public static bool GetKey(IVKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].isPressed;
        }

        public static bool GetKeyDown(IVKey k)
        {
            var kb = Keyboard.current;
            return kb != null && kb[Map(k)].wasPressedThisFrame;
        }

        static UnityEngine.InputSystem.Controls.ButtonControl MouseButtonControl(int i)
        {
            var m = Mouse.current;
            if (m == null) return null;
            if (i == 0) return m.leftButton;
            if (i == 1) return m.rightButton;
            return m.middleButton;
        }

        public static bool MouseButton(int i) { var b = MouseButtonControl(i); return b != null && b.isPressed; }
        public static bool MouseButtonDown(int i) { var b = MouseButtonControl(i); return b != null && b.wasPressedThisFrame; }
        public static bool MouseButtonUp(int i) { var b = MouseButtonControl(i); return b != null && b.wasReleasedThisFrame; }
        public static bool HasMouse { get { return Mouse.current != null; } }

        public static Vector2 MousePosition
        {
            get { var m = Mouse.current; return m != null ? m.position.ReadValue() : Vector2.zero; }
        }

        /// <summary>Returns -1, 0 or +1 per frame (normalised so all platforms behave the same).</summary>
        public static float ScrollSteps
        {
            get
            {
                var m = Mouse.current;
                if (m == null) return 0f;
                float y = m.scroll.ReadValue().y;
                return Mathf.Abs(y) < 0.01f ? 0f : Mathf.Sign(y);
            }
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        static KeyCode Map(IVKey k)
        {
            switch (k)
            {
                case IVKey.W: return KeyCode.W;
                case IVKey.A: return KeyCode.A;
                case IVKey.S: return KeyCode.S;
                case IVKey.D: return KeyCode.D;
                case IVKey.Q: return KeyCode.Q;
                case IVKey.E: return KeyCode.E;
                case IVKey.F: return KeyCode.F;
                case IVKey.G: return KeyCode.G;
                case IVKey.H: return KeyCode.H;
                case IVKey.R: return KeyCode.R;
                case IVKey.X: return KeyCode.X;
                case IVKey.C: return KeyCode.C;
                case IVKey.Space: return KeyCode.Space;
                case IVKey.LeftShift: return KeyCode.LeftShift;
                case IVKey.Escape: return KeyCode.Escape;
                case IVKey.Tab: return KeyCode.Tab;
                case IVKey.Alpha1: return KeyCode.Alpha1;
                case IVKey.Alpha2: return KeyCode.Alpha2;
                case IVKey.Alpha3: return KeyCode.Alpha3;
                case IVKey.UpArrow: return KeyCode.UpArrow;
                case IVKey.DownArrow: return KeyCode.DownArrow;
                default: return KeyCode.Return;
            }
        }
        public static bool GetKey(IVKey k) { return Input.GetKey(Map(k)); }
        public static bool GetKeyDown(IVKey k) { return Input.GetKeyDown(Map(k)); }
        public static bool MouseButton(int i) { return Input.GetMouseButton(i); }
        public static bool MouseButtonDown(int i) { return Input.GetMouseButtonDown(i); }
        public static bool MouseButtonUp(int i) { return Input.GetMouseButtonUp(i); }
        public static bool HasMouse { get { return Input.mousePresent; } }
        public static Vector2 MousePosition { get { return Input.mousePosition; } }
        public static float ScrollSteps
        {
            get { float y = Input.mouseScrollDelta.y; return Mathf.Abs(y) < 0.01f ? 0f : Mathf.Sign(y); }
        }
#else
        public static bool GetKey(IVKey k) { return false; }
        public static bool GetKeyDown(IVKey k) { return false; }
        public static bool MouseButton(int i) { return false; }
        public static bool MouseButtonDown(int i) { return false; }
        public static bool MouseButtonUp(int i) { return false; }
        public static bool HasMouse { get { return false; } }
        public static Vector2 MousePosition { get { return Vector2.zero; } }
        public static float ScrollSteps { get { return 0f; } }
#endif
    }
}
