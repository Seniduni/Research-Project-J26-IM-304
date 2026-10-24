// Umbilical cord: Verlet rope from the newborn's umbilicus back through the birth canal to the placenta,
// with pulsation (stops after delayed clamping time), clamps (pins), cutting (rope splits) and a procedural
// twisted three-vessel (2 arteries + 1 vein) Wharton's-jelly look.
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class UmbilicalCord : MonoBehaviour
    {
        public Transform babyEnd;          // ND_Z_NavelSkin
        public Transform placentaEnd;      // ND_Anchor_Placenta
        public float length = 0.55f;
        public int particles = 44;
        public float radius = 0.0085f;
        public int sides = 10;
        public int iterations = 14;
        public float damping = 0.985f;
        public Material material;
        public BirthCanalDeformer canal;
        [Range(0, 1)] public float pulsation = 1f;     // 1 = strongly pulsating, 0 = stopped (white, flat)
        public float heartRate = 140f;

        public struct Sphere { public Vector3 c; public float r; }
        public readonly List<Sphere> colliders = new List<Sphere>();
        public float floorY = -10f;

        Vector3[] _p, _prev; bool[] _pinned; Transform[] _pinT; float _seg;
        int _cutAfter = -1;
        public bool IsCut => _cutAfter >= 0;
        public float CutDistanceFromNavel => _cutAfter >= 0 ? (_cutAfter + 0.5f) * _seg : -1f;
        public readonly List<(Transform clamp, int index)> clamps = new List<(Transform, int)>();
        Mesh _mesh; Vector3[] _v; Vector3[] _n; Vector2[] _uv; int[] _tri;
        float _pulseT;

        void Awake() { Build(); }

        public void Build()
        {
            particles = Mathf.Max(8, particles);
            _p = new Vector3[particles]; _prev = new Vector3[particles]; _pinned = new bool[particles]; _pinT = new Transform[particles];
            _seg = length / (particles - 1);
            Vector3 a = babyEnd != null ? babyEnd.position : transform.position, b = placentaEnd != null ? placentaEnd.position : a + Vector3.down * length;
            for (int i = 0; i < particles; i++) { _p[i] = _prev[i] = Vector3.Lerp(a, b, i / (float)(particles - 1)); }
            _mesh = new Mesh { name = "ND_UmbilicalCord" }; _mesh.MarkDynamic();
            int rings = particles, vc = rings * (sides + 1);
            _v = new Vector3[vc]; _n = new Vector3[vc]; _uv = new Vector2[vc];
            _tri = new int[(rings - 1) * sides * 6];
            int t = 0;
            for (int i = 0; i < rings - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a0 = i * (sides + 1) + s, a1 = a0 + 1, b0 = a0 + sides + 1, b1 = b0 + 1;
                    _tri[t++] = a0; _tri[t++] = b0; _tri[t++] = a1; _tri[t++] = a1; _tri[t++] = b0; _tri[t++] = b1;
                }
            _mesh.vertices = _v; _mesh.triangles = _tri;
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = GetComponent<MeshRenderer>();
            if (material == null) material = DefaultMaterial();
            mr.sharedMaterial = material;
            transform.position = Vector3.zero; transform.rotation = Quaternion.identity; transform.localScale = Vector3.one;
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

        public void ResetRope()
        {
            _cutAfter = -1; clamps.Clear();
            for (int i = 0; i < particles; i++) { _pinned[i] = false; _pinT[i] = null; }
            Vector3 a = babyEnd.position, b = placentaEnd.position;
            for (int i = 0; i < particles; i++) _p[i] = _prev[i] = Vector3.Lerp(a, b, i / (float)(particles - 1));
        }

        /// <summary>Attach a clamp at the rope point closest to 'world'. Returns distance from the umbilicus (m), or -1.</summary>
        public float AttachClamp(Transform clamp, Vector3 world, float maxDist = 0.03f)
        {
            int best = -1; float bd = maxDist;
            for (int i = 1; i < particles - 1; i++) { float d = Vector3.Distance(world, _p[i]); if (d < bd) { bd = d; best = i; } }
            if (best < 0) return -1;
            clamps.Add((clamp, best));
            return best * _seg;
        }

        public float NearestDistanceFromNavel(Vector3 world, out float dist)
        {
            int best = 0; dist = 9f;
            for (int i = 0; i < particles; i++) { float d = Vector3.Distance(world, _p[i]); if (d < dist) { dist = d; best = i; } }
            return best * _seg;
        }

        /// <summary>Cut the cord near 'world'. Returns distance of the cut from the umbilicus (m) or -1 if the blades are not on the cord.</summary>
        public float TryCut(Vector3 world, float maxDist = 0.018f)
        {
            if (IsCut) return -1;
            float s = NearestDistanceFromNavel(world, out float d);
            if (d > maxDist) return -1;
            _cutAfter = Mathf.Clamp(Mathf.RoundToInt(s / _seg), 1, particles - 2);
            return (_cutAfter + 0.5f) * _seg;
        }

        public Vector3 PointAt(float distanceFromNavel) { int i = Mathf.Clamp(Mathf.RoundToInt(distanceFromNavel / _seg), 0, particles - 1); return _p[i]; }
        public Vector3 TangentAt(int i) { int a = Mathf.Max(0, i - 1), b = Mathf.Min(particles - 1, i + 1); return (_p[b] - _p[a]).normalized; }
        public int IndexAt(float distanceFromNavel) { return Mathf.Clamp(Mathf.RoundToInt(distanceFromNavel / _seg), 0, particles - 1); }

        public void Tick(float dt)
        {
            if (_p == null || babyEnd == null || placentaEnd == null || dt <= 0f) return;
            dt = Mathf.Min(dt, 1f / 30f);
            Vector3 g = Physics.gravity * dt * dt;
            for (int i = 0; i < particles; i++)
            {
                Vector3 cur = _p[i];
                _p[i] += (cur - _prev[i]) * damping + g;
                _prev[i] = cur;
            }
            for (int it = 0; it < iterations; it++)
            {
                _p[0] = babyEnd.position;
                if (!IsCut || _cutAfter < particles - 1) _p[particles - 1] = placentaEnd.position;
                foreach (var c in clamps) { if (c.clamp != null && c.index > 0) { } }
                for (int i = 0; i < particles - 1; i++)
                {
                    if (i == _cutAfter) continue;
                    Vector3 d = _p[i + 1] - _p[i]; float len = d.magnitude; if (len < 1e-6f) continue;
                    float diff = (len - _seg) / len;
                    float wa = i == 0 ? 0f : 0.5f, wb = (i + 1 == particles - 1) ? 0f : 0.5f;
                    if (wa + wb <= 0) continue;
                    float s = 1f / (wa + wb);
                    _p[i] += d * diff * wa * s; _p[i + 1] -= d * diff * wb * s;
                }
                Collide();
            }
            _p[0] = babyEnd.position; _p[particles - 1] = placentaEnd.position;
            // clamps ride on the cord
            foreach (var c in clamps)
            {
                if (c.clamp == null) continue;
                c.clamp.position = _p[c.index];
                Vector3 tg = TangentAt(c.index);
                if (tg.sqrMagnitude > 1e-6f) c.clamp.rotation = Quaternion.LookRotation(Vector3.Cross(tg, Vector3.up).normalized + Vector3.up * 0.001f, tg) * Quaternion.Euler(90, 0, 0);
            }
            _pulseT += dt * heartRate / 60f;
            BuildMesh();
        }

        void Collide()
        {
            for (int i = 1; i < particles - 1; i++)
            {
                Vector3 p = _p[i];
                foreach (var s in colliders)
                {
                    Vector3 d = p - s.c; float r = s.r + radius; float m = d.sqrMagnitude;
                    if (m < r * r && m > 1e-10f) p = s.c + d / Mathf.Sqrt(m) * r;
                }
                // inside the mother: keep the cord within the birth canal around its axis
                if (canal != null && canal.Ready)
                {
                    float o = canal.OutsideDistance(p);
                    if (o < 0f)
                    {
                        Vector3 axisP = canal.IntroitusCenter + canal.OutwardAxis * o;
                        Vector3 off = p - axisP; float maxR = Mathf.Max(0.004f, canal.IntroitusRadius * 0.6f);
                        if (off.magnitude > maxR) p = axisP + off.normalized * maxR;
                    }
                }
                if (p.y < floorY + radius) p.y = floorY + radius;
                _p[i] = p;
            }
        }

        void BuildMesh()
        {
            float pulse = pulsation * (0.5f + 0.5f * Mathf.Sin(_pulseT * Mathf.PI * 2f));
            Vector3 prevN = Vector3.up;
            int vi = 0;
            for (int i = 0; i < particles; i++)
            {
                Vector3 tg = TangentAt(i);
                if (tg.sqrMagnitude < 1e-8f) tg = Vector3.forward;
                Vector3 nrm = Vector3.Cross(tg, Vector3.Cross(prevN, tg)).normalized;
                if (nrm.sqrMagnitude < 1e-6f) nrm = Vector3.Cross(tg, Vector3.right).normalized;
                prevN = nrm; Vector3 bin = Vector3.Cross(tg, nrm);
                bool endCap = (i == _cutAfter || i == _cutAfter + 1);
                float r = radius * (1f + 0.06f * pulse) * (endCap ? 0.92f : 1f);
                // coil bumps
                float coil = 1f + 0.05f * Mathf.Sin(i * 1.9f);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    Vector3 dir = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    _v[vi] = _p[i] + dir * r * coil; _n[vi] = dir;
                    _uv[vi] = new Vector2(s / (float)sides, i * _seg * 4f);
                    vi++;
                }
            }
            _mesh.vertices = _v; _mesh.normals = _n; _mesh.uv = _uv;
            // hide the segment across the cut by collapsing it
            if (IsCut)
            {
                var tris = (int[])_tri.Clone();
                int start = _cutAfter * sides * 6;
                for (int k = start; k < start + sides * 6 && k < tris.Length; k++) tris[k] = 0;
                _mesh.triangles = tris;
            }
            else if (_mesh.triangles.Length != _tri.Length || _mesh.triangles[0] != _tri[0]) _mesh.triangles = _tri;
            _mesh.RecalculateBounds();
        }

        public Vector3 BabySideCutEnd => IsCut ? _p[_cutAfter] : _p[0];
        public Vector3 MotherSideCutEnd => IsCut ? _p[_cutAfter + 1] : _p[particles - 1];
    }
}
