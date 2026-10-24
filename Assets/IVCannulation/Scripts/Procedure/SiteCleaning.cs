using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// Tracks where the skin has been scrubbed with the alcohol swab, shows a wet sheen that fades as it dries,
    /// and measures coverage of the circle around the chosen site.
    /// </summary>
    public class SiteCleaning : MonoBehaviour, IResettable
    {
        [Tooltip("Material for the wet antiseptic sheen (transparent)")] public Material wetMaterial;
        [Tooltip("Diameter of one wet dab (m)")] public float dabSize = 0.014f;
        public int maxDabs = 220;

        readonly List<Vector3> dabPoints = new List<Vector3>();
        readonly List<GameObject> dabObjects = new List<GameObject>();
        Mesh discMesh;
        Material wetInstance;
        float wetBaseAlpha = 0.45f;

        public bool HasStarted { get; private set; }
        public bool IsComplete { get; private set; }
        public float ScrubSeconds { get; private set; }
        public float Coverage { get; private set; }
        public Vector3 Center { get; private set; }
        public float DryProgress { get; private set; }   // 0 = just cleaned, 1 = dry
        public bool IsDry { get { return IsComplete && DryProgress >= 1f; } }

        Vector3 centerNormal = Vector3.up;
        Vector3 lastDab;
        bool hasLastDab;
        float completeTime;

        void Start()
        {
            if (IVSession.Instance != null) IVSession.Instance.RegisterResettable(this);
            discMesh = MeshBuilder.Disc("WetDab", 18);
            if (wetMaterial != null)
            {
                wetInstance = new Material(wetMaterial);
                wetBaseAlpha = wetMaterial.GetColor("_BaseColor").a;
            }
        }

        public void ResetForSession()
        {
            foreach (var g in dabObjects) if (g != null) Destroy(g);
            dabObjects.Clear();
            dabPoints.Clear();
            HasStarted = false;
            IsComplete = false;
            ScrubSeconds = 0f;
            Coverage = 0f;
            DryProgress = 0f;
            hasLastDab = false;
            SetWetAlpha(1f);
        }

        /// <summary>Called every frame the wet swab is rubbing the skin.</summary>
        public void Scrub(Vector3 surfacePoint, Vector3 normal, float speed, float dt)
        {
            var s = IVSession.Instance;
            if (s == null) return;
            if (IsComplete) return;

            if (!HasStarted)
            {
                HasStarted = true;
                Center = s.SiteSelected ? s.SitePoint : surfacePoint;
                centerNormal = normal;
                SetWetAlpha(1f);
            }
            if (!s.SiteSelected) Center = Vector3.Lerp(Center, surfacePoint, 0.02f);

            if (speed > 0.01f) ScrubSeconds += dt;

            if (!hasLastDab || Vector3.Distance(lastDab, surfacePoint) > dabSize * 0.3f)
            {
                lastDab = surfacePoint;
                hasLastDab = true;
                dabPoints.Add(surfacePoint);
                if (dabObjects.Count < maxDabs) dabObjects.Add(MakeDab(surfacePoint, normal));
                Coverage = ComputeCoverage();
            }

            var c = s.clinical;
            s.OnCleaningProgress(Coverage, ScrubSeconds);
            if (Coverage >= c.requiredCoverage && ScrubSeconds >= c.requiredScrubSeconds)
            {
                IsComplete = true;
                completeTime = Time.time;
                DryProgress = 0f;
                s.OnCleaningComplete(Coverage, ScrubSeconds);
            }
        }

        GameObject MakeDab(Vector3 p, Vector3 n)
        {
            var g = new GameObject("WetDab");
            g.transform.SetParent(transform, true);
            g.transform.position = p + n * 0.0004f;
            g.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
            g.transform.localScale = Vector3.one * dabSize;
            g.AddComponent<MeshFilter>().sharedMesh = discMesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = wetInstance;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g;
        }

        float ComputeCoverage()
        {
            var s = IVSession.Instance;
            float R = s != null ? s.clinical.cleanRadius : 0.025f;
            Vector3 n = centerNormal;
            Vector3 t1 = Vector3.Cross(n, Vector3.forward);
            if (t1.sqrMagnitude < 1e-4f) t1 = Vector3.Cross(n, Vector3.right);
            t1.Normalize();
            Vector3 t2 = Vector3.Cross(n, t1);
            int total = 0, covered = 0;
            float step = R / 4f;
            float reach = dabSize * 0.75f;
            for (int i = -4; i <= 4; i++)
            {
                for (int j = -4; j <= 4; j++)
                {
                    float u = i * step, v = j * step;
                    if (u * u + v * v > R * R) continue;
                    total++;
                    Vector3 q = Center + t1 * u + t2 * v;
                    for (int k = 0; k < dabPoints.Count; k++)
                    {
                        Vector3 d = dabPoints[k] - q;
                        d -= n * Vector3.Dot(d, n);
                        if (d.sqrMagnitude <= reach * reach) { covered++; break; }
                    }
                }
            }
            return total > 0 ? covered / (float)total : 0f;
        }

        public void Contaminate()
        {
            foreach (var g in dabObjects) if (g != null) Destroy(g);
            dabObjects.Clear();
            dabPoints.Clear();
            HasStarted = false;
            IsComplete = false;
            ScrubSeconds = 0f;
            Coverage = 0f;
            DryProgress = 0f;
            hasLastDab = false;
        }

        void Update()
        {
            if (!IsComplete) return;
            var s = IVSession.Instance;
            float dry = s != null ? Mathf.Max(0.1f, s.clinical.dryingSeconds) : 10f;
            DryProgress = Mathf.Clamp01((Time.time - completeTime) / dry);
            SetWetAlpha(1f - DryProgress);
        }

        void SetWetAlpha(float k)
        {
            if (wetInstance == null) return;
            Color c = wetInstance.GetColor("_BaseColor");
            c.a = wetBaseAlpha * k;
            wetInstance.SetColor("_BaseColor", c);
        }
    }
}
