using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    /// <summary>Simple visual consequences: haematoma, bruise, blood spill and infiltration swelling.</summary>
    public static class BloodFX
    {
        static readonly List<GameObject> spawned = new List<GameObject>();
        static Mesh disc;

        static Mesh Disc { get { if (disc == null) disc = MeshBuilder.Disc("FXDisc", 24); return disc; } }

        public static void ClearAll()
        {
            foreach (var g in spawned) if (g != null) Object.Destroy(g);
            spawned.Clear();
        }

        static GameObject Make(string name, Mesh mesh, Material mat, Vector3 pos, Quaternion rot)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            g.transform.rotation = rot;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            spawned.Add(g);
            return g;
        }

        static Mesh SphereMesh()
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var m = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            return m;
        }

        public static void Hematoma(Vector3 at, Vector3 skinNormal)
        {
            var s = IVSession.Instance;
            if (s == null) return;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, skinNormal);
            if (s.hematomaMaterial != null)
            {
                var g = Make("Haematoma", SphereMesh(), s.hematomaMaterial, at, rot);
                g.AddComponent<FXGrow>().Init(new Vector3(0.014f, 0.005f, 0.014f), 3f);
            }
            if (s.bruiseMaterial != null && s.arm != null)
            {
                Vector3 sp, n; float d;
                if (s.arm.ProbeSurface(at, 0.05f, out sp, out n, out d))
                {
                    var b = Make("Bruise", Disc, s.bruiseMaterial, sp + n * 0.0006f, Quaternion.FromToRotation(Vector3.up, n));
                    b.AddComponent<FXGrow>().Init(Vector3.one * 0.026f, 4f);
                }
            }
        }

        public static void Spill(Vector3 at, Vector3 normal, float size)
        {
            var s = IVSession.Instance;
            if (s == null || s.bloodMaterial == null) return;
            Vector3 p = at, n = normal;
            if (s.arm != null)
            {
                Vector3 sp, sn; float d;
                if (s.arm.ProbeSurface(at, 0.05f, out sp, out sn, out d)) { p = sp; n = sn; }
            }
            var g = Make("BloodSpill", Disc, s.bloodMaterial, p + n * 0.0008f, Quaternion.FromToRotation(Vector3.up, n));
            g.AddComponent<FXGrow>().Init(Vector3.one * size, 1.5f);
        }

        public static void Swelling(Vector3 at, Vector3 normal)
        {
            var s = IVSession.Instance;
            if (s == null || s.SkinOpaqueInstance == null) return;
            var g = Make("Infiltration", SphereMesh(), s.SkinOpaqueInstance, at, Quaternion.FromToRotation(Vector3.up, normal));
            g.AddComponent<FXGrow>().Init(new Vector3(0.03f, 0.008f, 0.04f), 2.5f);
        }
    }
}
