// Minimal world-space UI kit (uGUI + TextMeshPro) usable with bare hands (finger poke + pinch ray), XR controllers and mouse.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonatalDelivery
{
    public static class NDTheme
    {
        public static Color Panel = new Color(0.07f, 0.10f, 0.13f, 0.92f);
        public static Color Accent = new Color(0.16f, 0.62f, 0.78f, 1f);
        public static Color Button = new Color(0.17f, 0.24f, 0.30f, 1f);
        public static Color ButtonHover = new Color(0.22f, 0.45f, 0.58f, 1f);
        public static Color ButtonOn = new Color(0.12f, 0.55f, 0.36f, 1f);
        public static Color Text = new Color(0.93f, 0.96f, 0.98f, 1f);
        public static Color Warn = new Color(1f, 0.55f, 0.3f, 1f);
        public static TMP_FontAsset Font;
    }

    public class NDBar
    {
        public RectTransform root, fill, marker;
        public void SetValue(float v) { fill.anchorMax = new Vector2(Mathf.Clamp01(v), 1); }
        public void SetMarker(float a, float b) { marker.gameObject.SetActive(true); marker.anchorMin = new Vector2(Mathf.Clamp01(a), 0); marker.anchorMax = new Vector2(Mathf.Clamp01(Mathf.Max(a + 0.01f, b)), 1); }
    }

}
