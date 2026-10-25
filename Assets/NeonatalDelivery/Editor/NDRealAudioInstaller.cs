// Opt-in fetch of verified CC0 REAL human voice recordings. Requires internet on the Unity development machine.
// No AI-generated/ghost voices. Other foley cues remain included locally for offline use.
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace NeonatalDelivery.EditorTools
{
    public static class NDRealAudioInstaller
    {
        const string Folder = "Assets/NeonatalDelivery/Resources/NeonatalDelivery/Audio";
        const string Cry = "https://upload.wikimedia.org/wikipedia/commons/2/2b/Crying_newborn_baby.ogg";
        const string Breath = "https://upload.wikimedia.org/wikipedia/commons/2/20/Ujjayi.ogg";

        [MenuItem("Neonatal Delivery/5. Download Real CC0 Voice Audio", priority = 5)]
        public static async void Install()
        {
            if (!EditorUtility.DisplayDialog("Recorded clinical voice audio (CC0)",
                "Download real newborn crying and human breathing recordings (CC0) from Wikimedia Commons? " +
                "These replace the synthetic voice sound effects in the project. This requires internet access.\n\n" +
                "Newborn: the_yura, Crying newborn baby (CC0).\n" +
                "Breathing: Lcporschen, Ujjayi (CC0).\n\n" +
                "These are real recorded sounds, but the breathing is NOT a clinical labour recording.", "Download", "Cancel")) return;
            try
            {
                byte[] c = await Get(Cry);
                byte[] b = await Get(Breath);
                if (c == null || b == null) throw new Exception("Download failed. Your original audio remains unchanged.");
                Directory.CreateDirectory(Folder);
                InstallOne("baby_cry", c);
                foreach (var name in new[] { "mother_breath", "mother_pant", "mother_push_strain", "mother_relief", "mother_cry_out" }) InstallOne(name, b);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[ND Audio] Installed 2 REAL, CC0 voice recordings for 6 sound-event names. Instrument foley remains included.");
                EditorUtility.DisplayDialog("Audio Installed", "Recorded CC0 sounds installed. They will be used automatically in Play Mode.", "OK");
            }
            catch (Exception e) { Debug.LogError("[ND Audio] " + e.Message); EditorUtility.DisplayDialog("Audio download failed", e.Message, "OK"); }
        }

        static async Task<byte[]> Get(string url)
        {
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = 30;
                var operation = req.SendWebRequest();
                while (!operation.isDone) await Task.Delay(100);
                if (req.result != UnityWebRequest.Result.Success) throw new Exception(url + ": " + req.error);
                return req.downloadHandler.data;
            }
        }

        static void InstallOne(string name, byte[] audio)
        {
            // Remove the same-named WAV to avoid an ambiguous Resources.LoadAll<AudioClip> lookup.
            string wav = Folder + "/" + name + ".wav";
            if (File.Exists(wav)) AssetDatabase.DeleteAsset(wav);
            File.WriteAllBytes(Folder + "/" + name + ".ogg", audio);
        }
    }
}
