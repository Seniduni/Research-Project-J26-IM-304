// Drives the introitus/canal ring bones from the newborn's girth as it passes each ring plane (crowning stretch,
// "ring of fire", perineal bulge) and renders perineal tears as a consequence.
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public class BirthCanalDeformer : MonoBehaviour
    {
        public struct Probe { public Transform t; public float radius; }
        public readonly List<Probe> probes = new List<Probe>();
        public float openSpeed = 0.25f, closeSpeed = 0.06f;     // m/s
        public float extraPosteriorStretch = 0.12f;
        [Range(0, 1)] public float tear;                         // 0 none, 0.5 2nd degree, 1 3rd degree
        public Transform perineum;
        public bool Ready { get; private set; }

        class Ring { public Transform[] b; public Vector3[] rest; public Vector3 center; public float restRadius; public float current; public float depth; }
        readonly List<Ring> _rings = new List<Ring>();
        Vector3 _inwardL, _anteriorL;  // in Perineum-local space
        public Vector3 OutwardAxis { get; private set; } = Vector3.forward;
        public Vector3 AnteriorAxis { get; private set; } = Vector3.up;
        public Vector3 LateralAxis { get; private set; } = Vector3.right;
        public Vector3 IntroitusCenter { get; private set; }
        public float IntroitusRadius => _rings.Count > 0 ? _rings[0].current : 0f;
        public float MaxRadius { get; private set; } = 0.05f;
        public float Stretch01 => _rings.Count > 0 ? Mathf.Clamp01((_rings[0].current - _rings[0].restRadius) / Mathf.Max(0.001f, MaxRadius - _rings[0].restRadius)) : 0f;
        public float Bulge { get; private set; }

        public void Init(Transform motherRoot)
        {
            var bones = RigUtil.MapBones(motherRoot);
            bones.TryGetValue("Perineum", out perineum);
            if (perineum == null) { ND.Error("Perineum bone not found - birth canal rig unavailable"); return; }
            _rings.Clear();
            AddRing(bones, "Introitus_{0:00}", 8);
            AddRing(bones, "CanalA_{0:00}", 6);
            AddRing(bones, "CanalB_{0:00}", 6);
            if (_rings.Count < 2) { ND.Error("Canal ring bones missing"); return; }
            _inwardL = (_rings[_rings.Count - 1].center - _rings[0].center).normalized;
            var r0 = _rings[0];
            Vector3 ant = r0.rest[4] - r0.rest[0];                                // Introitus_04 anterior, _00 posterior
            _anteriorL = Vector3.ProjectOnPlane(ant, _inwardL).normalized;
            for (int i = 0; i < _rings.Count; i++) _rings[i].depth = Vector3.Dot(_rings[i].center - r0.center, _inwardL);
            Ready = true; UpdateAxes();
        }

        void AddRing(Dictionary<string, Transform> bones, string fmt, int n)
        {
            var r = new Ring { b = new Transform[n], rest = new Vector3[n] };
            Vector3 c = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                if (!bones.TryGetValue(string.Format(fmt, i), out r.b[i])) return;
                r.rest[i] = perineum.InverseTransformPoint(r.b[i].position);
                c += r.rest[i];
            }
            r.center = c / n;
            float rr = 0; for (int i = 0; i < n; i++) rr += (r.rest[i] - r.center).magnitude; r.restRadius = rr / n; r.current = r.restRadius;
            _rings.Add(r);
        }

        void UpdateAxes()
        {
            OutwardAxis = -perineum.TransformDirection(_inwardL).normalized;
            AnteriorAxis = perineum.TransformDirection(_anteriorL).normalized;
            LateralAxis = Vector3.Cross(AnteriorAxis, OutwardAxis).normalized;
            IntroitusCenter = perineum.TransformPoint(_rings[0].center);
        }

        /// <summary>Signed distance of a world point along the outward axis from the introitus plane (+ = outside the mother).</summary>
        public float OutsideDistance(Vector3 p) { return Vector3.Dot(p - IntroitusCenter, OutwardAxis); }

        public void Tick(float dt)
        {
            if (!Ready) return;
            UpdateAxes();
            float bulgeTarget = 0f;
            for (int ri = 0; ri < _rings.Count; ri++)
            {
                var r = _rings[ri];
                Vector3 cW = perineum.TransformPoint(r.center);
                Vector3 inW = perineum.TransformDirection(_inwardL).normalized;
                float need = r.restRadius;
                foreach (var p in probes)
                {
                    if (p.t == null) continue;
                    Vector3 d = p.t.position - cW;
                    float s = Vector3.Dot(d, inW);
                    float lateral = (d - inW * s).magnitude;
                    float rad = p.radius;
                    if (Mathf.Abs(s) < rad)
                    {
                        float cross = Mathf.Sqrt(rad * rad - s * s);
                        need = Mathf.Max(need, cross + lateral * 0.8f);
                        if (ri == 0 && s > -rad * 0.2f && s < rad * 0.9f) bulgeTarget = Mathf.Max(bulgeTarget, 0.022f * Mathf.Sin(Mathf.Clamp01(s / rad + 0.2f) * Mathf.PI));
                    }
                }
                need = Mathf.Min(need, MaxRadius * (ri == 0 ? 1.1f : 1.0f));
                float sp = need > r.current ? openSpeed : closeSpeed;
                r.current = Mathf.MoveTowards(r.current, need, sp * dt);
                Apply(r, ri == 0);
            }
            Bulge = Mathf.MoveTowards(Bulge, bulgeTarget, dt * 0.08f);
        }

        void Apply(Ring r, bool introitus)
        {
            int n = r.b.Length;
            for (int i = 0; i < n; i++)
            {
                Vector3 v = r.rest[i] - r.center;
                Vector3 axial = _inwardL * Vector3.Dot(v, _inwardL);
                Vector3 radial = v - axial; float rr = radial.magnitude; if (rr < 1e-5f) continue;
                float post = Mathf.Max(0f, Mathf.Cos(2f * Mathf.PI * i / n));     // index 0 = posterior (perineum)
                float target = Mathf.Max(rr, r.current * (1f + extraPosteriorStretch * post));
                if (introitus && tear > 0f) target += tear * 0.012f * Mathf.Pow(post, 3f);
                Vector3 p = r.center + axial + radial / rr * target;
                if (introitus) p -= _inwardL * Bulge * (0.35f + 0.65f * post);
                r.b[i].position = perineum.TransformPoint(p);
            }
        }

        public void SetMaxRadiusFromHead(float headRadius) { MaxRadius = Mathf.Max(0.035f, headRadius * 1.04f); }
    }
}
