// MR Operating Theatre - shared player rig (VR via OpenXR + desktop fallback)
// VR : head tracking, two controller lasers, right-stick teleport (push forward, release),
//      right-stick left/right snap-turn, left-stick smooth walk (optional).
// Desktop: WASD / arrows to walk, hold RIGHT mouse to look, LEFT click to select,
//      F = foot tap, T = ask (push-to-talk), Esc/M = menu.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace MROT
{
    [DefaultExecutionOrder(-50)]
    public class PlayerRig : MonoBehaviour
    {
        public static PlayerRig Instance { get; private set; }

        [Header("Body")]
        public float eyeHeight = 1.62f;
        public float walkSpeed = 1.6f;
        public float runSpeed = 2.8f;
        public float lookSensitivity = 2.2f;
        public bool allowSmoothLocomotionInVR = true;
        public float snapTurnDegrees = 30f;

        public Camera Cam { get; private set; }
        public Transform Head { get { return Cam.transform; } }
        public Camera UICam { get; private set; }
        public Transform CameraOffset { get; private set; }
        public Transform LeftHand { get; private set; }
        public Transform RightHand { get; private set; }
        public Pointer MousePointer { get; private set; }
        public Pointer LeftPointer { get; private set; }
        public Pointer RightPointer { get; private set; }
        public bool IsVR { get; private set; }
        public bool LocomotionLocked { get; set; }

        CharacterController _cc;
        float _yaw, _pitch;
        bool _snapReady = true;
        bool _teleportAiming;
        LineRenderer _arc;
        Transform _teleportRing;
        Vector3 _teleportTarget; bool _teleportValid;
        float _vy;

        /// <summary>Virtual foot at floor level just in front of the user (used by the hands-free door sensor).</summary>
        public Vector3 FootPosition
        {
            get
            {
                var f = Head.forward; f.y = 0;
                if (f.sqrMagnitude < 0.001f) f = transform.forward;
                f.Normalize();
                var p = Head.position + f * 0.28f;
                p.y = transform.position.y + 0.08f;
                return p;
            }
        }
        public Vector3 FloorPosition { get { var p = Head.position; p.y = transform.position.y; return p; } }

        public static PlayerRig Create(Vector3 position, float yawDegrees)
        {
            var go = new GameObject("PlayerRig");
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yawDegrees, 0));
            return go.AddComponent<PlayerRig>();
        }

        void Awake()
        {
            Instance = this;
            _yaw = transform.eulerAngles.y;
            transform.rotation = Quaternion.identity;
            CameraOffset = new GameObject("CameraOffset").transform;
            CameraOffset.SetParent(transform, false);
            CameraOffset.localRotation = Quaternion.Euler(0, _yaw, 0);
            var existing = Camera.main;
            if (existing != null && existing.transform.parent == null) Destroy(existing.gameObject);
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(CameraOffset, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.nearClipPlane = 0.03f; Cam.farClipPlane = 80f;
            Cam.fieldOfView = 70f;
            var urp = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<AudioListener>();
            // UI overlay camera: world-space panels are drawn after post-processing so text stays
            // crisp and true-coloured (no bloom halo, no ACES tone-mapping) - on desktop and in VR.
            Cam.cullingMask &= ~(1 << UIKit.UILayer);
            var uiGo = new GameObject("UI Camera");
            uiGo.transform.SetParent(camGo.transform, false);
            UICam = uiGo.AddComponent<Camera>();
            UICam.cullingMask = 1 << UIKit.UILayer;
            UICam.clearFlags = CameraClearFlags.Depth;
            UICam.nearClipPlane = Cam.nearClipPlane; UICam.farClipPlane = Cam.farClipPlane; UICam.fieldOfView = Cam.fieldOfView;
            var uiData = uiGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            uiData.renderType = UnityEngine.Rendering.Universal.CameraRenderType.Overlay;
            uiData.renderPostProcessing = false;
            uiData.renderShadows = false;
            urp.cameraStack.Add(UICam);
            Cam.transform.localPosition = new Vector3(0, eyeHeight, 0);   // desktop default until XR is detected
            _cc = gameObject.AddComponent<CharacterController>();
            _cc.radius = 0.22f; _cc.height = 1.7f; _cc.center = new Vector3(0, 0.85f, 0); _cc.stepOffset = 0.25f; _cc.slopeLimit = 30;
            _cc.skinWidth = 0.02f;

            LeftHand = MakeHand("LeftHand", false);
            RightHand = MakeHand("RightHand", true);
            MousePointer = camGo.AddComponent<Pointer>();
            MousePointer.source = PointerSource.Mouse; MousePointer.cam = Cam;
            LeftPointer = LeftHand.gameObject.AddComponent<Pointer>(); LeftPointer.source = PointerSource.LeftHand;
            RightPointer = RightHand.gameObject.AddComponent<Pointer>(); RightPointer.source = PointerSource.RightHand;
            BuildTeleportVisuals();
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDestroy() { Application.onBeforeRender -= OnBeforeRender; if (Instance == this) Instance = null; }

        Transform MakeHand(string n, bool right)
        {
            var h = new GameObject(n).transform;
            h.SetParent(CameraOffset, false);
            // simple controller model
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(h, false);
            body.transform.localScale = new Vector3(0.035f, 0.055f, 0.035f);
            body.transform.localRotation = Quaternion.Euler(70, 0, 0);
            body.transform.localPosition = new Vector3(0, -0.02f, -0.04f);
            body.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Lit(new Color(0.15f, 0.16f, 0.18f), 0.6f);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(h, false);
            ring.transform.localScale = new Vector3(0.07f, 0.004f, 0.07f);
            ring.transform.localPosition = new Vector3(0, 0.0f, 0.0f);
            ring.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Unlit(right ? new Color(0.2f, 0.8f, 1f) : new Color(0.3f, 1f, 0.6f), false);
            h.gameObject.SetActive(false);
            return h;
        }

        void BuildTeleportVisuals()
        {
            var a = new GameObject("TeleportArc");
            a.transform.SetParent(transform, false);
            _arc = a.AddComponent<LineRenderer>();
            _arc.widthMultiplier = 0.012f; _arc.positionCount = 0; _arc.useWorldSpace = true;
            _arc.material = RuntimeMaterials.Unlit(new Color(0.3f, 0.9f, 1f, 0.8f));
            var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(r.GetComponent<Collider>());
            r.name = "TeleportTarget";
            r.transform.localScale = new Vector3(0.6f, 0.005f, 0.6f);
            r.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Unlit(new Color(0.3f, 0.9f, 1f, 0.5f));
            _teleportRing = r.transform;
            r.SetActive(false);
        }

        void Start()
        {
            RefreshMode();
        }

        void RefreshMode()
        {
            IsVR = InputBridge.XRActive;
            if (IsVR)
            {
                var subs = new List<XRInputSubsystem>();
                SubsystemManager.GetSubsystems(subs);
                foreach (var s in subs) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
                CameraOffset.localPosition = Vector3.zero;
            }
            else
            {
                CameraOffset.localPosition = Vector3.zero;
                Cam.transform.localPosition = new Vector3(0, eyeHeight, 0);
            }
            LeftHand.gameObject.SetActive(IsVR);
            RightHand.gameObject.SetActive(IsVR);
            LeftPointer.enabled = IsVR; RightPointer.enabled = IsVR;
            MousePointer.enabled = !IsVR;
        }

        float _modeCheck;
        void LateUpdate()
        {
            if (UICam != null && !IsVR) UICam.fieldOfView = Cam.fieldOfView;
        }

        void Update()
        {
            _modeCheck -= Time.unscaledDeltaTime;
            if (_modeCheck <= 0) { _modeCheck = 1f; if (InputBridge.XRActive != IsVR) RefreshMode(); }
            if (IsVR) UpdateVR(); else UpdateDesktop();
            // gravity / keep grounded
            if (_cc.enabled)
            {
                _vy = _cc.isGrounded ? -0.5f : _vy - 9.81f * Time.deltaTime;
                _cc.Move(new Vector3(0, _vy * Time.deltaTime, 0));
            }
        }

        void OnBeforeRender() { if (IsVR) TrackDevices(); }

        void TrackDevices()
        {
            var heads = new List<UnityEngine.XR.InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, heads);
            if (heads.Count > 0)
            {
                Vector3 p; Quaternion r;
                if (heads[0].TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyePosition, out p)) Cam.transform.localPosition = p;
                if (heads[0].TryGetFeatureValue(UnityEngine.XR.CommonUsages.centerEyeRotation, out r)) Cam.transform.localRotation = r;
            }
            Vector3 hp; Quaternion hr;
            if (InputBridge.HandPose(false, out hp, out hr)) { LeftHand.localPosition = hp; LeftHand.localRotation = hr; }
            if (InputBridge.HandPose(true, out hp, out hr)) { RightHand.localPosition = hp; RightHand.localRotation = hr; }
        }

        // ------------------------------------------------------------------ desktop
        void UpdateDesktop()
        {
            bool looking = InputBridge.MouseHeld(1);
            Pointer.InputBlocked = false;
            if (looking)
            {
                var d = InputBridge.MouseDelta;
                _yaw += d.x * lookSensitivity;
                _pitch = Mathf.Clamp(_pitch - d.y * lookSensitivity, -80f, 80f);
            }
            if (InputBridge.KeyHeld(KeyCode.LeftArrow)) _yaw -= 90f * Time.deltaTime;
            if (InputBridge.KeyHeld(KeyCode.RightArrow)) _yaw += 90f * Time.deltaTime;
            CameraOffset.localRotation = Quaternion.Euler(0, _yaw, 0);
            Cam.transform.localRotation = Quaternion.Euler(_pitch, 0, 0);
            Cam.transform.localPosition = new Vector3(0, eyeHeight, 0);
            if (LocomotionLocked) return;
            Vector3 mv = Vector3.zero;
            if (InputBridge.KeyHeld(KeyCode.W) || InputBridge.KeyHeld(KeyCode.UpArrow)) mv.z += 1;
            if (InputBridge.KeyHeld(KeyCode.S) || InputBridge.KeyHeld(KeyCode.DownArrow)) mv.z -= 1;
            if (InputBridge.KeyHeld(KeyCode.A)) mv.x -= 1;
            if (InputBridge.KeyHeld(KeyCode.D)) mv.x += 1;
            if (mv.sqrMagnitude > 0)
            {
                float sp = InputBridge.KeyHeld(KeyCode.LeftShift) ? runSpeed : walkSpeed;
                var w = CameraOffset.rotation * mv.normalized * sp * Time.deltaTime;
                _cc.Move(w);
            }
        }

        // ------------------------------------------------------------------ VR
        void UpdateVR()
        {
            TrackDevices();
            var rs = InputBridge.Stick(true);
            var ls = InputBridge.Stick(false);
            // snap turn
            if (Mathf.Abs(rs.x) > 0.75f && Mathf.Abs(rs.y) < 0.5f)
            {
                if (_snapReady) { SnapTurn(Mathf.Sign(rs.x) * snapTurnDegrees); _snapReady = false; }
            }
            else if (Mathf.Abs(rs.x) < 0.3f) _snapReady = true;
            if (LocomotionLocked) { HideTeleport(); return; }
            // teleport aim
            if (rs.y > 0.6f) { _teleportAiming = true; AimTeleport(); }
            else if (_teleportAiming && rs.y < 0.25f)
            {
                _teleportAiming = false;
                if (_teleportValid) TeleportTo(_teleportTarget, null);
                HideTeleport();
            }
            // smooth locomotion (head-relative)
            if (allowSmoothLocomotionInVR && ls.sqrMagnitude > 0.04f)
            {
                var f = Head.forward; f.y = 0; f.Normalize();
                var r = Head.right; r.y = 0; r.Normalize();
                _cc.Move((f * ls.y + r * ls.x) * walkSpeed * 0.8f * Time.deltaTime);
            }
            // keep the character controller under the head
            var local = Cam.transform.localPosition;
            var off = CameraOffset.localPosition + CameraOffset.localRotation * new Vector3(local.x, 0, local.z);
            _cc.center = new Vector3(off.x, 0.85f, off.z);
        }

        void SnapTurn(float deg)
        {
            var headBefore = Head.position;
            CameraOffset.Rotate(0, deg, 0, Space.World);
            var delta = headBefore - Head.position; delta.y = 0;
            CameraOffset.position += delta;
            _yaw = CameraOffset.eulerAngles.y;
        }

        void AimTeleport()
        {
            var o = RightHand.position; var v = RightHand.forward * 7f;
            var pts = new List<Vector3>();
            _teleportValid = false;
            var p = o;
            for (int i = 0; i < 60; i++)
            {
                pts.Add(p);
                var np = p + v * 0.03f; v += Physics.gravity * 0.03f;
                RaycastHit hit;
                if (Physics.Linecast(p, np, out hit, ~0, QueryTriggerInteraction.Ignore))
                {
                    pts.Add(hit.point);
                    _teleportValid = hit.normal.y > 0.8f && IsFloor(hit.collider);
                    _teleportTarget = hit.point;
                    break;
                }
                p = np;
            }
            _arc.positionCount = pts.Count; _arc.SetPositions(pts.ToArray());
            var c = _teleportValid ? new Color(0.3f, 0.9f, 1f, 0.85f) : new Color(1f, 0.3f, 0.3f, 0.6f);
            RuntimeMaterials.SetColor(_arc.material, c);
            _teleportRing.gameObject.SetActive(_teleportValid);
            if (_teleportValid) _teleportRing.position = _teleportTarget + Vector3.up * 0.01f;
        }

        void HideTeleport() { _arc.positionCount = 0; if (_teleportRing) _teleportRing.gameObject.SetActive(false); }

        public static bool IsFloor(Collider c)
        {
            if (c == null) return false;
            var n = c.gameObject.name;
            return n.Contains("Floor") || c.GetComponent<WalkableFloor>() != null;
        }

        /// <summary>Move the user so that their feet (head projected to floor) land at 'floorPos'. Optional facing.</summary>
        public void TeleportTo(Vector3 floorPos, Vector3? lookAt)
        {
            _cc.enabled = false;
            var headFloor = FloorPosition;
            var delta = floorPos - headFloor; delta.y = floorPos.y - transform.position.y;
            transform.position += delta;
            if (lookAt.HasValue)
            {
                var dir = lookAt.Value - Head.position; dir.y = 0;
                if (dir.sqrMagnitude > 0.01f)
                {
                    float target = Quaternion.LookRotation(dir).eulerAngles.y;
                    float current = IsVR ? Head.eulerAngles.y : _yaw;
                    float d = Mathf.DeltaAngle(current, target);
                    if (IsVR) SnapTurn(d); else { _yaw += d; CameraOffset.localRotation = Quaternion.Euler(0, _yaw, 0); }
                }
            }
            _cc.enabled = true;
            UIAudio.Play("teleport", 0.5f);
        }

        /// <summary>Desktop only: turn the view smoothly toward a point (used by the guide).</summary>
        public void FaceTowards(Vector3 worldPoint, float pitchHint = 0f)
        {
            if (IsVR) return;
            var dir = worldPoint - Head.position;
            var flat = dir; flat.y = 0;
            if (flat.sqrMagnitude < 0.001f) return;
            _yaw = Quaternion.LookRotation(flat).eulerAngles.y;
            _pitch = Mathf.Clamp(-Mathf.Atan2(dir.y, flat.magnitude) * Mathf.Rad2Deg, -35, 35);
        }

        /// <summary>Position in front of the user's eyes, used to place UI panels.</summary>
        public Vector3 InFront(float distance, float heightOffset = 0f)
        {
            var f = Head.forward; f.y = 0;
            if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
            f.Normalize();
            return Head.position + f * distance + Vector3.up * heightOffset;
        }
    }

    /// <summary>Marks a collider as walkable / teleportable floor.</summary>
    public class WalkableFloor : MonoBehaviour { }
}
