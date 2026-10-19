// MR Operating Theatre - scene bootstraps for the ward demo and the asset gallery.
using UnityEngine;

namespace MROT
{
    /// <summary>Asset gallery: every model on its own pedestal.</summary>
    public class GalleryBootstrap : MonoBehaviour
    {
        void Start()
        {
            foreach (var t in FindObjectsByType<EquipmentTag>(FindObjectsSortMode.None))
            {
                if (t.GetComponent<Collider>() != null) continue;
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                var bc = t.gameObject.AddComponent<BoxCollider>();
                bc.center = t.transform.InverseTransformPoint(b.center);
                var s = t.transform.InverseTransformVector(b.size);
                bc.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            }
            PlayerRig.Create(new Vector3(0, 0, -4f), 0f);
            var ex = gameObject.AddComponent<WardExplorer>();
            var probe = new GameObject("Probe").AddComponent<ReflectionProbe>();
            probe.transform.position = new Vector3(0, 1.5f, 6f);
            probe.size = new Vector3(40, 6, 40);
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;
        }
    }
}
