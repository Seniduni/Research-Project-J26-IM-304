// Generates a plausible 26-joint hand from a palm pose + curl/pinch values.
// Used by the controller fallback, the desktop simulator and the guided-mode ghost hands.
using UnityEngine;

namespace NeonatalDelivery
{
    public static class ProceduralHand
    {
        // finger base offsets in palm space (x: across toward thumb for the right hand, y: dorsal, z: toward fingers), metres
        static readonly Vector3[] Base = { new Vector3(0.025f, -0.010f, -0.035f), new Vector3(0.022f, 0, 0.035f), new Vector3(0.002f, 0, 0.038f), new Vector3(-0.017f, 0, 0.034f), new Vector3(-0.033f, -0.002f, 0.028f) };
        static readonly float[][] Seg = { new[] { 0.040f, 0.032f, 0.028f }, new[] { 0.040f, 0.024f, 0.020f }, new[] { 0.044f, 0.028f, 0.021f }, new[] { 0.041f, 0.026f, 0.020f }, new[] { 0.033f, 0.019f, 0.018f } };
        static readonly int[][] Chain = { new[] { J.ThumbMeta, J.ThumbProx, J.ThumbDist, J.ThumbTip }, new[] { J.IndexMeta, J.IndexProx, J.IndexInter, J.IndexDist, J.IndexTip },
            new[] { J.MiddleMeta, J.MiddleProx, J.MiddleInter, J.MiddleDist, J.MiddleTip }, new[] { J.RingMeta, J.RingProx, J.RingInter, J.RingDist, J.RingTip },
            new[] { J.LittleMeta, J.LittleProx, J.LittleInter, J.LittleDist, J.LittleTip } };

        /// <summary>palmRotation: forward = toward fingers, up = dorsal (back of hand).</summary>
        public static void Generate(TrackedHand h, Vector3 palmPosition, Quaternion palmRotation, float curl, float pinch, float spread = 0.1f)
        {
            float mirror = h.side == HandSide.Right ? 1f : -1f;
            Quaternion R = palmRotation;
            h.pos[J.Palm] = palmPosition; h.rot[J.Palm] = R;
            h.pos[J.Wrist] = palmPosition + R * new Vector3(0, 0, -0.045f); h.rot[J.Wrist] = R;
            for (int f = 0; f < 5; f++)
            {
                var chain = Chain[f];
                Vector3 b = Base[f]; b.x *= mirror;
                bool thumb = f == 0;
                float fc = thumb ? Mathf.Max(curl * 0.6f, pinch * 0.9f) : Mathf.Clamp01(curl + (f == 1 ? pinch * 0.55f : 0f));
                float splay = (f - 2) * spread * 12f * mirror;
                Quaternion dir = R * Quaternion.Euler(0, splay, 0);
                if (thumb) dir = R * Quaternion.Euler(-10f, mirror * (55f - 35f * fc), mirror * -40f);
                Vector3 p = palmPosition + R * b;
                if (thumb) { h.pos[chain[0]] = palmPosition + R * new Vector3(0.020f * mirror, -0.012f, -0.030f); h.rot[chain[0]] = dir; p = h.pos[chain[0]]; }
                else
                {
                    h.pos[chain[0]] = palmPosition + R * new Vector3(b.x * 0.7f, b.y, -0.03f); h.rot[chain[0]] = dir;
                    h.pos[chain[1]] = p; h.rot[chain[1]] = dir;
                }
                int start = thumb ? 0 : 1;
                Quaternion q = dir;
                for (int s = 0; s < Seg[f].Length; s++)
                {
                    float bend = (thumb ? 30f : (s == 0 ? 70f : (s == 1 ? 95f : 60f))) * fc;
                    q = q * Quaternion.Euler(bend, 0, 0);
                    p = p + q * new Vector3(0, 0, Seg[f][s]);
                    int ji = chain[start + s + 1];
                    h.pos[ji] = p; h.rot[ji] = q;
                }
            }
            for (int i = 0; i < J.Count; i++) h.radius[i] = (i == J.Palm || i == J.Wrist) ? 0.02f : 0.0085f;
            h.tracked = true;
        }
    }
}
