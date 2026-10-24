// MR Operating Theatre - shared library (H.M.H. Lakshan, IT23404564)
// InputBridge: one place that reads keyboard/mouse (old or new Input System) and
// XR controllers (UnityEngine.XR InputDevices - works with OpenXR on Meta Quest, Pico, Vive...).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using XRUsages = UnityEngine.XR.CommonUsages;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using IKey = UnityEngine.InputSystem.Key;
#endif

namespace MROT
{
    public static class InputBridge
    {
        // ------------------------------------------------------------------ XR
        static readonly List<UnityEngine.XR.InputDevice> _devs = new List<UnityEngine.XR.InputDevice>();
        static readonly Dictionary<string, bool> _prev = new Dictionary<string, bool>();
        static readonly Dictionary<string, bool> _cur = new Dictionary<string, bool>();
        static int _frame = -1;

        public static bool XRActive
        {
            get
            {
                if (!XRSettings.enabled) return false;
                var heads = new List<UnityEngine.XR.InputDevice>();
                InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, heads);
                return heads.Count > 0 && XRSettings.isDeviceActive;
            }
        }

        public static UnityEngine.XR.InputDevice Hand(bool right)
        {
            _devs.Clear();
            var c = InputDeviceCharacteristics.Controller | (right ? InputDeviceCharacteristics.Right : InputDeviceCharacteristics.Left);
            InputDevices.GetDevicesWithCharacteristics(c, _devs);
            return _devs.Count > 0 ? _devs[0] : default(UnityEngine.XR.InputDevice);
        }

        public static bool HandPose(bool right, out Vector3 pos, out Quaternion rot)
        {
            var d = Hand(right);
            pos = Vector3.zero; rot = Quaternion.identity;
            if (!d.isValid) return false;
            bool a = d.TryGetFeatureValue(XRUsages.devicePosition, out pos);
            bool b = d.TryGetFeatureValue(XRUsages.deviceRotation, out rot);
            return a && b;
        }

        public static Vector2 Stick(bool right)
        {
            var d = Hand(right);
            Vector2 v;
            if (d.isValid && d.TryGetFeatureValue(XRUsages.primary2DAxis, out v)) return v;
            return Vector2.zero;
        }

        static bool Btn(bool right, InputFeatureUsage<bool> u)
        {
            var d = Hand(right);
            bool v;
            return d.isValid && d.TryGetFeatureValue(u, out v) && v;
        }

        static void Tick()
        {
            if (_frame == Time.frameCount) return;
            _frame = Time.frameCount;
            _prev.Clear();
            foreach (var kv in _cur) _prev[kv.Key] = kv.Value;
            _cur["triggerR"] = Btn(true, XRUsages.triggerButton);
            _cur["triggerL"] = Btn(false, XRUsages.triggerButton);
            _cur["gripR"] = Btn(true, XRUsages.gripButton);
            _cur["gripL"] = Btn(false, XRUsages.gripButton);
            _cur["aR"] = Btn(true, XRUsages.primaryButton);
            _cur["bR"] = Btn(true, XRUsages.secondaryButton);
            _cur["xL"] = Btn(false, XRUsages.primaryButton);
            _cur["yL"] = Btn(false, XRUsages.secondaryButton);
            _cur["menu"] = Btn(false, XRUsages.menuButton);
        }
        static bool Down(string k) { Tick(); bool c, p; _cur.TryGetValue(k, out c); _prev.TryGetValue(k, out p); return c && !p; }
        static bool Held(string k) { Tick(); bool c; _cur.TryGetValue(k, out c); return c; }

        // ------------------------------------------------------------------ desktop helpers
        public static Vector2 MousePosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null) return Mouse.current.position.ReadValue();
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.mousePosition;
#else
                return Vector2.zero;
#endif
            }
        }
        public static Vector2 MouseDelta
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null) return Mouse.current.delta.ReadValue() * 0.1f;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#else
                return Vector2.zero;
#endif
            }
        }
        public static bool MouseDown(int b)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                var c = b == 0 ? Mouse.current.leftButton : (b == 1 ? Mouse.current.rightButton : Mouse.current.middleButton);
                return c.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(b);
#else
            return false;
#endif
        }
        public static bool MouseHeld(int b)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                var c = b == 0 ? Mouse.current.leftButton : (b == 1 ? Mouse.current.rightButton : Mouse.current.middleButton);
                return c.isPressed;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(b);
#else
            return false;
#endif
        }
        public static float Scroll
        {
            get
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null) return Mouse.current.scroll.ReadValue().y / 120f;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.mouseScrollDelta.y;
#else
                return 0f;
#endif
            }
        }

#if ENABLE_INPUT_SYSTEM
        static IKey ToKey(KeyCode k)
        {
            switch (k)
            {
                case KeyCode.W: return IKey.W; case KeyCode.A: return IKey.A; case KeyCode.S: return IKey.S; case KeyCode.D: return IKey.D;
                case KeyCode.Q: return IKey.Q; case KeyCode.E: return IKey.E; case KeyCode.F: return IKey.F; case KeyCode.T: return IKey.T;
                case KeyCode.H: return IKey.H; case KeyCode.M: return IKey.M; case KeyCode.R: return IKey.R; case KeyCode.Space: return IKey.Space;
                case KeyCode.Return: return IKey.Enter; case KeyCode.Escape: return IKey.Escape; case KeyCode.Tab: return IKey.Tab;
                case KeyCode.LeftShift: return IKey.LeftShift; case KeyCode.UpArrow: return IKey.UpArrow; case KeyCode.DownArrow: return IKey.DownArrow;
                case KeyCode.LeftArrow: return IKey.LeftArrow; case KeyCode.RightArrow: return IKey.RightArrow; case KeyCode.Backspace: return IKey.Backspace;
                case KeyCode.Alpha1: return IKey.Digit1; case KeyCode.Alpha2: return IKey.Digit2; case KeyCode.Alpha3: return IKey.Digit3; case KeyCode.Alpha4: return IKey.Digit4;
                case KeyCode.Y: return IKey.Y; case KeyCode.N: return IKey.N; case KeyCode.C: return IKey.C; case KeyCode.V: return IKey.V;
                default: return IKey.None;
            }
        }
#endif
        public static bool KeyHeld(KeyCode k)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) { var kk = ToKey(k); if (kk != IKey.None) return Keyboard.current[kk].isPressed; }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKey(k);
#else
            return false;
#endif
        }
        public static bool KeyDown(KeyCode k)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) { var kk = ToKey(k); if (kk != IKey.None) return Keyboard.current[kk].wasPressedThisFrame; }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(k);
#else
            return false;
#endif
        }

        // ------------------------------------------------------------------ semantic actions
        /// <summary>Select / click (left mouse or either trigger).</summary>
        public static bool SelectDown(bool right) { return Down(right ? "triggerR" : "triggerL"); }
        public static bool DesktopSelectDown { get { return MouseDown(0); } }
        /// <summary>Ask Nila (push-to-talk): hold B / Y on controllers or hold T / Space on keyboard.</summary>
        public static bool AskHeld { get { return Held("bR") || Held("yL") || KeyHeld(KeyCode.T); } }
        public static bool AskDown { get { return Down("bR") || Down("yL") || KeyDown(KeyCode.T); } }
        /// <summary>Foot tap (virtual foot): A / X on controllers or F on keyboard.</summary>
        public static bool FootTapDown { get { return Down("aR") || Down("xL") || KeyDown(KeyCode.F); } }
        public static bool MenuDown { get { return Down("menu") || KeyDown(KeyCode.Escape) || KeyDown(KeyCode.M); } }
        public static bool GripHeld(bool right) { return Held(right ? "gripR" : "gripL"); }
    }
}
