// Per-hand tracked state in WORLD space (26 OpenXR joints), derived features (pinch, grip curl, palm frame).
using UnityEngine;

namespace NeonatalDelivery
{
    /// <summary>Joint indices identical to UnityEngine.XR.Hands.XRHandJointID.ToIndex() (OpenXR order).</summary>
    public static class J
    {
        public const int Wrist = 0, Palm = 1;
        public const int ThumbMeta = 2, ThumbProx = 3, ThumbDist = 4, ThumbTip = 5;
        public const int IndexMeta = 6, IndexProx = 7, IndexInter = 8, IndexDist = 9, IndexTip = 10;
        public const int MiddleMeta = 11, MiddleProx = 12, MiddleInter = 13, MiddleDist = 14, MiddleTip = 15;
        public const int RingMeta = 16, RingProx = 17, RingInter = 18, RingDist = 19, RingTip = 20;
        public const int LittleMeta = 21, LittleProx = 22, LittleInter = 23, LittleDist = 24, LittleTip = 25;
        public const int Count = 26;
        public static readonly int[] Tips = { ThumbTip, IndexTip, MiddleTip, RingTip, LittleTip };
        /// <summary>Parent joint for each joint (for drawing bones), -1 for root.</summary>
        public static readonly int[] Parent = { -1, 0, 0, 2, 3, 4, 0, 6, 7, 8, 9, 0, 11, 12, 13, 14, 0, 16, 17, 18, 19, 0, 21, 22, 23, 24 };
    }

    public enum HandSourceKind { None, HandTracking, Controller, DesktopSimulator, Replay }

    public class TrackedHand
    {
        public readonly HandSide side;
        public HandSourceKind source;
        public bool tracked;
        public readonly Vector3[] pos = new Vector3[J.Count];
        public readonly Quaternion[] rot = new Quaternion[J.Count];
        public readonly float[] radius = new float[J.Count];
        public float pinch;          // 0..1 thumb-index pinch
        public float grip;           // 0..1 mean finger curl
        public float pinchGripForce; // controller analog value if available
        public Vector3 palmPos, palmNormal, palmForward, velocity;
        public Quaternion palmRot = Quaternion.identity;
        Vector3 _prevPalm; float _prevT = -1;

        public TrackedHand(HandSide s)
        {
            side = s;
            for (int i = 0; i < J.Count; i++) { rot[i] = Quaternion.identity; radius[i] = 0.009f; }
        }

        public Vector3 IndexTip => pos[J.IndexTip];
        public Vector3 ThumbTip => pos[J.ThumbTip];
        public Vector3 PinchPoint => (pos[J.IndexTip] + pos[J.ThumbTip]) * 0.5f;

        /// <summary>Recompute derived features after joints were written. 'up' of a joint pose = dorsal (back of hand), forward = toward fingertip.</summary>
        public void UpdateDerived(float time)
        {
            if (!tracked) { pinch = 0; grip = 0; velocity = Vector3.zero; _prevT = -1; return; }
            float d = Vector3.Distance(pos[J.ThumbTip], pos[J.IndexTip]);
            pinch = 1f - Mathf.Clamp01((d - 0.012f) / 0.045f);
            float c = 0f;
            c += Curl(J.IndexProx, J.IndexInter, J.IndexTip);
            c += Curl(J.MiddleProx, J.MiddleInter, J.MiddleTip);
            c += Curl(J.RingProx, J.RingInter, J.RingTip);
            c += Curl(J.LittleProx, J.LittleInter, J.LittleTip);
            grip = Mathf.Clamp01(c / 4f);
            palmPos = pos[J.Palm];
            // palm frame from geometry (robust to runtime axis conventions)
            Vector3 across = pos[J.IndexProx] - pos[J.LittleProx];
            Vector3 fwd = (pos[J.MiddleProx] - pos[J.Wrist]);
            if (fwd.sqrMagnitude < 1e-8f) fwd = rot[J.Palm] * Vector3.forward;
            fwd.Normalize();
            Vector3 n = Vector3.Cross(fwd, across).normalized;   // left-handed Unity: right hand -> palm side, flip for left
            if (side == HandSide.Left) n = -n;
            Vector3 dorsal = rot[J.Palm] * Vector3.up;
            if (Vector3.Dot(n, dorsal) > 0.2f) n = -n;           // palm normal must oppose the dorsal axis
            palmNormal = n; palmForward = fwd;
            palmRot = Quaternion.LookRotation(fwd, -n);
            if (_prevT > 0 && time > _prevT) velocity = Vector3.Lerp(velocity, (palmPos - _prevPalm) / (time - _prevT), 0.5f);
            _prevPalm = palmPos; _prevT = time;
        }

        /// <summary>Keep the motion history on the real (tracked) palm when a pose is overridden (auto-fit), so velocity stays continuous.</summary>
        public void OverrideMotion(Vector3 prevPalm, Vector3 vel) { _prevPalm = prevPalm; velocity = vel; }

        float Curl(int a, int b, int tip)
        {
            Vector3 v1 = pos[b] - pos[a], v2 = pos[tip] - pos[b];
            if (v1.sqrMagnitude < 1e-8f || v2.sqrMagnitude < 1e-8f) return 0;
            return Mathf.Clamp01(Vector3.Angle(v1, v2) / 140f);
        }

        public void CopyFrom(TrackedHand o)
        {
            source = o.source; tracked = o.tracked;
            for (int i = 0; i < J.Count; i++) { pos[i] = o.pos[i]; rot[i] = o.rot[i]; radius[i] = o.radius[i]; }
            pinch = o.pinch; grip = o.grip; palmPos = o.palmPos; palmNormal = o.palmNormal; palmForward = o.palmForward; palmRot = o.palmRot; velocity = o.velocity;
        }
    }
}
