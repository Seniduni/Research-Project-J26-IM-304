using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace IVSim
{
    /// <summary>
    /// Minimal XR rig: drives the head camera from the HMD pose, asks for floor-level tracking,
    /// and can recentre the learner at the bedside (B/Y button).
    /// Hierarchy: XRRig (this) -> Head (Camera), LeftHand (VRHand), RightHand (VRHand)
    /// </summary>
    public class XRRigController : MonoBehaviour
    {
        public Camera head;
        [Tooltip("Where the learner should stand, facing the arm")] public Transform standPoint;
        [Tooltip("Height added if the headset reports device-relative (not floor) tracking")] public float deviceModeHeight = 1.6f;

        InputDevice hmd;
        Vector3 cameraOffset;
        bool recenteredOnce;
        float startTime;

        void OnEnable()
        {
            startTime = Time.time;
            recenteredOnce = false;
            TrySetFloorOrigin();
            Application.onBeforeRender += OnBeforeRender;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= OnBeforeRender;
        }

        void TrySetFloorOrigin()
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            foreach (var s in subsystems)
            {
                if ((s.GetSupportedTrackingOriginModes() & TrackingOriginModeFlags.Floor) != 0)
                    s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
            }
        }

        bool ReadHead(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.zero; rot = Quaternion.identity;
            if (!hmd.isValid) hmd = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (!hmd.isValid) return false;
            if (hmd.TryGetFeatureValue(CommonUsages.centerEyePosition, out pos) && hmd.TryGetFeatureValue(CommonUsages.centerEyeRotation, out rot)) return true;
            return hmd.TryGetFeatureValue(CommonUsages.devicePosition, out pos) && hmd.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
        }

        void ApplyHead()
        {
            if (head == null) return;
            Vector3 p; Quaternion r;
            if (ReadHead(out p, out r))
            {
                head.transform.localPosition = p + cameraOffset;
                head.transform.localRotation = r;
            }
        }

        void Update()
        {
            ApplyHead();
            // After tracking settles: detect device-relative tracking and recentre once.
            if (!recenteredOnce && Time.time - startTime > 1.0f)
            {
                Vector3 p; Quaternion r;
                if (ReadHead(out p, out r))
                {
                    if (p.y < 0.5f) cameraOffset = new Vector3(0f, deviceModeHeight, 0f);
                    Recenter();
                    recenteredOnce = true;
                }
            }
        }

        void OnBeforeRender() { ApplyHead(); }

        /// <summary>Moves/rotates the rig so the learner's head is above the stand point, facing the arm.</summary>
        public void Recenter()
        {
            if (head == null || standPoint == null) return;
            ApplyHead();
            Vector3 headFwd = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
            Vector3 targetFwd = Vector3.ProjectOnPlane(standPoint.forward, Vector3.up);
            if (headFwd.sqrMagnitude > 1e-4f && targetFwd.sqrMagnitude > 1e-4f)
            {
                float yaw = Vector3.SignedAngle(headFwd, targetFwd, Vector3.up);
                transform.RotateAround(head.transform.position, Vector3.up, yaw);
            }
            Vector3 delta = standPoint.position - head.transform.position;
            delta.y = 0f;
            transform.position += delta;
        }
    }
}
