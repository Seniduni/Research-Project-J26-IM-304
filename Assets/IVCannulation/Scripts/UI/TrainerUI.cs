using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace IVSim
{
    /// <summary>
    /// Builds and updates the world-space trainer panel (steps, instructions, feedback, live readouts,
    /// side view, menu and final report), a floating "toast" near the arm, and vein name labels.
    /// Everything is created in code so there is nothing to wire up by hand.
    /// </summary>
    public class TrainerUI : MonoBehaviour
    {
        [Tooltip("Where the main panel sits (faces the learner)")] public Transform panelAnchor;
        [Tooltip("Where floating messages appear (above the forearm)")] public Transform toastAnchor;
        public SideViewCamera sideView;
        public float metresPerPixel = 0.0006f;

        Text title, timer, stepsText, stepTitle, stepBody, feedbackText, liveText, sideCaption, overlayText, toastText;
        RawImage sideImage;
        Canvas panelCanvas;
        RectTransform content;
        int layoutMode = -1;      // 0 = world (VR), 1 = screen overlay (desktop)
        bool desktopPanelHidden;
        GameObject mainGroup, overlayGroup, toastRoot;
        Canvas toastCanvas;
        readonly List<Canvas> veinLabels = new List<Canvas>();
        float nextRefresh;
        float toastUntil;
        string toastMsg = "";
        Color toastColor = Color.white;

        static readonly Color PanelBg = new Color(0.07f, 0.09f, 0.12f, 0.92f);
        static readonly Color BoxBg = new Color(0.13f, 0.16f, 0.21f, 0.95f);
        static readonly Color TextCol = new Color(0.86f, 0.9f, 0.95f, 1f);
        static readonly Color Accent = new Color(0.45f, 0.78f, 1f, 1f);

        void Start()
        {
            BuildPanel();
            BuildToast();
            BuildVeinLabels();
            var s = IVSession.Instance;
            if (s != null) s.FeedbackAdded += OnFeedback;
            Refresh();
        }

        void OnDestroy()
        {
            var s = IVSession.Instance;
            if (s != null) s.FeedbackAdded -= OnFeedback;
        }

        // ------------------------------------------------------------------ build

        void BuildPanel()
        {
            Transform anchor = panelAnchor != null ? panelAnchor : transform;
            panelCanvas = UIFactory.WorldCanvas("TrainerPanel", anchor, new Vector2(1400, 900), metresPerPixel);
            content = UIFactory.Rect("Content", panelCanvas.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            content.sizeDelta = new Vector2(1400, 900);
            Transform root = content;
            UIFactory.Panel("Background", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, PanelBg);

            title = UIFactory.Label("Title", root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -70), new Vector2(-260, -14), 36, TextAnchor.MiddleLeft, Accent, FontStyle.Bold);
            timer = UIFactory.Label("Timer", root, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-260, -70), new Vector2(-30, -14), 30, TextAnchor.MiddleRight, TextCol);

            mainGroup = new GameObject("Main", typeof(RectTransform));
            mainGroup.transform.SetParent(root, false);
            var mg = (RectTransform)mainGroup.transform;
            mg.anchorMin = Vector2.zero; mg.anchorMax = Vector2.one; mg.offsetMin = Vector2.zero; mg.offsetMax = Vector2.zero;

            // left: steps
            UIFactory.Panel("StepsBox", mg, new Vector2(0, 0), new Vector2(0, 1), new Vector2(20, 20), new Vector2(430, -84), BoxBg);
            UIFactory.Label("StepsHeader", mg, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -128), new Vector2(420, -90), 26, TextAnchor.MiddleLeft, Accent, FontStyle.Bold).text = "PROCEDURE";
            stepsText = UIFactory.Label("Steps", mg, new Vector2(0, 0), new Vector2(0, 1), new Vector2(36, 30), new Vector2(420, -130), 23, TextAnchor.UpperLeft, TextCol);
            stepsText.lineSpacing = 1.05f;

            // middle: current step + feedback
            UIFactory.Panel("StepBox", mg, new Vector2(0, 1), new Vector2(0, 1), new Vector2(445, -380), new Vector2(945, -84), BoxBg);
            stepTitle = UIFactory.Label("StepTitle", mg, new Vector2(0, 1), new Vector2(0, 1), new Vector2(465, -150), new Vector2(930, -92), 32, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            stepBody = UIFactory.Label("StepBody", mg, new Vector2(0, 1), new Vector2(0, 1), new Vector2(465, -372), new Vector2(930, -152), 25, TextAnchor.UpperLeft, TextCol);

            UIFactory.Panel("FeedbackBox", mg, new Vector2(0, 0), new Vector2(0, 1), new Vector2(445, 20), new Vector2(945, -395), BoxBg);
            UIFactory.Label("FeedbackHeader", mg, new Vector2(0, 1), new Vector2(0, 1), new Vector2(465, -440), new Vector2(930, -402), 26, TextAnchor.MiddleLeft, Accent, FontStyle.Bold).text = "FEEDBACK";
            feedbackText = UIFactory.Label("Feedback", mg, new Vector2(0, 0), new Vector2(0, 1), new Vector2(465, 30), new Vector2(930, -442), 22, TextAnchor.UpperLeft, TextCol);

            // right: live + side view
            UIFactory.Panel("LiveBox", mg, new Vector2(0, 1), new Vector2(0, 1), new Vector2(960, -430), new Vector2(1380, -84), BoxBg);
            UIFactory.Label("LiveHeader", mg, new Vector2(0, 1), new Vector2(0, 1), new Vector2(980, -128), new Vector2(1370, -90), 26, TextAnchor.MiddleLeft, Accent, FontStyle.Bold).text = "LIVE";
            liveText = UIFactory.Label("Live", mg, new Vector2(0, 1), new Vector2(0, 1), new Vector2(980, -425), new Vector2(1370, -130), 23, TextAnchor.UpperLeft, TextCol);

            UIFactory.Panel("SideBox", mg, new Vector2(0, 0), new Vector2(0, 1), new Vector2(960, 20), new Vector2(1380, -445), BoxBg);
            var imgRt = UIFactory.Rect("SideView", mg, new Vector2(0, 0), new Vector2(0, 0), new Vector2(990, 70), new Vector2(1350, 430));
            sideImage = imgRt.gameObject.AddComponent<RawImage>();
            sideImage.raycastTarget = false;
            if (sideView != null) sideImage.texture = sideView.Texture;
            sideCaption = UIFactory.Label("SideCaption", mg, new Vector2(0, 0), new Vector2(0, 0), new Vector2(975, 24), new Vector2(1370, 66), 20, TextAnchor.MiddleCenter, TextCol);

            // overlay (menu / report)
            overlayGroup = new GameObject("Overlay", typeof(RectTransform));
            overlayGroup.transform.SetParent(root, false);
            var og = (RectTransform)overlayGroup.transform;
            og.anchorMin = Vector2.zero; og.anchorMax = Vector2.one; og.offsetMin = Vector2.zero; og.offsetMax = Vector2.zero;
            UIFactory.Panel("OverlayBg", og, Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-20, -84), BoxBg);
            overlayText = UIFactory.Label("OverlayText", og, Vector2.zero, Vector2.one, new Vector2(50, 36), new Vector2(-50, -100), 24, TextAnchor.UpperLeft, TextCol);
            overlayText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        void BuildToast()
        {
            Transform anchor = toastAnchor != null ? toastAnchor : transform;
            toastCanvas = UIFactory.WorldCanvas("Toast", anchor, new Vector2(900, 160), 0.00025f);
            toastRoot = toastCanvas.gameObject;
            UIFactory.Panel("Bg", toastCanvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.05f, 0.07f, 0.1f, 0.82f));
            toastText = UIFactory.Label("Text", toastCanvas.transform, Vector2.zero, Vector2.one, new Vector2(20, 10), new Vector2(-20, -10), 34, TextAnchor.MiddleCenter, Color.white);
            toastRoot.SetActive(false);
        }

        void BuildVeinLabels()
        {
            foreach (var v in VascularSystem.All)
            {
                if (v == null || v.SampleCount < 2) continue;
                var c = UIFactory.WorldCanvas("Label_" + v.displayName, v.transform, new Vector2(420, 60), 0.0001f);
                int mid = v.SampleCount / 2;
                c.transform.position = v.SampleWorld(mid) + Vector3.up * 0.018f;
                var t = UIFactory.Label("Text", c.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 40, TextAnchor.MiddleCenter,
                                        v.vesselType == VesselType.Artery ? new Color(1f, 0.55f, 0.55f) : new Color(0.65f, 0.8f, 1f), FontStyle.Bold);
                t.text = v.displayName;
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.9f);
                veinLabels.Add(c);
            }
        }

        // ------------------------------------------------------------------ update

        void OnFeedback(FeedbackEntry e)
        {
            if (e.kind == FeedbackKind.Guidance) return;
            ShowToast(e.text, e.severity);
            nextRefresh = 0f;
        }

        void ShowToast(string msg, Severity sev)
        {
            toastMsg = msg;
            toastColor = sev == Severity.Good ? new Color(0.6f, 1f, 0.7f) : sev == Severity.Warning ? new Color(1f, 0.85f, 0.4f) : sev == Severity.Error ? new Color(1f, 0.55f, 0.55f) : Color.white;
            toastUntil = Time.time + Mathf.Clamp(msg.Length * 0.06f, 2.5f, 6f);
        }

        /// <summary>VR: panel floats in the room. Desktop: panel docks to the top-right of the screen (Tab hides it).</summary>
        void UpdateLayout()
        {
            if (panelCanvas == null) return;
            bool vr = RigBootstrap.IsVR;
            if (!vr && IVInput.GetKeyDown(IVKey.Tab)) desktopPanelHidden = !desktopPanelHidden;
            int mode = vr ? 0 : 1;
            if (mode != layoutMode)
            {
                layoutMode = mode;
                var rt = (RectTransform)panelCanvas.transform;
                if (vr)
                {
                    panelCanvas.renderMode = RenderMode.WorldSpace;
                    rt.sizeDelta = new Vector2(1400, 900);
                    rt.localScale = Vector3.one * metresPerPixel;
                    rt.localPosition = Vector3.zero;
                    rt.localRotation = Quaternion.identity;
                    content.anchorMin = content.anchorMax = content.pivot = new Vector2(0.5f, 0.5f);
                    content.anchoredPosition = Vector2.zero;
                    content.localScale = Vector3.one;
                }
                else
                {
                    panelCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    content.anchorMin = content.anchorMax = content.pivot = new Vector2(1f, 1f);
                    content.anchoredPosition = new Vector2(-12f, -12f);
                }
            }
            if (!vr)
            {
                float k = Mathf.Min(Screen.height * 0.6f / 900f, Screen.width * 0.5f / 1400f);
                content.localScale = Vector3.one * Mathf.Max(0.2f, k);
                if (panelCanvas.gameObject.activeSelf == desktopPanelHidden) panelCanvas.gameObject.SetActive(!desktopPanelHidden);
            }
            else if (!panelCanvas.gameObject.activeSelf) panelCanvas.gameObject.SetActive(true);
        }

        void Update()
        {
            UpdateLayout();
            UpdateToast();
            UpdateLabels();
            if (Time.time >= nextRefresh)
            {
                nextRefresh = Time.time + 0.12f;
                Refresh();
            }
        }

        void UpdateToast()
        {
            if (toastRoot == null) return;
            string palp = null;
            foreach (var h in IVHand.All)
                if (h != null && !string.IsNullOrEmpty(h.PalpationText) && Time.time - h.PalpationTime < 0.25f) palp = h.PalpationText;

            bool show = palp != null || Time.time < toastUntil;
            if (toastRoot.activeSelf != show) toastRoot.SetActive(show);
            if (!show) return;
            if (palp != null) { toastText.text = "Palpation: " + palp; toastText.color = new Color(0.8f, 0.9f, 1f); }
            else { toastText.text = toastMsg; toastText.color = toastColor; }
            Billboard(toastCanvas.transform);
        }

        void UpdateLabels()
        {
            var s = IVSession.Instance;
            bool show = s != null && s.Profile != null && s.Profile.showVeinLabels;
            foreach (var c in veinLabels)
            {
                if (c == null) continue;
                if (c.gameObject.activeSelf != show) c.gameObject.SetActive(show);
                if (show) Billboard(c.transform);
            }
        }

        static void Billboard(Transform t)
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 d = t.position - cam.transform.position;
            if (d.sqrMagnitude < 1e-6f) return;
            t.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        void Refresh()
        {
            var s = IVSession.Instance;
            if (s == null || title == null) return;
            bool vr = RigBootstrap.IsVR;

            title.text = "IV CANNULATION TRAINER" + (s.State == SessionState.Menu ? "" : "  <size=26><color=#9FB3C8>" + s.Profile.label + "  |  " + (vr ? "VR" : "Desktop") + "</color></size>");
            if (s.State == SessionState.Running) timer.text = FormatTime(Time.time - s.Metrics.startTime);
            else if (s.State == SessionState.Report) timer.text = FormatTime(s.Metrics.Duration);
            else timer.text = "";

            bool overlay = s.State != SessionState.Running;
            if (mainGroup.activeSelf == overlay) mainGroup.SetActive(!overlay);
            if (overlayGroup.activeSelf != overlay) overlayGroup.SetActive(overlay);

            if (s.State == SessionState.Menu) { overlayText.text = MenuText(s, vr); return; }
            if (s.State == SessionState.Report) { overlayText.text = ReportText(s, vr); return; }

            // steps
            var sb = new StringBuilder();
            for (int i = 0; i < s.steps.Count; i++)
            {
                var st = s.GetStatus(i);
                string tag = st == StepStatus.Done ? "<color=#5FD38D>[OK]</color>" :
                             st == StepStatus.Current ? "<color=#73C7FF>[>>]</color>" :
                             st == StepStatus.Missed ? "<color=#FF6B6B>[ X]</color>" : "<color=#5A6675>[  ]</color>";
                string name = st == StepStatus.Current ? "<b><color=#FFFFFF>" + s.steps[i].title + "</color></b>" :
                              st == StepStatus.Pending ? "<color=#8593A3>" + s.steps[i].title + "</color>" : s.steps[i].title;
                sb.Append(tag).Append(' ').Append(i + 1).Append(". ").Append(name).Append('\n');
            }
            stepsText.text = sb.ToString();

            var cur = s.CurrentStepDef;
            if (cur != null)
            {
                stepTitle.text = (s.CurrentIndex + 1) + ". " + cur.title;
                string body = cur.instruction;
                if (s.Profile.showHints && s.HintsVisible && !string.IsNullOrEmpty(cur.hint))
                    body += "\n\n<color=#9FD0FF><i>Hint: " + cur.hint + "</i></color>";
                body += "\n\n<size=19><color=#7E8B99>" + ControlsLine(s, vr) + "</color></size>";
                stepBody.text = body;
            }

            // feedback (newest first)
            sb.Length = 0;
            int shown = 0;
            for (int i = s.feedback.Count - 1; i >= 0 && shown < 7; i--)
            {
                var e = s.feedback[i];
                if (!e.shown) continue;
                sb.Append("<color=").Append(UIFactory.Hex(e.severity)).Append(">• ").Append(e.text).Append("</color>\n");
                shown++;
            }
            feedbackText.text = sb.ToString();

            liveText.text = LiveText(s);

            bool side = sideView != null && sideView.Active;
            sideImage.enabled = side;
            if (side && sideImage.texture == null) sideImage.texture = sideView.Texture;
            sideCaption.text = side ? "Side view (skin hidden): needle vs vein" : "Side view is available in Easy mode";
        }

        string LiveText(IVSession s)
        {
            var sb = new StringBuilder();
            var c = s.cannula;
            var p = s.Profile;
            if (p.liveAngleGauge && c != null && (c.IsInSkin || (c.IsHeld && c.LiveValid)))
            {
                float a = c.IsInSkin ? c.CurrentAngle : c.LiveAngle;
                string txt;
                Severity sev = s.ClassifyAngle(a, out txt);
                string hex = UIFactory.Hex(sev == Severity.Info ? Severity.Warning : sev == Severity.Warning ? Severity.Error : sev);
                sb.Append("Angle: ").Append(UIFactory.Tint(a.ToString("0") + "°", hex)).Append("  <size=19>(ideal ")
                  .Append(s.clinical.idealAngleMin.ToString("0")).Append('-').Append(s.clinical.idealAngleMax.ToString("0")).Append("°)</size>\n");
                bool bevelOk = Mathf.Abs(c.LiveRoll) <= s.clinical.bevelTolerance;
                sb.Append("Bevel: ").Append(bevelOk ? UIFactory.Tint("UP", "#5FD38D") : UIFactory.Tint("rotated " + Mathf.Abs(c.LiveRoll).ToString("0") + "°", "#F2C14E")).Append('\n');
                if (c.IsInSkin)
                {
                    sb.Append("Depth: ").Append((c.Depth * 1000f).ToString("0.0")).Append(" mm\n");
                    sb.Append("Flashback: ").Append(c.Flashback ? UIFactory.Tint("YES", "#FF6B6B") : "no").Append('\n');
                    if (c.ThreadFraction > 0f) sb.Append("Catheter advanced: ").Append(Mathf.RoundToInt(c.ThreadFraction * 100f)).Append("%\n");
                }
                else sb.Append("Distance to skin: ").Append((c.LiveDistance * 1000f).ToString("0")).Append(" mm\n");
            }
            else if (!p.liveAngleGauge)
            {
                sb.Append("<color=#7E8B99>Assessment mode: live angle hidden.</color>\n");
            }
            else sb.Append("<color=#7E8B99>Pick up the cannula to see the angle.</color>\n");

            sb.Append("Tourniquet: ").Append(s.TourniquetOn ? UIFactory.Tint("ON " + FormatTime(s.TourniquetSeconds), s.TourniquetSeconds > s.clinical.tourniquetLimitSeconds ? "#FF6B6B" : "#F2C14E") : "off").Append('\n');
            sb.Append("Gloves: ").Append(s.GlovesOn ? "on" : UIFactory.Tint("off", "#F2C14E")).Append('\n');
            var cl = s.cleaning;
            if (cl != null && cl.HasStarted)
            {
                if (!cl.IsComplete)
                    sb.Append("Cleaning: ").Append(Mathf.RoundToInt(cl.Coverage * 100f)).Append("% area, ")
                      .Append(cl.ScrubSeconds.ToString("0")).Append('/').Append(s.clinical.requiredScrubSeconds.ToString("0")).Append(" s\n");
                else
                    sb.Append("Antiseptic: ").Append(cl.IsDry ? UIFactory.Tint("dry", "#5FD38D") : "drying " + Mathf.RoundToInt(cl.DryProgress * 100f) + "%").Append('\n');
            }
            var gz = ProcedureAction.Find(ActionKind.Gauze);
            if (gz != null && gz.IsPlacedOnSite)
            {
                float secs = Mathf.Max(0.5f, s.clinical.pressureSeconds);
                sb.Append("Pressure: ").Append(gz.IsDone ? UIFactory.Tint("done", "#5FD38D")
                    : (gz.IsPressing ? UIFactory.Tint("pressing ", "#5FD38D") : UIFactory.Tint("not pressing ", "#F2C14E"))
                      + (gz.Progress * secs).ToString("0.0") + " / " + secs.ToString("0") + " s").Append('\n');
            }
            if (s.Metrics.attempts.Count > 0)
                sb.Append("Attempts: ").Append(s.Metrics.attempts.Count).Append(p.maxAttempts > 0 ? " / " + p.maxAttempts : "").Append('\n');
            return sb.ToString();
        }

        static string ControlsLine(IVSession s, bool vr)
        {
            if (vr) return "Grip: grab  |  Trigger: use / palpate / advance catheter  |  A/X: hints  |  B/Y: recentre";
            return "L-click: pick up/use  |  hold L on skin: palpate/scrub  |  R-click: put down  |  R-drag: orbit  |  Tab: hide panel  |  " +
                   "Cannula: W/S angle, A/D aim, Q/E bevel, Space push, X pull, F catheter, Shift fine";
        }

        string MenuText(IVSession s, bool vr)
        {
            var sb = new StringBuilder();
            sb.Append("<size=34><b>Peripheral IV cannulation - practice module</b></size>\n\n");
            sb.Append("Practise the full procedure safely: preparation, tourniquet, vein selection, skin antisepsis, ");
            sb.Append("insertion at the correct angle, advancing the catheter, sampling, flushing, securing and removal.\n\n");
            sb.Append("<b>Choose a level</b>  (buttons on the left of the table").Append(vr ? "" : ", or keys 1 / 2 / 3").Append(")\n");
            foreach (var p in s.profiles)
                sb.Append("  <color=#73C7FF><b>").Append(p.label).Append("</b></color>: ").Append(p.description).Append('\n');
            sb.Append("\n<b>Controls</b>\n");
            if (vr)
            {
                sb.Append("  Grip = grab tools,  Trigger = use / palpate (empty hand on skin) / advance catheter,\n");
                sb.Append("  A or X = hints on/off,  B or Y = recentre at the bedside. Push buttons by touching them.\n");
            }
            else
            {
                sb.Append("  Left-click a tool to pick it up; right-click or G to put it down. Right-drag orbits, scroll zooms, C centres.\n");
                sb.Append("  Hold left on the skin with an empty hand to palpate; hold left while moving the swab to scrub.\n");
                sb.Append("  Cannula: W/S angle, A/D aim, Q/E bevel, Space push in, X pull back, F advance catheter, Shift = fine control.\n");
            }
            sb.Append("\n<size=20><color=#7E8B99>Participant: ").Append(s.participantId).Append("   (set on the IVSession object)</color></size>");
            return sb.ToString();
        }

        string ReportText(IVSession s, bool vr)
        {
            var sb = new StringBuilder();
            string hex = s.criticalErrors.Count > 0 ? "#FF6B6B" : s.ReportScore >= 85f ? "#5FD38D" : s.ReportScore >= 70f ? "#F2C14E" : "#FF6B6B";
            sb.Append("<size=40><b>Score: ").Append(UIFactory.Tint(Mathf.RoundToInt(s.ReportScore) + " / 100", hex)).Append("</b></size>   ")
              .Append("<size=28>").Append(UIFactory.Tint(s.ReportGrade, hex)).Append("</size>\n");
            sb.Append("<size=21><color=#9FB3C8>").Append(s.Profile.label).Append("  |  time ").Append(FormatTime(s.Metrics.Duration))
              .Append("  |  ").Append(s.Metrics.endReason).Append("</color></size>\n");
            if (s.criticalErrors.Count > 0)
                sb.Append("<size=22><color=#FF6B6B>Critical: ").Append(string.Join(", ", s.criticalErrors.ToArray())).Append("</color></size>\n");
            sb.Append('\n');
            sb.Append("<size=19>");
            foreach (var l in s.reportLines) sb.Append(l).Append('\n');
            sb.Append("</size>");
            sb.Append("\n<size=19><color=#7E8B99>Use RESTART to try again or MENU to change level").Append(vr ? "" : " (keys 1/2/3)").Append(".</color></size>");
            return sb.ToString();
        }

        static string FormatTime(float t)
        {
            int m = Mathf.FloorToInt(t / 60f);
            int sec = Mathf.FloorToInt(t % 60f);
            return m.ToString("00") + ":" + sec.ToString("00");
        }
    }
}
