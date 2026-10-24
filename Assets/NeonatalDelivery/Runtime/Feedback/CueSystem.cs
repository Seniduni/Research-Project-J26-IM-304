// Real-time in-VR visual/haptic cues (component architecture: "Real-Time In-VR Visual & Haptic Cues").
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NeonatalDelivery
{
    public class CueSystem : MonoBehaviour
    {
        public HandPlacementEvaluator evaluator;
        public Material ghostMaterial;      // ND/Ghost
        public Camera head;
        public TMP_FontAsset font;
        public bool showTractionArrows = true;
        readonly List<GameObject> _marks = new List<GameObject>();
        LineRenderer _axisArrow, _tractionArrow;
        Renderer _flash; float _flashUntil; Color _flashCol;

        void Awake()
        {
            _axisArrow = MakeLine("AxisArrow", new Color(0.2f, 1f, 0.5f, 0.85f));
            _tractionArrow = MakeLine("TractionArrow", new Color(1f, 0.85f, 0.2f, 0.95f));
        }

        LineRenderer MakeLine(string n, Color c)
        {
            var go = new GameObject(n); go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>(); lr.positionCount = 5; lr.widthMultiplier = 0.006f; lr.useWorldSpace = true;
            lr.material = ghostMaterial != null ? new Material(ghostMaterial) : RigUtil.MakeMaterial("Universal Render Pipeline/Unlit", c);
            if (lr.material.HasProperty("_BaseColor")) lr.material.SetColor("_BaseColor", c);
            lr.startColor = lr.endColor = c; lr.enabled = false; lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        public void ShowArrow(LineRenderer lr, Vector3 from, Vector3 dir, float len, Color c)
        {
            if (!showTractionArrows || dir.sqrMagnitude < 1e-6f) { lr.enabled = false; return; }
            dir.Normalize(); Vector3 to = from + dir * len;
            Vector3 side = Vector3.Cross(dir, head != null ? (head.transform.position - to).normalized : Vector3.up).normalized * len * 0.18f;
            lr.SetPositions(new[] { from, to, to - dir * len * 0.25f + side, to, to - dir * len * 0.25f - side });
            lr.startColor = lr.endColor = c; if (lr.material.HasProperty("_BaseColor")) lr.material.SetColor("_BaseColor", c);
            lr.enabled = true;
        }
        public void ShowExpectedAxis(Vector3 from, Vector3 dir) { ShowArrow(_axisArrow, from, dir, 0.12f, new Color(0.2f, 1f, 0.5f, 0.8f)); }
        public void ShowTraction(Vector3 from, Vector3 force, bool ok)
        {
            float len = Mathf.Clamp(force.magnitude / 40f, 0f, 0.15f);
            if (len < 0.01f) { _tractionArrow.enabled = false; return; }
            ShowArrow(_tractionArrow, from, force, len, ok ? new Color(0.3f, 0.9f, 1f, 0.95f) : new Color(1f, 0.25f, 0.2f, 0.95f));
        }
        public void HideArrows() { _axisArrow.enabled = false; _tractionArrow.enabled = false; }

        public void CueZones(IEnumerable<LandmarkZoneId> ids, Color c, float seconds = 1.2f)
        {
            if (evaluator == null) return;
            foreach (var id in ids) foreach (var z in evaluator.zones) if (z != null && z.id == id) z.Cue(ghostMaterial, c, seconds);
        }

        public void MarkZone(LandmarkZoneId id, string label, Color c)
        {
            if (evaluator == null) return;
            foreach (var z in evaluator.zones)
                if (z != null && z.id == id) { z.Cue(ghostMaterial, new Color(c.r, c.g, c.b, 0.45f), 6f); Label(z.transform, Vector3.zero, label, c); break; }
        }

        public void MarkPoint(Vector3 p, string label, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(go.GetComponent<Collider>());
            go.transform.position = p; go.transform.localScale = Vector3.one * 0.02f;
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = ghostMaterial != null ? new Material(ghostMaterial) : RigUtil.MakeMaterial("Universal Render Pipeline/Unlit", c);
            if (r.sharedMaterial.HasProperty("_BaseColor")) r.sharedMaterial.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.6f));
            _marks.Add(go); Destroy(go, 8f);
            Label(null, p, label, c);
        }

        void Label(Transform follow, Vector3 p, string text, Color c)
        {
            var go = new GameObject("CueLabel");
            if (follow != null) { go.transform.SetParent(follow, false); go.transform.localPosition = Vector3.zero; }
            else go.transform.position = p;
            var tmp = go.AddComponent<TextMeshPro>(); tmp.text = text; tmp.fontSize = 0.35f; tmp.color = c; tmp.alignment = TextAlignmentOptions.Center;
            if (font != null) tmp.font = font;
            tmp.rectTransform.sizeDelta = new Vector2(0.4f, 0.1f);
            go.AddComponent<Billboard>().offset = new Vector3(0, 0.07f, 0);
            _marks.Add(go); Destroy(go, 8f);
        }

        public void Flash(Color c, float seconds)
        {
            if (head == null) return;
            if (_flash == null)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>());
                q.transform.SetParent(head.transform, false); q.transform.localPosition = new Vector3(0, 0, head.nearClipPlane + 0.05f); q.transform.localScale = new Vector3(0.5f, 0.4f, 1);
                _flash = q.GetComponent<Renderer>(); _flash.sharedMaterial = ghostMaterial != null ? new Material(ghostMaterial) : RigUtil.MakeMaterial("Universal Render Pipeline/Unlit", c);
            }
            _flashCol = c; _flashUntil = Time.time + seconds; _flash.enabled = true;
        }

        public void ClearMarks() { foreach (var m in _marks) if (m != null) Destroy(m); _marks.Clear(); }

        void Update()
        {
            if (_flash != null)
            {
                float k = Mathf.Clamp01((_flashUntil - Time.time) / 0.8f);
                if (k <= 0f) _flash.enabled = false;
                else { var c = _flashCol; c.a *= k; var m = _flash.sharedMaterial; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); }
            }
        }
    }

}
