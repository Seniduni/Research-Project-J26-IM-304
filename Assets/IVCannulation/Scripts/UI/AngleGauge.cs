using UnityEngine;
using UnityEngine.UI;

namespace IVSim
{
    /// <summary>
    /// A protractor drawn at the needle's skin entry point: skin line, needle line, arc and the angle in degrees,
    /// coloured green (ideal 15-30), yellow (acceptable) or red. In Easy mode faint guide lines show the ideal range.
    /// </summary>
    public class AngleGauge : MonoBehaviour
    {
        public LineRenderer skinLine, needleLine, arcLine, idealMinLine, idealMaxLine;
        public Material lineMaterial;
        public float length = 0.03f;
        public float arcRadius = 0.016f;

        Text label;
        Canvas labelCanvas;
        Material good, ok, bad, guide;

        void Start()
        {
            if (lineMaterial != null)
            {
                good = MakeMat(new Color(0.25f, 0.85f, 0.45f, 1f));
                ok = MakeMat(new Color(0.98f, 0.78f, 0.25f, 1f));
                bad = MakeMat(new Color(1f, 0.35f, 0.35f, 1f));
                guide = MakeMat(new Color(0.25f, 0.85f, 0.45f, 0.45f));
                if (skinLine != null) skinLine.sharedMaterial = MakeMat(new Color(0.95f, 0.95f, 0.95f, 1f));
                if (idealMinLine != null) idealMinLine.sharedMaterial = guide;
                if (idealMaxLine != null) idealMaxLine.sharedMaterial = guide;
            }
            labelCanvas = UIFactory.WorldCanvas("AngleLabel", transform, new Vector2(260, 70), 0.00012f);
            label = UIFactory.Label("Text", labelCanvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 44, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            SetVisible(false);
        }

        Material MakeMat(Color c)
        {
            var m = new Material(lineMaterial);
            m.SetColor("_BaseColor", c);
            return m;
        }

        void SetVisible(bool on)
        {
            foreach (var l in new[] { skinLine, needleLine, arcLine, idealMinLine, idealMaxLine })
                if (l != null) l.enabled = on;
            if (labelCanvas != null) labelCanvas.gameObject.SetActive(on);
        }

        void LateUpdate()
        {
            var s = IVSession.Instance;
            var c = s != null ? s.cannula : null;
            bool show = s != null && s.Profile != null && s.Profile.liveAngleGauge && c != null && c.LiveValid
                        && (c.IsHeld || c.IsInSkin) && !c.CatheterPlaced
                        && (c.IsInSkin || c.LiveDistance < 0.04f);
            SetVisible(show);
            if (!show) return;

            Vector3 P = c.LivePoint;
            Vector3 n = c.LiveNormal;
            Vector3 d = c.transform.forward;
            Vector3 flat = d - Vector3.Dot(d, n) * n;
            if (flat.sqrMagnitude < 1e-6f) return;
            flat.Normalize();
            Vector3 lift = n * 0.0006f;

            float angle = c.IsInSkin ? c.CurrentAngle : c.LiveAngle;
            string txt;
            Severity sev = s.ClassifyAngle(angle, out txt);
            Material m = sev == Severity.Good ? good : sev == Severity.Info ? ok : bad;

            Set(skinLine, P + lift, P + lift - flat * length);
            Set(needleLine, P + lift, P + lift - d * length);
            if (needleLine != null && m != null) needleLine.sharedMaterial = m;

            if (arcLine != null)
            {
                int k = 16;
                arcLine.positionCount = k + 1;
                for (int i = 0; i <= k; i++)
                {
                    Vector3 v = Vector3.Slerp(-flat, -d, i / (float)k);
                    arcLine.SetPosition(i, P + lift + v * arcRadius);
                }
                if (m != null) arcLine.sharedMaterial = m;
            }

            bool guides = s.Profile.showSiteMarkers;
            if (idealMinLine != null) { idealMinLine.enabled = guides; if (guides) Set(idealMinLine, P + lift, P + lift + Guide(flat, n, s.clinical.idealAngleMin) * length * 0.8f); }
            if (idealMaxLine != null) { idealMaxLine.enabled = guides; if (guides) Set(idealMaxLine, P + lift, P + lift + Guide(flat, n, s.clinical.idealAngleMax) * length * 0.8f); }

            if (label != null)
            {
                string bevel = Mathf.Abs(c.LiveRoll) <= s.clinical.bevelTolerance ? "bevel up" : "bevel " + Mathf.RoundToInt(c.LiveRoll) + "°";
                label.text = UIFactory.Tint(angle.ToString("0") + "°", UIFactory.Hex(sev == Severity.Info ? Severity.Warning : sev == Severity.Warning ? Severity.Error : sev))
                             + "  <size=30>" + bevel + "</size>";
                Vector3 lp = P - flat * (length * 0.9f) + n * 0.03f;
                labelCanvas.transform.position = lp;
                var camT = Camera.main != null ? Camera.main.transform : null;
                if (camT != null) labelCanvas.transform.rotation = Quaternion.LookRotation(lp - camT.position, camT.up);
            }
        }

        static Vector3 Guide(Vector3 flat, Vector3 n, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            return (-flat * Mathf.Cos(r) + n * Mathf.Sin(r)).normalized;
        }

        static void Set(LineRenderer l, Vector3 a, Vector3 b)
        {
            if (l == null) return;
            l.positionCount = 2;
            l.SetPosition(0, a);
            l.SetPosition(1, b);
        }
    }
}
