using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace IVSim
{
    public enum ActionKind
    {
        HandHygiene,
        Tourniquet,
        AlcoholBottle,
        SharpsBin,
        BloodSyringe,
        SalineSyringe,
        Dressing,
        RemoveCannula,
        Gauze
    }

    /// <summary>
    /// The simpler procedure steps, each triggered by clicking (desktop) or trigger-touching (VR) an object.
    /// One component handles all kinds so the logic for the whole "supporting" workflow lives in one place.
    /// </summary>
    public class ProcedureAction : Interactable, IResettable
    {
        static readonly List<ProcedureAction> all = new List<ProcedureAction>();
        public static ProcedureAction Find(ActionKind k)
        {
            foreach (var a in all) if (a != null && a.kind == k) return a;
            return null;
        }

        public ActionKind kind;

        [Header("Visuals (optional, depends on kind)")]
        [Tooltip("Shown while the item is on the tray (e.g. tourniquet strip)")] public GameObject trayVisual;
        [Tooltip("Shown when applied to the patient (tourniquet band, dressing on the arm)")] public GameObject appliedVisual;
        [Tooltip("Object that moves onto the arm (syringe, gauze)")] public Transform movable;
        [Tooltip("Fluid inside a syringe; scaled along local Z (pivot at the nozzle end)")] public Transform fill;
        public Transform plunger;
        public float plungerTravel = 0.035f;
        [Tooltip("Seconds of holding needed (syringes)")] public float holdSeconds = 2.5f;

        bool attached, done;
        float progress;
        Vector3 movableHomePos; Quaternion movableHomeRot;
        Vector3 plungerHome; Vector3 fillHomeScale;
        Vector3 appliedHomePos; Quaternion appliedHomeRot;

        // ---- gauze pressure visuals (created at runtime, so existing scenes get them without rebuilding)
        float lastHoldTime = -10f;
        float arriveTime;
        float doneTime;
        bool wasPressing;
        float stainSize;
        Transform padT, pressFingers, stain;
        Vector3 padBaseScale, padBasePos;
        float padTop;
        Canvas barCanvas;
        RectTransform barFill;
        Text barText;
        BoxCollider ownBox;
        Vector3 boxBaseSize, boxBaseCenter;

        /// <summary>Gauze: true once it has been placed on the puncture site.</summary>
        public bool IsPlacedOnSite { get { return attached; } }
        public bool IsDone { get { return done; } }
        public float Progress { get { return progress; } }
        public bool IsPressing { get { return attached && !done && Time.time - lastHoldTime < 0.12f && Time.time >= arriveTime; } }

        protected override void Awake()
        {
            base.Awake();
            if (!all.Contains(this)) all.Add(this);
            if (movable != null) { movableHomePos = movable.position; movableHomeRot = movable.rotation; }
            if (plunger != null) plungerHome = plunger.localPosition;
            if (fill != null) fillHomeScale = fill.localScale;
            if (appliedVisual != null) { appliedHomePos = appliedVisual.transform.position; appliedHomeRot = appliedVisual.transform.rotation; }
        }

        void OnDestroy() { all.Remove(this); }

        void Start()
        {
            if (IVSession.Instance != null) IVSession.Instance.RegisterResettable(this);
            ResetVisuals();
        }

        public override bool IsAvailable { get { return isActiveAndEnabled && !(done && (kind == ActionKind.BloodSyringe || kind == ActionKind.SalineSyringe || kind == ActionKind.Gauze)); } }

        public override string HoverText(IVHand hand)
        {
            var s = IVSession.Instance;
            switch (kind)
            {
                case ActionKind.Tourniquet: return s != null && s.TourniquetOn ? "Tourniquet - use to RELEASE" : "Tourniquet - use to APPLY";
                case ActionKind.AlcoholBottle: return hand != null && hand.Held is Swab ? "Alcohol - use to wet the swab" : "Alcohol antiseptic (pick up a swab first)";
                case ActionKind.SharpsBin: return "Sharps container";
                case ActionKind.BloodSyringe: return attached ? "Hold to draw blood" : "Syringe for blood sample";
                case ActionKind.SalineSyringe: return attached ? "Hold to flush" : "0.9% saline flush";
                case ActionKind.Gauze: return attached ? "HOLD to press on the gauze" : "Sterile gauze - use to place on the site";
                default: return base.HoverText(hand);
            }
        }

        void Update()
        {
            if (kind == ActionKind.Tourniquet)
            {
                bool on = IVSession.Instance != null && IVSession.Instance.TourniquetOn;
                if (trayVisual != null && trayVisual.activeSelf == on) trayVisual.SetActive(!on);
                if (appliedVisual != null && appliedVisual.activeSelf != on) appliedVisual.SetActive(on);
            }
            if (kind == ActionKind.Gauze) UpdateGauzeVisuals();
        }

        // ------------------------------------------------------------------ use

        public override void OnUseStart(IVHand hand)
        {
            var s = IVSession.Instance;
            if (s == null) return;
            switch (kind)
            {
                case ActionKind.HandHygiene:
                    s.PerformHandHygiene();
                    hand.Haptic(0.2f, 0.05f);
                    break;

                case ActionKind.Tourniquet:
                    if (s.TourniquetOn) s.ReleaseTourniquet();
                    else s.TryApplyTourniquet();
                    hand.Haptic(0.3f, 0.06f);
                    break;

                case ActionKind.AlcoholBottle:
                    {
                        var sw = hand.Held as Swab;
                        if (sw != null) sw.Wet();
                        else s.FeedbackOnce("bottle", "Pick up a swab first, then touch it to the alcohol bottle.", Severity.Info, FeedbackKind.Guidance, 3f);
                    }
                    break;

                case ActionKind.SharpsBin:
                    {
                        var c = hand.Held as Cannula;
                        if (c != null && c.IsUsed && !c.IsInSkin)
                        {
                            hand.ForceRelease();
                            c.Dispose();
                            hand.Haptic(0.3f, 0.05f);
                        }
                        else s.FeedbackOnce("bin", "Sharps container - used needles go straight in here.", Severity.Info, FeedbackKind.Guidance, 3f);
                    }
                    break;

                case ActionKind.BloodSyringe:
                case ActionKind.SalineSyringe:
                    TryAttachSyringe(s);
                    break;

                case ActionKind.Dressing:
                    TryDressing(s);
                    break;

                case ActionKind.RemoveCannula:
                    TryRemove(s);
                    break;

                case ActionKind.Gauze:
                    TryGauze(s);
                    break;
            }
        }

        public override void OnUseHold(IVHand hand, float dt)
        {
            var s = IVSession.Instance;
            if (s == null || done || !attached) return;
            if (kind == ActionKind.BloodSyringe) HoldBlood(s, hand, dt);
            else if (kind == ActionKind.SalineSyringe) HoldSaline(s, hand, dt);
            else if (kind == ActionKind.Gauze) HoldGauze(s, hand, dt);
        }

        bool CatheterReady(IVSession s)
        {
            var c = s.cannula;
            if (c == null || !c.CatheterPlaced)
            {
                s.FeedbackOnce("nocath", "The cannula has not been placed yet.", Severity.Info, FeedbackKind.Guidance, 3f);
                return false;
            }
            if (c.Phase == CannulaPhase.InSkin)
            {
                s.FeedbackOnce("needlein", "Withdraw the needle first.", Severity.Info, FeedbackKind.Guidance, 3f);
                return false;
            }
            return true;
        }

        void TryAttachSyringe(IVSession s)
        {
            if (attached || done) return;
            StepId step = kind == ActionKind.BloodSyringe ? StepId.DrawBlood : StepId.FlushLine;
            if (!CatheterReady(s) || !s.RequestAction(step)) return;
            Pose p = s.cannula.HubPortPose;
            if (movable != null) MoveTo(movable, p.position, p.rotation, 0.6f);
            attached = true;
            s.Feedback(kind == ActionKind.BloodSyringe ? "Syringe connected. Hold to draw back gently." : "Saline syringe connected. Hold to flush.", Severity.Info, FeedbackKind.Guidance);
        }

        void HoldBlood(IVSession s, IVHand hand, float dt)
        {
            if (!s.cannula.CatheterInVein)
            {
                s.FeedbackOnce("noblood", "No blood return - the catheter is not inside the vein.", Severity.Warning, FeedbackKind.Event, 3f);
                return;
            }
            progress = Mathf.Min(1f, progress + dt / holdSeconds);
            SetSyringeVisual(progress, true);
            if (Time.frameCount % 8 == 0) hand.Haptic(0.05f, 0.02f);
            if (progress >= 1f)
            {
                done = true;
                s.Metrics.bloodDrawn = true;
                s.Feedback("Blood sample obtained.", Severity.Good, FeedbackKind.Event);
                s.CompleteStep(StepId.DrawBlood);
                if (movable != null) MoveTo(movable, movableHomePos, movableHomeRot, 0.6f);
            }
        }

        void HoldSaline(IVSession s, IVHand hand, float dt)
        {
            progress = Mathf.Min(1f, progress + dt / holdSeconds);
            SetSyringeVisual(progress, false);
            if (Time.frameCount % 8 == 0) hand.Haptic(0.05f, 0.02f);
            if (!s.cannula.CatheterInVein && progress > 0.3f && !s.Metrics.flushed)
            {
                s.Metrics.flushed = true;
                s.Metrics.flushOk = false;
                Vector3 n = s.cannula.EntryNormal;
                BloodFX.Swelling(s.cannula.EntryPoint + s.cannula.PlacedDirection * 0.015f, n);
                s.Feedback("Swelling and pain around the site - INFILTRATION. Stop the flush, remove the cannula and re-site.", Severity.Error, FeedbackKind.Event);
            }
            if (progress >= 1f)
            {
                done = true;
                if (!s.Metrics.flushed)
                {
                    s.Metrics.flushed = true;
                    s.Metrics.flushOk = s.cannula.CatheterInVein;
                }
                if (s.Metrics.flushOk) s.Feedback("Flushes easily with no swelling or pain - the cannula is patent.", Severity.Good, FeedbackKind.Event);
                s.CompleteStep(StepId.FlushLine);
                if (movable != null) MoveTo(movable, movableHomePos, movableHomeRot, 0.6f);
            }
        }

        void SetSyringeVisual(float k, bool drawing)
        {
            if (fill != null)
            {
                fill.gameObject.SetActive(drawing ? k > 0.01f : k < 0.99f);
                Vector3 sc = fillHomeScale;
                sc.z = Mathf.Max(0.001f, fillHomeScale.z * (drawing ? k : 1f - k));
                fill.localScale = sc;
            }
            if (plunger != null)
                plunger.localPosition = plungerHome + Vector3.forward * (drawing ? -plungerTravel * k : plungerTravel * k);
        }

        void TryDressing(IVSession s)
        {
            if (done) return;
            if (!CatheterReady(s) || !s.RequestAction(StepId.SecureDressing)) return;
            var c = s.cannula;
            Vector3 n = c.EntryNormal;
            Vector3 d = c.PlacedDirection;
            Vector3 flat = (d - Vector3.Dot(d, n) * n).normalized;
            Vector3 center = c.EntryPoint - flat * 0.012f + n * 0.0035f;
            if (appliedVisual != null)
            {
                appliedVisual.SetActive(true);
                appliedVisual.transform.SetPositionAndRotation(center, Quaternion.LookRotation(flat, n));
            }
            if (trayVisual != null) trayVisual.SetActive(false);
            done = true;
            s.Metrics.dressingApplied = true;
            s.CompleteStep(StepId.SecureDressing);
            s.Feedback("Transparent dressing applied - the site stays visible for monitoring.", Severity.Good, FeedbackKind.Event);
        }

        void TryRemove(IVSession s)
        {
            if (done) return;
            if (!s.RequestAction(StepId.RemoveCannula)) return;
            done = true;
            var dressing = Find(ActionKind.Dressing);
            if (dressing != null) dressing.HideApplied();
            if (s.cannula != null)
            {
                s.cannula.RemovePlacedCatheter(() =>
                {
                    s.Metrics.cannulaRemoved = true;
                    BloodFX.Spill(s.cannula.EntryPoint, s.cannula.EntryNormal, 0.006f);
                    s.Feedback("Catheter removed. Check that the catheter tip is intact.", Severity.Good, FeedbackKind.Event);
                    s.CompleteStep(StepId.RemoveCannula);
                    s.Feedback("The puncture site is oozing blood - place the gauze on it and apply pressure.", Severity.Info, FeedbackKind.Event);
                });
            }
        }

        public void HideApplied()
        {
            if (appliedVisual != null) appliedVisual.SetActive(false);
        }

        void TryGauze(IVSession s)
        {
            if (attached || done) return;
            if (!s.RequestAction(StepId.ApplyPressure)) return;
            var c = s.cannula;
            if (c == null || movable == null) return;
            EnsureGauzeVisuals();
            Vector3 n = c.EntryNormal;
            Vector3 fwd = Vector3.ProjectOnPlane(c.PlacedDirection, n);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(Vector3.forward, n);
            Vector3 target = c.EntryPoint + n * 0.0015f;
            MoveTo(movable, target, Quaternion.LookRotation(fwd.normalized, n), 0.6f);
            arriveTime = Time.time + 0.65f;
            attached = true;
            stainSize = 0f;
            // a bigger target so it is easy to press on, in VR and with the mouse
            if (ownBox != null)
            {
                ownBox.size = new Vector3(0.06f, 0.04f, 0.06f);
                ownBox.center = new Vector3(0f, 0.012f, 0f);
            }
            string how = RigBootstrap.IsVR
                ? "Gauze placed. Rest your hand on it and HOLD the trigger (or grip) until the bar fills."
                : "Gauze placed. Now HOLD the left mouse button on the gauze until the bar fills.";
            s.Feedback(how, Severity.Info, FeedbackKind.Event);
        }

        void HoldGauze(IVSession s, IVHand hand, float dt)
        {
            if (Time.time < arriveTime) return;          // still moving onto the site
            lastHoldTime = Time.time;
            progress = Mathf.Min(1f, progress + dt / Mathf.Max(0.5f, s.clinical.pressureSeconds));
            if (Time.frameCount % 10 == 0) hand.Haptic(0.15f, 0.03f);
            if (progress >= 1f)
            {
                done = true;
                doneTime = Time.time;
                s.Metrics.pressureApplied = true;
                s.Feedback("Bleeding has stopped. Well done!", Severity.Good, FeedbackKind.Event);
                s.CompleteStep(StepId.ApplyPressure);
            }
        }

        void EnsureGauzeVisuals()
        {
            if (barCanvas != null || movable == null) return;
            var s = IVSession.Instance;
            ownBox = GetComponent<BoxCollider>();
            if (ownBox != null) { boxBaseSize = ownBox.size; boxBaseCenter = ownBox.center; }

            var padR = movable.GetComponentInChildren<MeshRenderer>();
            if (padR != null)
            {
                padT = padR.transform;
                padBaseScale = padT.localScale;
                padBasePos = padT.localPosition;
                padTop = padBasePos.y + padBaseScale.y * 0.5f;
            }

            // two gloved fingertips that press down (desktop; in VR your own hand does this)
            pressFingers = new GameObject("PressingFingers").transform;
            pressFingers.SetParent(movable, false);
            Material glove = null;
            if (padR != null)
            {
                glove = new Material(padR.sharedMaterial);
                glove.SetColor("_BaseColor", new Color(0.35f, 0.55f, 0.95f, 1f));
            }
            for (int i = 0; i < 2; i++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                f.name = "Fingertip";
                var col = f.GetComponent<Collider>();
                if (col != null) Destroy(col);
                f.transform.SetParent(pressFingers, false);
                f.transform.localPosition = new Vector3(i == 0 ? -0.0085f : 0.0085f, padTop + 0.02f, 0.002f);
                f.transform.localRotation = Quaternion.Euler(i == 0 ? 12f : -12f, 0f, i == 0 ? -8f : 8f);
                f.transform.localScale = new Vector3(0.015f, 0.02f, 0.015f);
                if (glove != null) f.GetComponent<MeshRenderer>().sharedMaterial = glove;
            }
            pressFingers.gameObject.SetActive(false);

            // blood soaking into the gauze
            if (s != null && s.bloodMaterial != null)
            {
                var st = new GameObject("BloodStain");
                st.transform.SetParent(movable, false);
                st.transform.localPosition = new Vector3(0f, padTop + 0.0004f, 0f);
                st.AddComponent<MeshFilter>().sharedMesh = MeshBuilder.Disc("Stain", 20);
                var r = st.AddComponent<MeshRenderer>();
                r.sharedMaterial = s.bloodMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                st.transform.localScale = Vector3.zero;
                stain = st.transform;
            }

            // progress bar above the gauze
            barCanvas = UIFactory.WorldCanvas("PressureBar", movable, new Vector2(460, 110), 0.00016f);
            barCanvas.transform.localPosition = new Vector3(0f, 0.055f, 0f);
            var root = barCanvas.transform;
            UIFactory.Panel("Bg", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.05f, 0.07f, 0.1f, 0.85f));
            UIFactory.Panel("Track", root, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(16f, 12f), new Vector2(-16f, 40f), new Color(0.25f, 0.28f, 0.33f, 1f));
            var fillImg = UIFactory.Panel("Fill", root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(16f, 12f), new Vector2(-16f, 40f), new Color(0.37f, 0.83f, 0.55f, 1f));
            barFill = fillImg.rectTransform;
            barText = UIFactory.Label("Text", root, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(10f, 44f), new Vector2(-10f, -6f), 30, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
            barCanvas.gameObject.SetActive(false);
        }

        void UpdateGauzeVisuals()
        {
            var s = IVSession.Instance;
            if (barCanvas == null || s == null) return;
            bool pressing = IsPressing;
            float dt = Time.deltaTime;

            // squash the pad while pressed
            if (padT != null)
            {
                float k = pressing || done ? 1f : 0f;
                Vector3 targetScale = Vector3.Scale(padBaseScale, new Vector3(1f + 0.06f * k, 1f - 0.45f * k, 1f + 0.06f * k));
                Vector3 targetPos = padBasePos - new Vector3(0f, padBaseScale.y * 0.22f * k, 0f);
                padT.localScale = Vector3.Lerp(padT.localScale, targetScale, dt * 14f);
                padT.localPosition = Vector3.Lerp(padT.localPosition, targetPos, dt * 14f);
            }

            // fingertips (desktop only) move down when pressing
            if (pressFingers != null)
            {
                bool showFingers = pressing && !RigBootstrap.IsVR;
                if (pressFingers.gameObject.activeSelf != showFingers) pressFingers.gameObject.SetActive(showFingers);
                if (showFingers)
                {
                    float bob = 0.0006f * Mathf.Sin(Time.time * 9f);
                    pressFingers.localPosition = new Vector3(0f, -padBaseScale.y * 0.42f + bob, 0f);
                }
            }

            // the site bleeds into the gauze until firm pressure is held
            if (stain != null && attached && Time.time >= arriveTime)
            {
                float maxStain = done ? stainSize : 0.032f;
                if (!pressing && !done) stainSize = Mathf.MoveTowards(stainSize, maxStain, dt * 0.004f);
                else if (stainSize < 0.008f) stainSize = Mathf.MoveTowards(stainSize, 0.008f, dt * 0.004f);
                stain.localScale = new Vector3(stainSize, 1f, stainSize * 0.85f);
                if (padT != null) stain.localPosition = new Vector3(0f, padT.localPosition.y + padT.localScale.y * 0.5f + 0.0004f, 0f);
            }

            // warn if pressure is released before the bleeding stops
            if (wasPressing && !pressing && attached && !done && progress > 0.05f)
                s.FeedbackOnce("keeppress", "Keep continuous, firm pressure until the bleeding stops.", Severity.Warning, FeedbackKind.Technique, 4f);
            wasPressing = pressing;

            // progress bar
            bool showBar = attached && (!done || Time.time - doneTime < 2.5f) && Time.time >= arriveTime;
            if (barCanvas.gameObject.activeSelf != showBar) barCanvas.gameObject.SetActive(showBar);
            if (!showBar) return;
            barFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 0f);
            float secs = Mathf.Max(0.5f, s.clinical.pressureSeconds);
            if (done) barText.text = "<color=#5FD38D>Bleeding stopped</color>";
            else if (pressing) barText.text = "Pressing...  " + (progress * secs).ToString("0.0") + " / " + secs.ToString("0") + " s";
            else barText.text = RigBootstrap.IsVR ? "<color=#F2C14E>HOLD trigger on the gauze</color>" : "<color=#F2C14E>HOLD left mouse on the gauze</color>";
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 d = barCanvas.transform.position - cam.transform.position;
                if (d.sqrMagnitude > 1e-6f) barCanvas.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
            }
        }

        // ------------------------------------------------------------------ reset

        void ResetVisuals()
        {
            attached = false;
            done = false;
            progress = 0f;
            StopMove();
            if (movable != null) movable.SetPositionAndRotation(movableHomePos, movableHomeRot);
            if (plunger != null) plunger.localPosition = plungerHome;
            if (fill != null)
            {
                fill.localScale = fillHomeScale;
                fill.gameObject.SetActive(kind == ActionKind.SalineSyringe);
            }
            if (kind == ActionKind.Gauze)
            {
                lastHoldTime = -10f;
                wasPressing = false;
                stainSize = 0f;
                if (padT != null) { padT.localScale = padBaseScale; padT.localPosition = padBasePos; }
                if (stain != null) stain.localScale = Vector3.zero;
                if (pressFingers != null) pressFingers.gameObject.SetActive(false);
                if (barCanvas != null) barCanvas.gameObject.SetActive(false);
                if (ownBox != null) { ownBox.size = boxBaseSize; ownBox.center = boxBaseCenter; }
            }
            if (kind == ActionKind.Dressing)
            {
                if (appliedVisual != null)
                {
                    appliedVisual.SetActive(false);
                    appliedVisual.transform.SetPositionAndRotation(appliedHomePos, appliedHomeRot);
                }
                if (trayVisual != null) trayVisual.SetActive(true);
            }
        }

        public void ResetForSession()
        {
            ResetVisuals();
        }
    }
}
