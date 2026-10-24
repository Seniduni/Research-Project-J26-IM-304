using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    public enum Difficulty { Easy, Medium, Hard }
    public enum SessionState { Menu, Running, Report }
    public enum Severity { Info, Good, Warning, Error }

    /// <summary>
    /// Guidance  = coaching hints (shown when the profile has hints on)
    /// Technique = live technique feedback such as angle/bevel (shown when liveFeedback is on)
    /// Event     = things the learner would see/feel in reality (always shown)
    /// </summary>
    public enum FeedbackKind { Guidance, Technique, Event }

    public enum StepId
    {
        Prepare = 0,
        ApplyTourniquet,
        SelectVein,
        CleanSite,
        LetDry,
        InsertNeedle,
        LowerAndAdvance,
        ThreadCatheter,
        ReleaseTourniquet,
        WithdrawNeedle,
        DisposeSharps,
        DrawBlood,
        FlushLine,
        SecureDressing,
        RemoveCannula,
        ApplyPressure
    }

    public enum StepStatus { Pending, Current, Done, Missed }

    [System.Serializable]
    public class StepDef
    {
        public StepId id;
        public string title;
        [TextArea(2, 5)] public string instruction;
        [TextArea(2, 5)] public string hint;

        public StepDef(StepId id, string title, string instruction, string hint)
        {
            this.id = id; this.title = title; this.instruction = instruction; this.hint = hint;
        }

        /// <summary>
        /// Default step text. Review this with your nursing lecturers and edit it in the IVSession inspector so it
        /// matches your institution's protocol.
        /// </summary>
        public static List<StepDef> Defaults()
        {
            return new List<StepDef>
            {
                new StepDef(StepId.Prepare, "Hand hygiene & gloves",
                    "Perform hand hygiene and put on clean gloves.",
                    "Use the glove box on the left of the table."),
                new StepDef(StepId.ApplyTourniquet, "Apply tourniquet",
                    "Apply the tourniquet above the intended site so the veins fill.",
                    "Use the blue tourniquet strip. Watch the veins become larger."),
                new StepDef(StepId.SelectVein, "Palpate & select a vein",
                    "Palpate the forearm to find a straight, bouncy vein away from joints and valves.",
                    "With an empty hand, press and hold on the skin over a vein for a second."),
                new StepDef(StepId.CleanSite, "Clean the site",
                    "Wet the swab with alcohol, then scrub the site with firm back-and-forth strokes.",
                    "Pick up the swab, touch it to the alcohol bottle, then rub the skin around your chosen site."),
                new StepDef(StepId.LetDry, "Let the skin dry",
                    "Allow the antiseptic to dry completely. Do not touch or re-palpate the cleaned area.",
                    "Wait until the wet sheen has disappeared."),
                new StepDef(StepId.InsertNeedle, "Insert the cannula",
                    "Bevel up, enter the skin at a shallow 15-30 degree angle in line with the vein until you see flashback.",
                    "Watch the clear chamber at the back of the cannula for blood (flashback)."),
                new StepDef(StepId.LowerAndAdvance, "Lower the angle & advance 1-2 mm",
                    "After flashback, lower the cannula almost flat to the skin and advance a further 1-2 mm so the catheter tip is inside the vein.",
                    "Reduce the angle first, then push in just a little."),
                new StepDef(StepId.ThreadCatheter, "Advance the catheter",
                    "Hold the needle still and slide the plastic catheter off the needle into the vein up to the hub.",
                    "VR: squeeze the trigger. Desktop: hold F."),
                new StepDef(StepId.ReleaseTourniquet, "Release the tourniquet",
                    "Release the tourniquet before removing the needle.",
                    "Use the tourniquet on the arm again."),
                new StepDef(StepId.WithdrawNeedle, "Withdraw the needle",
                    "Occlude the vein above the catheter tip and withdraw the needle, leaving the catheter in place.",
                    "VR: pull your hand straight back. Desktop: hold X."),
                new StepDef(StepId.DisposeSharps, "Dispose of the needle",
                    "Place the used needle immediately into the sharps container.",
                    "The yellow bin with the red lid."),
                new StepDef(StepId.DrawBlood, "Draw a blood sample",
                    "Attach the syringe to the cannula hub and gently draw back to obtain a sample.",
                    "Use the syringe with the red label, then hold to draw."),
                new StepDef(StepId.FlushLine, "Flush with saline",
                    "Flush the cannula with 0.9% saline and check there is no swelling or pain (patency).",
                    "Use the saline syringe, then hold to push."),
                new StepDef(StepId.SecureDressing, "Secure with a dressing",
                    "Apply a sterile transparent dressing over the cannula.",
                    "Use the dressing pack on the tray."),
                new StepDef(StepId.RemoveCannula, "Remove the cannula",
                    "When therapy is finished: remove the dressing and withdraw the catheter smoothly. Check the tip is intact.",
                    "Use the cannula hub on the arm."),
                new StepDef(StepId.ApplyPressure, "Apply pressure",
                    "Place gauze over the site and apply firm pressure until bleeding stops.",
                    "Use the gauze, then hold on it for a few seconds."),
            };
        }
    }

    [System.Serializable]
    public class DifficultyProfile
    {
        public Difficulty level;
        public string label;
        [TextArea(2, 4)] public string description;
        [Range(0f, 1f)] public float skinOpacity = 1f;
        public bool showVeins = true;
        public bool showVeinLabels = false;
        public bool showSiteMarkers = false;
        public bool liveAngleGauge = true;
        public bool liveFeedback = true;
        public bool showSideView = false;
        public bool showAngleReference = true;
        public bool enforceStepOrder = true;
        public bool showHints = true;
        [Tooltip("0 = unlimited")] public int maxAttempts = 0;

        public static DifficultyProfile[] Defaults()
        {
            return new[]
            {
                new DifficultyProfile
                {
                    level = Difficulty.Easy, label = "Easy - Guided",
                    description = "See-through skin, labelled veins, recommended sites, live angle gauge and side view.",
                    skinOpacity = 0.35f, showVeins = true, showVeinLabels = true, showSiteMarkers = true,
                    liveAngleGauge = true, liveFeedback = true, showSideView = true, showAngleReference = true,
                    enforceStepOrder = true, showHints = true, maxAttempts = 0
                },
                new DifficultyProfile
                {
                    level = Difficulty.Medium, label = "Medium - Assisted",
                    description = "Veins faintly visible through the skin. Angle readout and feedback, no site markers.",
                    skinOpacity = 0.88f, showVeins = true, showVeinLabels = false, showSiteMarkers = false,
                    liveAngleGauge = true, liveFeedback = true, showSideView = false, showAngleReference = true,
                    enforceStepOrder = true, showHints = true, maxAttempts = 3
                },
                new DifficultyProfile
                {
                    level = Difficulty.Hard, label = "Hard - Assessment",
                    description = "Opaque skin: find the vein by palpation. No live hints; feedback in the final report. Two attempts.",
                    skinOpacity = 1f, showVeins = false, showVeinLabels = false, showSiteMarkers = false,
                    liveAngleGauge = false, liveFeedback = false, showSideView = false, showAngleReference = false,
                    enforceStepOrder = false, showHints = false, maxAttempts = 2
                },
            };
        }
    }

    [System.Serializable]
    public class ClinicalSettings
    {
        [Header("Insertion angle (degrees, measured from the skin surface)")]
        public float idealAngleMin = 15f;
        public float idealAngleMax = 30f;
        public float acceptableAngleMin = 10f;
        public float acceptableAngleMax = 35f;
        [Tooltip("Max bevel rotation away from 'bevel up' that still counts as correct")]
        public float bevelTolerance = 25f;
        [Tooltip("Angle reduction after flashback that counts as 'lowered'")]
        public float lowerAngleBy = 4f;
        [Tooltip("Advance after flashback that counts as 'advanced 1-2 mm' (m)")]
        public float advanceAfterFlash = 0.0008f;

        [Header("Cleaning")]
        [Tooltip("Seconds of scrubbing required (many protocols use 30 s; shortened for training flow)")]
        public float requiredScrubSeconds = 15f;
        [Tooltip("Seconds to air-dry (many protocols use ~30 s)")]
        public float dryingSeconds = 10f;
        [Tooltip("Radius around the chosen site that must be cleaned (m)")]
        public float cleanRadius = 0.025f;
        [Range(0f, 1f)] public float requiredCoverage = 0.8f;

        [Header("Other")]
        [Tooltip("Warn if the tourniquet stays on longer than this (s)")]
        public float tourniquetLimitSeconds = 120f;
        public float pressureSeconds = 3f;
    }

    public class AttemptRecord
    {
        public int number;
        public float time;
        public Vector3 entryPoint;
        public float entryAngle;
        public float bevelRoll;
        public string siteVessel = "";
        public SiteRating siteRating = SiteRating.NotSuitable;
        public bool flashback;
        public float angleAtFlashback;
        public bool lowered;
        public bool advancedAfterFlash;
        public bool threaded;
        public bool transfixed;
        public bool arteryHit;
        public bool catheterInVein;
        public string outcome = "In progress";
    }

    /// <summary>Everything measured during a session; the report and the CSV log are built from this.</summary>
    public class SessionMetrics
    {
        public string sessionId = "";
        public string participantId = "";
        public string mode = "";
        public Difficulty difficulty;
        public float startTime;
        public float endTime;
        public bool endedEarly;
        public string endReason = "";

        public bool handHygiene;
        public bool handHygieneBeforeContact = true;
        public bool tourniquetOnAtFirstPierce;
        public float tourniquetMaxSeconds;
        public bool tourniquetOnAtNeedleRemoval;

        public bool siteSelected;
        public Vector3 sitePoint;
        public string siteVessel = "";
        public SiteRating siteRating = SiteRating.NotSuitable;
        public string siteNote = "";
        public bool palpated;

        public float cleaningCoverage;
        public float scrubSeconds;
        public bool cleaningComplete;
        public bool insertedBeforeDry;
        public bool recontaminated;
        public bool insertedOutsideClean;
        public bool insertedBeforeCleaning;

        public readonly List<AttemptRecord> attempts = new List<AttemptRecord>();
        public bool reusedCannula;
        public int orderViolations;

        public bool catheterPlaced;
        public bool catheterInVein;
        public bool needleDisposed;
        public bool unsafeSharps;
        public bool bloodDrawn;
        public bool flushed;
        public bool flushOk;
        public bool dressingApplied;
        public bool cannulaRemoved;
        public bool pressureApplied;

        public AttemptRecord CurrentAttempt { get { return attempts.Count > 0 ? attempts[attempts.Count - 1] : null; } }
        public AttemptRecord SuccessfulAttempt
        {
            get
            {
                foreach (var a in attempts) if (a.threaded && a.catheterInVein) return a;
                return null;
            }
        }
        public int Transfixions { get { int n = 0; foreach (var a in attempts) if (a.transfixed) n++; return n; } }
        public int ArteryHits { get { int n = 0; foreach (var a in attempts) if (a.arteryHit) n++; return n; } }
        public float Duration { get { return (endTime > 0f ? endTime : Time.time) - startTime; } }
    }
}
