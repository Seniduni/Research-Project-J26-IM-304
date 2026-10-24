// Enables the OpenXR features the module needs on Android (Meta Quest 3) and Standalone (Quest Link):
// hand tracking (XR Hands), Meta hand-tracking aim, hand interaction profile, Meta Quest support, and - when the
// Unity OpenXR: Meta package is installed - AR session/camera (passthrough) and anchors. Compiled only with OpenXR present.
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;

namespace NeonatalDelivery.EditorTools
{
    public static class NDOpenXRConfigurator
    {
        static readonly string[] Wanted =
        {
            "HandTracking", "MetaHandTrackingAim", "HandInteractionProfile", "MetaQuestFeature",
            "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "MetaQuestTouchProControllerProfile",
            "ARSessionFeature", "ARCameraFeature", "ARAnchorFeature", "MetaOpenXRFeature"
        };

        [MenuItem("Neonatal Delivery/2. Configure OpenXR (hand tracking + passthrough)", priority = 2)]
        public static void Configure()
        {
            int n = 0;
            foreach (var g in new[] { BuildTargetGroup.Android, BuildTargetGroup.Standalone })
            {
                var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(g);
                if (settings == null) continue;
                foreach (var f in settings.GetFeatures())
                {
                    if (f == null) continue;
                    var tn = f.GetType().Name;
                    if (!Wanted.Contains(tn)) continue;
                    if (g == BuildTargetGroup.Standalone && tn.StartsWith("AR")) continue;   // passthrough is Quest-only
                    if (!f.enabled) { f.enabled = true; EditorUtility.SetDirty(f); n++; }
                }
                EditorUtility.SetDirty(settings);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[NeonatalDelivery] OpenXR configured ({n} feature(s) enabled). Run 'Project Validation' (Edit > Project Settings > XR Plug-in Management) and apply the suggested fixes for Meta Quest.");
        }
    }
}
