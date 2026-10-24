// Aligns the virtual delivery station to the physical silicone birthing mannequin: the trainee/educator pinches three
// landmarks on the mannequin (introitus centre, left ASIS, right ASIS); a rigid (yaw-only by default) fit is computed and
// persisted (spatial anchor when AR Foundation is available, otherwise PlayerPrefs relative to the tracking space).
using System;
using System.Collections.Generic;
using UnityEngine;
#if ND_ARFOUNDATION
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#endif

namespace NeonatalDelivery
{
    public class MannequinCalibration : MonoBehaviour
    {
        public Transform station;
        public Transform trackingSpace;
        public Transform virtualIntroitus, virtualAsisL, virtualAsisR;   // mother markers
        public HandTrackingSource hands;
        public HandSide pinchHand = HandSide.Right;
        public bool yawOnly = true;
        public bool Active { get; private set; }
        public float LastRmsErrorMm { get; private set; } = -1;
        public Action<string> OnInstruction;
        public Action<bool> OnFinished;
        readonly List<Vector3> _picked = new List<Vector3>();
        bool _prevPinch; GameObject[] _markers = new GameObject[3];
        const string PrefKey = "ND_StationPose_v1", AnchorKey = "ND_StationAnchor_v1";
        static readonly string[] Names = { "the centre of the mannequin's introitus", "the LEFT anterior superior iliac spine (front hip bone)", "the RIGHT anterior superior iliac spine" };

        public void Begin()
        {
            _picked.Clear(); Active = true;
            foreach (var m in _markers) if (m != null) Destroy(m);
            OnInstruction?.Invoke($"Calibration 1/3: pinch your thumb and index finger at {Names[0]}.");
        }

        void Update()
        {
            if (!Active || hands == null) return;
            var h = hands.Get(pinchHand); if (!h.tracked) h = hands.Get(pinchHand == HandSide.Right ? HandSide.Left : HandSide.Right);
            bool p = h.tracked && h.pinch > 0.9f;
            bool key = false;
#if ENABLE_INPUT_SYSTEM && ND_INPUTSYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current; key = kb != null && kb[UnityEngine.InputSystem.Key.C].wasPressedThisFrame;
#endif
            if ((p && !_prevPinch) || key)
            {
                var pt = h.tracked ? h.PinchPoint : Camera.main.transform.position + Camera.main.transform.forward * 0.6f;
                var mk = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(mk.GetComponent<Collider>()); mk.transform.position = pt; mk.transform.localScale = Vector3.one * 0.015f;
                _markers[_picked.Count] = mk; _picked.Add(pt);
                NDAudio.Instance?.UI("ui_click", 0.8f);
                if (_picked.Count < 3) OnInstruction?.Invoke($"Calibration {_picked.Count + 1}/3: pinch at {Names[_picked.Count]}.");
                else Solve();
            }
            _prevPinch = p;
        }

        void Solve()
        {
            Active = false;
            var a = new List<Vector3> { virtualIntroitus.position, virtualAsisL.position, virtualAsisR.position };
            if (!RigUtil.RigidAlign(a, _picked, out Quaternion R, out Vector3 t)) { OnInstruction?.Invoke("Calibration failed - try again."); OnFinished?.Invoke(false); return; }
            if (yawOnly)
            {
                Vector3 f = R * Vector3.forward; f.y = 0; R = Quaternion.LookRotation(f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward, Vector3.up);
                Vector3 ca = (a[0] + a[1] + a[2]) / 3f, cb = (_picked[0] + _picked[1] + _picked[2]) / 3f;
                t = cb - R * ca;
            }
            station.SetPositionAndRotation(R * station.position + t, R * station.rotation);
            float err = 0; var a2 = new[] { virtualIntroitus.position, virtualAsisL.position, virtualAsisR.position };
            for (int i = 0; i < 3; i++) err += (a2[i] - _picked[i]).sqrMagnitude;
            LastRmsErrorMm = Mathf.Sqrt(err / 3f) * 1000f;
            Save();
            OnInstruction?.Invoke(LastRmsErrorMm < 25f ? $"Calibrated (RMS error {LastRmsErrorMm:F0} mm)." : $"Calibrated with a large error ({LastRmsErrorMm:F0} mm) - check the mannequin landmarks and repeat.");
            OnFinished?.Invoke(true);
        }

        [Serializable] class PoseData { public Vector3 p; public Quaternion r; }

        public void Save()
        {
            var ts = trackingSpace != null ? trackingSpace : null;
            var d = new PoseData { p = ts ? ts.InverseTransformPoint(station.position) : station.position, r = ts ? Quaternion.Inverse(ts.rotation) * station.rotation : station.rotation };
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(d)); PlayerPrefs.Save();
#if ND_ARFOUNDATION
            SaveAnchor();
#endif
        }

        public bool Load()
        {
            if (!PlayerPrefs.HasKey(PrefKey)) return false;
            var d = JsonUtility.FromJson<PoseData>(PlayerPrefs.GetString(PrefKey));
            var ts = trackingSpace;
            station.SetPositionAndRotation(ts ? ts.TransformPoint(d.p) : d.p, ts ? ts.rotation * d.r : d.r);
#if ND_ARFOUNDATION
            LoadAnchor();
#endif
            return true;
        }

#if ND_ARFOUNDATION
        async void SaveAnchor()
        {
            try
            {
                var mgr = AnchorManager(); if (mgr == null) return;
                var add = await mgr.TryAddAnchorAsync(new Pose(station.position, station.rotation));
                if (!add.status.IsSuccess() || add.value == null) return;
                var saved = await mgr.TrySaveAnchorAsync(add.value);
                if (saved.status.IsSuccess()) { PlayerPrefs.SetString(AnchorKey, saved.value.guid.ToString()); PlayerPrefs.Save(); ND.Log("Station anchor saved"); }
            }
            catch (Exception e) { ND.Warn("Anchor save failed: " + e.Message); }
        }

        // The anchor manager is created only on a headset with a running anchor provider (Quest + Unity OpenXR: Meta);
        // in the Editor / desktop simulator there is none, so nothing is added and no AR warning is logged.
        static ARAnchorManager AnchorManager()
        {
            var mgr = FindAnyObjectByType<ARAnchorManager>(); if (mgr != null) return mgr;
            if (!UnityEngine.XR.XRSettings.isDeviceActive) return null;
            var subs = new System.Collections.Generic.List<XRAnchorSubsystem>(); SubsystemManager.GetSubsystems(subs);
            if (subs.Count == 0) return null;
#if ND_XR_COREUTILS
            var origin = FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            return origin != null ? origin.gameObject.AddComponent<ARAnchorManager>() : null;
#else
            return null;
#endif
        }

        async void LoadAnchor()
        {
            try
            {
                if (!PlayerPrefs.HasKey(AnchorKey)) return; var mgr = AnchorManager(); if (mgr == null) return;
                var guid = new SerializableGuid(new Guid(PlayerPrefs.GetString(AnchorKey)));
                var res = await mgr.TryLoadAnchorAsync(guid);
                if (res.status.IsSuccess() && res.value != null) { station.SetPositionAndRotation(res.value.transform.position, res.value.transform.rotation); ND.Log("Station restored from spatial anchor"); }
            }
            catch (Exception e) { ND.Warn("Anchor load failed: " + e.Message); }
        }
#endif
    }
}
