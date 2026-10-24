// NDPanel: split from NDUI.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonatalDelivery
{
    public class NDPanel : MonoBehaviour
    {
        // derived from the GameObject (not cached in fields that would be lost when the scene is saved)
        public RectTransform Content => (RectTransform)transform;
        public Canvas Canvas => GetComponent<Canvas>();
        public float width = 900f;

        public static NDPanel Create(string name, Transform parent, float widthPx = 900f, float metresPerPixel = 0.0006f)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var p = go.AddComponent<NDPanel>(); p.width = widthPx;
            var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(widthPx, 200); rt.localScale = Vector3.one * metresPerPixel;
            var bg = go.AddComponent<Image>(); bg.color = NDTheme.Panel;
            var v = go.AddComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(28, 28, 22, 22); v.spacing = 12; v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandHeight = false; v.childForceExpandWidth = true;
            var fit = go.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return p;
        }

        public TMP_Text AddText(string text, float size, FontStyles style = FontStyles.Normal, Transform parent = null)
        {
            var go = new GameObject("Text", typeof(RectTransform)); go.transform.SetParent(parent != null ? parent : Content, false);
            var t = go.AddComponent<TextMeshProUGUI>(); t.text = text; t.fontSize = size; t.fontStyle = style; t.color = NDTheme.Text; t.richText = true;
            
#pragma warning disable 0618   // enableWordWrapping: still valid in TMP 3.x / uGUI 2.0 (textWrappingMode in newer TMP)
            t.enableWordWrapping = true;
#pragma warning restore 0618
            if (NDTheme.Font != null) t.font = NDTheme.Font;
            return t;
        }

        public Transform AddRow(float spacing = 10f)
        {
            var go = new GameObject("Row", typeof(RectTransform)); go.transform.SetParent(Content, false);
            var h = go.AddComponent<HorizontalLayoutGroup>(); h.spacing = spacing; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = true; h.childForceExpandHeight = false;
            return go.transform;
        }

        public NDButton AddButton(Transform row, string label, Action onClick, float height = 64f)
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform)); go.transform.SetParent(row != null ? row : Content, false);
            var img = go.AddComponent<Image>(); img.color = NDTheme.Button;
            var le = go.AddComponent<LayoutElement>(); le.minHeight = height; le.preferredHeight = height; le.flexibleWidth = 1;
            var b = go.AddComponent<NDButton>(); b.image = img; b.onClick = onClick;
            var t = AddText(label, 26, FontStyles.Bold, go.transform); t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            b.label = t;
            var col = go.AddComponent<BoxCollider>(); col.isTrigger = true; b.box = col;
            return b;
        }

        public RawImage AddRawImage(Texture tex, Vector2 size)
        {
            var go = new GameObject("Image", typeof(RectTransform)); go.transform.SetParent(Content, false);
            var r = go.AddComponent<RawImage>(); r.texture = tex;
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = size.y; le.preferredWidth = size.x; le.minHeight = size.y;
            return r;
        }

        public NDBar AddBar(Vector2 size)
        {
            var go = new GameObject("Bar", typeof(RectTransform)); go.transform.SetParent(Content, false);
            go.AddComponent<Image>().color = new Color(1, 1, 1, 0.12f);
            var le = go.AddComponent<LayoutElement>(); le.preferredHeight = size.y; le.minHeight = size.y;
            var bar = new NDBar { root = (RectTransform)go.transform };
            var f = new GameObject("Fill", typeof(RectTransform)); f.transform.SetParent(go.transform, false); f.AddComponent<Image>().color = NDTheme.Accent;
            bar.fill = (RectTransform)f.transform; bar.fill.anchorMin = Vector2.zero; bar.fill.anchorMax = new Vector2(0, 1); bar.fill.offsetMin = bar.fill.offsetMax = Vector2.zero;
            var m = new GameObject("Marker", typeof(RectTransform)); m.transform.SetParent(go.transform, false); m.AddComponent<Image>().color = new Color(1f, 0.3f, 0.25f, 0.85f);
            bar.marker = (RectTransform)m.transform; bar.marker.offsetMin = bar.marker.offsetMax = Vector2.zero; m.SetActive(false);
            return bar;
        }

        public void Clear()
        {
            for (int i = Content.childCount - 1; i >= 0; i--) Destroy(Content.GetChild(i).gameObject);
        }
    }
}
