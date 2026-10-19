// MR Operating Theatre - one-click editor setup (H.M.H. Lakshan, IT23404564)
// Menu: MR Operating Theatre > 1. Build Ward (materials, prefabs, scene)
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace MROT.EditorTools
{
    [Serializable] class MatDef { public string name; public float[] color; public float metallic; public float roughness; public float[] emission; }
    [Serializable] class MatList { public MatDef[] materials; }

    public static class MROTSetup
    {
        public const string Root = "Assets/MR_OperatingTheatre";
        public const string MatDir = Root + "/Materials";
        public const string PrefabDir = Root + "/Prefabs";
        public const string WardFbx = Root + "/Models/Ward/MR_OperatingTheatre_Ward.fbx";
        public const string WardPrefab = Root + "/Prefabs/MR_OperatingTheatre_Ward.prefab";
        public const string DemoScene = Root + "/Scenes/MR_OperatingTheatre.unity";
        public const string GalleryScene = Root + "/Scenes/MR_OperatingTheatre_AssetGallery.unity";

        static readonly Dictionary<string, string> ScreenTex = new Dictionary<string, string>
        {
            {"M_Screen_Monitor", "screen_vitals"}, {"M_Screen_Anaesthesia", "screen_anaesthesia"}, {"M_Screen_PACS", "screen_pacs"},
            {"M_Screen_PC_Nurse", "screen_pc_nurse"}, {"M_Screen_PC_Doctor", "screen_pc_doctor"}, {"M_Screen_Diathermy", "screen_diathermy"},
            {"M_Screen_Clock", "screen_clock"}, {"M_Screen_Pump", "screen_pump"}, {"M_Screen_Defib", "screen_defib"},
            {"M_Screen_Teaching", "screen_teaching"}, {"M_Screen_Ventilation", "screen_ventilation"},
        };

        [MenuItem("MR Operating Theatre/1. Build Ward (materials, prefabs, scene)", priority = 1)]
        public static void BuildAll()
        {
            try
            {
                EditorUtility.DisplayProgressBar("MR Operating Theatre", "Project settings", 0.05f);
                ProjectSettingsSetup();
                ConfigureFonts();
                EditorUtility.DisplayProgressBar("MR Operating Theatre", "Materials", 0.15f);
                BuildMaterials();
                EditorUtility.DisplayProgressBar("MR Operating Theatre", "Model import settings", 0.35f);
                ConfigureModels();
                EditorUtility.DisplayProgressBar("MR Operating Theatre", "Prefabs", 0.6f);
                BuildPrefabs();
                EditorUtility.DisplayProgressBar("MR Operating Theatre", "Scenes", 0.85f);
                BuildDemoScene();
                BuildGalleryScene();
            }
            finally { EditorUtility.ClearProgressBar(); }
            Debug.Log("[MROT] Ward build complete.");
        }

        /// <summary>Imports TextMeshPro Essential Resources (shaders + settings) once.</summary>
        [MenuItem("MR Operating Theatre/0. Import TextMeshPro Essentials", priority = 0)]
        public static void ImportTMPEssentials()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) { Debug.Log("[MROT] TMP essentials already present."); return; }
            string pkg = null;
            if (Directory.Exists("Library/PackageCache"))
                foreach (var p in Directory.GetFiles("Library/PackageCache", "TMP Essential Resources.unitypackage", SearchOption.AllDirectories)) { pkg = p; break; }
            if (pkg == null) { Debug.LogWarning("[MROT] TMP Essential Resources package not found - use Window > TextMeshPro > Import TMP Essential Resources."); return; }
            AssetDatabase.ImportPackage(pkg, false);
            Debug.Log("[MROT] Imported " + pkg);
        }

        /// <summary>Bakes TextMeshPro SDF font assets (Poppins + DejaVu symbol fallback) - crisp text at any distance and in VR.</summary>
        public static void ConfigureFonts()
        {
            string dir = Root + "/Resources/MROT/Fonts";
            foreach (var g in AssetDatabase.FindAssets("t:Font", new[] { dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var fi = AssetImporter.GetAtPath(p) as TrueTypeFontImporter;
                if (fi == null) continue;
                fi.includeFontData = true;
                fi.fontTextureCase = FontTextureCase.Dynamic;
                fi.SaveAndReimport();
            }
            if (Shader.Find("TextMeshPro/Distance Field") == null) ImportTMPEssentials();
            var sym = BakeFontAsset(dir, "DejaVuSans", null);
            foreach (var n in new[] { "Poppins-Regular", "Poppins-SemiBold", "Poppins-Bold" }) BakeFontAsset(dir, n, sym);
            AssetDatabase.SaveAssets();
        }

        const string WarmChars = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\u00B7\u2022\u2013\u2014\u2019\u201C\u201D\u00B0\u00D7";

        static TMP_FontAsset BakeFontAsset(string dir, string name, TMP_FontAsset fallback)
        {
            string path = dir + "/" + name + " SDF.asset";
            var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (fa == null)
            {
                var font = AssetDatabase.LoadAssetAtPath<Font>(dir + "/" + name + ".ttf");
                if (font == null) { Debug.LogWarning("[MROT] font missing: " + name); return null; }
                fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (fa == null || fa.material == null) { Debug.LogWarning("[MROT] could not create SDF font asset for " + name); return null; }
                fa.name = name + " SDF";
                AssetDatabase.CreateAsset(fa, path);
                fa.material.name = name + " SDF Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
                if (fa.atlasTextures != null)
                    foreach (var tex in fa.atlasTextures) { if (tex == null) continue; tex.name = name + " SDF Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
                string missing;
                fa.TryAddCharacters(WarmChars, out missing);
            }
            if (fallback != null && fallback != fa)
            {
                if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (!fa.fallbackFontAssetTable.Contains(fallback)) fa.fallbackFontAssetTable.Add(fallback);
            }
            EditorUtility.SetDirty(fa);
            return fa;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ project settings
        public static void ProjectSettingsSetup()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.companyName = "SLIIT J26-IM-304";
            QualitySettings.shadowDistance = 18f;
            QualitySettings.antiAliasing = 4;
        }

        static Shader LitShader { get { return Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"); } }
        static Shader UnlitShader { get { return Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"); } }

        // ------------------------------------------------------------------ materials
        public static void BuildMaterials()
        {
            EnsureFolder(MatDir);
            EnsureFolder(Root + "/Resources/MROT");
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Resources/MROT/Data/materials.json");
            if (ta == null) { Debug.LogError("[MROT] materials.json missing"); return; }
            var list = JsonUtility.FromJson<MatList>(ta.text);
            foreach (var d in list.materials)
            {
                var path = MatDir + "/" + d.name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool isNew = m == null;
                if (isNew) m = new Material(LitShader);
                else m.shader = LitShader;
                var c = new Color(d.color[0], d.color[1], d.color[2], d.color[3]);
                // Blender colours are linear; Unity material colours are sRGB in the inspector
                var srgb = c.gamma; srgb.a = c.a;
                SetC(m, "_BaseColor", srgb); SetC(m, "_Color", srgb);
                SetF(m, "_Metallic", d.metallic);
                SetF(m, "_Smoothness", Mathf.Clamp01(1f - d.roughness)); SetF(m, "_Glossiness", Mathf.Clamp01(1f - d.roughness));
                SetF(m, "_EnvironmentReflections", 1f); SetF(m, "_SpecularHighlights", 1f);
                if (c.a < 0.999f) MakeTransparent(m);
                else MakeOpaque(m);
                // emission (always enabled so highlights via MaterialPropertyBlock work)
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (d.emission != null && d.emission.Length == 4)
                {
                    var e = new Color(d.emission[0], d.emission[1], d.emission[2]) * Mathf.Min(d.emission[3], 3f);
                    SetC(m, "_EmissionColor", e);
                }
                else SetC(m, "_EmissionColor", Color.black);
                // textures
                string tex;
                if (ScreenTex.TryGetValue(d.name, out tex))
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Screens/" + tex + ".png");
                    if (t != null)
                    {
                        SetT(m, "_BaseMap", t); SetT(m, "_MainTex", t); SetT(m, "_EmissionMap", t);
                        SetC(m, "_BaseColor", Color.white); SetC(m, "_EmissionColor", Color.white * 1.05f);
                        SetF(m, "_Smoothness", 0.85f); SetF(m, "_Metallic", 0f);
                    }
                }
                if (d.name == "M_Floor_Vinyl" || d.name == "M_Floor_Corridor" || d.name == "M_Wall_Panel")
                {
                    var file = d.name == "M_Floor_Vinyl" ? "floor_vinyl" : d.name == "M_Floor_Corridor" ? "floor_corridor" : "wall_panel";
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Surfaces/" + file + ".png");
                    if (t != null) { SetT(m, "_BaseMap", t); SetT(m, "_MainTex", t); SetC(m, "_BaseColor", Color.white); }
                }
                if (isNew) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            }
            // runtime base materials (guarantee shaders are included in builds)
            CreateBase(Root + "/Resources/MROT/M_LitBase.mat", LitShader);
            CreateBase(Root + "/Resources/MROT/M_UnlitBase.mat", UnlitShader);
            AssetDatabase.SaveAssets();
        }

        static void CreateBase(string path, Shader s)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var m = new Material(s);
            AssetDatabase.CreateAsset(m, path);
        }

        static void SetC(Material m, string p, Color c) { if (m.HasProperty(p)) m.SetColor(p, c); }
        static void SetF(Material m, string p, float v) { if (m.HasProperty(p)) m.SetFloat(p, v); }
        static void SetT(Material m, string p, Texture t) { if (m.HasProperty(p)) m.SetTexture(p, t); }

        static void MakeTransparent(Material m)
        {
            SetF(m, "_Surface", 1f); SetF(m, "_Blend", 0f); SetF(m, "_AlphaClip", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            SetF(m, "_SrcBlend", (float)BlendMode.SrcAlpha); SetF(m, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetF(m, "_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        static void MakeOpaque(Material m)
        {
            SetF(m, "_Surface", 0f);
            m.SetOverrideTag("RenderType", "Opaque");
            SetF(m, "_SrcBlend", (float)BlendMode.One); SetF(m, "_DstBlend", (float)BlendMode.Zero);
            SetF(m, "_ZWrite", 1f);
            m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = -1;
        }

        // ------------------------------------------------------------------ models
        public static void ConfigureModels()
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models" });
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var mi = AssetImporter.GetAtPath(p) as ModelImporter;
                if (mi == null) continue;
                mi.globalScale = 1f; mi.useFileScale = true;
                mi.importCameras = false; mi.importLights = false; mi.importVisibility = true;
                mi.animationType = ModelImporterAnimationType.None; mi.importAnimation = false;
                mi.importNormals = ModelImporterNormals.Import;
                mi.meshCompression = ModelImporterMeshCompression.Off;
                mi.isReadable = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                mi.materialLocation = ModelImporterMaterialLocation.External;
                mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                mi.materialSearch = ModelImporterMaterialSearch.Everywhere;
                mi.SaveAndReimport();
                mi.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
                mi.SaveAndReimport();
            }
            // Remove any duplicate materials Unity extracted next to the models (we use ours in /Materials)
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { Root + "/Models" }))
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));
        }

        // ------------------------------------------------------------------ prefabs
        public static void BuildPrefabs()
        {
            EnsureFolder(PrefabDir);
            EnsureFolder(PrefabDir + "/Equipment");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(WardFbx);
            if (model == null) { Debug.LogError("[MROT] Ward FBX not found at " + WardFbx); return; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "MR_OperatingTheatre_Ward";
            if (inst.GetComponent<WardSetup>() == null) inst.AddComponent<WardSetup>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = r.name.Contains("Ceiling") || r.name.Contains("Room") ? ShadowCastingMode.On : ShadowCastingMode.On;
                r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
            }
            GameObjectUtility.SetStaticEditorFlags(inst, StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.OccludeeStatic);
            PrefabUtility.SaveAsPrefabAsset(inst, WardPrefab);
            UnityEngine.Object.DestroyImmediate(inst);
            // individual equipment prefabs (for teammates' modules)
            foreach (var sub in new[] { "Equipment", "Instruments" })
            {
                foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Models/" + sub }))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var m = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                    var i = (GameObject)PrefabUtility.InstantiatePrefab(m);
                    var id = Path.GetFileNameWithoutExtension(p);
                    var tag = i.GetComponent<EquipmentTag>() ?? i.AddComponent<EquipmentTag>();
                    tag.id = id;
                    var dir = PrefabDir + "/" + sub;
                    EnsureFolder(dir);
                    PrefabUtility.SaveAsPrefabAsset(i, dir + "/" + id + ".prefab");
                    UnityEngine.Object.DestroyImmediate(i);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ scenes
        public static GameObject InstantiateWard()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WardPrefab);
            var w = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            w.transform.position = Vector3.zero;
            return w;
        }

        public static void BuildDemoScene()
        {
            EnsureFolder(Root + "/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            InstantiateWard();
            var boot = new GameObject("MR_OT_Bootstrap");
            boot.AddComponent<WardDemoBootstrap>();
            // editor preview camera (replaced by the player rig at runtime)
            var cam = new GameObject("Main Camera"); cam.tag = "MainCamera";
            var c = cam.AddComponent<Camera>(); c.nearClipPlane = 0.03f;
            cam.transform.position = new Vector3(-2.4f, 1.9f, 2.8f);
            cam.transform.LookAt(new Vector3(0, 1.0f, -0.2f));
            EditorSceneManager.SaveScene(scene, DemoScene);
            AddToBuild(DemoScene, false);
        }

        /// <summary>Every asset on a pedestal with a label - lets the panel inspect each model individually.</summary>
        public static void BuildGalleryScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "GalleryFloor"; floor.transform.localScale = new Vector3(6, 1, 6);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_Floor_Corridor.mat");
            floor.AddComponent<WalkableFloor>();
            var sun = new GameObject("Key Light"); var l = sun.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.3f; l.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.7f, 0.74f, 0.78f); RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.55f); RenderSettings.ambientGroundColor = new Color(0.3f, 0.3f, 0.32f);
            var root = new GameObject("AssetGallery");
            int i = 0;
            var prefabs = new List<string>();
            foreach (var sub in new[] { "Equipment", "Instruments" })
                foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir + "/" + sub })) prefabs.Add(AssetDatabase.GUIDToAssetPath(g));
            prefabs.Sort();
            int cols = 8;
            foreach (var p in prefabs)
            {
                var id = Path.GetFileNameWithoutExtension(p);
                if (id == "OT_RoomShell") continue;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, root.transform);
                bool instr = id.StartsWith("Instr_");
                float spacing = 3.2f;
                int r = i / cols, c = i % cols;
                var pos = new Vector3((c - cols / 2f) * spacing, instr ? 0.9f : 0f, r * spacing);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0, 180, 0);
                if (instr)
                {
                    go.transform.localScale = Vector3.one * 3f;   // instruments shown 3x on a plinth
                    var plinth = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    plinth.name = id + "_Plinth"; plinth.transform.SetParent(root.transform);
                    plinth.transform.position = pos - new Vector3(0, 0.45f, 0); plinth.transform.localScale = new Vector3(1.0f, 0.9f, 0.8f);
                    plinth.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/M_Plastic_DarkGrey.mat");
                }
                var label = new GameObject(id + "_Label", typeof(RectTransform));
                label.transform.SetParent(root.transform);
                label.transform.position = new Vector3(pos.x, 0.02f, pos.z - 1.2f);
                label.transform.rotation = Quaternion.Euler(90, 0, 0);
                var tm = label.AddComponent<TextMeshPro>();
                var lf = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Root + "/Resources/MROT/Fonts/Poppins-SemiBold SDF.asset");
                if (lf != null) tm.font = lf;
                tm.text = id.Replace("Instr_", "").Replace("_", " ");
                tm.fontSize = 2.4f; tm.alignment = TextAlignmentOptions.Center; tm.color = new Color(0.1f, 0.2f, 0.3f);
                tm.textWrappingMode = TextWrappingModes.Normal;
                tm.rectTransform.sizeDelta = new Vector2(3.0f, 0.6f);
                i++;
            }
            var boot = new GameObject("Gallery_Bootstrap");
            boot.AddComponent<GalleryBootstrap>();
            EditorSceneManager.SaveScene(scene, GalleryScene);
            AddToBuild(GalleryScene, false);
        }

        public static void AddToBuild(string scenePath, bool first)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == scenePath);
            var e = new EditorBuildSettingsScene(scenePath, true);
            if (first) list.Insert(0, e); else list.Add(e);
            EditorBuildSettings.scenes = list.ToArray();
        }

        [MenuItem("MR Operating Theatre/Export Shared Ward Package (.unitypackage)", priority = 20)]
        public static void ExportPackage()
        {
            var path = EditorUtility.SaveFilePanel("Export MR Operating Theatre package", "", "MR_OperatingTheatre_SharedWard.unitypackage", "unitypackage");
            if (string.IsNullOrEmpty(path)) return;
            AssetDatabase.ExportPackage(Root, path, ExportPackageOptions.Recurse);
            EditorUtility.RevealInFinder(path);
        }

        [MenuItem("MR Operating Theatre/Open Ward Demo Scene", priority = 30)]
        public static void OpenDemo() { EditorSceneManager.OpenScene(DemoScene); }
        [MenuItem("MR Operating Theatre/Open Asset Gallery Scene", priority = 31)]
        public static void OpenGallery() { EditorSceneManager.OpenScene(GalleryScene); }
    }
}
