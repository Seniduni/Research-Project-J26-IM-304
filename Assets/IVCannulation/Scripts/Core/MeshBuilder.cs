using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IVSim
{
    /// <summary>
    /// Small procedural-mesh toolkit used for the arm, veins, fingers, tourniquet band,
    /// catheter tube and needle tip. Everything is built from "rings" (ellipses) that are
    /// lofted together, which is the same idea as Blender's "skin"/"loft" tools.
    /// </summary>
    public static class MeshBuilder
    {
        public struct Ring
        {
            public Vector3 center;
            public Vector3 forward;   // direction the loft travels
            public Vector3 up;        // ellipse "ry" axis
            public float rx;          // half-size along right = Cross(up, forward)
            public float ry;          // half-size along up
            public float axial;       // -1..1 : bends normals toward -forward/+forward (rounded ends)
        }

        public class Builder
        {
            public readonly List<Vector3> verts = new List<Vector3>();
            public readonly List<Vector3> norms = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> tris = new List<int>();

            public Mesh ToMesh(string name)
            {
                var m = new Mesh();
                m.name = name;
                m.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                m.SetVertices(verts);
                m.SetNormals(norms);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                m.RecalculateTangents();
                return m;
            }

            public void WriteTo(Mesh m)
            {
                m.Clear();
                m.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                m.SetVertices(verts);
                m.SetNormals(norms);
                m.SetUVs(0, uvs);
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
            }

            public void Clear()
            {
                verts.Clear(); norms.Clear(); uvs.Clear(); tris.Clear();
            }
        }

        static Vector3 RightOf(Ring r)
        {
            return Vector3.Cross(r.up, r.forward).normalized;
        }

        /// <summary>Lofts a list of rings into a tube surface. Optional flat caps.</summary>
        public static void Loft(Builder b, IList<Ring> rings, int segments, bool capStart, bool capEnd, bool closeLoop = false)
        {
            int ringCount = rings.Count;
            if (ringCount < 2) return;
            int start = b.verts.Count;
            int stride = segments + 1;
            int total = closeLoop ? ringCount + 1 : ringCount;

            for (int ii = 0; ii < total; ii++)
            {
                Ring r = rings[ii % ringCount];
                Vector3 right = RightOf(r);
                Vector3 up = r.up.normalized;
                float axial = Mathf.Clamp(r.axial, -1f, 1f);
                float radialW = Mathf.Sqrt(Mathf.Max(0f, 1f - axial * axial));
                for (int j = 0; j <= segments; j++)
                {
                    float phi = (j / (float)segments) * Mathf.PI * 2f;
                    float c = Mathf.Cos(phi), s = Mathf.Sin(phi);
                    Vector3 p = r.center + right * (c * r.rx) + up * (s * r.ry);
                    Vector3 radial = (right * (c / Mathf.Max(r.rx, 1e-6f)) + up * (s / Mathf.Max(r.ry, 1e-6f))).normalized;
                    Vector3 n = (radial * radialW + r.forward.normalized * axial).normalized;
                    b.verts.Add(p);
                    b.norms.Add(n);
                    b.uvs.Add(new Vector2(j / (float)segments, ii / (float)(total - 1)));
                }
            }

            for (int i = 0; i < total - 1; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int a = start + i * stride + j;
                    int bb = a + 1;
                    int c = a + stride;
                    int d = c + 1;
                    b.tris.Add(a); b.tris.Add(bb); b.tris.Add(c);
                    b.tris.Add(bb); b.tris.Add(d); b.tris.Add(c);
                }
            }

            if (!closeLoop && capStart) AddCap(b, rings[0], segments, false);
            if (!closeLoop && capEnd) AddCap(b, rings[ringCount - 1], segments, true);
        }

        static void AddCap(Builder b, Ring r, int segments, bool isEnd)
        {
            Vector3 right = RightOf(r);
            Vector3 up = r.up.normalized;
            Vector3 n = isEnd ? r.forward.normalized : -r.forward.normalized;
            int centerIndex = b.verts.Count;
            b.verts.Add(r.center); b.norms.Add(n); b.uvs.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j <= segments; j++)
            {
                float phi = (j / (float)segments) * Mathf.PI * 2f;
                float c = Mathf.Cos(phi), s = Mathf.Sin(phi);
                b.verts.Add(r.center + right * (c * r.rx) + up * (s * r.ry));
                b.norms.Add(n);
                b.uvs.Add(new Vector2(0.5f + c * 0.5f, 0.5f + s * 0.5f));
            }
            for (int j = 0; j < segments; j++)
            {
                int p0 = centerIndex + 1 + j;
                int p1 = p0 + 1;
                if (isEnd) { b.tris.Add(centerIndex); b.tris.Add(p0); b.tris.Add(p1); }
                else { b.tris.Add(centerIndex); b.tris.Add(p1); b.tris.Add(p0); }
            }
        }

        /// <summary>Computes parallel-transport frames along a polyline.</summary>
        public static void Frames(IList<Vector3> pts, out Vector3[] fwd, out Vector3[] up, Vector3 preferredUp)
        {
            int n = pts.Count;
            fwd = new Vector3[n];
            up = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 t;
                if (i == 0) t = pts[1] - pts[0];
                else if (i == n - 1) t = pts[n - 1] - pts[n - 2];
                else t = pts[i + 1] - pts[i - 1];
                if (t.sqrMagnitude < 1e-12f) t = i > 0 ? fwd[i - 1] : Vector3.forward;
                fwd[i] = t.normalized;
            }
            Vector3 u0 = preferredUp - Vector3.Dot(preferredUp, fwd[0]) * fwd[0];
            if (u0.sqrMagnitude < 1e-6f)
            {
                Vector3 alt = Mathf.Abs(fwd[0].y) < 0.9f ? Vector3.up : Vector3.right;
                u0 = alt - Vector3.Dot(alt, fwd[0]) * fwd[0];
            }
            up[0] = u0.normalized;
            for (int i = 1; i < n; i++)
            {
                Quaternion q = Quaternion.FromToRotation(fwd[i - 1], fwd[i]);
                Vector3 u = q * up[i - 1];
                u = (u - Vector3.Dot(u, fwd[i]) * fwd[i]).normalized;
                up[i] = u;
            }
        }

        /// <summary>
        /// Circular tube through points with per-point radius. Ends can be rounded (hemispheres) or flat.
        /// </summary>
        public static void Tube(Builder b, IList<Vector3> pts, IList<float> radii, int segments,
                                bool roundStart, bool roundEnd, bool flatCaps = true, int roundRings = 5)
        {
            if (pts.Count < 2) return;
            Vector3[] fwd, up;
            Frames(pts, out fwd, out up, Vector3.up);
            var rings = new List<Ring>();

            if (roundStart)
            {
                float r0 = radii[0];
                for (int k = roundRings; k >= 1; k--)
                {
                    float a = (k / (float)roundRings) * Mathf.PI * 0.5f;
                    rings.Add(new Ring
                    {
                        center = pts[0] - fwd[0] * (r0 * Mathf.Sin(a)),
                        forward = fwd[0], up = up[0],
                        rx = Mathf.Max(r0 * Mathf.Cos(a), r0 * 0.02f),
                        ry = Mathf.Max(r0 * Mathf.Cos(a), r0 * 0.02f),
                        axial = -Mathf.Sin(a)
                    });
                }
            }
            for (int i = 0; i < pts.Count; i++)
            {
                rings.Add(new Ring { center = pts[i], forward = fwd[i], up = up[i], rx = radii[i], ry = radii[i], axial = 0f });
            }
            if (roundEnd)
            {
                int last = pts.Count - 1;
                float r1 = radii[last];
                for (int k = 1; k <= roundRings; k++)
                {
                    float a = (k / (float)roundRings) * Mathf.PI * 0.5f;
                    rings.Add(new Ring
                    {
                        center = pts[last] + fwd[last] * (r1 * Mathf.Sin(a)),
                        forward = fwd[last], up = up[last],
                        rx = Mathf.Max(r1 * Mathf.Cos(a), r1 * 0.02f),
                        ry = Mathf.Max(r1 * Mathf.Cos(a), r1 * 0.02f),
                        axial = Mathf.Sin(a)
                    });
                }
            }
            Loft(b, rings, segments, flatCaps && !roundStart, flatCaps && !roundEnd);
        }

        public static Mesh TubeMesh(string name, IList<Vector3> pts, IList<float> radii, int segments, bool roundStart, bool roundEnd)
        {
            var b = new Builder();
            Tube(b, pts, radii, segments, roundStart, roundEnd);
            return b.ToMesh(name);
        }

        /// <summary>Flat disc in the XZ plane facing +Y, radius 0.5 (unit diameter).</summary>
        public static Mesh Disc(string name, int segments = 24)
        {
            var b = new Builder();
            b.verts.Add(Vector3.zero); b.norms.Add(Vector3.up); b.uvs.Add(new Vector2(0.5f, 0.5f));
            for (int j = 0; j <= segments; j++)
            {
                float a = j / (float)segments * Mathf.PI * 2f;
                b.verts.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
                b.norms.Add(Vector3.up);
                b.uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }
            for (int j = 0; j < segments; j++)
            {
                b.tris.Add(0); b.tris.Add(j + 2); b.tris.Add(j + 1);
            }
            return b.ToMesh(name);
        }

        /// <summary>A cone along +Z from z=0 (base, radius r) to z=length (point). Used for the needle bevel tip.</summary>
        public static Mesh Cone(string name, float radius, float length, int segments = 12)
        {
            var pts = new List<Vector3>();
            var radii = new List<float>();
            int steps = 6;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                pts.Add(new Vector3(0, 0, t * length));
                radii.Add(Mathf.Lerp(radius, radius * 0.04f, t));
            }
            return TubeMesh(name, pts, radii, segments, false, false);
        }

        /// <summary>Uniform Catmull-Rom interpolation through control points.</summary>
        public static List<Vector3> CatmullRom(IList<Vector3> cps, int samplesPerSpan)
        {
            var result = new List<Vector3>();
            int n = cps.Count;
            if (n == 0) return result;
            if (n == 1) { result.Add(cps[0]); return result; }
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = i == 0 ? cps[0] * 2f - cps[1] : cps[i - 1];
                Vector3 p1 = cps[i];
                Vector3 p2 = cps[i + 1];
                Vector3 p3 = i + 2 < n ? cps[i + 2] : cps[n - 1] * 2f - cps[n - 2];
                for (int s = 0; s < samplesPerSpan; s++)
                {
                    float t = s / (float)samplesPerSpan;
                    result.Add(CR(p0, p1, p2, p3, t));
                }
            }
            result.Add(cps[n - 1]);
            return result;
        }

        public static Vector3 CR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        public static float CR1(float p0, float p1, float p2, float p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
    }
}
