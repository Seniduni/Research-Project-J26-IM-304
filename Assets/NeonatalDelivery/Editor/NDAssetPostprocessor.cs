// Import rules for the Neonatal Delivery art (FBX rigs/clips, textures, audio). Only touches assets under Assets/NeonatalDelivery.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NeonatalDelivery.EditorTools
{
    public class NDAssetPostprocessor : AssetPostprocessor
    {
        const string Root = "Assets/NeonatalDelivery/";
        static readonly HashSet<string> Looping = new HashSet<string> { "ND_M_Idle", "ND_M_Pant", "ND_M_Cradle", "ND_B_Calm", "ND_B_Cry" };

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var mi = (ModelImporter)assetImporter;
            mi.globalScale = 1f; mi.useFileScale = true; mi.bakeAxisConversion = true;
            mi.importCameras = false; mi.importLights = false; mi.importVisibility = false;
            mi.importBlendShapes = true; mi.importNormals = ModelImporterNormals.Import; mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.isReadable = false;   // v4: no physics drape any more (the gown is skinned) -> keep meshes GPU-only on Quest
            // FBX materials carry embedded textures: import them through the render pipeline's material description
            // (URP Lit, textured) so the model is never white, even before the module's own materials are remapped on top.
            mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;   // embedded materials (never the legacy external Materials/ + .fbm extraction)
            bool character = assetPath.Contains("ND_Mother") || assetPath.Contains("ND_Baby");
            mi.animationType = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            mi.importAnimation = character;
            mi.optimizeGameObjects = false;                     // bones must stay as Transforms (ring bones, markers, anchors)
            if (assetPath.Contains("ND_Baby")) mi.preserveHierarchy = true;   // v5 unrigged baby: keep ND_BabyModel/Root/... as authored
            mi.animationCompression = ModelImporterAnimationCompression.Off;
            mi.skinWeights = ModelImporterSkinWeights.Standard;
            mi.addCollider = false;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Root)) return;
            var mi = (ModelImporter)assetImporter;
            var defs = mi.defaultClipAnimations;
            if (defs == null || defs.Length == 0) return;
            var clips = new List<ModelImporterClipAnimation>();
            // Blender writes one take per object|action; prefer the armature's take (ND_MotherRig|...). The v5 baby has a single
            // object take (Root|ND_B_Birth).
            var ordered = new List<ModelImporterClipAnimation>(defs);
            ordered.Sort((x, y) => (y.takeName != null && y.takeName.Contains("Rig|") ? 1 : 0).CompareTo(x.takeName != null && x.takeName.Contains("Rig|") ? 1 : 0));
            foreach (var c in ordered)
            {
                string shortName = c.name; int bar = shortName.LastIndexOf('|'); if (bar >= 0) shortName = shortName.Substring(bar + 1);
                if (!shortName.StartsWith("ND_")) continue;
                if (clips.Exists(x => x.name == shortName)) continue;
                c.name = shortName;
                c.loopTime = Looping.Contains(shortName); c.loopPose = false;
                c.lockRootRotation = false; c.lockRootHeightY = false; c.lockRootPositionXZ = false;
                c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
                clips.Add(c);
            }
            if (clips.Count > 0) mi.clipAnimations = clips.ToArray();
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "Art/Textures")) return;
            var ti = (TextureImporter)assetImporter;
            string p = assetPath.ToLowerInvariant();
            ti.maxTextureSize = 2048; ti.mipmapEnabled = true; ti.anisoLevel = 4;
            if (p.Contains("_normal")) ti.textureType = TextureImporterType.NormalMap;
            else { ti.textureType = TextureImporterType.Default; ti.sRGBTexture = !p.Contains("_metalrough") && !p.Contains("_occlusion"); }
            ti.alphaIsTransparency = p.Contains("hair") || p.Contains("eyelash") || p.Contains("skullcap");
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true; android.maxTextureSize = p.Contains("mother/std_skin") || p.Contains("baby") ? 2048 : 1024;
            android.format = TextureImporterFormat.ASTC_6x6; ti.SetPlatformTextureSettings(android);
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ai = (AudioImporter)assetImporter;
            ai.forceToMono = true;
            var s = ai.defaultSampleSettings; s.loadType = AudioClipLoadType.DecompressOnLoad; s.compressionFormat = AudioCompressionFormat.Vorbis; s.quality = 0.6f;
            ai.defaultSampleSettings = s;
        }
    }
}
