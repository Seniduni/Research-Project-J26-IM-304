using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace IVSim.EditorTools
{
    /// <summary>
    /// Minimal Wavefront OBJ exporter so the generated arm and veins can be opened in Blender
    /// (File > Import > Wavefront). Each mesh becomes its own object. Units: metres.
    /// Unity is left-handed, OBJ/Blender right-handed, so X is mirrored and triangle order flipped.
    /// </summary>
    public static class ObjExporter
    {
        public static void Export(Transform root, string path)
        {
            var sb = new StringBuilder();
            var ci = CultureInfo.InvariantCulture;
            sb.AppendLine("# IV Cannulation module export");
            int offset = 1;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null) continue;
                sb.AppendLine("o " + mf.gameObject.name.Replace(' ', '_'));
                Matrix4x4 m = root.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices)
                {
                    Vector3 p = m.MultiplyPoint3x4(v);
                    sb.AppendFormat(ci, "v {0:0.######} {1:0.######} {2:0.######}\n", -p.x, p.y, p.z);
                }
                foreach (var n in mesh.normals)
                {
                    Vector3 q = m.MultiplyVector(n).normalized;
                    sb.AppendFormat(ci, "vn {0:0.####} {1:0.####} {2:0.####}\n", -q.x, q.y, q.z);
                }
                var tris = mesh.triangles;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    int a = tris[i] + offset, b = tris[i + 1] + offset, c = tris[i + 2] + offset;
                    sb.AppendFormat(ci, "f {0}//{0} {2}//{2} {1}//{1}\n", a, b, c);
                }
                offset += mesh.vertexCount;
            }
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[IVSim] Exported OBJ to " + path);
        }
    }
}
