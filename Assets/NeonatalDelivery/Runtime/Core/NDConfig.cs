// Session configuration (FR7) - set by the educator panel or loaded from JSON so it can be changed without recompiling.
using System;
using System.IO;
using UnityEngine;

namespace NeonatalDelivery
{
    [Serializable]
    public class SessionConfig
    {
        public string studentCode = "STUDENT-001";      // pseudonymous trainee identifier (NFR7)
        public string educatorCode = "";
        public Difficulty difficulty = Difficulty.Guided;
        public CensorshipLevel censorship = CensorshipLevel.Moderate;
        public HandSide dominantHand = HandSide.Right;    // NFR10: left/right-hand dominance
        public bool guidedGhostHands = true;              // FR12: ghosted reference demonstration
        public bool ablationMode = false;                 // Section 3.3 internal baseline: no consequence mapping / no reflective loop
        public bool scenarioNuchalCord = false;           // variant: loose nuchal cord present
        public bool mixedReality = false;                 // passthrough + calibrated silicone mannequin
        public MotherRenderMode mrMotherRender = MotherRenderMode.Occluder;
        public bool showVirtualBabyInMR = true;
        public float timeScale = 1.0f;                    // compresses waiting periods (contraction rest, delayed clamping)
        public float delayedClampingSeconds = 60f;        // WHO: not earlier than 1 minute after birth
        public float sessionLengthLimitMinutes = 30f;     // NFR6: rest prompts
        public float babyLengthCm = 47f;                  // match the physical silicone doll
        public bool seated = false;                       // NFR10: seated play lowers the station
        public bool subtitles = true;                     // NFR10
        public float masterVolume = 1f;

        public static string Folder => Path.Combine(Application.persistentDataPath, "NeonatalDelivery");
        public static string FilePath => Path.Combine(Folder, "nd_session_config.json");

        public static SessionConfig LoadOrDefault()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonUtility.FromJson<SessionConfig>(File.ReadAllText(FilePath)) ?? new SessionConfig();
                var ta = Resources.Load<TextAsset>("NeonatalDelivery/nd_session_config");
                if (ta != null) return JsonUtility.FromJson<SessionConfig>(ta.text) ?? new SessionConfig();
            }
            catch (Exception e) { ND.Warn("Config load failed: " + e.Message); }
            return new SessionConfig();
        }

        public void Save()
        {
            try { Directory.CreateDirectory(Folder); File.WriteAllText(FilePath, JsonUtility.ToJson(this, true)); }
            catch (Exception e) { ND.Warn("Config save failed: " + e.Message); }
        }

        public SessionConfig Clone() { return JsonUtility.FromJson<SessionConfig>(JsonUtility.ToJson(this)); }
    }
}
