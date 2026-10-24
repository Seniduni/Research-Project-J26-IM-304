using UnityEngine;
using UnityEngine.UI;

namespace IVSim
{
    /// <summary>Helpers to build world-space uGUI in code (legacy Text, no TextMeshPro setup needed).</summary>
    public static class UIFactory
    {
        static Font font;
        public static Font DefaultFont
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static Canvas WorldCanvas(string name, Transform parent, Vector2 sizePx, float metresPerPixel)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 3f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizePx;
            rt.localScale = Vector3.one * metresPerPixel;
            return canvas;
        }

        public static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        public static Image Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
                                 int size, TextAnchor align, Color color, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = DefaultFont;
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.fontStyle = style;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        public static string Hex(Severity s)
        {
            switch (s)
            {
                case Severity.Good: return "#5FD38D";
                case Severity.Warning: return "#F2C14E";
                case Severity.Error: return "#FF6B6B";
                default: return "#D7E3F4";
            }
        }

        public static string Tint(string text, string hex) { return "<color=" + hex + ">" + text + "</color>"; }
    }
}
