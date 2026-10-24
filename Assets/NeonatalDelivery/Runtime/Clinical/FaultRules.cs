// Clinician-reviewable hand-placement fault taxonomy and fault->complication mapping (FR3/FR4, NFR9).
// The rules live in external JSON (StreamingAssets/NeonatalDelivery/nd_fault_rules.json) so midwives can review and
// edit thresholds without recompiling. An override in persistentDataPath/NeonatalDelivery takes precedence.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace NeonatalDelivery
{
    /// <summary>Generic, data-driven detector types. Each rule picks one and supplies parameters.</summary>
    public static class Detector
    {
        public const string NoContactOnZones = "NoContactOnZones";       // no hand on required zones for minDuration
        public const string ContactOnZones = "ContactOnZones";           // a hand on forbidden zones for minDuration
        public const string ForceAbove = "ForceAbove";                   // estimated force on zones > threshold N
        public const string TractionAxis = "TractionAxis";               // traction deviates from expected axis by > maxAngleDeg
        public const string LateralForceAbove = "LateralForceAbove";     // lateral traction component > threshold N
        public const string TractionBeforeProgress = "TractionBeforeProgress"; // traction > threshold while progress < progressMax
        public const string ReleaseDuring = "ReleaseDuring";             // support lost while baby is being delivered/held
        public const string ProgressRateAbove = "ProgressRateAbove";     // precipitous (uncontrolled) progress
        public const string StepSkipped = "StepSkipped";                 // checklist action not performed
        public const string ClampPosition = "ClampPosition";             // clamp outside [minCm,maxCm] from umbilicus
        public const string CutOutsideClamps = "CutOutsideClamps";
        public const string CutWithoutClamps = "CutWithoutClamps";
        public const string EarlyClamping = "EarlyClamping";
        public const string BabyDropped = "BabyDropped";
    }

    [Serializable]
    public class FaultRule
    {
        public string code;
        public string name;
        public string[] steps;           // DeliveryStep names in which the rule is active
        public string detector;
        public string[] zones;           // LandmarkZoneId names
        public string hands = "Any";     // Any | Both | Dominant | NonDominant
        public float minDuration = 0.5f; // seconds the condition must persist
        public float threshold;          // N, or progress/second, depending on detector
        public float maxAngleDeg = 40f;
        public string expectedAxis;      // Posterior | Anterior | Outward
        public float progressMin = 0f;
        public float progressMax = 1f;
        public float minCm, maxCm;
        public bool requireGrip;
        public float severity = 0.5f;
        public string consequence;       // PerinealTear | ErbsPalsy | Hypoxia | Haemorrhage | FetalBradycardia | NearDrop | FacialBruising | CervicalInjury | ShoulderDystocia | ClavicleFracture | Anaemia | UmbilicalInjury
        public string complication;      // clinical complication text shown to the trainee
        public string explanation;       // why the fault produces the complication
        public string remediation;       // what to do instead (shown in the reflective loop)
        public string source;            // guideline / reference
        public string requiresScenario;  // e.g. "NuchalCord": the consequence is rendered only when that scenario variant is active
        [NonSerialized] public HashSet<DeliveryStep> stepSet;
        [NonSerialized] public HashSet<LandmarkZoneId> zoneSet;

        public void Prepare()
        {
            stepSet = new HashSet<DeliveryStep>();
            if (steps != null) foreach (var s in steps) if (Enum.TryParse(s, out DeliveryStep ds)) stepSet.Add(ds);
            zoneSet = new HashSet<LandmarkZoneId>();
            if (zones != null) foreach (var z in zones) if (Enum.TryParse(z, out LandmarkZoneId zi)) zoneSet.Add(zi);
        }
        public bool ActiveIn(DeliveryStep s) { return stepSet != null && stepSet.Contains(s); }
    }

    [Serializable]
    public class FaultRuleSet
    {
        public string version = "1.0";
        public string status = "DRAFT - pending review by >= 3 midwifery/obstetric experts";
        public string[] reviewers;
        public string lastReviewed;
        public List<FaultRule> rules = new List<FaultRule>();

        public const string FileName = "nd_fault_rules.json";

        public FaultRule Get(string code) { foreach (var r in rules) if (r.code == code) return r; return null; }

        public void Prepare() { foreach (var r in rules) r.Prepare(); }

        /// <summary>Load order: persistentDataPath override -> StreamingAssets -> Resources fallback.</summary>
        public static IEnumerator Load(Action<FaultRuleSet> done)
        {
            string text = null;
            var overridePath = Path.Combine(SessionConfig.Folder, FileName);
            if (File.Exists(overridePath)) { try { text = File.ReadAllText(overridePath); ND.Log("Fault rules: using override " + overridePath); } catch { } }
            if (text == null)
            {
                var sa = Path.Combine(Application.streamingAssetsPath, "NeonatalDelivery", FileName);
                if (sa.Contains("://") || sa.Contains(":///"))
                {
                    using (var req = UnityWebRequest.Get(sa))
                    {
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success) text = req.downloadHandler.text;
                    }
                }
                else if (File.Exists(sa)) { try { text = File.ReadAllText(sa); } catch { } }
            }
            if (text == null)
            {
                var ta = Resources.Load<TextAsset>("NeonatalDelivery/nd_fault_rules");
                if (ta != null) text = ta.text;
            }
            FaultRuleSet set = null;
            if (!string.IsNullOrEmpty(text))
            {
                try { set = JsonUtility.FromJson<FaultRuleSet>(text); } catch (Exception e) { ND.Error("Fault rule JSON invalid: " + e.Message); }
            }
            if (set == null || set.rules == null || set.rules.Count == 0) { ND.Warn("Fault rules missing - using built-in defaults."); set = BuiltIn(); }
            set.Prepare();
            done?.Invoke(set);
        }

        public static FaultRuleSet Parse(string json)
        {
            var s = JsonUtility.FromJson<FaultRuleSet>(json); s?.Prepare(); return s;
        }

        /// <summary>Minimal built-in set, mirrors the shipped JSON, used only if the file cannot be read.</summary>
        public static FaultRuleSet BuiltIn()
        {
            var s = new FaultRuleSet();
            s.rules.Add(new FaultRule { code = "F01_UNSUPPORTED_CRANIUM", name = "Unsupported cranium", steps = new[] { "CrowningHeadControl" }, detector = Detector.NoContactOnZones,
                zones = new[] { "Occiput", "Vertex", "ParietalLeft", "ParietalRight" }, minDuration = 1.5f, progressMin = 0.36f, progressMax = 0.55f, severity = 0.8f, consequence = "PerinealTear",
                complication = "Rapid, uncontrolled expulsion of the head: perineal tear and risk of neonatal intracranial injury.",
                explanation = "Without flexion control the head extends early and is born with a larger diameter, overstretching the perineum.",
                remediation = "Rest the flat fingers of your dominant hand on the occiput to keep the head flexed and slow its birth between contractions." });
            s.rules.Add(new FaultRule { code = "F07_WRONG_TRACTION_AXIS", name = "Wrong axis of traction", steps = new[] { "AnteriorShoulder" }, detector = Detector.TractionAxis,
                expectedAxis = "Axial", maxAngleDeg = 45f, threshold = 8f, minDuration = 0.4f, severity = 0.8f, consequence = "ErbsPalsy",
                complication = "Brachial plexus stretch (Erb's palsy) / shoulder dystocia.",
                explanation = "Traction that is not in line with the fetal spine stretches the upper brachial plexus roots (C5-C6).",
                remediation = "Hold the head on both parietal bones and apply gentle AXIAL traction in line with the fetal spine (no downward or sideways pull)." });
            return s;
        }
    }
}
