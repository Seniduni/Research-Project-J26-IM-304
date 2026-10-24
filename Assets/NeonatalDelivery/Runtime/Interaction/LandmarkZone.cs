// Anatomical landmark zone (sphere or capsule) attached to a marker on the baby/mother rig.
using UnityEngine;

namespace NeonatalDelivery
{
    public class LandmarkZone : MonoBehaviour
    {
        public LandmarkZoneId id;
        public float radius = 0.03f;
        public Transform capsuleEnd;        // optional: capsule from this transform to capsuleEnd
        public Color cueColor = new Color(0.2f, 0.9f, 1f, 0.35f);
        public bool isBaby = true;
        Renderer _cue; float _cueUntil; Color _cueCol;

        public Vector3 Center => transform.position;

        /// <summary>Signed distance from p to the zone surface (negative = inside).</summary>
        public float SignedDistance(Vector3 p)
        {
            if (capsuleEnd == null) return Vector3.Distance(p, transform.position) - radius;
            Vector3 a = transform.position, b = capsuleEnd.position, ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector3.Distance(p, a + ab * t) - radius;
        }

        public Vector3 ClosestSurfacePoint(Vector3 p)
        {
            Vector3 c = transform.position;
            if (capsuleEnd != null) { Vector3 a = transform.position, ab = capsuleEnd.position - a; c = a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude)); }
            Vector3 d = p - c; if (d.sqrMagnitude < 1e-8f) d = Vector3.up;
            return c + d.normalized * radius;
        }

        /// <summary>Show a soft translucent highlight (guided cue / fault marker) for a few seconds.</summary>
        public void Cue(Material ghost, Color c, float seconds)
        {
            if (_cue == null && ghost != null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(go.GetComponent<Collider>());
                go.name = "Cue_" + id; go.transform.SetParent(transform, false);
                _cue = go.GetComponent<Renderer>(); _cue.sharedMaterial = new Material(ghost);
                _cue.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (_cue == null) return;
            _cueCol = c; _cueUntil = Time.time + seconds; _cue.enabled = true;
        }

        void LateUpdate()
        {
            if (_cue == null) return;
            if (Time.time > _cueUntil) { _cue.enabled = false; return; }
            float s = radius * 2.15f;
            if (capsuleEnd != null) { _cue.transform.position = (transform.position + capsuleEnd.position) * 0.5f; s = Mathf.Max(s, Vector3.Distance(transform.position, capsuleEnd.position) + radius * 2f); }
            else _cue.transform.localPosition = Vector3.zero;
            _cue.transform.localScale = Vector3.one * s / Mathf.Max(1e-4f, transform.lossyScale.x);
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * 6f);
            var m = _cue.sharedMaterial; var col = _cueCol; col.a *= pulse;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        }
    }
}
