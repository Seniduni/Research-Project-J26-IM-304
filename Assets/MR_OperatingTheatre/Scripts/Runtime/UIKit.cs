// MR Operating Theatre - world-space UI kit (works in VR and on desktop).
// All UI is built in code: 1 UI unit = 1 mm in the world (canvas scale 0.001).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TextMeshProUGUI;   // all UI text is TextMeshPro (signed-distance-field: sharp at any distance / in VR)

namespace MROT
{
    public static class Theme
    {
        public static readonly Color Panel = new Color32(12, 24, 40, 238);
        public static readonly Color PanelLight = new Color32(24, 42, 66, 245);
        public static readonly Color Card = new Color32(250, 252, 253, 250);
        public static readonly Color Accent = new Color32(32, 190, 174, 255);     // teal
        public static readonly Color AccentDark = new Color32(18, 128, 118, 255);
        public static readonly Color Blue = new Color32(48, 120, 220, 255);
        public static readonly Color Success = new Color32(52, 199, 105, 255);
        public static readonly Color Warning = new Color32(255, 176, 32, 255);
        public static readonly Color Danger = new Color32(232, 72, 72, 255);
        public static readonly Color Gold = new Color32(255, 200, 60, 255);
        public static readonly Color Text = new Color32(240, 246, 250, 255);
        public static readonly Color TextDim = new Color32(160, 182, 204, 255);
        public static readonly Color TextDark = new Color32(22, 36, 52, 255);
        public static readonly Color Button = new Color32(36, 64, 98, 255);
        public static readonly Color ButtonHover = new Color32(54, 96, 146, 255);
    }

    public static class UIKit
    {
        static TMP_FontAsset _reg, _semi, _bold, _sym;
        static Sprite _round, _round8, _circle, _pill;
        public const int UILayer = 5;   // built-in "UI" layer - drawn by the player's UI overlay camera (no bloom / tone-mapping)

        public static TMP_FontAsset Regular { get { if (_reg == null) _reg = LoadFont("Poppins-Regular"); return _reg; } }
        public static TMP_FontAsset SemiBold { get { if (_semi == null) _semi = LoadFont("Poppins-SemiBold"); return _semi; } }
        public static TMP_FontAsset Bold { get { if (_bold == null) _bold = LoadFont("Poppins-Bold"); return _bold; } }
        /// <summary>Fallback for symbols Poppins lacks (check marks, stars, arrows).</summary>
        public static TMP_FontAsset Symbols { get { if (_sym == null) _sym = LoadFontRaw("DejaVuSans"); return _sym; } }

        static TMP_FontAsset LoadFontRaw(string n)
        {
            // editor setup bakes "<name> SDF" font assets; create one at runtime if they are missing
            var fa = Resources.Load<TMP_FontAsset>("MROT/Fonts/" + n + " SDF");
            if (fa == null)
            {
                var f = Resources.Load<Font>("MROT/Fonts/" + n);
                if (f != null) fa = TMP_FontAsset.CreateFontAsset(f);
            }
            return fa;
        }

        static TMP_FontAsset LoadFont(string n)
        {
            var fa = LoadFontRaw(n);
            if (fa == null) fa = TMP_Settings.defaultFontAsset;
            var sym = Symbols;
            if (fa != null && sym != null && sym != fa)
            {
                if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!fa.fallbackFontAssetTable.Contains(sym)) fa.fallbackFontAssetTable.Add(sym);
            }
            return fa;
        }

        public static TextAlignmentOptions Align(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        public static Sprite Rounded { get { if (_round == null) _round = MakeRounded(128, 40); return _round; } }
        public static Sprite RoundedSmall { get { if (_round8 == null) _round8 = MakeRounded(64, 14); return _round8; } }
        public static Sprite Circle { get { if (_circle == null) _circle = MakeRounded(128, 64, false); return _circle; } }

        static Sprite MakeRounded(int size, int radius, bool sliced = true)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - 0.5f, x + 0.5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - 0.5f, y + 0.5f - (size - radius)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    if (dx == 0 && dy == 0) a = 1;
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px); t.Apply();
            var b = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, b);
        }

        // ------------------------------------------------------------------ canvases
        public static Canvas WorldCanvas(string name, Vector2 sizeMM, Transform parent = null, bool overlay = true)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = 100;
            if (overlay) { go.layer = UILayer; go.AddComponent<UILayerSync>(); }   // in-world screens (overlay=false) stay occludable
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizeMM;
            go.transform.localScale = Vector3.one * 0.001f;
            return c;
        }

        public static RectTransform Rect(GameObject go, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            return rt;
        }

        /// <summary>Place a child rect by pixel position from the parent's top-left (x, y) with size (w, h).</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static Image Panel(Transform parent, string name, Color color, Sprite sprite = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite ?? Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.UpperLeft, TMP_FontAsset font = null)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            var t = go.AddComponent<Text>();
            var fa = font ?? Regular;
            if (fa != null) t.font = fa;
            t.text = text; t.fontSize = size; t.color = color; t.alignment = Align(align);
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow;
            t.richText = true; t.raycastTarget = false;
            t.lineSpacing = 4f;
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return t;
        }

        public static Image Icon(Transform parent, Sprite s, Color c)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var i = go.GetComponent<Image>(); i.sprite = s; i.color = c; i.preserveAspect = true; i.raycastTarget = false;
            return i;
        }

        public static WorldButton Button(Transform parent, string text, Action onClick, Color? color = null, int fontSize = 30, TMP_FontAsset font = null)
        {
            var go = new GameObject("Button_" + text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>(); img.sprite = RoundedSmall; img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            var b = go.AddComponent<WorldButton>();
            b.baseColor = color ?? Theme.Button;
            b.background = img;
            b.label = Label(go.transform, text, fontSize, Theme.Text, TextAnchor.MiddleCenter, font ?? SemiBold);
            ((RectTransform)b.label.transform).offsetMin = new Vector2(12, 4); ((RectTransform)b.label.transform).offsetMax = new Vector2(-12, -4);
            b.onClick = onClick;
            return b;
        }

        public static Image ProgressBar(Transform parent, Color bg, Color fill, out Image fillImg)
        {
            var back = Panel(parent, "ProgressBack", bg, RoundedSmall);
            var f = Panel(back.transform, "Fill", fill, RoundedSmall);
            f.type = Image.Type.Sliced;
            var rt = f.rectTransform; rt.anchorMax = new Vector2(0, 1);
            fillImg = f;
            return back;
        }

        public static void SetProgress(Image fill, float t)
        {
            var rt = fill.rectTransform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
            rt.offsetMax = Vector2.zero;
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayerRecursively(c.gameObject, layer);
        }
    }

    /// <summary>Button for world-space UI. Uses a BoxCollider so the VR laser and the mouse ray can hit it.</summary>
    public class WorldButton : MonoBehaviour, IPointerTarget
    {
        public Image background;
        public Text label;
        public Color baseColor = Theme.Button;
        public Action onClick;
        public bool interactable = true;
        public Color? lockedColor;          // when set, the button keeps this colour (e.g. correct/incorrect answer)
        BoxCollider _col;
        bool _hover;
        float _pulse;

        public bool IsInteractable { get { return interactable && isActiveAndEnabled; } }

        void Start()
        {
            _col = gameObject.AddComponent<BoxCollider>();
            UpdateCollider();
            Refresh();
        }
        void OnRectTransformDimensionsChange() { if (_col) UpdateCollider(); }
        void UpdateCollider()
        {
            var rt = (RectTransform)transform;
            _col.size = new Vector3(rt.rect.width, rt.rect.height, 12f);
            _col.center = new Vector3((0.5f - rt.pivot.x) * rt.rect.width, (0.5f - rt.pivot.y) * rt.rect.height, -4f);
        }

        void Update()
        {
            if (_hover) { _pulse += Time.unscaledDeltaTime * 6f; }
            var target = _hover ? 1.04f : 1f;
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, Time.unscaledDeltaTime * 14f);
        }

        public void Refresh()
        {
            if (background == null) return;
            var c = lockedColor.HasValue ? lockedColor.Value : interactable ? (_hover ? Color.Lerp(baseColor, Color.white, 0.22f) : baseColor) : new Color(0.3f, 0.33f, 0.36f, 0.8f);
            background.color = c;
            if (label) label.color = interactable || lockedColor.HasValue ? Theme.Text : new Color(0.7f, 0.72f, 0.75f, 0.8f);
        }

        public void OnHoverEnter(PointerEvent e) { _hover = true; Refresh(); }
        public void OnHoverExit(PointerEvent e) { _hover = false; Refresh(); }
        public void OnSelect(PointerEvent e)
        {
            if (!IsInteractable) return;
            if (onClick != null) onClick();
        }
        public void SetText(string t) { if (label) label.text = t; }
        public void SetInteractable(bool v) { interactable = v; Refresh(); }
    }

    /// <summary>Keeps a panel in front of the user (smoothly), facing them. Used for HUD and popups.</summary>
    public class LazyFollow : MonoBehaviour
    {
        public float distance = 1.2f;
        public float height = -0.05f;
        public float followSpeed = 3f;
        public float angleThreshold = 28f;
        public bool snapOnEnable = true;
        public static float DesktopScale = 0.62f;     // panels come closer on a flat monitor so text is readable
        bool _moving;
        float D { get { var r = PlayerRig.Instance; return r != null && !r.IsVR ? distance * DesktopScale : distance; } }
        float H { get { var r = PlayerRig.Instance; return r != null && !r.IsVR ? height * DesktopScale : height; } }

        void OnEnable() { if (snapOnEnable) Snap(); }
        public void Snap()
        {
            var rig = PlayerRig.Instance; if (rig == null) return;
            transform.position = Target(rig);
            Face(rig, 1f);
        }
        Vector3 Target(PlayerRig rig)
        {
            var f = rig.Head.forward; f.y = 0; if (f.sqrMagnitude < 0.01f) f = Vector3.forward; f.Normalize();
            return rig.Head.position + f * D + Vector3.up * H;
        }
        void Face(PlayerRig rig, float t)
        {
            var dir = transform.position - rig.Head.position; dir.y = 0;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), t);
        }
        public bool lockToViewOnDesktop = true;
        public float desktopLateral = 0f;    // metres left(-)/right(+) when screen-locked on desktop
        void LateUpdate()
        {
            var rig = PlayerRig.Instance; if (rig == null) return;
            if (!rig.IsVR && lockToViewOnDesktop)
            {
                // flat monitor: keep the panel fixed on screen (like a screen-space HUD, but still raycastable)
                var cam = rig.Head;
                transform.position = cam.position + cam.rotation * new Vector3(desktopLateral, H, D);
                transform.rotation = cam.rotation;
                return;
            }
            var tgt = Target(rig);
            var toPanel = transform.position - rig.Head.position; toPanel.y = 0;
            var fwd = rig.Head.forward; fwd.y = 0;
            float ang = Vector3.Angle(toPanel, fwd);
            float dist = toPanel.magnitude;
            if (ang > angleThreshold || Mathf.Abs(dist - D) > 0.35f || Mathf.Abs(transform.position.y - tgt.y) > 0.3f) _moving = true;
            if (_moving)
            {
                transform.position = Vector3.Lerp(transform.position, tgt, Time.unscaledDeltaTime * followSpeed);
                if (Vector3.Distance(transform.position, tgt) < 0.03f) _moving = false;
            }
            Face(rig, Time.unscaledDeltaTime * followSpeed * 2f);
        }
    }

    /// <summary>Rotates an object around Y so it always faces the camera (billboard).</summary>
    public class Billboard : MonoBehaviour
    {
        public bool yawOnly = true;
        void LateUpdate()
        {
            var cam = Camera.main; if (cam == null) return;
            var d = transform.position - cam.transform.position;
            if (yawOnly) d.y = 0;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }

    /// <summary>Tiny UI sound helper (hover, click, success...). Clips from Resources/MROT/Audio.</summary>
    public static class UIAudio
    {
        static AudioSource _src;
        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        public static float Volume = 0.7f;
        static AudioSource Src
        {
            get
            {
                if (_src == null)
                {
                    var go = new GameObject("UIAudio");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    _src = go.AddComponent<AudioSource>();
                    _src.spatialBlend = 0f; _src.playOnAwake = false;
                }
                return _src;
            }
        }
        public static AudioClip Clip(string n)
        {
            AudioClip c;
            if (!_cache.TryGetValue(n, out c)) { c = Resources.Load<AudioClip>("MROT/Audio/" + n); _cache[n] = c; }
            return c;
        }
        public static void Play(string n, float vol = 1f)
        {
            var c = Clip(n); if (c != null) Src.PlayOneShot(c, vol * Volume);
        }
        public static void Click() { Play("ui_click", 0.6f); }
        public static void Hover() { Play("ui_hover", 0.25f); }
    }

    /// <summary>Keeps every object under a UI canvas on the UI layer (rendered crisp by the UI overlay camera).</summary>
    public class UILayerSync : MonoBehaviour
    {
        int _count = -1; float _next;
        void LateUpdate()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.2f;
            var all = GetComponentsInChildren<Transform>(true);
            if (all.Length == _count) return;
            _count = all.Length;
            foreach (var t in all) t.gameObject.layer = UIKit.UILayer;
        }
    }
}
