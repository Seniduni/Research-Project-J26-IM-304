// XR rig for Meta Quest 3 (OpenXR): floor-level tracking space, head pose, desktop fallback camera.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if ND_XR_COREUTILS
using Unity.XR.CoreUtils;
#endif
#if ENABLE_INPUT_SYSTEM && ND_INPUTSYSTEM
using UnityEngine.InputSystem;
#endif

namespace NeonatalDelivery
{
    [DefaultExecutionOrder(-300)]
    public class NDXRRig : MonoBehaviour
    {
        public Transform trackingSpace;
        public Camera cam;
        public float desktopEyeHeight = 1.62f;
        public float seatedHeightOffset = 0.38f;
        public bool seated;
        public bool IsXR { get; private set; }
        float _yaw, _pitch = 18f;
        static readonly List<XRInputSubsystem> s_Inputs = new List<XRInputSubsystem>();
        static readonly List<UnityEngine.XR.InputDevice> s_Heads = new List<UnityEngine.XR.InputDevice>();

        public void EnsureBuilt()
        {
            if (trackingSpace == null) { trackingSpace = new GameObject("TrackingSpace").transform; trackingSpace.SetParent(transform, false); }
            if (cam == null)
            {
                var existing = Camera.main;
                if (existing != null && existing.transform.parent == null && existing.GetComponentInParent<NDXRRig>() == null) { if (Application.isPlaying) Destroy(existing.gameObject); else DestroyImmediate(existing.gameObject); }
                var go = new GameObject("Main Camera"); go.tag = "MainCamera"; go.transform.SetParent(trackingSpace, false);
                cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>();
            }
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 60f;
#if ND_URP
            var data = NDUtil.GetOrAdd<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(cam.gameObject);
            data.requiresColorOption = UnityEngine.Rendering.Universal.CameraOverrideOption.On;   // privacy veil samples the opaque texture
            data.renderPostProcessing = true;
#endif
#if ENABLE_INPUT_SYSTEM && ND_INPUTSYSTEM
            // head pose from the HMD through the Input System (standard XR setup; silent when no headset is connected)
            if (!cam.TryGetComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>(out _))
            {
                var tpd = cam.gameObject.AddComponent<UnityEngine.InputSystem.XR.TrackedPoseDriver>();
                tpd.positionInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
                tpd.rotationInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
                tpd.trackingStateInput = new UnityEngine.InputSystem.InputActionProperty(new UnityEngine.InputSystem.InputAction("Tracking State", binding: "<XRHMD>/trackingState", expectedControlType: "Integer"));
            }
#endif
#if ND_XR_COREUTILS
            var origin = NDUtil.GetOrAdd<XROrigin>(gameObject);
            origin.Origin = gameObject; origin.CameraFloorOffsetObject = trackingSpace.gameObject; origin.Camera = cam;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
#endif
        }

        void Awake() { EnsureBuilt(); _yaw = transform.eulerAngles.y; }
        void OnEnable() { Application.onBeforeRender += Track; }
        void OnDisable() { Application.onBeforeRender -= Track; }

        void Start()
        {
            s_Inputs.Clear(); SubsystemManager.GetSubsystems(s_Inputs);
            foreach (var s in s_Inputs) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }

        void Update()
        {
            IsXR = XRSettings.isDeviceActive;
            trackingSpace.localPosition = new Vector3(0, seated ? seatedHeightOffset : 0f, 0);
            if (IsXR) Track(); else Desktop();
        }

        void Track()
        {
            if (!XRSettings.isDeviceActive) return;
            s_Heads.Clear(); InputDevices.GetDevicesAtXRNode(XRNode.CenterEye, s_Heads);
            if (s_Heads.Count == 0) InputDevices.GetDevicesAtXRNode(XRNode.Head, s_Heads);
            if (s_Heads.Count == 0) return;
            var h = s_Heads[0];
            if (h.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyePosition, out Vector3 p) || h.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out p)) cam.transform.localPosition = p;
            if (h.TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyeRotation, out Quaternion q) || h.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out q)) cam.transform.localRotation = q;
        }

        // Desktop: WASD move, arrow keys look (mouse is reserved for the hand simulator), PageUp/Down height.
        void Desktop()
        {
            float dt = Time.deltaTime; Vector3 mv = Vector3.zero;
#if ENABLE_INPUT_SYSTEM && ND_INPUTSYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb[Key.W].isPressed) mv.z += 1; if (kb[Key.S].isPressed) mv.z -= 1; if (kb[Key.A].isPressed) mv.x -= 1; if (kb[Key.D].isPressed) mv.x += 1;
                if (kb[Key.LeftArrow].isPressed) _yaw -= 70 * dt; if (kb[Key.RightArrow].isPressed) _yaw += 70 * dt;
                if (kb[Key.UpArrow].isPressed) _pitch -= 50 * dt; if (kb[Key.DownArrow].isPressed) _pitch += 50 * dt;
                if (kb[Key.PageUp].isPressed) desktopEyeHeight += 0.5f * dt; if (kb[Key.PageDown].isPressed) desktopEyeHeight -= 0.5f * dt;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.middleButton.isPressed) { var d = mouse.delta.ReadValue(); _yaw += d.x * 0.15f; _pitch -= d.y * 0.15f; }
#endif
            _pitch = Mathf.Clamp(_pitch, -70f, 80f);
            var rot = Quaternion.Euler(0, _yaw, 0);
            transform.position += rot * mv.normalized * 1.2f * dt;
            cam.transform.localPosition = new Vector3(0, desktopEyeHeight, 0);
            cam.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0);
        }

        /// <summary>Move the rig so the user stands at 'floorPos' facing 'lookAt'.</summary>
        public void PlaceUser(Vector3 floorPos, Vector3 lookAt)
        {
            Vector3 headLocal = IsXR ? cam.transform.localPosition : Vector3.zero; headLocal.y = 0;
            Vector3 dir = lookAt - floorPos; dir.y = 0; if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            float targetYaw = Quaternion.LookRotation(dir).eulerAngles.y;
            float headYaw = IsXR ? cam.transform.localEulerAngles.y : 0f;
            transform.rotation = Quaternion.Euler(0, targetYaw - headYaw, 0);
            transform.position = floorPos - transform.rotation * headLocal;
            _yaw = targetYaw; _pitch = 22f;
        }
    }
}
