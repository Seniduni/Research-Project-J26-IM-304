// Hand-placement evaluation engine (FR1/FR2): resolves each hand against the newborn's cranial, cervical and shoulder
// landmark zones, estimates contact force and traction (direction + magnitude) and exposes one EvaluationFrame per update.
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public class HandEval
    {
        public HandSide side;
        public bool tracked;
        public LandmarkZoneId zone;            // dominant zone in contact
        public uint contacts;                  // bitmask of zones in contact
        public float force;                    // estimated contact force (N)
        public float minDistance;              // distance to the nearest baby zone surface (m)
        public bool engaged;                   // gripping/holding the baby
        public Vector3 traction;               // world-space traction force estimate (N)
        public Vector3 palm; public Quaternion palmRot; public float grip, pinch;
        public float fingerOnNeckTime;         // accumulated fingertip contact on the neck (nuchal cord palpation)
        public bool Has(LandmarkZoneId z) => (contacts & (1u << (int)z)) != 0;
        public bool HasAny(params LandmarkZoneId[] zs) { foreach (var z in zs) if (Has(z)) return true; return false; }
    }

    public class EvaluationFrame
    {
        public float time;
        public HandEval left = new HandEval { side = HandSide.Left }, right = new HandEval { side = HandSide.Right };
        public Vector3 tractionWorld;          // sum of both hands (N)
        public Vector3 tractionCanal;          // x = lateral (mother's right), y = outward, z = anterior (pubic)
        public float tractionMagnitude;
        public float angleFromAxial;           // degrees between traction and the fetal spine axis (outward)
        public float lateralComponent;         // N, perpendicular to fetal spine within the lateral plane
        public bool biparietalHold;
        public bool headControl;               // hand on occiput/vertex applying gentle pressure
        public bool perinealSupport;           // hand on the maternal perineum
        public bool bodySupported;             // baby supported by at least one hand under head/neck/back
        public bool fullySupported;            // both hands supporting
        public HandEval Hand(HandSide s) => s == HandSide.Left ? left : right;
        public bool AnyContact => left.contacts != 0 || right.contacts != 0;
    }

    public class HandPlacementEvaluator : MonoBehaviour
    {
        [Header("Model constants (calibrate against expert recordings)")]
        [Tooltip("N per metre of virtual penetration into a zone (no haptics in VR - penetration is the pressure proxy)")]
        public float contactStiffness = 420f;
        [Tooltip("N per metre of hand displacement from the engaged grip anchor (traction spring)")]
        public float tractionStiffness = 260f;
        public float contactTolerance = 0.008f;
        public float engageGrip = 0.32f;
        public float maxForce = 60f;

        public readonly List<LandmarkZone> zones = new List<LandmarkZone>();
        public EvaluationFrame Current { get; private set; } = new EvaluationFrame();
        public Transform babyReference;   // Head marker transform (traction anchors live in its local space)
        public BirthCanalDeformer canal;
        public BabyController baby;

        readonly Vector3[] _anchorLocal = new Vector3[2]; readonly bool[] _engaged = new bool[2];
        static readonly int[] s_Probes = { J.Palm, J.ThumbTip, J.IndexTip, J.MiddleTip, J.RingTip, J.LittleTip, J.IndexInter, J.MiddleInter };

        public void Register(LandmarkZone z) { if (z != null && !zones.Contains(z)) zones.Add(z); }
        public void ClearZones() { zones.Clear(); }

        public EvaluationFrame Evaluate(TrackedHand left, TrackedHand right, float time)
        {
            var f = Current; f.time = time;
            EvalHand(left, f.left, 0); EvalHand(right, f.right, 1);
            f.tractionWorld = f.left.traction + f.right.traction;
            f.tractionMagnitude = f.tractionWorld.magnitude;
            // canal frame
            if (canal != null && canal.Ready)
            {
                f.tractionCanal = new Vector3(Vector3.Dot(f.tractionWorld, canal.LateralAxis), Vector3.Dot(f.tractionWorld, canal.OutwardAxis), Vector3.Dot(f.tractionWorld, canal.AnteriorAxis));
            }
            Vector3 axial = baby != null ? baby.SpineAxisOut : (canal != null ? canal.OutwardAxis : Vector3.forward);
            f.angleFromAxial = f.tractionMagnitude > 0.5f ? Vector3.Angle(f.tractionWorld, axial) : 0f;
            Vector3 lat = canal != null ? canal.LateralAxis : Vector3.right;
            f.lateralComponent = Mathf.Abs(Vector3.Dot(f.tractionWorld, lat));
            f.biparietalHold = (f.left.Has(LandmarkZoneId.ParietalLeft) && f.right.Has(LandmarkZoneId.ParietalRight)) ||
                               (f.left.Has(LandmarkZoneId.ParietalRight) && f.right.Has(LandmarkZoneId.ParietalLeft));
            f.headControl = HeadControl(f.left) || HeadControl(f.right);
            f.perinealSupport = f.left.Has(LandmarkZoneId.MaternalPerineum) || f.right.Has(LandmarkZoneId.MaternalPerineum);
            bool ls = Supports(f.left), rs = Supports(f.right);
            f.bodySupported = ls || rs; f.fullySupported = ls && rs;
            return f;
        }

        static bool HeadControl(HandEval h) => h.HasAny(LandmarkZoneId.Occiput, LandmarkZoneId.Vertex) && h.force >= 0.5f && h.force <= 25f;
        static bool Supports(HandEval h) => h.HasAny(LandmarkZoneId.Neck, LandmarkZoneId.Occiput, LandmarkZoneId.Back, LandmarkZoneId.Buttocks, LandmarkZoneId.Legs, LandmarkZoneId.ShoulderPosterior, LandmarkZoneId.ShoulderAnterior, LandmarkZoneId.ParietalLeft, LandmarkZoneId.ParietalRight, LandmarkZoneId.Chest);

        void EvalHand(TrackedHand th, HandEval e, int idx)
        {
            e.tracked = th != null && th.tracked;
            e.contacts = 0; e.zone = LandmarkZoneId.None; e.force = 0; e.traction = Vector3.zero; e.minDistance = 9f;
            if (!e.tracked) { _engaged[idx] = false; e.engaged = false; return; }
            e.palm = th.palmPos; e.palmRot = th.palmRot; e.grip = th.grip; e.pinch = th.pinch;
            float best = 9f; bool babyContact = false;
            foreach (var z in zones)
            {
                if (z == null || !z.isActiveAndEnabled) continue;
                int hits = 0; float pen = 0f; float zmin = 9f;
                for (int k = 0; k < s_Probes.Length; k++)
                {
                    int j = s_Probes[k];
                    float pr = j == J.Palm ? 0.022f : Mathf.Max(0.007f, th.radius[j]);
                    float sd = z.SignedDistance(th.pos[j]) - pr;
                    if (z.isBaby && sd < e.minDistance) e.minDistance = sd;
                    zmin = Mathf.Min(zmin, sd);
                    if (sd < contactTolerance) { hits++; pen += Mathf.Max(0f, -sd); }
                }
                if (hits > 0)
                {
                    e.contacts |= 1u << (int)z.id;
                    e.force += contactStiffness * pen;
                    if (z.isBaby) babyContact = true;
                    if (zmin < best) { best = zmin; e.zone = z.id; }
                    if (z.id == LandmarkZoneId.Neck)
                    {
                        float tipD = Mathf.Min(z.SignedDistance(th.pos[J.IndexTip]), z.SignedDistance(th.pos[J.MiddleTip]));
                        if (tipD < 0.012f) e.fingerOnNeckTime += Time.deltaTime;
                    }
                }
            }
            e.force = Mathf.Min(e.force, maxForce);
            // engagement + traction spring
            bool canEngage = babyContact && (th.grip >= engageGrip || th.pinch > 0.75f || e.HasAny(LandmarkZoneId.ParietalLeft, LandmarkZoneId.ParietalRight));
            if (canEngage && !_engaged[idx] && babyReference != null) { _engaged[idx] = true; _anchorLocal[idx] = babyReference.InverseTransformPoint(th.palmPos); }
            else if (_engaged[idx] && (!babyContact || babyReference == null) && e.minDistance > 0.03f) _engaged[idx] = false;
            e.engaged = _engaged[idx];
            if (e.engaged && babyReference != null)
            {
                Vector3 anchor = babyReference.TransformPoint(_anchorLocal[idx]);
                Vector3 d = th.palmPos - anchor;
                e.traction = Vector3.ClampMagnitude(d * tractionStiffness, maxForce);
                // keep the anchor slowly following the hand once the baby has moved toward it (grip slippage)
                _anchorLocal[idx] = Vector3.Lerp(_anchorLocal[idx], babyReference.InverseTransformPoint(th.palmPos), Time.deltaTime * 0.6f);
            }
        }

        public void ResetEngagement() { _engaged[0] = _engaged[1] = false; Current.left.fingerOnNeckTime = Current.right.fingerOnNeckTime = 0; }
    }
}
