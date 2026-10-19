// Configures XR Plug-in Management + OpenXR for PC VR (Quest Link / SteamVR) and Android (Meta Quest).
// Compiled only when the OpenXR package is installed (see asmdef define constraint).
using System.Linq;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace MROT.EditorTools
{
    public static class MROTXRConfigurator
    {
        [MenuItem("MR Operating Theatre/3. Configure OpenXR (after packages installed)", priority = 3)]
        public static void Configure()
        {
            Setup(BuildTargetGroup.Standalone);
            Setup(BuildTargetGroup.Android);
            EnableFeatures(BuildTargetGroup.Standalone);
            EnableFeatures(BuildTargetGroup.Android);
            AssetDatabase.SaveAssets();
            Debug.Log("[MROT] OpenXR configured for Windows (PC VR) and Android (Meta Quest).");
        }

        static void Setup(BuildTargetGroup g)
        {
            XRGeneralSettingsPerBuildTarget perBT;
            EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out perBT);
            if (perBT == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/XR")) AssetDatabase.CreateFolder("Assets", "XR");
                perBT = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(perBT, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, perBT, true);
            }
            var s = perBT.SettingsForBuildTarget(g);
            if (s == null)
            {
                s = ScriptableObject.CreateInstance<XRGeneralSettings>();
                s.name = g + " Settings";
                perBT.SetSettingsForBuildTarget(g, s);
                AssetDatabase.AddObjectToAsset(s, perBT);
            }
            if (s.Manager == null)
            {
                var m = ScriptableObject.CreateInstance<XRManagerSettings>();
                m.name = g + " Providers";
                s.Manager = m;
                AssetDatabase.AddObjectToAsset(m, perBT);
            }
            s.InitManagerOnStart = true;
            XRPackageMetadataStore.AssignLoader(s.Manager, "UnityEngine.XR.OpenXR.OpenXRLoader", g);
            EditorUtility.SetDirty(s); EditorUtility.SetDirty(perBT);
        }

        static void EnableFeatures(BuildTargetGroup g)
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(g);
            if (settings == null) return;
            var wanted = new[] { "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "MetaQuestTouchProControllerProfile",
                                 "KHRSimpleControllerProfile", "ValveIndexControllerProfile", "HTCViveControllerProfile", "MetaQuestFeature" };
            foreach (var f in settings.GetFeatures())
            {
                if (f == null) continue;
                if (wanted.Contains(f.GetType().Name)) { f.enabled = true; EditorUtility.SetDirty(f); }
            }
            settings.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
            EditorUtility.SetDirty(settings);
        }
    }
}
