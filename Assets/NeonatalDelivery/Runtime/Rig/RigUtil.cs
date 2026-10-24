// Bone lookup, clip lookup and small math helpers (incl. Kabsch rigid alignment for mannequin calibration).
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public static class RigUtil
    {
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        public static Dictionary<string, Transform> MapBones(Transform root)
        {
            var d = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (!d.ContainsKey(t.name)) d[t.name] = t;
            return d;
        }

        /// <summary>Find an imported clip whose name equals or ends with the given short name (FBX takes are "Rig|Clip").</summary>
        public static AnimationClip FindClip(IList<AnimationClip> clips, string shortName)
        {
            if (clips == null) return null;
            AnimationClip best = null;
            foreach (var c in clips)
            {
                if (c == null) continue;
                if (c.name == shortName) return c;
                if (c.name.EndsWith("|" + shortName) || c.name.EndsWith(shortName))
                {
                    if (best == null || c.name.StartsWith("ND_")) best = c;
                }
            }
            return best;
        }

        public static float Smooth01(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        public static float Remap(float v, float a, float b, float c, float d) { return Mathf.Lerp(c, d, Mathf.InverseLerp(a, b, v)); }

        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { return v - Vector3.Dot(v, n) * n; }

        /// <summary>Signed angle of v around axis, measured from reference.</summary>
        public static float SignedAngle(Vector3 reference, Vector3 v, Vector3 axis)
        {
            return Vector3.SignedAngle(ProjectOnPlane(reference, axis), ProjectOnPlane(v, axis), axis);
        }

        /// <summary>
        /// Kabsch / Horn rigid alignment: rotation R and translation t minimising |R*a_i + t - b_i|^2.
        /// Implemented with Horn's quaternion method (4x4 symmetric eigen problem via power iteration) - robust for 3+ points.
        /// </summary>
        public static bool RigidAlign(IList<Vector3> a, IList<Vector3> b, out Quaternion rotation, out Vector3 translation)
        {
            rotation = Quaternion.identity; translation = Vector3.zero;
            int n = Mathf.Min(a.Count, b.Count);
            if (n < 2) return false;
            Vector3 ca = Vector3.zero, cb = Vector3.zero;
            for (int i = 0; i < n; i++) { ca += a[i]; cb += b[i]; }
            ca /= n; cb /= n;
            float Sxx = 0, Sxy = 0, Sxz = 0, Syx = 0, Syy = 0, Syz = 0, Szx = 0, Szy = 0, Szz = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = a[i] - ca, q = b[i] - cb;
                Sxx += p.x * q.x; Sxy += p.x * q.y; Sxz += p.x * q.z;
                Syx += p.y * q.x; Syy += p.y * q.y; Syz += p.y * q.z;
                Szx += p.z * q.x; Szy += p.z * q.y; Szz += p.z * q.z;
            }
            double[,] N = new double[4, 4]
            {
                { Sxx + Syy + Szz, Syz - Szy,        Szx - Sxz,        Sxy - Syx },
                { Syz - Szy,       Sxx - Syy - Szz,  Sxy + Syx,        Szx + Sxz },
                { Szx - Sxz,       Sxy + Syx,       -Sxx + Syy - Szz,  Syz + Szy },
                { Sxy - Syx,       Szx + Sxz,        Syz + Szy,       -Sxx - Syy + Szz }
            };
            // shift to make it positive definite, then power-iterate for the dominant eigenvector
            double shift = 0; for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) shift += System.Math.Abs(N[i, j]);
            for (int i = 0; i < 4; i++) N[i, i] += shift;
            double[] v = { 1, 0.01, 0.01, 0.01 };
            for (int it = 0; it < 200; it++)
            {
                double[] w = new double[4];
                for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) w[i] += N[i, j] * v[j];
                double len = System.Math.Sqrt(w[0] * w[0] + w[1] * w[1] + w[2] * w[2] + w[3] * w[3]);
                if (len < 1e-12) return false;
                for (int i = 0; i < 4; i++) v[i] = w[i] / len;
            }
            rotation = new Quaternion((float)v[1], (float)v[2], (float)v[3], (float)v[0]);
            rotation = Quaternion.Normalize(rotation);
            translation = cb - rotation * ca;
            return true;
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        public static Material MakeMaterial(string shaderName, Color c)
        {
            var sh = Shader.Find(shaderName);
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null) sh = Shader.Find("Unlit/Color");
            var m = new Material(sh);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            return m;
        }
    }
}

namespace NeonatalDelivery
{
    public static class NDUtil
    {
        /// <summary>Unity-null-safe "get or add" (GetComponent returns a fake-null object in the Editor, so '??' must not be used).</summary>
        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            return go.TryGetComponent<T>(out var c) ? c : go.AddComponent<T>();
        }
    }
}
