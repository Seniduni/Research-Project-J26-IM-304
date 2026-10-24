using System.Collections.Generic;
using UnityEngine;

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
                case ActionKind.Gauze: return attached ? "Hold to apply pressure" : "Sterile gauze";
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
                    s.Feedback("Catheter removed. Check that the catheter tip is intact.", Severity.Good, FeedbackKind.Event);
                    s.CompleteStep(StepId.RemoveCannula);
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
            if (c == null) return;
            Vector3 n = c.EntryNormal;
            if (movable != null)
                MoveTo(movable, c.EntryPoint + n * 0.003f, Quaternion.LookRotation(Vector3.ProjectOnPlane(c.PlacedDirection, n).normalized, n), 0.6f);
            attached = true;
            s.Feedback("Gauze in place - now hold firm pressure.", Severity.Info, FeedbackKind.Guidance);
        }

        void HoldGauze(IVSession s, IVHand hand, float dt)
        {
            progress = Mathf.Min(1f, progress + dt / Mathf.Max(0.5f, s.clinical.pressureSeconds));
            if (Time.frameCount % 10 == 0) hand.Haptic(0.12f, 0.03f);
            if (progress >= 1f)
            {
                done = true;
                s.Metrics.pressureApplied = true;
                s.Feedback("Bleeding has stopped.", Severity.Good, FeedbackKind.Event);
                s.CompleteStep(StepId.ApplyPressure);
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
