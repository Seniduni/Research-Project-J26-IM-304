// Dynamic consequence mapping engine (FR4, NFR2 <= 500 ms): renders the specific clinical complication that a detected
// hand-placement fault produces, on the newborn or the mother, attenuated by the censorship level.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public class ConsequenceEngine : MonoBehaviour
    {
        public BabyController baby;
        public MotherController mother;
        public BirthCanalDeformer canal;
        public UmbilicalCord cord;
        public CensorshipFilter censor;
        public CueSystem cues;
        public ContractionModel contractions;
        public ParticleSystem bloodFx, fluidFx;
        public float lastLatencyMs;
        readonly List<string> _active = new List<string>();
        public IReadOnlyList<string> Active => _active;

        /// <summary>Render the consequence of a fault. Returns the measured detection->render latency (ms).</summary>
        public float Render(FaultEvent f, FaultRule rule, float detectedRealtime)
        {
            string c = rule != null ? rule.consequence : "";
            if (!_active.Contains(c)) _active.Add(c);
            switch (c)
            {
                case "PerinealTear":
                    if (canal != null) canal.tear = Mathf.Max(canal.tear, f.severity);
                    Bleed(canal != null ? canal.IntroitusCenter - canal.AnteriorAxis * (canal.IntroitusRadius + 0.01f) : Vector3.zero, 18);
                    if (mother != null) mother.pain = 1f;
                    NDAudio.Instance?.Mother("mother_cry_out", 1f);
                    if (cues != null) cues.MarkPoint(canal.IntroitusCenter - canal.AnteriorAxis * (canal.IntroitusRadius + 0.012f), "Perineal tear", Color.red);
                    break;
                case "ErbsPalsy":
                case "BrachialPlexus":
                    if (baby != null) baby.erbsPalsy = 1f;
                    if (cues != null) cues.MarkZone(LandmarkZoneId.ShoulderAnterior, "Brachial plexus stretch (C5-C6)", Color.red);
                    break;
                case "ClavicleFracture":
                    if (cues != null) cues.MarkZone(LandmarkZoneId.ShoulderAnterior, "Clavicle fracture risk", Color.red);
                    break;
                case "CervicalInjury":
                    if (cues != null) cues.MarkZone(LandmarkZoneId.Neck, "Cervical spine / carotid compression", Color.red);
                    break;
                case "FacialBruising":
                    if (cues != null) cues.MarkZone(LandmarkZoneId.Face, "Facial / ocular injury", Color.red);
                    break;
                case "Hypoxia":
                    if (baby != null) { baby.limp = 1f; baby.cyanosis = 1f; }
                    if (contractions != null) contractions.Bradycardia = true;
                    NDAudio.Instance?.StopBaby();
                    if (cues != null) cues.MarkZone(LandmarkZoneId.Neck, "Nuchal cord tightened - hypoxia", Color.red);
                    break;
                case "FetalBradycardia":
                    if (contractions != null) contractions.Bradycardia = true;
                    break;
                case "ShoulderDystocia":
                    if (cues != null) cues.MarkZone(LandmarkZoneId.ShoulderAnterior, "Anterior shoulder impacted behind the pubic symphysis", new Color(1f, 0.4f, 0.1f));
                    break;
                case "Haemorrhage":
                    if (cord != null) Bleed(cord.IsCut ? cord.BabySideCutEnd : cord.PointAt(0.04f), 30);
                    break;
                case "NearDrop":
                    if (cues != null) cues.Flash(new Color(1f, 0f, 0f, 0.35f), 0.8f);
                    break;
                case "UmbilicalInjury":
                    if (cues != null) cues.MarkZone(LandmarkZoneId.Chest, "Umbilical skin / bowel injury risk", Color.red);
                    break;
                case "Anaemia":
                    if (cues != null) cues.MarkZone(LandmarkZoneId.Chest, "Lost placental transfusion (iron stores)", new Color(1f, 0.6f, 0.1f));
                    break;
            }
            NDAudio.Instance?.UI("fault_alert", 0.6f);
            HandTrackingSource.Instance?.Haptic(f.hand, 0.7f, 0.25f);
            lastLatencyMs = (Time.realtimeSinceStartup - detectedRealtime) * 1000f;
            if (lastLatencyMs > 500f) ND.Warn($"Consequence latency {lastLatencyMs:F0} ms exceeds 500 ms target");
            NDEvents.RaiseConsequence(f);
            return lastLatencyMs;
        }

        void Bleed(Vector3 at, int count)
        {
            if (bloodFx == null || censor == null) return;
            int n = Mathf.RoundToInt(count * censor.BloodScale);
            if (n <= 0) return;
            bloodFx.transform.position = at;
            var main = bloodFx.main;
            var col = Color.Lerp(new Color(0.45f, 0.02f, 0.03f), new Color(0.35f, 0.32f, 0.33f), censor.GoreDesaturation);
            main.startColor = col;
            bloodFx.Emit(n);
        }

        public void AmnioticGush(Vector3 at)
        {
            if (fluidFx == null) return; fluidFx.transform.position = at; fluidFx.Emit(40);
            if (bloodFx != null && censor != null) Bleed(at, 6);
        }

        /// <summary>Undo consequences when the step is rewound for re-practice.</summary>
        public void Clear()
        {
            _active.Clear();
            if (canal != null) canal.tear = 0f;
            if (baby != null) { baby.erbsPalsy = 0f; baby.limp = 0f; }
            if (contractions != null) contractions.Bradycardia = false;
            if (cues != null) cues.ClearMarks();
        }
    }
}
