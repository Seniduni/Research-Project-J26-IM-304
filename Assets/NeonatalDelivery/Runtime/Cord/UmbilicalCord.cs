// Umbilical cord, kinematic version (v4): a smooth hanging curve from the newborn's umbilicus to the introitus.
// No rope physics -> no jitter, no stretching, no tunnelling through the mother or the bed.
// * shape: cubic Bezier that leaves the introitus along the birth canal axis and sags with its slack under gravity
// * hidden while the umbilicus is still inside the mother
// * clamps ride on the curve at the distance where they were applied; cutting splits it into two hanging stumps
// * procedural three-vessel texture and pulsation (stops after clamping)
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class UmbilicalCord : MonoBehaviour
    {
        public Transform babyEnd;          // ND_Z_NavelSkin
        public Transform babyAxisLow, babyAxisHigh;   // baby Hips / Chest bones: the cord leaves the belly along its outward normal
        public Transform placentaEnd;      // ND_Anchor_Placenta (used only when no canal is available)
        public float length = 0.55f;
        public int particles = 40;         // samples along the cord
        public float radius = 0.0085f;
        public int sides = 10;
        public Material material;
        public BirthCanalDeformer canal;
        [Range(0, 1)] public float pulsation = 1f;     // 1 = strongly pulsating, 0 = stopped (white, flat)
        public float heartRate = 140f;

        public struct Sphere { public Vector3 c; public float r; }
        public readonly List<Sphere> colliders = new List<Sphere>();   // kept for API compatibility (not needed by the kinematic cord)
        public float floorY = -10f;

        public readonly List<(Transform clamp, float s)> clamps = new List<(Transform, float)>();
        float _cut = -1f;                 // cut distance from the navel (m)
        public bool IsCut => _cut >= 0f;
        public float CutDistanceFromNavel => _cut;

        Vector3[] _p;                      // current centre line, navel (0) -> introitus (particles-1)
        Vector3[] _stumpB, _stumpM;        // after the cut
        Mesh _mesh; MeshRenderer _mr;
        readonly List<Vector3> _v = new List<Vector3>(); readonly List<Vector3> _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>(); readonly List<int> _tri = new List<int>();
        float _pulseT;

        void Awake() { Build(); }

        public void Build()
        {
            particles = Mathf.Max(12, particles);
            _p = new Vector3[particles]; _stumpB = new Vector3[particles]; _stumpM = new Vector3[particles];
            _mesh = new Mesh { name = "ND_UmbilicalCord" }; _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            _mr = GetComponent<MeshRenderer>();
            if (material == null) material = DefaultMaterial();
            _mr.sharedMaterial = material;
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); transform.localScale = Vector3.one;
            ResetRope();
        }

        public static Material DefaultMaterial()
        {
            var sh = Shader.Find("NeonatalDelivery/Skin") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var m = new Material(sh) { name = "ND_Cord_Runtime" };
            var tex = CordTexture();
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.78f);
            if (m.HasProperty("_Wetness")) m.SetFloat("_Wetness", 0.9f);
            if (m.HasProperty("_SSSColor")) m.SetColor("_SSSColor", new Color(0.75f, 0.45f, 0.5f));
            return m;
        }

        /// <summary>Procedural Wharton's jelly with two spiralling arteries and one vein (u = around, v = along).</summary>
        public static Texture2D CordTexture()
        {
            const int W = 64, H = 256;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "ND_CordProcedural", wrapMode = TextureWrapMode.Repeat };
            var jelly = new Color(0.86f, 0.84f, 0.88f); var vein = new Color(0.33f, 0.22f, 0.42f); var artery = new Color(0.62f, 0.20f, 0.26f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float tw = v * 3f;                                   // helical twist (cord is coiled ~ 1 turn / 5 cm)
                    float dv = Mathf.Abs(Mathf.Repeat(u - tw + 0.5f, 1f) - 0.5f);
                    float da1 = Mathf.Abs(Mathf.Repeat(u - tw + 0.5f + 0.33f, 1f) - 0.5f);
                    float da2 = Mathf.Abs(Mathf.Repeat(u - tw + 0.5f - 0.33f, 1f) - 0.5f);
                    Color c = jelly;
                    c = Color.Lerp(c, vein, Mathf.Clamp01(1f - dv / 0.085f) * 0.75f);
                    c = Color.Lerp(c, artery, Mathf.Clamp01(1f - da1 / 0.045f) * 0.7f);
                    c = Color.Lerp(c, artery, Mathf.Clamp01(1f - da2 / 0.045f) * 0.7f);
                    float n = Mathf.PerlinNoise(u * 18f, v * 40f) * 0.08f;
                    t.SetPixel(x, y, c * (0.96f + n));
                }
            t.Apply(true);
            return t;
        }

        public void ResetRope() { _cut = -1f; clamps.Clear(); Solve(); }

        // ------------------------------------------------------------------ geometry
        Vector3 Exit(out Vector3 outward)
        {
            if (canal != null && canal.Ready) { outward = canal.OutwardAxis; return canal.IntroitusCenter - outward * 0.015f; }
            outward = Vector3.down;
            return placentaEnd != null ? placentaEnd.position : (babyEnd != null ? babyEnd.position + Vector3.down * 0.3f : transform.position);
        }

        static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            float u = 1f - t; return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        // hanging curve from 'a' to 'b' with total length 'len'; ta/tb = preferred leaving directions at each end
        void Hang(Vector3[] dst, Vector3 a, Vector3 ta, Vector3 b, Vector3 tb, float len)
        {
            float chord = Vector3.Distance(a, b);
            float slack = Mathf.Max(0f, len - chord);
            float sag = Mathf.Clamp(slack * 0.55f, 0f, 0.35f);
            float h = Mathf.Max(0.04f, chord * 0.33f);
            Vector3 c1 = a + ta * h + Vector3.down * sag;
            Vector3 c2 = b + tb * h + Vector3.down * sag;
            for (int i = 0; i < dst.Length; i++)
            {
                var p = Bez(a, c1, c2, b, i / (float)(dst.Length - 1));
                if (p.y < floorY + radius) p.y = floorY + radius;
                dst[i] = p;
            }
        }

        static float ArcLength(Vector3[] p) { float L = 0; for (int i = 1; i < p.Length; i++) L += Vector3.Distance(p[i - 1], p[i]); return L; }

        static Vector3 AtDistance(Vector3[] p, float s)
        {
            if (s <= 0) return p[0];
            for (int i = 1; i < p.Length; i++)
            {
                float d = Vector3.Distance(p[i - 1], p[i]);
                if (s <= d && d > 1e-6f) return Vector3.Lerp(p[i - 1], p[i], s / d);
                s -= d;
            }
            return p[p.Length - 1];
        }

        static Vector3 TangentAtDistance(Vector3[] p, float s)
        {
            var a = AtDistance(p, Mathf.Max(0, s - 0.01f)); var b = AtDistance(p, s + 0.01f);
            var t = b - a; return t.sqrMagnitude > 1e-10f ? t.normalized : Vector3.forward;
        }

        bool NavelInside()
        {
            return babyEnd != null && canal != null && canal.Ready && canal.OutsideDistance(babyEnd.position) < 0.01f;
        }

        // direction out of the baby's belly at the umbilicus (perpendicular to the spine), falls back to 'down'
        Vector3 BellyOut(Vector3 n, Vector3 e)
        {
            if (babyAxisLow == null || babyAxisHigh == null) return (Vector3.down * 0.6f + (e - n).normalized * 0.4f).normalized;
            Vector3 ax = babyAxisHigh.position - babyAxisLow.position;
            if (ax.sqrMagnitude < 1e-8f) return Vector3.down;
            ax.Normalize();
            Vector3 o = Vector3.ProjectOnPlane(n - babyAxisLow.position, ax);
            return o.sqrMagnitude > 1e-8f ? o.normalized : Vector3.down;
        }

        void Solve()
        {
            if (_p == null || babyEnd == null) return;
            Vector3 n = babyEnd.position, e = Exit(out var outward);
            if (!IsCut)
            {
                Vector3 tn = (BellyOut(n, e) * 0.85f + (e - n).normalized * 0.15f).normalized;   // leaves the belly outwards (clear of the flexed thighs)
                Hang(_p, n, tn, e, outward, length);
            }
            else
            {
                // baby stump hangs from the navel; placenta side hangs out of the introitus
                Vector3 dirB = (BellyOut(n, e) * 0.6f + Vector3.down * 0.4f).normalized;
                Hang(_stumpB, n, dirB, n + dirB * _cut * 0.92f, Vector3.up, _cut);
                float rest = Mathf.Max(0.05f, length - _cut);
                Vector3 endM = e + outward * rest * 0.35f + Vector3.down * rest * 0.6f;
                Hang(_stumpM, e, outward, endM, Vector3.up, rest);
            }
        }

        // ------------------------------------------------------------------ interaction
        /// <summary>Attach a clamp at the cord point closest to 'world'. Returns distance from the umbilicus (m), or -1.</summary>
        public float AttachClamp(Transform clamp, Vector3 world, float maxDist = 0.03f)
        {
            if (IsCut || !_mr.enabled) return -1f;
            float s = NearestDistanceFromNavel(world, out float d);
            if (d > maxDist || s < 0.01f || s > length - 0.02f) return -1f;
            clamps.Add((clamp, s));
            return s;
        }

        /// <summary>Index of the cord centre-line sample nearest to 'world' (0 = umbilicus).</summary>
        public int IndexAt(Vector3 world)
        {
            if (_p == null) return -1;
            int best = 0; float bd = float.MaxValue;
            for (int i = 0; i < _p.Length; i++) { float d = (world - _p[i]).sqrMagnitude; if (d < bd) { bd = d; best = i; } }
            return best;
        }

        /// <summary>Distance (m) from 'world' to the visible cord, or +inf when the cord is hidden / already cut.</summary>
        public float DistanceTo(Vector3 world)
        {
            if (_p == null || IsCut || _mr == null || !_mr.enabled) return float.PositiveInfinity;
            NearestDistanceFromNavel(world, out float d); return d;
        }

        public float NearestDistanceFromNavel(Vector3 world, out float dist)
        {
            // exact point-to-polyline distance (segments, not just samples) -> arc length from the umbilicus
            dist = 9f; float best = 0, acc = 0;
            if (_p == null || _p.Length < 2) return 0f;
            for (int i = 1; i < _p.Length; i++)
            {
                Vector3 a = _p[i - 1], ab = _p[i] - a; float len = ab.magnitude;
                float u = len > 1e-6f ? Mathf.Clamp01(Vector3.Dot(world - a, ab) / (len * len)) : 0f;
                float d = Vector3.Distance(world, a + ab * u);
                if (d < dist) { dist = d; best = acc + u * len; }
                acc += len;
            }
            return best;
        }

        /// <summary>Cut the cord near 'world'. Returns distance of the cut from the umbilicus (m) or -1 if the blades are not on the cord.</summary>
        public float TryCut(Vector3 world, float maxDist = 0.018f)
        {
            if (IsCut || !_mr.enabled) return -1f;
            float s = NearestDistanceFromNavel(world, out float d);
            if (d > maxDist) return -1f;
            _cut = Mathf.Clamp(s, 0.02f, length - 0.03f);
            return _cut;
        }

        public Vector3 PointAt(float distanceFromNavel)
        {
            if (!IsCut) return AtDistance(_p, distanceFromNavel);
            return distanceFromNavel <= _cut ? AtDistance(_stumpB, distanceFromNavel) : AtDistance(_stumpM, distanceFromNavel - _cut);
        }
        public Vector3 BabySideCutEnd => IsCut ? _stumpB[_stumpB.Length - 1] : (_p != null ? _p[0] : Vector3.zero);
        public Vector3 MotherSideCutEnd => IsCut ? _stumpM[0] : (_p != null ? _p[_p.Length - 1] : Vector3.zero);

        // ------------------------------------------------------------------ per frame
        public void Tick(float dt)
        {
            if (_p == null || babyEnd == null) return;
            bool hidden = !IsCut && NavelInside();
            _mr.enabled = !hidden;
            if (hidden) return;
            Solve();
            foreach (var c in clamps)
            {
                if (c.clamp == null) continue;
                Vector3[] seg = !IsCut || c.s <= _cut ? (IsCut ? _stumpB : _p) : _stumpM;
                float s = !IsCut || c.s <= _cut ? c.s : c.s - _cut;
                c.clamp.position = AtDistance(seg, s);
                Vector3 tg = TangentAtDistance(seg, s);
                Vector3 side = Vector3.Cross(tg, Vector3.up); if (side.sqrMagnitude < 1e-6f) side = Vector3.right;
                c.clamp.rotation = Quaternion.LookRotation(side.normalized, tg) * Quaternion.Euler(90, 0, 0);
            }
            _pulseT += dt * heartRate / 60f;
            BuildMesh();
        }

        void BuildMesh()
        {
            _v.Clear(); _n.Clear(); _uv.Clear(); _tri.Clear();
            float pulse = pulsation * (0.5f + 0.5f * Mathf.Sin(_pulseT * Mathf.PI * 2f));
            if (!IsCut) Tube(_p, 0f, pulse);
            else { Tube(_stumpB, 0f, pulse); Tube(_stumpM, _cut, pulse); }
            _mesh.Clear(); _mesh.SetVertices(_v); _mesh.SetNormals(_n); _mesh.SetUVs(0, _uv); _mesh.SetTriangles(_tri, 0);
            _mesh.RecalculateBounds();
        }

        void Tube(Vector3[] p, float s0, float pulse)
        {
            int baseV = _v.Count; Vector3 prevN = Vector3.up; float s = s0;
            for (int i = 0; i < p.Length; i++)
            {
                if (i > 0) s += Vector3.Distance(p[i - 1], p[i]);
                Vector3 tg = (p[Mathf.Min(p.Length - 1, i + 1)] - p[Mathf.Max(0, i - 1)]);
                tg = tg.sqrMagnitude > 1e-10f ? tg.normalized : Vector3.forward;
                Vector3 nrm = Vector3.Cross(tg, Vector3.Cross(prevN, tg));
                if (nrm.sqrMagnitude < 1e-8f) nrm = Vector3.Cross(tg, Vector3.right);
                nrm.Normalize(); prevN = nrm; Vector3 bin = Vector3.Cross(tg, nrm);
                float end = (i == 0 || i == p.Length - 1) ? 0.9f : 1f;
                float r = radius * (1f + 0.06f * pulse) * end * (1f + 0.05f * Mathf.Sin(s * 120f));
                for (int k = 0; k <= sides; k++)
                {
                    float a = k / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    _v.Add(p[i] + dir * r); _n.Add(dir); _uv.Add(new Vector2(k / (float)sides, s * 4f));
                }
            }
            for (int i = 0; i < p.Length - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a0 = baseV + i * (sides + 1) + k, a1 = a0 + 1, b0 = a0 + sides + 1, b1 = b0 + 1;
                    _tri.Add(a0); _tri.Add(b0); _tri.Add(a1); _tri.Add(a1); _tri.Add(b0); _tri.Add(b1);
                }
        }
    }
}
