// MR Operating Theatre - shared VR / desktop interaction framework
using System;
using UnityEngine;

namespace MROT
{
    public enum PointerSource { Mouse, LeftHand, RightHand, Gaze }

    public struct PointerEvent
    {
        public PointerSource source;
        public Vector3 point;
        public Ray ray;
    }

    /// <summary>Anything that can be pointed at and selected (buttons, equipment, doors).</summary>
    public interface IPointerTarget
    {
        void OnHoverEnter(PointerEvent e);
        void OnHoverExit(PointerEvent e);
        void OnSelect(PointerEvent e);
        bool IsInteractable { get; }
    }

    /// <summary>Global events so other modules can listen (e.g. the orientation module, analytics).</summary>
    public static class InteractionEvents
    {
        public static event Action<IPointerTarget, PointerEvent> Selected;
        public static event Action<IPointerTarget> Hovered;
        public static void RaiseSelected(IPointerTarget t, PointerEvent e) { if (Selected != null) Selected(t, e); }
        public static void RaiseHovered(IPointerTarget t) { if (Hovered != null) Hovered(t); }
    }

    /// <summary>
    /// Ray pointer: mouse ray on desktop; laser from each controller in XR.
    /// Draws a laser + reticle and dispatches hover/select to IPointerTarget components.
    /// </summary>
    public class Pointer : MonoBehaviour
    {
        public PointerSource source = PointerSource.Mouse;
        public Camera cam;
        public float maxDistance = 25f;
        public LayerMask mask = ~0;
        public Color laserColor = new Color(0.25f, 0.85f, 1f, 0.9f);
        public static bool InputBlocked;          // set while dragging the camera on desktop

        IPointerTarget _hover;
        LineRenderer _line;
        Transform _reticle;
        public Vector3 LastHitPoint { get; private set; }
        public Collider LastHitCollider { get; private set; }
        public bool HasHit { get; private set; }

        void Start()
        {
            if (source != PointerSource.Mouse)
            {
                _line = gameObject.AddComponent<LineRenderer>();
                _line.positionCount = 2;
                _line.widthMultiplier = 0.004f;
                _line.material = RuntimeMaterials.Unlit(laserColor);
                _line.startColor = laserColor; _line.endColor = new Color(laserColor.r, laserColor.g, laserColor.b, 0.1f);
                _line.useWorldSpace = true;
            }
            var r = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(r.GetComponent<Collider>());
            r.name = "Reticle_" + source;
            r.transform.localScale = Vector3.one * (source == PointerSource.Mouse ? 0.0f : 0.015f);
            r.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Unlit(laserColor);
            _reticle = r.transform;
        }

        public Ray CurrentRay()
        {
            if (source == PointerSource.Mouse && cam != null)
                return cam.ScreenPointToRay(InputBridge.MousePosition);
            if (source == PointerSource.Gaze && cam != null)
                return new Ray(cam.transform.position, cam.transform.forward);
            return new Ray(transform.position, transform.forward);
        }

        void Update()
        {
            if (source == PointerSource.Mouse && InputBridge.XRActive) { SetHover(null, default(PointerEvent)); return; }
            var ray = CurrentRay();
            RaycastHit hit;
            IPointerTarget target = null;
            HasHit = Physics.Raycast(ray, out hit, maxDistance, mask, QueryTriggerInteraction.Collide);
            var e = new PointerEvent { source = source, ray = ray, point = HasHit ? hit.point : ray.GetPoint(maxDistance) };
            if (HasHit)
            {
                LastHitPoint = hit.point; LastHitCollider = hit.collider;
                target = hit.collider.GetComponentInParent<IPointerTarget>();
                if (target != null && !target.IsInteractable) target = null;
            }
            SetHover(target, e);

            if (_line != null)
            {
                _line.enabled = true;
                _line.SetPosition(0, ray.origin);
                _line.SetPosition(1, e.point);
            }
            if (_reticle != null)
            {
                _reticle.position = e.point;
                _reticle.gameObject.SetActive(HasHit && source != PointerSource.Mouse);
            }

            bool select = source == PointerSource.Mouse ? (InputBridge.DesktopSelectDown && !InputBlocked)
                        : InputBridge.SelectDown(source == PointerSource.RightHand);
            if (select && _hover != null)
            {
                UIAudio.Click();
                _hover.OnSelect(e);
                InteractionEvents.RaiseSelected(_hover, e);
            }
        }

        void SetHover(IPointerTarget t, PointerEvent e)
        {
            if (ReferenceEquals(t, _hover)) return;
            if (_hover != null && !(_hover is UnityEngine.Object && (UnityEngine.Object)_hover == null)) _hover.OnHoverExit(e);
            _hover = t;
            if (_hover != null) { _hover.OnHoverEnter(e); InteractionEvents.RaiseHovered(_hover); UIAudio.Hover(); }
        }

        void OnDisable() { SetHover(null, default(PointerEvent)); if (_reticle) _reticle.gameObject.SetActive(false); if (_line) _line.enabled = false; }
    }

    /// <summary>Simple cached unlit / lit materials created at runtime (URP or Built-in).</summary>
    public static class RuntimeMaterials
    {
        static Shader _unlit, _lit;
        public static Shader UnlitShader
        {
            get
            {
                if (_unlit == null) _unlit = Shader.Find("Universal Render Pipeline/Unlit");
                if (_unlit == null) _unlit = Shader.Find("Unlit/Color");
                return _unlit;
            }
        }
        public static Shader LitShader
        {
            get
            {
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null) _lit = Shader.Find("Standard");
                return _lit;
            }
        }
        static Material Base(string name, Shader fallback)
        {
            var b = Resources.Load<Material>("MROT/" + name);   // created by the editor setup so shaders are included in builds
            return b != null ? new Material(b) : new Material(fallback);
        }
        public static Material Lit(Color c, float smooth = 0.5f, float metal = 0f)
        {
            var m = Base("M_LitBase", LitShader);
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }
        public static Material Unlit(Color c, bool transparent = true)
        {
            var m = Base("M_UnlitBase", UnlitShader);
            SetColor(m, c);
            if (transparent && c.a < 0.999f) MakeTransparent(m);
            return m;
        }
        public static Material UnlitTexture(Texture t, bool transparent = true)
        {
            var m = Base("M_UnlitBase", UnlitShader);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
            SetColor(m, Color.white);
            if (transparent) MakeTransparent(m);
            return m;
        }
        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }
        public static void MakeTransparent(Material m)
        {
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.renderQueue = 3000;
            }
        }
    }
}
