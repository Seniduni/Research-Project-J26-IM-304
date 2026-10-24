using System;
using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    public interface IResettable { void ResetForSession(); }

    public class FeedbackEntry
    {
        public string text;
        public Severity severity;
        public FeedbackKind kind;
        public float time;
        public bool shown;
    }

    /// <summary>
    /// The heart of the module: tracks the procedure steps, difficulty, scoring metrics and feedback.
    /// Tools (cannula, swab, tourniquet...) call into this class; the UI reads from it.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class IVSession : MonoBehaviour
    {
        public static IVSession Instance { get; private set; }

        [Header("Research logging")]
        [Tooltip("Set this before each participant. Written to the CSV logs.")]
        public string participantId = "P001";
        public bool writeCsvLogs = true;

        [Header("Difficulty levels")]
        public DifficultyProfile[] profiles = DifficultyProfile.Defaults();
        [Tooltip("Difficulty shown while in the menu")] public Difficulty menuPreview = Difficulty.Easy;

        [Header("Clinical thresholds")]
        public ClinicalSettings clinical = new ClinicalSettings();

        [Header("Procedure steps (edit text to match your protocol)")]
        public List<StepDef> steps = StepDef.Defaults();

        [Header("Patient")]
        public Color skinTone = new Color(0.71f, 0.52f, 0.40f, 1f);

        [Header("Scene references (set by the scene builder)")]
        public ArmModel arm;
        public SiteCleaning cleaning;
        public Cannula cannula;
        public Material skinOpaque;
        public Material skinTransparent;
        public Material bloodMaterial;
        public Material hematomaMaterial;
        public Material bruiseMaterial;
        public GameObject angleReferenceBoard;

        public SessionState State { get; private set; }
        public DifficultyProfile Profile { get; private set; }
        public SessionMetrics Metrics { get; private set; }
        public string ControlMode = "Desktop";
        public bool HintsVisible = true;

        public bool GlovesOn { get; private set; }
        public bool TourniquetOn { get; private set; }
        float tourniquetOnSince;
        bool tourniquetWarned;

        public bool SiteSelected { get { return Metrics != null && Metrics.siteSelected; } }
        public Vector3 SitePoint { get { return Metrics != null ? Metrics.sitePoint : Vector3.zero; } }

        public readonly List<FeedbackEntry> feedback = new List<FeedbackEntry>();
        public event Action<FeedbackEntry> FeedbackAdded;
        public event Action Changed;

        // report
        public float ReportScore { get; private set; }
        public string ReportGrade { get; private set; }
        public readonly List<string> reportLines = new List<string>();
        public readonly List<string> criticalErrors = new List<string>();

        StepStatus[] status;
        readonly List<IResettable> resettables = new List<IResettable>();
        readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();
        readonly HashSet<StepId> orderViolationSteps = new HashSet<StepId>();
        Material skinOpaqueInst, skinTransInst;
        int sessionCounter;

        // ------------------------------------------------------------------ lifecycle

        void Awake()
        {
            Instance = this;
            if (steps == null || steps.Count == 0) steps = StepDef.Defaults();
            if (profiles == null || profiles.Length == 0) profiles = DifficultyProfile.Defaults();
            status = new StepStatus[steps.Count];
            Metrics = new SessionMetrics();
            Profile = GetProfile(menuPreview);
            if (skinOpaque != null) skinOpaqueInst = new Material(skinOpaque);
            if (skinTransparent != null) skinTransInst = new Material(skinTransparent);
            if (arm == null) arm = ArmModel.Instance;
            State = SessionState.Menu;
        }

        void Start()
        {
            ApplyDifficultyVisuals(Profile);
            Feedback("Welcome! Choose a difficulty level to begin.", Severity.Info, FeedbackKind.Event);
        }

        public void RegisterResettable(IResettable r) { if (!resettables.Contains(r)) resettables.Add(r); }

        public DifficultyProfile GetProfile(Difficulty d)
        {
            foreach (var p in profiles) if (p.level == d) return p;
            return profiles[0];
        }

        public void StartSession(Difficulty d)
        {
            Profile = GetProfile(d);
            ResetWorld();
            sessionCounter++;
            Metrics = new SessionMetrics
            {
                sessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + sessionCounter,
                participantId = participantId,
                mode = ControlMode,
                difficulty = d,
                startTime = Time.time
            };
            status = new StepStatus[steps.Count];
            UpdateCurrent();
            feedback.Clear();
            reportLines.Clear();
            criticalErrors.Clear();
            orderViolationSteps.Clear();
            cooldowns.Clear();
            State = SessionState.Running;
            ApplyDifficultyVisuals(Profile);
            Feedback("Session started: " + Profile.label + ". " + CurrentStepDef.instruction, Severity.Info, FeedbackKind.Event);
            RaiseChanged();
        }

        public void RestartSession() { StartSession(Profile.level); }

        public void ReturnToMenu()
        {
            ResetWorld();
            State = SessionState.Menu;
            Profile = GetProfile(menuPreview);
            ApplyDifficultyVisuals(Profile);
            feedback.Clear();
            Feedback("Choose a difficulty level to begin.", Severity.Info, FeedbackKind.Event);
            RaiseChanged();
        }

        void ResetWorld()
        {
            GlovesOn = false;
            TourniquetOn = false;
            tourniquetWarned = false;
            VascularSystem.SnapEngorged(false);
            BloodFX.ClearAll();
            foreach (var r in resettables) if (r != null) r.ResetForSession();
        }

        public void ApplyDifficultyVisuals(DifficultyProfile p)
        {
            if (arm != null && arm.skinRenderer != null)
            {
                if (p.skinOpacity < 0.999f && skinTransInst != null)
                {
                    Color c = skinTone; c.a = p.skinOpacity;
                    skinTransInst.SetColor("_BaseColor", c);
                    arm.skinRenderer.sharedMaterial = skinTransInst;
                }
                else if (skinOpaqueInst != null)
                {
                    Color c = skinTone; c.a = 1f;
                    skinOpaqueInst.SetColor("_BaseColor", c);
                    arm.skinRenderer.sharedMaterial = skinOpaqueInst;
                }
            }
            VascularSystem.SetVisible(p.showVeins);
            SiteZone.ShowMarkers(p.showSiteMarkers);
            if (angleReferenceBoard != null) angleReferenceBoard.SetActive(p.showAngleReference);
        }

        /// <summary>Opaque skin-coloured material (used for swelling).</summary>
        public Material SkinOpaqueInstance { get { return skinOpaqueInst; } }

        void Update()
        {
            if (State != SessionState.Running) return;

            if (TourniquetOn)
            {
                float t = Time.time - tourniquetOnSince;
                if (t > Metrics.tourniquetMaxSeconds) Metrics.tourniquetMaxSeconds = t;
                if (!tourniquetWarned && t > clinical.tourniquetLimitSeconds)
                {
                    tourniquetWarned = true;
                    Feedback("The tourniquet has been on for over " + Mathf.RoundToInt(clinical.tourniquetLimitSeconds) +
                             " s. Prolonged application causes discomfort and can affect blood results - release and re-apply if needed.",
                             Severity.Warning, FeedbackKind.Technique);
                }
            }

            if (cleaning != null && IsCurrent(StepId.LetDry) && cleaning.IsComplete && cleaning.IsDry)
            {
                CompleteStep(StepId.LetDry);
            }
        }

        public float TourniquetSeconds { get { return TourniquetOn ? Time.time - tourniquetOnSince : 0f; } }

        // ------------------------------------------------------------------ steps

        public int IndexOf(StepId id)
        {
            for (int i = 0; i < steps.Count; i++) if (steps[i].id == id) return i;
            return -1;
        }

        public int CurrentIndex { get; private set; }

        public StepDef CurrentStepDef
        {
            get { return CurrentIndex >= 0 && CurrentIndex < steps.Count ? steps[CurrentIndex] : null; }
        }

        public StepStatus GetStatus(int i) { return status != null && i >= 0 && i < status.Length ? status[i] : StepStatus.Pending; }

        public bool IsDone(StepId id)
        {
            int i = IndexOf(id);
            return i >= 0 && (status[i] == StepStatus.Done);
        }

        public bool IsCurrent(StepId id) { return State == SessionState.Running && IndexOf(id) == CurrentIndex; }

        void UpdateCurrent()
        {
            CurrentIndex = steps.Count;
            for (int i = 0; i < steps.Count; i++)
            {
                if (status[i] == StepStatus.Pending || status[i] == StepStatus.Current)
                {
                    CurrentIndex = i;
                    break;
                }
            }
            for (int i = 0; i < steps.Count; i++)
                if (status[i] == StepStatus.Current) status[i] = StepStatus.Pending;
            if (CurrentIndex < steps.Count) status[CurrentIndex] = StepStatus.Current;
        }

        /// <summary>
        /// Asks whether an action belonging to 'id' may happen now. In guided modes actions ahead of the current
        /// step are blocked with a hint; in assessment mode they are allowed but recorded as order violations.
        /// </summary>
        public bool RequestAction(StepId id)
        {
            if (State != SessionState.Running)
            {
                FeedbackOnce("start", "Choose a difficulty level first (buttons on the left of the table).", Severity.Info, FeedbackKind.Event, 3f);
                return false;
            }
            int idx = IndexOf(id);
            if (idx < 0 || idx <= CurrentIndex || status[idx] == StepStatus.Done) return true;
            if (Profile.enforceStepOrder)
            {
                FeedbackOnce("order" + id, "Not yet - first: " + CurrentStepDef.title + ".", Severity.Warning, FeedbackKind.Event, 2.5f);
                return false;
            }
            if (orderViolationSteps.Add(id)) Metrics.orderViolations++;
            return true;
        }

        public void CompleteStep(StepId id)
        {
            if (State != SessionState.Running) return;
            int idx = IndexOf(id);
            if (idx < 0 || status[idx] == StepStatus.Done) return;
            status[idx] = StepStatus.Done;
            for (int i = 0; i < idx; i++)
                if (status[i] == StepStatus.Pending || status[i] == StepStatus.Current) status[i] = StepStatus.Missed;
            UpdateCurrent();
            Feedback("Done: " + steps[idx].title, Severity.Good, FeedbackKind.Event);
            if (CurrentIndex >= steps.Count)
            {
                Finish(false, "Procedure complete");
                return;
            }
            if (Profile.showHints && HintsVisible)
                Feedback("Next: " + CurrentStepDef.title + " - " + CurrentStepDef.instruction, Severity.Info, FeedbackKind.Guidance);
            RaiseChanged();
        }

        /// <summary>Marks a step (and every later step) as not done, e.g. after a failed attempt.</summary>
        public void RollbackTo(StepId id)
        {
            int idx = IndexOf(id);
            if (idx < 0) return;
            for (int i = idx; i < steps.Count; i++) status[i] = StepStatus.Pending;
            UpdateCurrent();
            RaiseChanged();
        }

        void SetPending(StepId id)
        {
            int idx = IndexOf(id);
            if (idx < 0) return;
            status[idx] = StepStatus.Pending;
            UpdateCurrent();
            RaiseChanged();
        }

        // ------------------------------------------------------------------ feedback

        public void Feedback(string text, Severity severity, FeedbackKind kind)
        {
            var e = new FeedbackEntry { text = text, severity = severity, kind = kind, time = Time.time };
            bool show = true;
            if (State == SessionState.Running && Profile != null)
            {
                if (kind == FeedbackKind.Technique && !Profile.liveFeedback) show = false;
                if (kind == FeedbackKind.Guidance && !(Profile.showHints && HintsVisible)) show = false;
            }
            e.shown = show;
            feedback.Add(e);
            if (feedback.Count > 200) feedback.RemoveAt(0);
            if (show && FeedbackAdded != null) FeedbackAdded(e);
            RaiseChanged();
        }

        /// <summary>Feedback that will not repeat for 'cooldown' seconds.</summary>
        public void FeedbackOnce(string key, string text, Severity severity, FeedbackKind kind, float cooldown)
        {
            float last;
            if (cooldowns.TryGetValue(key, out last) && Time.time - last < cooldown) return;
            cooldowns[key] = Time.time;
            Feedback(text, severity, kind);
        }

        void RaiseChanged() { if (Changed != null) Changed(); }

        // ------------------------------------------------------------------ preparation

        public void PerformHandHygiene()
        {
            if (!RequestAction(StepId.Prepare)) return;
            if (GlovesOn) { FeedbackOnce("gloves", "Gloves are already on.", Severity.Info, FeedbackKind.Guidance, 3f); return; }
            GlovesOn = true;
            Metrics.handHygiene = true;
            CompleteStep(StepId.Prepare);
        }

        void NoteContact()
        {
            if (!GlovesOn && State == SessionState.Running)
            {
                Metrics.handHygieneBeforeContact = false;
                FeedbackOnce("nogloves", "Perform hand hygiene and put on gloves before touching the patient.", Severity.Warning, FeedbackKind.Technique, 6f);
            }
        }

        public bool TryApplyTourniquet()
        {
            if (!RequestAction(StepId.ApplyTourniquet)) return false;
            if (IsDone(StepId.ThreadCatheter))
            {
                FeedbackOnce("noreapply", "The catheter is already in - there is no need to re-apply the tourniquet.", Severity.Info, FeedbackKind.Guidance, 3f);
                return false;
            }
            NoteContact();
            TourniquetOn = true;
            tourniquetOnSince = Time.time;
            tourniquetWarned = false;
            VascularSystem.SetEngorged(true);
            if (!IsDone(StepId.ApplyTourniquet)) CompleteStep(StepId.ApplyTourniquet);
            Feedback("Tourniquet applied - the veins are filling and becoming easier to feel.", Severity.Good, FeedbackKind.Event);
            return true;
        }

        public void ReleaseTourniquet()
        {
            if (!TourniquetOn) return;
            TourniquetOn = false;
            VascularSystem.SetEngorged(false);
            if (State != SessionState.Running) return;

            if (IsDone(StepId.ThreadCatheter) || Metrics.catheterPlaced)
            {
                CompleteStep(StepId.ReleaseTourniquet);
            }
            else if (IsDone(StepId.InsertNeedle))
            {
                Feedback("Tourniquet released before the catheter was advanced - the vein may collapse. Release it after advancing the catheter.",
                         Severity.Warning, FeedbackKind.Technique);
            }
            else
            {
                if (IsDone(StepId.ApplyTourniquet)) SetPending(StepId.ApplyTourniquet);
                Feedback("Tourniquet released. Re-apply it before inserting the cannula.", Severity.Info, FeedbackKind.Guidance);
            }
        }

        // ------------------------------------------------------------------ assessment / palpation

        /// <summary>Called continuously while a hand presses on the skin.</summary>
        public string Palpate(Vector3 surfacePoint, Vector3 normal, float heldSeconds)
        {
            NoteContact();
            var res = VascularSystem.Palpate(surfacePoint, normal);
            string feel;
            if (!res.found) feel = "Firm tissue - no vein here.";
            else if (res.vessel.vesselType == VesselType.Artery) feel = "Pulsating - this is an ARTERY. Avoid.";
            else if (TourniquetOn) feel = "Soft, bouncy, refilling vein" + (Profile.showVeinLabels ? " (" + res.vessel.displayName + ")" : "") + ".";
            else feel = "A faint, flat vein - a tourniquet would make it easier to feel.";

            if (State != SessionState.Running) return feel;
            Metrics.palpated = true;

            // touching the cleaned area contaminates it
            if (cleaning != null && cleaning.HasStarted && !Metrics.catheterPlaced && !IsDone(StepId.InsertNeedle)
                && Vector3.Distance(surfacePoint, cleaning.Center) < clinical.cleanRadius)
            {
                bool wasComplete = cleaning.IsComplete;
                cleaning.Contaminate();
                if (wasComplete)
                {
                    Metrics.recontaminated = true;
                    if (IsDone(StepId.CleanSite)) SetPending(StepId.CleanSite);
                    if (IsDone(StepId.LetDry)) SetPending(StepId.LetDry);
                    Feedback("You touched the cleaned site - it is no longer aseptic. Clean it again with a new swab.", Severity.Error, FeedbackKind.Event);
                }
                else
                {
                    FeedbackOnce("cleanreset", "Palpating inside the area you are cleaning resets the cleaning.", Severity.Warning, FeedbackKind.Guidance, 4f);
                }
            }

            bool canReselect = !IsDone(StepId.SelectVein) || (cleaning == null || !cleaning.HasStarted);
            if (res.found && res.vessel.vesselType == VesselType.Vein && heldSeconds >= 0.8f && canReselect)
            {
                if (!IsDone(StepId.SelectVein) && !RequestAction(StepId.SelectVein)) return feel;
                Vector3 site = res.vesselPoint + normal * res.depth;
                if (Metrics.siteSelected && Vector3.Distance(site, Metrics.sitePoint) < 0.006f) return feel;
                string note;
                SiteRating rating = SiteZone.Rate(site, res.vessel, out note);
                Metrics.siteSelected = true;
                Metrics.sitePoint = site;
                Metrics.siteVessel = res.vessel.displayName;
                Metrics.siteRating = rating;
                Metrics.siteNote = note;
                if (!IsDone(StepId.SelectVein))
                {
                    if (Profile.level == Difficulty.Hard) Feedback("Site selected.", Severity.Info, FeedbackKind.Event);
                    CompleteStep(StepId.SelectVein);
                }
                string label = Profile.level == Difficulty.Hard ? "Site" : res.vessel.displayName;
                Severity sev = rating == SiteRating.Ideal ? Severity.Good : rating == SiteRating.Acceptable ? Severity.Info : Severity.Warning;
                Feedback(label + ": " + RatingText(rating) + (string.IsNullOrEmpty(note) ? "" : " - " + note), sev, FeedbackKind.Technique);
            }
            return feel;
        }

        public static string RatingText(SiteRating r)
        {
            switch (r)
            {
                case SiteRating.Ideal: return "ideal site";
                case SiteRating.Acceptable: return "acceptable site";
                case SiteRating.Avoid: return "site to AVOID";
                default: return "not suitable";
            }
        }

        // ------------------------------------------------------------------ cleaning

        public bool CanClean()
        {
            if (!RequestAction(StepId.CleanSite)) return false;
            if (!Metrics.siteSelected && Profile.enforceStepOrder)
            {
                FeedbackOnce("selectfirst", "Palpate and select a vein before cleaning.", Severity.Warning, FeedbackKind.Guidance, 3f);
                return false;
            }
            NoteContact();
            return true;
        }

        public void OnCleaningComplete(float coverage, float seconds)
        {
            Metrics.cleaningCoverage = coverage;
            Metrics.scrubSeconds = seconds;
            Metrics.cleaningComplete = true;
            CompleteStep(StepId.CleanSite);
            Feedback("Site cleaned. Let it air-dry - do not touch or blow on it.", Severity.Good, FeedbackKind.Event);
        }

        public void OnCleaningProgress(float coverage, float seconds)
        {
            Metrics.cleaningCoverage = Mathf.Max(Metrics.cleaningCoverage, coverage);
            Metrics.scrubSeconds = Mathf.Max(Metrics.scrubSeconds, seconds);
        }

        // ------------------------------------------------------------------ insertion

        public bool CanPierce(Cannula c)
        {
            if (State != SessionState.Running)
            {
                FeedbackOnce("start", "Choose a difficulty level first (buttons on the left of the table).", Severity.Info, FeedbackKind.Event, 3f);
                return false;
            }
            if (c.IsUsed)
            {
                if (Profile.enforceStepOrder)
                {
                    FeedbackOnce("reuse", "This cannula has already been used. Dispose of it in the sharps bin and take a new one.", Severity.Error, FeedbackKind.Event, 3f);
                    return false;
                }
                Metrics.reusedCannula = true;
            }
            if (Profile.maxAttempts > 0 && Metrics.attempts.Count >= Profile.maxAttempts) return false;
            return RequestAction(StepId.InsertNeedle);
        }

        public void OnPierce(Vector3 point, Vector3 normal, float angle, float roll)
        {
            NoteContact();
            var a = new AttemptRecord
            {
                number = Metrics.attempts.Count + 1,
                time = Time.time - Metrics.startTime,
                entryPoint = point,
                entryAngle = angle,
                bevelRoll = roll
            };
            Metrics.attempts.Add(a);
            if (a.number == 1) Metrics.tourniquetOnAtFirstPierce = TourniquetOn;

            if (!TourniquetOn)
                Feedback("No tourniquet - the vein is flat and harder to enter.", Severity.Warning, FeedbackKind.Technique);

            if (cleaning != null)
            {
                if (!cleaning.IsComplete)
                {
                    Metrics.insertedBeforeCleaning = true;
                    Feedback("The skin was not cleaned before insertion - infection risk.", Severity.Error, FeedbackKind.Technique);
                }
                else
                {
                    if (Vector3.Distance(point, cleaning.Center) > clinical.cleanRadius)
                    {
                        Metrics.insertedOutsideClean = true;
                        Feedback("You inserted outside the cleaned area.", Severity.Error, FeedbackKind.Technique);
                    }
                    if (!cleaning.IsDry)
                    {
                        Metrics.insertedBeforeDry = true;
                        Feedback("The antiseptic was still wet - it needs to dry to work (and stings).", Severity.Warning, FeedbackKind.Technique);
                    }
                }
            }

            string sev;
            Severity s = ClassifyAngle(angle, out sev);
            Feedback("Skin entry angle " + angle.ToString("0") + "° - " + sev, s, FeedbackKind.Technique);
            if (Mathf.Abs(roll) > clinical.bevelTolerance)
                Feedback("Bevel was not facing up (rotated " + Mathf.Abs(roll).ToString("0") + "°). Bevel-up gives a cleaner, less painful puncture.", Severity.Warning, FeedbackKind.Technique);
        }

        public Severity ClassifyAngle(float angle, out string text)
        {
            if (angle >= clinical.idealAngleMin && angle <= clinical.idealAngleMax) { text = "ideal (15-30°)"; return Severity.Good; }
            if (angle >= clinical.acceptableAngleMin && angle < clinical.idealAngleMin) { text = "a little shallow"; return Severity.Info; }
            if (angle > clinical.idealAngleMax && angle <= clinical.acceptableAngleMax) { text = "a little steep"; return Severity.Info; }
            if (angle < clinical.acceptableAngleMin) { text = "too shallow - the needle may travel above the vein"; return Severity.Warning; }
            text = "too steep - high risk of going through the back wall of the vein"; return Severity.Warning;
        }

        public void OnFlashback(VeinPath vessel, float currentAngle)
        {
            var a = Metrics.CurrentAttempt;
            if (a == null) return;
            a.flashback = true;
            a.angleAtFlashback = currentAngle;
            a.siteVessel = vessel != null ? vessel.displayName : "";
            string note;
            a.siteRating = SiteZone.Rate(a.entryPoint, vessel, out note);
            Feedback("Flashback! Blood in the chamber - the needle tip is in the vein.", Severity.Good, FeedbackKind.Event);
            CompleteStep(StepId.InsertNeedle);
            Feedback("Now lower the angle almost flat and advance 1-2 mm before advancing the catheter.", Severity.Info, FeedbackKind.Guidance);
        }

        public void OnArteryPuncture()
        {
            var a = Metrics.CurrentAttempt;
            if (a != null) a.arteryHit = true;
            Feedback("Bright red, pulsatile flashback - ARTERIAL puncture! Withdraw immediately and apply firm pressure for at least 5 minutes.",
                     Severity.Error, FeedbackKind.Event);
        }

        public void OnTransfix(float angle)
        {
            var a = Metrics.CurrentAttempt;
            if (a != null) a.transfixed = true;
            Feedback("The needle went through the back wall of the vein - a haematoma is forming. Withdraw and apply pressure.",
                     Severity.Error, FeedbackKind.Event);
            if (angle > clinical.idealAngleMax)
                Feedback("Cause: the angle (" + angle.ToString("0") + "°) was too steep. Keep it at 15-30° and lower it after flashback.", Severity.Info, FeedbackKind.Technique);
            else
                Feedback("Cause: the needle was advanced too far after flashback. Advance only 1-2 mm, then slide the catheter.", Severity.Info, FeedbackKind.Technique);
        }

        public void OnMissedVein()
        {
            FeedbackOnce("missed", "No flashback yet - you may have missed the vein. Withdraw slightly and re-direct, or withdraw and reassess.",
                         Severity.Warning, FeedbackKind.Technique, 8f);
        }

        public void OnLoweredAndAdvanced()
        {
            var a = Metrics.CurrentAttempt;
            if (a != null) { a.lowered = true; a.advancedAfterFlash = true; }
            CompleteStep(StepId.LowerAndAdvance);
            Feedback("Good - the catheter tip is now inside the vein. Hold the needle still and advance the catheter.", Severity.Good, FeedbackKind.Technique);
        }

        public void OnThreadComplete(bool catheterInVein)
        {
            var a = Metrics.CurrentAttempt;
            if (a != null) { a.threaded = true; a.catheterInVein = catheterInVein; }
            Metrics.catheterInVein = catheterInVein;
            if (!IsDone(StepId.LowerAndAdvance)) CompleteStep(StepId.LowerAndAdvance);
            CompleteStep(StepId.ThreadCatheter);
            if (!catheterInVein)
                Feedback("The catheter met resistance - it is not inside the vein.", Severity.Warning, FeedbackKind.Event);
            if (!TourniquetOn) CompleteStep(StepId.ReleaseTourniquet);
        }

        public bool CanWithdrawNeedleWithCatheter()
        {
            if (!TourniquetOn) return true;
            if (Profile.enforceStepOrder)
            {
                FeedbackOnce("releasefirst", "Release the tourniquet before withdrawing the needle.", Severity.Warning, FeedbackKind.Event, 3f);
                return false;
            }
            return true;
        }

        public void OnCatheterPlaced(Vector3 hubPoint)
        {
            Metrics.catheterPlaced = true;
            var a = Metrics.CurrentAttempt;
            if (a != null) a.outcome = a.catheterInVein ? "Success" : "Catheter not in vein";
            if (TourniquetOn)
            {
                Metrics.tourniquetOnAtNeedleRemoval = true;
                BloodFX.Spill(hubPoint, Vector3.up, 0.018f);
                Feedback("Blood is spilling from the hub - always release the tourniquet before removing the needle.", Severity.Error, FeedbackKind.Event);
            }
        }

        public void OnNeedleOut(bool catheterPlaced)
        {
            if (catheterPlaced)
            {
                CompleteStep(StepId.WithdrawNeedle);
                Feedback("Needle out - dispose of it immediately in the sharps container.", Severity.Info, FeedbackKind.Guidance);
                return;
            }

            var a = Metrics.CurrentAttempt;
            if (a != null && a.outcome == "In progress")
            {
                a.outcome = a.arteryHit ? "Arterial puncture" : a.transfixed ? "Back wall punctured" : a.flashback ? "Withdrawn before catheter advanced" : "Missed vein";
            }
            Feedback("Attempt " + (a != null ? a.number : 0) + " unsuccessful (" + (a != null ? a.outcome : "") + "). Apply pressure, dispose of the cannula and use a new one.",
                     Severity.Warning, FeedbackKind.Event);
            RollbackTo(StepId.InsertNeedle);
            if (Profile.maxAttempts > 0 && Metrics.attempts.Count >= Profile.maxAttempts)
            {
                Finish(true, "Maximum attempts reached. In practice, stop after two unsuccessful attempts and ask a more experienced colleague.");
            }
        }

        public void OnNeedleDisposed(bool afterPlacement)
        {
            if (afterPlacement)
            {
                Metrics.needleDisposed = true;
                CompleteStep(StepId.DisposeSharps);
            }
            else
            {
                Feedback("Used cannula disposed. A new sterile cannula is on the tray.", Severity.Good, FeedbackKind.Event);
            }
        }

        public void OnUnsafeSharps()
        {
            Metrics.unsafeSharps = true;
            FeedbackOnce("sharps", "Never put a used needle down - place it directly into the sharps container.", Severity.Error, FeedbackKind.Event, 4f);
        }

        // ------------------------------------------------------------------ end

        public void Finish(bool early, string reason)
        {
            if (State != SessionState.Running) return;
            Metrics.endTime = Time.time;
            Metrics.endedEarly = early;
            Metrics.endReason = reason;
            if (TourniquetOn) Metrics.tourniquetMaxSeconds = Mathf.Max(Metrics.tourniquetMaxSeconds, Time.time - tourniquetOnSince);
            State = SessionState.Report;

            float score; string grade;
            ReportBuilder.Build(Metrics, clinical, reportLines, criticalErrors, out score, out grade);
            ReportScore = score;
            ReportGrade = grade;
            if (writeCsvLogs)
            {
                string path = SessionLogger.Write(Metrics, score, grade, criticalErrors);
                if (!string.IsNullOrEmpty(path)) Debug.Log("[IVSim] Session logged to " + path);
            }
            Feedback(reason, early ? Severity.Warning : Severity.Good, FeedbackKind.Event);
            RaiseChanged();
        }
    }
}
