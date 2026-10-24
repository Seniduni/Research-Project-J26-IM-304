// Neonatal Delivery & Hand-Placement Training Module (J26-IM-304, Himal G.M.H - IT23152632)
// Shared enums, constants and small value types.
using System;
using UnityEngine;

namespace NeonatalDelivery
{
    /// <summary>Clinical steps of the simulated normal vertex (LOA) delivery, in order.</summary>
    public enum DeliveryStep
    {
        Briefing = 0,          // educator configuration / start
        Preparation = 1,       // gloves on, towel & clamps ready, position at the perineum
        SecondStagePushing = 2,// contractions + maternal pushing, head descends until visible
        CrowningHeadControl = 3, // flexion control of the occiput + perineal support, panting, birth of head by extension
        NuchalCordCheck = 4,   // palpate the neck for a nuchal cord
        Restitution = 5,       // restitution + external rotation (hands off, wait for the next contraction)
        AnteriorShoulder = 6,  // biparietal hold, gentle downward (posterior) traction
        PosteriorShoulder = 7, // gentle upward (anterior) traction
        BodyDelivery = 8,      // support head/neck and slide hand along the back to the buttocks
        SkinToSkin = 9,        // lift baby onto the mother's chest
        DryAndStimulate = 10,  // dry with towel, stimulate, assess colour/tone/cry
        DelayedCordClamping = 11, // wait >= 1 minute / until cord stops pulsating
        ClampCord = 12,        // first clamp ~2-3 cm, second clamp ~5 cm from the umbilicus
        CutCord = 13,          // cut between the clamps, shielding with the hand
        Complete = 14
    }

    public enum Difficulty { Guided = 0, Standard = 1, Assessment = 2 }

    /// <summary>Sensitive-situation censorship (FR8): graded visual attenuation and audio damping.</summary>
    public enum CensorshipLevel { FullRealism = 0, Moderate = 1, Clinical = 2, Minimal = 3 }

    public enum HandSide { Left = 0, Right = 1 }

    /// <summary>Landmark zones on the newborn (cranial, cervical, shoulder - FR1) and on the mother.</summary>
    public enum LandmarkZoneId
    {
        None = 0,
        Occiput, Vertex, ParietalLeft, ParietalRight, Face, Neck,
        ShoulderAnterior, ShoulderPosterior, Back, Axilla, Buttocks, Legs, Chest,
        MaternalPerineum, MaternalAbdomen, Cord
    }

    public enum CompetencyStatus { NotAttempted, Novice, Developing, Competent }

    public enum MotherRenderMode { Visible = 0, Occluder = 1, Hidden = 2 }

    /// <summary>One detected hand-placement fault (FR3) with the exact interval it occurred in.</summary>
    [Serializable]
    public class FaultEvent
    {
        public string code;
        public string name;
        public DeliveryStep step;
        public float startTime;     // session time (s)
        public float endTime;
        public float severity;      // 0..1
        public HandSide hand;
        public string evidence;     // human-readable measurement summary
        public string complication; // filled by the consequence engine
        public string explanation;
        public string remediation;
        public int attempt;
        public float feedbackLatencyMs = -1f; // NFR2: detection -> consequence render (ms)
    }

    public static class ND
    {
        public const string Version = "1.0.0";
        public const string ModuleCode = "J26-IM-304-ND";
        public const string LogTag = "[NeonatalDelivery] ";
        public static void Log(string m) { Debug.Log(LogTag + m); }
        public static void Warn(string m) { Debug.LogWarning(LogTag + m); }
        public static void Error(string m) { Debug.LogError(LogTag + m); }

        /// <summary>Delivery-progress values (normalised time of the ND_B_Birth clip) at which each mechanism stage ends.</summary>
        public static class Progress
        {
            public const float HeadVisible = 0.30f;
            public const float Crowned = 0.45f;
            public const float HeadBorn = 0.55f;
            public const float Restituted = 0.62f;
            public const float ExternallyRotated = 0.70f;
            public const float AnteriorShoulderBorn = 0.80f;
            public const float PosteriorShoulderBorn = 0.88f;
            public const float Born = 1.0f;
        }

        public static string StepTitle(DeliveryStep s)
        {
            switch (s)
            {
                case DeliveryStep.Briefing: return "Briefing";
                case DeliveryStep.Preparation: return "Preparation";
                case DeliveryStep.SecondStagePushing: return "Second stage - pushing";
                case DeliveryStep.CrowningHeadControl: return "Crowning - controlled delivery of the head";
                case DeliveryStep.NuchalCordCheck: return "Check for nuchal cord";
                case DeliveryStep.Restitution: return "Restitution & external rotation";
                case DeliveryStep.AnteriorShoulder: return "Delivery of the anterior shoulder";
                case DeliveryStep.PosteriorShoulder: return "Delivery of the posterior shoulder";
                case DeliveryStep.BodyDelivery: return "Delivery of the body";
                case DeliveryStep.SkinToSkin: return "Skin-to-skin contact";
                case DeliveryStep.DryAndStimulate: return "Dry & stimulate";
                case DeliveryStep.DelayedCordClamping: return "Delayed cord clamping";
                case DeliveryStep.ClampCord: return "Clamp the cord";
                case DeliveryStep.CutCord: return "Cut the cord";
                case DeliveryStep.Complete: return "Delivery complete";
            }
            return s.ToString();
        }
    }
}
