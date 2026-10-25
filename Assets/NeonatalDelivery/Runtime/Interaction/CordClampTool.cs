// CordClampTool: split from DeliveryTools.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using UnityEngine;

namespace NeonatalDelivery
{
    public class CordClampTool : MonoBehaviour
    {
        public UmbilicalCord cord;
        public Transform movingArm;           // ND_CordClamp_ArmB (pivot at hinge)
        public float openAngle = 35f;
        public bool Attached { get; private set; }
        public float DistanceFromNavelCm { get; private set; } = -1;
        public event Action<CordClampTool> Clamped;
        [Tooltip("v5: a held clamp that rests on the cord for this long closes on it automatically (s).")] public float autoAttachTime = 0.35f;
        Grabbable _g; float _close, _dwell; Quaternion _armRest;

        void Awake()
        {
            _g = GetComponent<Grabbable>();
            if (movingArm == null) movingArm = RigUtil.FindDeep(transform, "ND_CordClamp_ArmB");
            if (movingArm != null) _armRest = movingArm.localRotation;
            if (_g != null) _g.Released += OnReleased;
        }

        void OnReleased(Grabbable g, TrackedHand h)
        {
            if (Attached || cord == null) return;
            float s = cord.AttachClamp(transform, transform.position, 0.035f);
            if (s < 0) return;
            Attached = true; _g.locked = true; DistanceFromNavelCm = s * 100f;
            transform.SetParent(null, true);
            NDAudio.Play("clamp_click", transform.position, 0.9f);
            Clamped?.Invoke(this);
        }

        void Update()
        {
            // auto-attach: hold the open clamp on the cord for a moment -> it snaps shut there (no need to let go)
            if (!Attached && cord != null && _g != null && _g.IsHeld)
            {
                _dwell = cord.DistanceTo(transform.position) < 0.03f ? _dwell + Time.deltaTime : 0f;
                if (_dwell >= autoAttachTime) { _dwell = 0f; _g.EndHold(); }   // EndHold -> Released -> OnReleased attaches it
            }
            else _dwell = 0f;
            float target = Attached ? 1f : (_g != null && _g.IsHeld ? _g.heldBy.pinch * 0.3f : 0f);
            _close = Mathf.MoveTowards(_close, target, Time.deltaTime * 6f);
            if (movingArm != null) movingArm.localRotation = _armRest * Quaternion.AngleAxis(openAngle * _close, Vector3.up);
        }

        public void ResetTool() { _dwell = 0f; Attached = false; DistanceFromNavelCm = -1; _close = 0; if (_g != null) _g.ReturnToRest(); }
    }
}
