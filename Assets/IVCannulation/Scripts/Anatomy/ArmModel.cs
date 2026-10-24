using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    [System.Serializable]
    public struct ArmSection
    {
        public float x;           // distance along the arm (m), 0 = proximal cut, increases toward the fingers
        public float halfWidth;   // half width of the cross-section (m) along local Z
        public float halfHeight;  // half height of the cross-section (m) along local Y
        public ArmSection(float x, float w, float h) { this.x = x; halfWidth = w; halfHeight = h; }
    }

    /// <summary>
    /// A supinated (palm-up) adult forearm and lower upper-arm, built procedurally.
    /// Local space: +X runs proximal -> distal (shoulder to fingers), +Y is anterior (palm side, facing up),
    /// +Z is lateral (radial / thumb side). The arm rests on the table at local y = 0.
    /// The analytic shape is also used to place veins under the skin and to estimate surface normals.
    /// </summary>
    public class ArmModel : MonoBehaviour
    {
        public static ArmModel Instance { get; private set; }

        [Header("Shape (metres)")]
        public ArmSection[] sections = DefaultSections();
        [Tooltip("Antecubital fossa (front of the elbow)")] public float elbowX = 0.20f;
        [Tooltip("Wrist crease")] public float wristX = 0.44f;
        [Tooltip("Knuckles / end of palm")] public float handEndX = 0.585f;

        [Header("Mesh detail")]
        public int rings = 120;
        public int segments = 56;

        [Header("References (filled by the scene builder)")]
        public MeshFilter skinFilter;
        public MeshRenderer skinRenderer;
        public MeshCollider skinCollider;

        void Awake()
        {
            Instance = this;
        }

        void OnEnable() { Instance = this; }

        public static ArmSection[] DefaultSections()
        {
            return new[]
            {
                new ArmSection(-0.02f, 0.047f, 0.045f),
                new ArmSection(0.00f, 0.047f, 0.045f),   // upper arm (cut end, hidden by gown sleeve)
                new ArmSection(0.08f, 0.045f, 0.043f),
                new ArmSection(0.15f, 0.045f, 0.040f),
                new ArmSection(0.20f, 0.046f, 0.038f),   // elbow / antecubital fossa
                new ArmSection(0.25f, 0.043f, 0.035f),   // proximal forearm (muscle bulk)
                new ArmSection(0.31f, 0.038f, 0.030f),
                new ArmSection(0.37f, 0.032f, 0.024f),
                new ArmSection(0.43f, 0.029f, 0.019f),   // wrist
                new ArmSection(0.47f, 0.032f, 0.016f),   // heel of hand
                new ArmSection(0.53f, 0.041f, 0.0145f),  // palm
                new ArmSection(0.585f, 0.042f, 0.0125f), // knuckles
                new ArmSection(0.60f, 0.042f, 0.0125f),
            };
        }

        // ------------------------------------------------------------------ analytic shape

        public void Section(float x, out float a, out float b)
        {
            var s = sections;
            if (s == null || s.Length < 2) { a = 0.04f; b = 0.03f; return; }
            if (x <= s[0].x) { a = s[0].halfWidth; b = s[0].halfHeight; return; }
            if (x >= s[s.Length - 1].x) { a = s[s.Length - 1].halfWidth; b = s[s.Length - 1].halfHeight; return; }
            int i = 0;
            while (i < s.Length - 2 && x > s[i + 1].x) i++;
            float t = Mathf.InverseLerp(s[i].x, s[i + 1].x, x);
            ArmSection p0 = s[Mathf.Max(0, i - 1)], p1 = s[i], p2 = s[i + 1], p3 = s[Mathf.Min(s.Length - 1, i + 2)];
            a = Mathf.Max(0.002f, MeshBuilder.CR1(p0.halfWidth, p1.halfWidth, p2.halfWidth, p3.halfWidth, t));
            b = Mathf.Max(0.002f, MeshBuilder.CR1(p0.halfHeight, p1.halfHeight, p2.halfHeight, p3.halfHeight, t));
        }

        /// <summary>Local-space point on (or under) the skin. theta: 0 = top (anterior), +90 = thumb side, -90 = little-finger side.</summary>
        public Vector3 SurfacePointLocal(float x, float thetaDeg, float depth = 0f)
        {
            float a, b;
            Section(x, out a, out b);
            float th = thetaDeg * Mathf.Deg2Rad;
            Vector3 p = new Vector3(x, b + b * Mathf.Cos(th), a * Mathf.Sin(th));
            if (depth != 0f) p -= SurfaceNormalLocal(x, thetaDeg) * depth;
            return p;
        }

        public Vector3 SurfaceNormalLocal(float x, float thetaDeg)
        {
            float a, b;
            Section(x, out a, out b);
            float th = thetaDeg * Mathf.Deg2Rad;
            return new Vector3(0f, Mathf.Cos(th) / b, Mathf.Sin(th) / a).normalized;
        }

        public Vector3 SurfacePoint(float x, float thetaDeg, float depth = 0f)
        {
            return transform.TransformPoint(SurfacePointLocal(x, thetaDeg, depth));
        }

        public Vector3 SurfaceNormal(float x, float thetaDeg)
        {
            return transform.TransformDirection(SurfaceNormalLocal(x, thetaDeg));
        }

        /// <summary>Arm coordinates (x along arm, theta around it) of any world point.</summary>
        public void ToArmCoords(Vector3 world, out float x, out float thetaDeg, out float normalizedRadius)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            x = p.x;
            float a, b;
            Section(x, out a, out b);
            float yy = (p.y - b) / b;
            float zz = p.z / a;
            thetaDeg = Mathf.Atan2(zz, yy) * Mathf.Rad2Deg;
            normalizedRadius = Mathf.Sqrt(yy * yy + zz * zz);
        }

        public bool IsOnForearmOrArm(Vector3 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return p.x > 0.0f && p.x < wristX + 0.03f;
        }

        /// <summary>Projects a world point radially onto the analytic skin surface.</summary>
        public Vector3 ProjectToSurface(Vector3 world, out Vector3 normal)
        {
            float x, th, r;
            ToArmCoords(world, out x, out th, out r);
            x = Mathf.Clamp(x, 0f, handEndX);
            normal = SurfaceNormal(x, th);
            return SurfacePoint(x, th, 0f);
        }

        /// <summary>
        /// Finds the skin surface near a point using the real mesh collider (works if you swap in a custom arm mesh,
        /// as long as the analytic profile roughly matches). signedDistance &gt; 0 means outside the skin.
        /// </summary>
        public bool ProbeSurface(Vector3 world, float maxDistance, out Vector3 surfacePoint, out Vector3 normal, out float signedDistance)
        {
            surfacePoint = world; normal = Vector3.up; signedDistance = float.MaxValue;
            if (skinCollider == null) return false;
            Vector3 n0;
            ProjectToSurface(world, out n0);
            float probe = 0.06f;
            RaycastHit hit;
            if (skinCollider.Raycast(new Ray(world + n0 * probe, -n0), out hit, probe * 2f))
            {
                surfacePoint = hit.point;
                normal = hit.normal;
                signedDistance = Vector3.Dot(world - hit.point, hit.normal);
                return Mathf.Abs(signedDistance) <= maxDistance;
            }
            return false;
        }

        public bool RaycastSkin(Ray ray, float maxDistance, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            return skinCollider != null && skinCollider.Raycast(ray, out hit, maxDistance);
        }

        /// <summary>Proximal direction along the arm in world space (the usual direction of needle travel).</summary>
        public Vector3 ProximalDirection { get { return transform.TransformDirection(Vector3.left); } }

        // ------------------------------------------------------------------ mesh generation

        public Mesh BuildSkinMesh()
        {
            var b = new MeshBuilder.Builder();
            var ringsList = new List<MeshBuilder.Ring>();
            float x0 = 0f, x1 = handEndX;
            for (int i = 0; i < rings; i++)
            {
                float x = Mathf.Lerp(x0, x1, i / (float)(rings - 1));
                float a, bb;
                Section(x, out a, out bb);
                ringsList.Add(new MeshBuilder.Ring
                {
                    center = new Vector3(x, bb, 0f),
                    forward = Vector3.right,
                    up = Vector3.up,
                    rx = a, ry = bb, axial = 0f
                });
            }
            // Rounded distal end of the palm (knuckles)
            {
                float a, bb;
                Section(x1, out a, out bb);
                float e = 0.012f;
                int k = 6;
                for (int i = 1; i <= k; i++)
                {
                    float ang = i / (float)k * Mathf.PI * 0.5f;
                    float sc = Mathf.Max(Mathf.Cos(ang), 0.03f);
                    ringsList.Add(new MeshBuilder.Ring
                    {
                        center = new Vector3(x1 + e * Mathf.Sin(ang), bb, 0f),
                        forward = Vector3.right, up = Vector3.up,
                        rx = a * sc, ry = bb * sc, axial = Mathf.Sin(ang)
                    });
                }
            }
            MeshBuilder.Loft(b, ringsList, segments, true, false);

            // Fingers (palm up, relaxed and slightly curled toward the palm)
            AddFinger(b, 0.568f, 0.027f, 0.074f, 0.0088f, 0.12f);  // index (thumb side, +Z)
            AddFinger(b, 0.572f, 0.009f, 0.082f, 0.0092f, 0.10f);  // middle
            AddFinger(b, 0.570f, -0.009f, 0.077f, 0.0087f, 0.11f); // ring
            AddFinger(b, 0.562f, -0.026f, 0.060f, 0.0078f, 0.14f); // little
            AddThumb(b);
            return b.ToMesh("IV_ArmSkin");
        }

        void AddFinger(MeshBuilder.Builder b, float xStart, float z, float length, float radius, float curl)
        {
            float a, bb;
            Section(xStart, out a, out bb);
            float y0 = bb * 1.05f;
            var pts = new List<Vector3>();
            var radii = new List<float>();
            int n = 9;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float x = xStart + t * length;
                float y = y0 + Mathf.Sin(t * Mathf.PI * 0.5f) * curl * length * t;
                pts.Add(new Vector3(x, Mathf.Max(y, radius), z * (1f + 0.08f * t)));
                radii.Add(radius * Mathf.Lerp(1f, 0.82f, t));
            }
            MeshBuilder.Tube(b, pts, radii, 16, false, true);
        }

        void AddThumb(MeshBuilder.Builder b)
        {
            var pts = new List<Vector3>
            {
                new Vector3(0.475f, 0.016f, 0.026f),
                new Vector3(0.500f, 0.019f, 0.040f),
                new Vector3(0.525f, 0.022f, 0.051f),
                new Vector3(0.550f, 0.024f, 0.058f),
                new Vector3(0.572f, 0.025f, 0.062f),
            };
            var radii = new List<float> { 0.0125f, 0.0115f, 0.0105f, 0.0098f, 0.009f };
            MeshBuilder.Tube(b, pts, radii, 16, false, true);
        }

        /// <summary>Gown sleeve that hides the cut end of the arm.</summary>
        public Mesh BuildSleeveMesh(float fromX, float toX, float thickness)
        {
            var b = new MeshBuilder.Builder();
            var ringsList = new List<MeshBuilder.Ring>();
            int n = 12;
            for (int i = 0; i < n; i++)
            {
                float x = Mathf.Lerp(fromX, toX, i / (float)(n - 1));
                float a, bb;
                Section(Mathf.Max(x, 0f), out a, out bb);
                float wrinkle = 1f + 0.03f * Mathf.Sin(i * 1.7f);
                ringsList.Add(new MeshBuilder.Ring
                {
                    center = new Vector3(x, bb + thickness * 0.3f, 0f),
                    forward = Vector3.right, up = Vector3.up,
                    rx = (a + thickness) * wrinkle, ry = bb + thickness, axial = 0f
                });
            }
            MeshBuilder.Loft(b, ringsList, 40, true, true);
            return b.ToMesh("IV_Sleeve");
        }

        /// <summary>A tourniquet band wrapped around the arm at position x.</summary>
        public Mesh BuildTourniquetBandMesh(float x, float width, float thickness)
        {
            float a, bb;
            Section(x, out a, out bb);
            a += thickness * 0.5f + 0.0015f;
            bb += thickness * 0.5f + 0.0015f;
            var loop = new List<MeshBuilder.Ring>();
            int n = 48;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n * Mathf.PI * 2f;
                float tn = (i + 1) / (float)n * Mathf.PI * 2f;
                Vector3 p = new Vector3(x, bb + bb * Mathf.Cos(t), a * Mathf.Sin(t));
                Vector3 pn = new Vector3(x, bb + bb * Mathf.Cos(tn), a * Mathf.Sin(tn));
                loop.Add(new MeshBuilder.Ring
                {
                    center = p,
                    forward = (pn - p).normalized,
                    up = Vector3.right,
                    rx = thickness * 0.5f,
                    ry = width * 0.5f,
                    axial = 0f
                });
            }
            var b = new MeshBuilder.Builder();
            MeshBuilder.Loft(b, loop, 8, false, false, true);
            return b.ToMesh("IV_TourniquetBand");
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(SurfacePoint(elbowX, 0f), 0.01f);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(SurfacePoint(wristX, 0f), 0.01f);
        }
    }
}
