using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// A spherical region that rates a cannulation site (e.g. "straight forearm cephalic = Ideal",
    /// "cephalic at the wrist = Avoid"). Outside every zone the vein's own default rating is used.
    /// In Easy mode the zone's marker disc is shown on the skin.
    /// </summary>
    public class SiteZone : MonoBehaviour
    {
        static readonly List<SiteZone> zones = new List<SiteZone>();

        public string zoneName = "Site";
        public SiteRating rating = SiteRating.Ideal;
        public float radius = 0.03f;
        [TextArea(2, 4)] public string note = "";
        [Tooltip("Disc shown on the skin in Easy mode")] public GameObject marker;

        void OnEnable() { if (!zones.Contains(this)) zones.Add(this); }
        void OnDisable() { zones.Remove(this); }

        public static void ShowMarkers(bool on)
        {
            foreach (var z in zones) if (z.marker != null) z.marker.SetActive(on);
        }

        public static SiteZone Find(Vector3 world)
        {
            SiteZone best = null;
            float bestD = float.MaxValue;
            foreach (var z in zones)
            {
                float d = Vector3.Distance(world, z.transform.position);
                if (d <= z.radius && d / z.radius < bestD) { bestD = d / z.radius; best = z; }
            }
            return best;
        }

        /// <summary>Rates a chosen site: zone rating if inside one, else the vessel's default.</summary>
        public static SiteRating Rate(Vector3 world, VeinPath vessel, out string note)
        {
            var z = Find(world);
            if (z != null) { note = z.note; return z.rating; }
            if (vessel == null) { note = "No palpable vein at this point."; return SiteRating.NotSuitable; }
            if (vessel.vesselType == VesselType.Artery) { note = "This is an artery (pulsating). Never cannulate an artery for IV access."; return SiteRating.NotSuitable; }
            note = vessel.siteNote;
            return vessel.defaultRating;
        }

        void OnDrawGizmos()
        {
            Color c = rating == SiteRating.Ideal ? Color.green : rating == SiteRating.Acceptable ? Color.yellow : Color.red;
            c.a = 0.35f;
            Gizmos.color = c;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
