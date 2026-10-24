// Hand input for Meta Quest 3 through OpenXR:
//   1) XR Hands (com.unity.xr.hands) articulated hand tracking - preferred (26 joints, ~72-120 Hz)
//   2) XR controllers (UnityEngine.XR.InputDevices) -> procedural hand (grip/trigger drive curl/pinch)
//   3) Desktop simulator (mouse + keyboard) so the module is testable in the Editor without a headset.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if ND_XR_HANDS
using UnityEngine.XR.Hands;
#endif
#if ENABLE_INPUT_SYSTEM && ND_INPUTSYSTEM
using UnityEngine.InputSystem;
#endif

namespace NeonatalDelivery
{
    [DefaultExecutionOrder(-200)]
    public class HandTrackingSource : MonoBehaviour
    {
        public static HandTrackingSource Instance { get; private set; }
        [Tooltip("XR origin / tracking-space transform. Joint poses from the runtime are relative to this.")]
        public Transform trackingSpace;
        public Camera headCamera;
        public bool allowControllerFallback = true;
        public bool allowDesktopSimulator = true;
        [Header("Desktop simulator")]
        public float simReach = 0.45f;
        public KeyCode simInfo = KeyCode.H;

        public readonly TrackedHand Left = new TrackedHand(HandSide.Left);
        public readonly TrackedHand Right = new TrackedHand(HandSide.Right);
        public TrackedHand Get(HandSide s) => s == HandSide.Left ? Left : Right;
        public HandSourceKind ActiveSource { get; private set; }
        public bool XRPresent { get; private set; }
        public bool HandTrackingAvailable { get; private set; }
        public float LastUpdateTime { get; private set; }

        // desktop simulator state
        HandSide _simActive = HandSide.Right;
        float _simDepth = 0.45f;
        readonly Vector2[] _simScreen = { new Vector2(0.42f, 0.42f), new Vector2(0.58f, 0.42f) };
        readonly float[] _simCurl = new float[2]; readonly float[] _simPinch = new float[2];
        readonly Vector3[] _simEuler = { new Vector3(70, 0, 20), new Vector3(70, 0, -20) };
        bool _simLocked; Vector3[] _simLockPos = new Vector3[2];

#if ND_XR_HANDS
        XRHandSubsystem _hands;
        static readonly List<XRHandSubsystem> s_Subs = new List<XRHandSubsystem>();
#endif
        static readonly List<UnityEngine.XR.InputDevice> s_Devs = new List<UnityEngine.XR.InputDevice>();

        void Awake() { Instance = this; }
        void OnEnable() { Application.onBeforeRender += OnBeforeRender; }
        void OnDisable() { Application.onBeforeRender -= OnBeforeRender; }

        void Update() { Poll(false); }
        void OnBeforeRender() { if (ActiveSource == HandSourceKind.HandTracking) Poll(true); }

        void Poll(bool beforeRender)
        {
            XRPresent = XRSettings.isDeviceActive;
            bool got = false;
#if ND_XR_HANDS
            got = PollXRHands();
#endif
            HandTrackingAvailable = got;
            if (got) ActiveSource = HandSourceKind.HandTracking;
            else if (!beforeRender && allowControllerFallback && XRPresent && PollControllers()) ActiveSource = HandSourceKind.Controller;
            else if (!beforeRender && allowDesktopSimulator && !XRPresent) { PollDesktop(); ActiveSource = HandSourceKind.DesktopSimulator; }
            else if (!beforeRender && !got) { Left.tracked = Right.tracked = false; ActiveSource = HandSourceKind.None; }
            float t = Time.time;
            Left.UpdateDerived(t); Right.UpdateDerived(t);
            LastUpdateTime = t;
        }

        Vector3 TS(Vector3 p) { return trackingSpace != null ? trackingSpace.TransformPoint(p) : p; }
        Quaternion TS(Quaternion q) { return trackingSpace != null ? trackingSpace.rotation * q : q; }

#if ND_XR_HANDS
        bool PollXRHands()
        {
            if (_hands == null || !_hands.running)
            {
                s_Subs.Clear(); SubsystemManager.GetSubsystems(s_Subs);
                _hands = null;
                foreach (var s in s_Subs) if (s.running) { _hands = s; break; }
                if (_hands == null) return false;
            }
            bool l = ReadHand(_hands.leftHand, Left), r = ReadHand(_hands.rightHand, Right);
            return l || r;
        }

        bool ReadHand(XRHand hand, TrackedHand dst)
        {
            dst.tracked = hand.isTracked;
            if (!dst.tracked) return false;
            dst.source = HandSourceKind.HandTracking;
            for (int i = 0; i < J.Count; i++)
            {
                var joint = hand.GetJoint(XRHandJointIDUtility.FromIndex(i));
                if (joint.TryGetPose(out Pose p)) { dst.pos[i] = TS(p.position); dst.rot[i] = TS(p.rotation); }
                if (joint.TryGetRadius(out float rad)) dst.radius[i] = rad;
            }
            return true;
        }
#endif

        bool PollControllers()
        {
            bool any = false;
            any |= ReadController(XRNode.LeftHand, Left);
            any |= ReadController(XRNode.RightHand, Right);
            return any;
        }

        bool ReadController(XRNode node, TrackedHand dst)
        {
            s_Devs.Clear(); InputDevices.GetDevicesAtXRNode(node, s_Devs);
            if (s_Devs.Count == 0) { dst.tracked = false; return false; }
            var d = s_Devs[0];
            if (!d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 p) || !d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion q)) { dst.tracked = false; return false; }
            d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.grip, out float grip);
            d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float trig);
            // controller grip pose -> palm pose (palm sits slightly below/behind the controller ring)
            Quaternion palmRot = TS(q) * Quaternion.Euler(40f, 0, 0);
            Vector3 palmPos = TS(p) + palmRot * new Vector3(0, -0.015f, -0.03f);
            ProceduralHand.Generate(dst, palmPos, palmRot, grip, trig);
            dst.pinchGripForce = Mathf.Max(grip, trig);
            dst.source = HandSourceKind.Controller;
            return true;
        }

        /// <summary>Haptic pulse on the matching controller (no-op with bare-hand tracking).</summary>
        public void Haptic(HandSide side, float amplitude, float seconds)
        {
            s_Devs.Clear(); InputDevices.GetDevicesAtXRNode(side == HandSide.Left ? XRNode.LeftHand : XRNode.RightHand, s_Devs);
            foreach (var d in s_Devs)
                if (d.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse) d.SendHapticImpulse(0, Mathf.Clamp01(amplitude), seconds);
        }

        // ------------------------------------------------------------------ desktop simulator
        // Mouse moves the active hand in the view plane, scroll = depth, Tab = switch hand, LMB = pinch, RMB = grip,
        // Q/E roll the palm, R/F pitch the palm, Space = lock both hands in place.
        void PollDesktop()
        {
            var cam = headCamera != null ? headCamera : Camera.main;
            if (cam == null) return;
#if ENABLE_INPUT_SYSTEM && ND_INPUTSYSTEM
            var kb = Keyboard.current; var mouse = Mouse.current;
            int a = (int)_simActive;
            if (kb != null)
            {
                if (kb[Key.Tab].wasPressedThisFrame) _simActive = _simActive == HandSide.Left ? HandSide.Right : HandSide.Left;
                if (kb[Key.Space].wasPressedThisFrame) { _simLocked = !_simLocked; }
                float rs = 90f * Time.deltaTime;
                if (kb[Key.Q].isPressed) _simEuler[a].z += rs; if (kb[Key.E].isPressed) _simEuler[a].z -= rs;
                if (kb[Key.R].isPressed) _simEuler[a].x -= rs; if (kb[Key.F].isPressed) _simEuler[a].x += rs;
                if (kb[Key.Z].isPressed) _simEuler[a].y -= rs; if (kb[Key.X].isPressed) _simEuler[a].y += rs;
            }
            if (mouse != null && !_simLocked)
            {
                var mp = mouse.position.ReadValue();
                _simScreen[a] = new Vector2(mp.x / Mathf.Max(1, Screen.width), mp.y / Mathf.Max(1, Screen.height));
                _simDepth = Mathf.Clamp(_simDepth + mouse.scroll.ReadValue().y * 0.0004f, 0.15f, 1.2f);
                _simPinch[a] = Mathf.MoveTowards(_simPinch[a], mouse.leftButton.isPressed ? 1f : 0f, Time.deltaTime * 6f);
                _simCurl[a] = Mathf.MoveTowards(_simCurl[a], mouse.rightButton.isPressed ? 0.9f : 0.12f, Time.deltaTime * 4f);
            }
#endif
            for (int i = 0; i < 2; i++)
            {
                var h = i == 0 ? Left : Right;
                var ray = cam.ViewportPointToRay(new Vector3(_simScreen[i].x, _simScreen[i].y, 0));
                Vector3 p = ray.GetPoint(i == (int)_simActive ? _simDepth : Mathf.Max(0.3f, _simDepth));
                if (_simLocked) p = _simLockPos[i]; else _simLockPos[i] = p;
                Quaternion r = cam.transform.rotation * Quaternion.Euler(_simEuler[i]);
                ProceduralHand.Generate(h, p, r, _simCurl[i], _simPinch[i]);
                h.source = HandSourceKind.DesktopSimulator;
            }
        }

        public string DescribeSimulatorControls()
        {
            return "Desktop simulator: mouse = move hand, scroll = depth, Tab = switch hand, LMB = pinch, RMB = grip, Q/E roll, R/F pitch, Z/X yaw, Space = freeze hands";
        }
    }
}
