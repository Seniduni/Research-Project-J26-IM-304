// InstructionPanel: split from NDPanels.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonatalDelivery
{
    public class InstructionPanel : MonoBehaviour
    {
        public NDPanel panel;
        public Action<string> OnCommand;
        TMP_Text _step, _text, _status, _sub;
        NDBar _progress; RawImage _ctg; Texture2D _ctgTex; float _ctgTimer; int _ctgX;
        NDButton _push, _pant, _pause;

        public void Build()
        {
            panel.Clear();
            _step = panel.AddText("", 32, FontStyles.Bold);
            _text = panel.AddText("", 25);
            _progress = panel.AddBar(new Vector2(800, 14));
            _ctgTex = new Texture2D(256, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var clear = new Color[256 * 64]; for (int i = 0; i < clear.Length; i++) clear[i] = new Color(0.02f, 0.06f, 0.05f, 1); _ctgTex.SetPixels(clear); _ctgTex.Apply();
            _ctg = panel.AddRawImage(_ctgTex, new Vector2(800, 150));
            _status = panel.AddText("", 22);
            var row = panel.AddRow();
            _push = panel.AddButton(row, "Encourage push", () => OnCommand?.Invoke("push"));
            _pant = panel.AddButton(row, "Ask to pant", () => OnCommand?.Invoke("pant"));
            _pause = panel.AddButton(row, "Pause", () => OnCommand?.Invoke("pause"));
            panel.AddButton(row, "Restart", () => OnCommand?.Invoke("restart"));
            _sub = panel.AddText("", 22, FontStyles.Italic);
        }

        public void SetStep(DeliveryStep s, string text) { _step.text = $"{(int)s}. {ND.StepTitle(s)}"; _text.text = text; }
        public void SetSubtitle(string s) { if (_sub != null) _sub.text = s; }
        public void SetPaused(bool p) { if (_pause != null) { _pause.label.text = p ? "Resume" : "Pause"; _pause.toggled = p; } }
        public void SetPanting(bool p) { if (_pant != null) _pant.toggled = p; }

        public void Tick(float progress, ContractionModel labour, string status)
        {
            if (_progress != null) _progress.SetValue(progress);
            if (_status != null) _status.text = status;
            if (labour == null || _ctgTex == null) return;
            _ctgTimer += Time.deltaTime;
            if (_ctgTimer < 0.25f) return; _ctgTimer = 0f;
            // scrolling CTG: FHR (top, green) 60-200 bpm, toco (bottom, amber)
            int x = _ctgX = (_ctgX + 1) % 256;
            for (int y = 0; y < 64; y++) _ctgTex.SetPixel(x, y, (y % 16 == 0) ? new Color(0.06f, 0.14f, 0.12f) : new Color(0.02f, 0.06f, 0.05f));
            int fy = 34 + Mathf.Clamp(Mathf.RoundToInt((labour.FetalHeartRate - 60f) / 140f * 29f), 0, 29);
            int ty = 2 + Mathf.RoundToInt(labour.Intensity * 26f);
            _ctgTex.SetPixel(x, fy, new Color(0.3f, 1f, 0.5f)); _ctgTex.SetPixel(x, Mathf.Min(63, fy + 1), new Color(0.3f, 1f, 0.5f));
            _ctgTex.SetPixel(x, ty, new Color(1f, 0.75f, 0.2f));
            for (int k = 1; k < 4; k++) for (int y = 0; y < 64; y++) _ctgTex.SetPixel((x + k) % 256, y, new Color(0.02f, 0.06f, 0.05f));
            _ctgTex.Apply(false);
        }
    }
}
