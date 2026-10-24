// GrabInteractor: split from Grabbable.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    /// <summary>Runs grabbing for both hands each frame.</summary>
    public class GrabInteractor : MonoBehaviour
    {
        public HandTrackingSource hands;
        readonly bool[] _wasWanting = new bool[2];

        void Update()
        {
            if (hands == null) hands = HandTrackingSource.Instance;
            if (hands == null) return;
            Run(hands.Left, 0); Run(hands.Right, 1);
            foreach (var g in Grabbable.All) if (g.IsHeld) g.UpdateHold();
        }

        void Run(TrackedHand h, int idx)
        {
            Grabbable held = null;
            foreach (var g in Grabbable.All) if (g.heldBy == h) { held = g; break; }
            if (!h.tracked) { if (held != null) held.EndHold(); return; }
            if (held != null) { if (held.WantsRelease(h)) held.EndHold(); return; }
            // new grab only on the rising edge of the gesture (prevents grabbing while already closed)
            bool wantPinch = h.pinch > 0.82f, wantGrip = h.grip > 0.62f;
            bool rising = (wantPinch || wantGrip) && !_wasWanting[idx];
            _wasWanting[idx] = wantPinch || wantGrip;
            if (!rising) return;
            Grabbable best = null; float bd = 9f;
            foreach (var g in Grabbable.All)
            {
                if (g.locked || g.IsHeld || !g.WantsGrab(h)) continue;
                Grabbable.HandFrame(h, g.style, out var p, out _);
                float d = Vector3.Distance(p, g.GrabPointWorld);
                if (d < g.grabRadius && d < bd) { bd = d; best = g; }
            }
            if (best != null) best.BeginHold(h);
        }
    }
}
