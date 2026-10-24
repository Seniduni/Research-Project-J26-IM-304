using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace IVSim
{
    public enum RigMode { Auto, ForceDesktop, ForceVR }

    /// <summary>
    /// Picks the VR rig when a headset is running, otherwise the desktop (mouse/keyboard) rig.
    /// </summary>
    public class RigBootstrap : MonoBehaviour
    {
        public RigMode mode = RigMode.Auto;
        public GameObject desktopRig;
        public GameObject xrRig;
        [Tooltip("Seconds to wait for the headset to start before falling back to desktop")] public float waitForXR = 1.5f;

        public static bool IsVR { get; private set; }

        IEnumerator Start()
        {
            ApplyCullingMasks();
            if (mode == RigMode.ForceDesktop) { Use(false); yield break; }
            if (mode == RigMode.ForceVR) { Use(true); yield break; }

            Use(false);
            float t = 0f;
            while (t < waitForXR)
            {
                if (XRRunning()) { Use(true); yield break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        public static bool XRRunning()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays) if (d.running) return true;
            return false;
        }

        void Use(bool vr)
        {
            IsVR = vr;
            if (desktopRig != null) desktopRig.SetActive(!vr);
            if (xrRig != null) xrRig.SetActive(vr);
            if (IVSession.Instance != null) IVSession.Instance.ControlMode = vr ? "VR" : "Desktop";
        }

        void ApplyCullingMasks()
        {
            int inset = LayerMask.NameToLayer("IVInset");
            if (inset < 0) return;
            foreach (var root in new[] { desktopRig, xrRig })
            {
                if (root == null) continue;
                foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                    cam.cullingMask &= ~(1 << inset);
            }
        }
    }
}
