using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    public enum VesselType { Vein, Artery }
    public enum SiteRating { Ideal, Acceptable, Avoid, NotSuitable }

    /// <summary>
    /// One superficial vein (or a nearby artery). The path is defined by its child transforms (P0, P1, ...),
    /// so you can drag the points in the Scene view to trace the veins on a different arm model, then use
    /// the "Rebuild Mesh" context menu. Lumen radius is used for the needle-in-vein test.
    /// </summary>
    public class VeinPath : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "Vein";
        public VesselType vesselType = VesselType.Vein;

        [Header("Size (metres)")]
        [Tooltip("Lumen radius at the proximal end of the path")] public float radius = 0.0017f;
        [Tooltip("Radius multiplier at the first (distal) control point")] public float startRadiusScale = 0.8f;
        [Tooltip("How much bigger the vein gets when the tourniquet is on")] public float engorgedScale = 1.3f;

        [Header("Teaching data")]
        public SiteRating defaultRating = SiteRating.Acceptable;
        [TextArea(2, 4)] public string siteNote = "";
        public bool palpable = true;

        [Header("Mesh")]
        public int samplesPerSpan = 10;
        public int tubeSegments = 12;
        public MeshFilter meshFilter;
        public MeshRenderer meshRenderer;

        // cached in LOCAL space so the arm can be moved
        readonly List<Vector3> samples = new List<Vector3>();
        readonly List<float> sampleRadius = new List<float>();
        float engorge;          // 0..1 current
        float engorgeTarget;    // 0..1 target
        float lastBuiltEngorge = -1f;
        Mesh runtimeMesh;

        public float Engorgement { get { return engorge; } }

        void OnEnable()
        {
            VascularSystem.Register(this);
            RebuildSamples();
        }

        void OnDisable()
        {
            VascularSystem.Unregister(this);
        }

        public List<Transform> ControlPoints()
        {
            var list = new List<Transform>();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform c = transform.GetChild(i);
                if (c.name.StartsWith("P")) list.Add(c);
            }
            return list;
        }

        public void RebuildSamples()
        {
            samples.Clear();
            sampleRadius.Clear();
            var cps = ControlPoints();
            if (cps.Count < 2) return;
            var local = new List<Vector3>();
            foreach (var c in cps) local.Add(c.localPosition);
            var pts = MeshBuilder.CatmullRom(local, Mathf.Max(1, samplesPerSpan));
            float total = 0f;
            var cum = new List<float> { 0f };
            for (int i = 1; i < pts.Count; i++) { total += (pts[i] - pts[i - 1]).magnitude; cum.Add(total); }
            for (int i = 0; i < pts.Count; i++)
            {
                samples.Add(pts[i]);
                float u = total > 0f ? cum[i] / total : 0f;
                sampleRadius.Add(radius * Mathf.Lerp(startRadiusScale, 1f, u));
            }
        }

        public float CurrentScale { get { return Mathf.Lerp(1f, engorgedScale, engorge); } }

        public void SetEngorged(bool on) { engorgeTarget = on ? 1f : 0f; }

        public void SnapEngorgement(bool on)
        {
            engorge = engorgeTarget = on ? 1f : 0f;
            RebuildMesh(true);
        }

        void Update()
        {
            if (Mathf.Abs(engorge - engorgeTarget) > 0.0001f)
            {
                engorge = Mathf.MoveTowards(engorge, engorgeTarget, Time.deltaTime / 1.5f);
                if (Mathf.Abs(engorge - lastBuiltEngorge) > 0.05f || engorge == engorgeTarget) RebuildMesh(true);
            }
        }

        /// <summary>Builds the visible tube. When runtime==true a fresh in-memory mesh is used.</summary>
        public Mesh RebuildMesh(bool runtime)
        {
            if (samples.Count < 2) RebuildSamples();
            if (samples.Count < 2) return null;
            float sc = CurrentScale;
            var radii = new List<float>();
            foreach (var r in sampleRadius) radii.Add(r * sc);
            var b = new MeshBuilder.Builder();
            MeshBuilder.Tube(b, samples, radii, tubeSegments, true, true);
            Mesh m;
            if (runtime)
            {
                if (runtimeMesh == null) { runtimeMesh = new Mesh(); runtimeMesh.name = displayName + "_runtime"; }
                b.WriteTo(runtimeMesh);
                runtimeMesh.RecalculateTangents();
                m = runtimeMesh;
            }
            else
            {
                m = b.ToMesh("IV_" + displayName.Replace(" ", ""));
            }
            if (meshFilter != null) meshFilter.sharedMesh = m;
            lastBuiltEngorge = engorge;
            return m;
        }

        [ContextMenu("Rebuild Mesh (from control points)")]
        void ContextRebuild()
        {
            RebuildSamples();
            RebuildMesh(false);
        }

        public struct Hit
        {
            public VeinPath vessel;
            public float distance;       // from point to the centreline
            public float radius;         // effective lumen radius here
            public Vector3 closestPoint; // world
            public Vector3 tangent;      // world, direction of increasing sample index (distal -> proximal)
            public int segment;
            public float segmentT;
            public bool Inside { get { return vessel != null && distance < radius; } }
        }

        public bool Closest(Vector3 world, out Hit hit)
        {
            hit = default(Hit);
            if (samples.Count < 2) return false;
            Vector3 p = transform.InverseTransformPoint(world);
            float best = float.MaxValue;
            int bestI = 0; float bestT = 0f; Vector3 bestQ = samples[0];
            for (int i = 0; i < samples.Count - 1; i++)
            {
                Vector3 a = samples[i], b = samples[i + 1];
                Vector3 ab = b - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-12f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
                Vector3 q = a + ab * t;
                float d = (p - q).sqrMagnitude;
                if (d < best) { best = d; bestI = i; bestT = t; bestQ = q; }
            }
            float scale = transform.lossyScale.x;
            hit.vessel = this;
            hit.distance = Mathf.Sqrt(best) * scale;
            hit.radius = Mathf.Lerp(sampleRadius[bestI], sampleRadius[bestI + 1], bestT) * CurrentScale * scale;
            hit.closestPoint = transform.TransformPoint(bestQ);
            hit.tangent = transform.TransformDirection(samples[bestI + 1] - samples[bestI]).normalized;
            hit.segment = bestI;
            hit.segmentT = bestT;
            return true;
        }

        /// <summary>
        /// Walks along the centreline from a hit, in the direction that best matches 'dir', returning world points
        /// covering 'length' metres. Used to bend the threaded catheter along the vein.
        /// </summary>
        public void WalkAlong(Hit from, Vector3 dir, float length, List<Vector3> output)
        {
            if (samples.Count < 2) return;
            bool forward = Vector3.Dot(from.tangent, dir) >= 0f;
            float remaining = length;
            Vector3 prev = from.closestPoint;
            int idx = forward ? from.segment + 1 : from.segment;
            while (remaining > 0f && idx >= 0 && idx < samples.Count)
            {
                Vector3 next = transform.TransformPoint(samples[idx]);
                float seg = (next - prev).magnitude;
                if (seg >= remaining)
                {
                    output.Add(prev + (next - prev).normalized * remaining);
                    return;
                }
                if (seg > 1e-5f) output.Add(next);
                remaining -= seg;
                prev = next;
                idx += forward ? 1 : -1;
            }
        }

        public int SampleCount { get { return samples.Count; } }
        public Vector3 SampleWorld(int i) { return transform.TransformPoint(samples[i]); }

        void OnDrawGizmos()
        {
            var cps = ControlPoints();
            Gizmos.color = vesselType == VesselType.Artery ? new Color(1f, 0.2f, 0.2f) : new Color(0.3f, 0.5f, 1f);
            for (int i = 0; i < cps.Count; i++)
            {
                Gizmos.DrawSphere(cps[i].position, 0.0015f);
                if (i > 0) Gizmos.DrawLine(cps[i - 1].position, cps[i].position);
            }
        }
    }
}
