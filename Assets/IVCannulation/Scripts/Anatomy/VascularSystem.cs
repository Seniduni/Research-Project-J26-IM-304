using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    /// <summary>Registry and spatial queries for all veins and arteries in the scene.</summary>
    public static class VascularSystem
    {
        static readonly List<VeinPath> vessels = new List<VeinPath>();
        public static IList<VeinPath> All { get { return vessels; } }

        public static void Register(VeinPath v) { if (!vessels.Contains(v)) vessels.Add(v); }
        public static void Unregister(VeinPath v) { vessels.Remove(v); }

        /// <summary>The vessel whose lumen is closest to containing the point (smallest distance - radius).</summary>
        public static bool Query(Vector3 world, out VeinPath.Hit best)
        {
            best = default(VeinPath.Hit);
            float bestScore = float.MaxValue;
            bool found = false;
            foreach (var v in vessels)
            {
                if (v == null || !v.isActiveAndEnabled) continue;
                VeinPath.Hit h;
                if (!v.Closest(world, out h)) continue;
                float score = h.distance - h.radius;
                // prefer veins over arteries when both contain the point
                if (h.Inside && v.vesselType == VesselType.Vein) score -= 0.001f;
                if (score < bestScore) { bestScore = score; best = h; found = true; }
            }
            return found;
        }

        public static void SetEngorged(bool on)
        {
            foreach (var v in vessels)
                if (v != null && v.vesselType == VesselType.Vein) v.SetEngorged(on);
        }

        public static void SnapEngorged(bool on)
        {
            foreach (var v in vessels)
                if (v != null && v.vesselType == VesselType.Vein) v.SnapEngorgement(on);
        }

        public static void SetVisible(bool veinsVisible)
        {
            foreach (var v in vessels)
                if (v != null && v.meshRenderer != null) v.meshRenderer.enabled = veinsVisible;
        }

        public struct PalpationResult
        {
            public bool found;
            public VeinPath vessel;
            public float lateralDistance;  // distance across the skin from the vessel centre line
            public float depth;            // depth of the vessel centre under the skin
            public Vector3 vesselPoint;    // world point on the centreline
        }

        /// <summary>
        /// What a fingertip pressed on the skin at 'surfacePoint' would feel.
        /// Veins feel soft and bouncy; arteries pulsate.
        /// </summary>
        public static PalpationResult Palpate(Vector3 surfacePoint, Vector3 normal)
        {
            var res = new PalpationResult();
            float bestScore = float.MaxValue;
            foreach (var v in vessels)
            {
                if (v == null || !v.isActiveAndEnabled || !v.palpable) continue;
                // sample a short column under the fingertip
                for (int k = 0; k < 6; k++)
                {
                    float d = 0.001f + k * 0.002f;
                    VeinPath.Hit h;
                    if (!v.Closest(surfacePoint - normal * d, out h)) continue;
                    Vector3 rel = h.closestPoint - surfacePoint;
                    float depth = -Vector3.Dot(rel, normal);
                    float lateral = (rel + normal * depth).magnitude;
                    float maxDepth = v.vesselType == VesselType.Artery ? 0.009f : 0.0065f;
                    float maxLateral = h.radius + 0.0035f;
                    if (depth < 0f || depth > maxDepth || lateral > maxLateral) continue;
                    float score = lateral + depth * 0.3f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        res.found = true;
                        res.vessel = v;
                        res.lateralDistance = lateral;
                        res.depth = depth;
                        res.vesselPoint = h.closestPoint;
                    }
                }
            }
            return res;
        }
    }
}
