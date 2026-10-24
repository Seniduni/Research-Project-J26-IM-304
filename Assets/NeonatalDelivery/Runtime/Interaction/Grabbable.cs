// Hand-tracked grabbing of instruments (pinch for clamps/scissors, grip for the towel).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public enum GrabStyle { Pinch, Grip, Either }

    public class Grabbable : MonoBehaviour
    {
        public static readonly List<Grabbable> All = new List<Grabbable>();
        public GrabStyle style = GrabStyle.Pinch;
        public float grabRadius = 0.05f;
        public Transform grabPoint;                 // where the fingers hold the tool (defaults to transform)
        public bool locked;                         // e.g. clamp already attached to the cord
        public Vector3 restPosition; public Quaternion restRotation; public Transform restParent;
        public bool IsHeld => heldBy != null;
        public TrackedHand heldBy;
        public event Action<Grabbable, TrackedHand> Grabbed, Released;
        Vector3 _offPos; Quaternion _offRot;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void Start() { SaveRest(); }
        public void SaveRest() { restParent = transform.parent; restPosition = transform.localPosition; restRotation = transform.localRotation; }
        public void ReturnToRest()
        {
            heldBy = null; locked = false;
            transform.SetParent(restParent, false); transform.localPosition = restPosition; transform.localRotation = restRotation;
        }

        public Vector3 GrabPointWorld => grabPoint != null ? grabPoint.position : transform.position;

        public static void HandFrame(TrackedHand h, GrabStyle s, out Vector3 pos, out Quaternion rot)
        {
            rot = h.palmRot;
            pos = s == GrabStyle.Grip ? h.palmPos + h.palmNormal * 0.03f : h.PinchPoint;
        }

        public bool WantsGrab(TrackedHand h)
        {
            if (style == GrabStyle.Pinch) return h.pinch > 0.82f;
            if (style == GrabStyle.Grip) return h.grip > 0.62f;
            return h.pinch > 0.82f || h.grip > 0.62f;
        }
        public bool WantsRelease(TrackedHand h)
        {
            if (style == GrabStyle.Pinch) return h.pinch < 0.45f;
            if (style == GrabStyle.Grip) return h.grip < 0.35f;
            return h.pinch < 0.45f && h.grip < 0.35f;
        }

        public void BeginHold(TrackedHand h)
        {
            heldBy = h; transform.SetParent(null, true);
            HandFrame(h, style, out var p, out var r);
            _offRot = Quaternion.Inverse(r) * transform.rotation;
            _offPos = Quaternion.Inverse(r) * (transform.position - p);
            Grabbed?.Invoke(this, h);
        }

        public void UpdateHold()
        {
            if (heldBy == null) return;
            HandFrame(heldBy, style, out var p, out var r);
            transform.rotation = r * _offRot;
            transform.position = p + r * _offPos;
        }

        public void EndHold()
        {
            var h = heldBy; heldBy = null;
            Released?.Invoke(this, h);
        }
    }

}
