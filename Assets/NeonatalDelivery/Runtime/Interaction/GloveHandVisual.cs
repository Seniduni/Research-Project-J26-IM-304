// Procedural gloved-hand renderer (sterile nitrile gloves) for tracked hands, ghost demonstration hands and replay hands.
using UnityEngine;

namespace NeonatalDelivery
{
    public class GloveHandVisual : MonoBehaviour
    {
        public HandSide side;
        public Material material;
        public bool isGhost;
        public float scale = 1f;
        Transform[] _joints; Transform[] _bones; Transform _palm;
        static Mesh s_Sphere, s_Capsule;
        TrackedHand _src;
        public bool Visible { get => _visible; set { _visible = value; if (_root) _root.gameObject.SetActive(value); } }
        bool _visible = true; Transform _root;

        public void Bind(TrackedHand src) { _src = src; }

        void Build()
        {
            if (_root != null) return;
            if (s_Sphere == null) { var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); s_Sphere = g.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(g); }
            if (s_Capsule == null) { var g = GameObject.CreatePrimitive(PrimitiveType.Capsule); s_Capsule = g.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(g); }
            _root = new GameObject("Glove_" + side).transform; _root.SetParent(transform, false);
            _joints = new Transform[J.Count]; _bones = new Transform[J.Count];
            for (int i = 0; i < J.Count; i++)
            {
                if (i == J.Palm) continue;
                _joints[i] = Part("J" + i, s_Sphere);
                if (J.Parent[i] >= 0 && i != J.Palm) _bones[i] = Part("B" + i, s_Capsule);
            }
            _palm = Part("PalmPad", s_Sphere);
        }

        Transform Part(string n, Mesh m)
        {
            var go = new GameObject(n); go.transform.SetParent(_root, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material;
            r.shadowCastingMode = isGhost ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            return go.transform;
        }

        void LateUpdate() { if (_src != null) Draw(_src); }

        public void Draw(TrackedHand h)
        {
            Build();
            bool show = _visible && h != null && h.tracked;
            _root.gameObject.SetActive(show);
            if (!show) return;
            for (int i = 0; i < J.Count; i++)
            {
                if (i == J.Palm) continue;
                float r = Mathf.Max(0.0065f, h.radius[i]) * scale;
                _joints[i].position = h.pos[i]; _joints[i].localScale = Vector3.one * r * 2f;
                int p = J.Parent[i];
                if (_bones[i] != null && p >= 0)
                {
                    Vector3 a = h.pos[p == J.Palm ? J.Wrist : p], b = h.pos[i];
                    if (p == J.Wrist && i != J.ThumbMeta) a = Vector3.Lerp(h.pos[J.Wrist], h.pos[i], 0.15f);
                    Vector3 d = b - a; float len = d.magnitude;
                    if (len < 1e-4f) { _bones[i].gameObject.SetActive(false); continue; }
                    _bones[i].gameObject.SetActive(true);
                    _bones[i].position = (a + b) * 0.5f;
                    _bones[i].rotation = Quaternion.FromToRotation(Vector3.up, d / len);
                    _bones[i].localScale = new Vector3(r * 2f, len * 0.5f + r * 0.5f, r * 2f);
                }
            }
            // palm pad: flattened ellipsoid spanning the metacarpals
            Vector3 c = (h.pos[J.Wrist] + h.pos[J.IndexProx] + h.pos[J.LittleProx] + h.pos[J.MiddleProx]) * 0.25f;
            _palm.position = c;
            _palm.rotation = h.palmRot;
            float w = Vector3.Distance(h.pos[J.IndexProx], h.pos[J.LittleProx]) + 0.02f;
            float l = Vector3.Distance(h.pos[J.Wrist], h.pos[J.MiddleProx]) + 0.02f;
            _palm.localScale = new Vector3(w, 0.028f, l) * scale;
        }
    }
}
