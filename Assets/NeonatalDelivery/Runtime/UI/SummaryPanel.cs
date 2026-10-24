// SummaryPanel: split from NDPanels.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonatalDelivery
{
    public class SummaryPanel : MonoBehaviour
    {
        public NDPanel panel;
        public Action OnRestart;
        public void Show(SessionResult r)
        {
            panel.Clear();
            panel.gameObject.SetActive(true);
            panel.AddText("Session summary", 38, FontStyles.Bold);
            panel.AddText($"{r.studentCode}  |  {r.difficulty}  |  censorship {r.censorship}  |  {(r.ablationMode ? "ablated build" : "full feedback")}", 22);
            panel.AddText($"<b>Overall score {r.overallScore:F0}/100</b>   Competency: <b>{r.competency}</b>   Re-practice attempts: {r.rePracticeAttempts}   Time: {r.totalSeconds / 60f:F1} min", 26);
            var sb = new StringBuilder();
            foreach (var s in r.steps)
                sb.AppendLine($"{s.step,-22}  attempts {s.attempts}  faults {s.faultCount}  score {s.score:F0}  {(s.timeToCorrectPlacement >= 0 ? $"correct placement after {s.timeToCorrectPlacement:F1}s" : "")}");
            panel.AddText("<size=20><mspace=0.55em>" + sb + "</mspace></size>", 20);
            if (r.faults.Count > 0)
            {
                var fb = new StringBuilder("<b>Faults and their clinical consequences</b>\n");
                foreach (var f in r.faults) fb.AppendLine($"- {f.name} ({ND.StepTitle(f.step)}, {f.startTime:F1}s): {f.complication}");
                panel.AddText(fb.ToString(), 21);
            }
            else panel.AddText("No hand-placement faults detected.", 24);
            panel.AddText($"Saved: persistentDataPath/NeonatalDelivery/Sessions ({r.trajectoryFile})", 18);
            var row = panel.AddRow();
            panel.AddButton(row, "New session", () => OnRestart?.Invoke());
        }
    }
}
