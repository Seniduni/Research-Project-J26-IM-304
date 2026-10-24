// EducatorPanel: split from NDPanels.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonatalDelivery
{
    public class EducatorPanel : MonoBehaviour
    {
        public NDPanel panel;
        public Action<SessionConfig> OnStart;
        public Action OnCalibrate;
        SessionConfig _cfg;
        TMP_Text _summary;
        NDButton _diff, _cens, _hand, _ghost, _abl, _nuchal, _mr, _mother, _speed, _seated, _student;
        static readonly string[] Students = { "STUDENT-001", "STUDENT-002", "STUDENT-003", "STUDENT-004", "STUDENT-005", "EXPERT-REF" };
        int _si;

        public void Build(SessionConfig cfg)
        {
            _cfg = cfg.Clone();
            panel.Clear();
            panel.AddText("Neonatal Delivery & Hand-Placement Training", 38, FontStyles.Bold);
            panel.AddText("Educator configuration - set before the session begins", 24);
            _student = Cycle("Student code", () => { _si = (_si + 1) % Students.Length; _cfg.studentCode = Students[_si]; });
            _diff = Cycle("Difficulty", () => { _cfg.difficulty = (Difficulty)(((int)_cfg.difficulty + 1) % 3); _cfg.guidedGhostHands = _cfg.difficulty == Difficulty.Guided; });
            _cens = Cycle("Censorship", () => _cfg.censorship = (CensorshipLevel)(((int)_cfg.censorship + 1) % 4));
            _hand = Cycle("Dominant hand", () => _cfg.dominantHand = _cfg.dominantHand == HandSide.Right ? HandSide.Left : HandSide.Right);
            _ghost = Cycle("Ghost hands", () => _cfg.guidedGhostHands = !_cfg.guidedGhostHands);
            _nuchal = Cycle("Scenario", () => _cfg.scenarioNuchalCord = !_cfg.scenarioNuchalCord);
            _abl = Cycle("Feedback", () => _cfg.ablationMode = !_cfg.ablationMode);
            _speed = Cycle("Waiting time", () => _cfg.timeScale = _cfg.timeScale >= 1f ? 0.5f : (_cfg.timeScale >= 0.5f ? 0.25f : 1f));
            _seated = Cycle("Posture", () => _cfg.seated = !_cfg.seated);
            _mr = Cycle("Reality", () => _cfg.mixedReality = !_cfg.mixedReality);
            _mother = Cycle("MR mother", () => _cfg.mrMotherRender = (MotherRenderMode)(((int)_cfg.mrMotherRender + 1) % 3));
            var row = panel.AddRow();
            panel.AddButton(row, "Calibrate mannequin", () => OnCalibrate?.Invoke());
            var go = panel.AddButton(row, "START SESSION", () => { _cfg.Save(); OnStart?.Invoke(_cfg); }); go.toggled = true;
            _summary = panel.AddText("", 20);
            Refresh();
        }

        NDButton Cycle(string title, Action a)
        {
            var row = panel.AddRow();
            var t = panel.AddText(title, 26, FontStyles.Normal, row); t.alignment = TextAlignmentOptions.MidlineLeft;
            NDButton b = null; b = panel.AddButton(row, "", () => { a(); Refresh(); }, 56);
            return b;
        }

        void Refresh()
        {
            _student.label.text = _cfg.studentCode;
            _diff.label.text = _cfg.difficulty.ToString();
            _cens.label.text = _cfg.censorship switch { CensorshipLevel.FullRealism => "0 - Full realism", CensorshipLevel.Moderate => "1 - Moderate", CensorshipLevel.Clinical => "2 - Clinical", _ => "3 - Minimal" };
            _hand.label.text = _cfg.dominantHand + " hand";
            _ghost.label.text = _cfg.guidedGhostHands ? "Show ghost demonstration" : "Off"; _ghost.toggled = _cfg.guidedGhostHands;
            _nuchal.label.text = _cfg.scenarioNuchalCord ? "Loose nuchal cord" : "Normal LOA delivery";
            _abl.label.text = _cfg.ablationMode ? "Ablated (score only)" : "Consequences + reflective loop";
            _speed.label.text = _cfg.timeScale >= 1f ? "Real-time" : $"x{1f / _cfg.timeScale:F0} faster";
            _seated.label.text = _cfg.seated ? "Seated" : "Standing";
            _mr.label.text = _cfg.mixedReality ? "Mixed reality (passthrough + mannequin)" : "Virtual reality"; _mr.toggled = _cfg.mixedReality;
            _mother.label.text = _cfg.mrMotherRender.ToString(); _mother.interactable = _cfg.mixedReality;
            _summary.text = "<size=18>Rules: StreamingAssets/NeonatalDelivery/nd_fault_rules.json (override in persistentDataPath). Results: persistentDataPath/NeonatalDelivery/Sessions.</size>";
        }
    }
}
