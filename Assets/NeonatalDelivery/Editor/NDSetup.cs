// Neonatal Delivery - one-click setup: dependency install, materials, model remapping, scene + prefab construction.
// Everything is created under Assets/NeonatalDelivery; shared team assets (ward prefab, M_* materials) are only *referenced*.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonatalDelivery.EditorTools
{
    public static class NDSetup
    {
        public const string Root = "Assets/NeonatalDelivery";
        public const string Models = Root + "/Art/Models";
        public const string ScenePath = Root + "/Scenes/ND_NeonatalDelivery.unity";
        public const string PrefabDir = Root + "/Prefabs";
        const string WardPrefab = "Assets/MR_OperatingTheatre/Prefabs/MR_OperatingTheatre_Ward.prefab";
        static readonly string[] Packages = { "com.unity.xr.hands@1.9.0", "com.unity.xr.meta-openxr@2.6.1" };
        static AddAndRemoveRequest _req;

        // ---------------------------------------------------------------- menus
        [MenuItem("Neonatal Delivery/1. Install Dependencies (XR Hands + Unity OpenXR Meta)", priority = 1)]
        public static void InstallDependencies()
        {
            if (!EditorUtility.DisplayDialog("Neonatal Delivery", "Install com.unity.xr.hands 1.9.0 (hand tracking) and com.unity.xr.meta-openxr 2.6.1 (Quest passthrough + spatial anchors, brings AR Foundation 6)?\n\nThis adds two lines to Packages/manifest.json; other modules are not affected.", "Install", "Cancel")) return;
            _req = Client.AddAndRemove(Packages, null);
            EditorApplication.update += PollInstall;
            Debug.Log("[NeonatalDelivery] Installing " + string.Join(", ", Packages));
        }
        static void PollInstall()
        {
            if (_req == null || !_req.IsCompleted) return;
            EditorApplication.update -= PollInstall;
            if (_req.Status == StatusCode.Success) Debug.Log("[NeonatalDelivery] Dependencies installed. Next: 'Neonatal Delivery > 2. Configure OpenXR'.");
            else Debug.LogError("[NeonatalDelivery] Package install failed: " + (_req.Error != null ? _req.Error.message : "unknown"));
            _req = null;
        }

        [MenuItem("Neonatal Delivery/3. Build Everything (materials, models, scene)", priority = 3)]
        public static void BuildEverything()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // scenes cannot be created in Play mode: leave Play mode and run once the Editor is back in Edit mode
                Debug.Log("[NeonatalDelivery] Leaving Play mode - Build Everything will run automatically.");
                EditorApplication.playModeStateChanged += RunAfterPlay; EditorApplication.isPlaying = false; return;
            }
            try
            {
                EditorUtility.DisplayProgressBar("Neonatal Delivery", "Checking importers", 0.05f);
                RepairImporters();
                EditorUtility.DisplayProgressBar("Neonatal Delivery", "Importing models and textures", 0.1f);
                EnsureFolder(Root + "/Scenes"); EnsureFolder(PrefabDir);
                RemoveObsolete();
                foreach (var dir in new[] { Models, Root + "/Art/Textures" })
                    foreach (var g in AssetDatabase.FindAssets("", new[] { dir })) { var p = AssetDatabase.GUIDToAssetPath(g); if (!AssetDatabase.IsValidFolder(p)) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); }
                EditorUtility.DisplayProgressBar("Neonatal Delivery", "Building materials", 0.35f);
                NDMaterialBuilder.BuildAll();
                EditorUtility.DisplayProgressBar("Neonatal Delivery", "Remapping model materials", 0.55f);
                NDMaterialBuilder.RemapModels();
                EditorUtility.DisplayProgressBar("Neonatal Delivery", "Building scene", 0.8f);
                NDSceneBuilder.Build();
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        /// <summary>
        /// Every module asset must use the right importer (a .meta written without an importer block makes Unity import a
        /// texture as a plain DefaultAsset, which leaves the materials untextured). Wrong importer -> regenerate its .meta.
        /// Also removes the legacy "Materials" / ".fbm" folders that external-material FBX import creates.
        /// </summary>
        [MenuItem("Neonatal Delivery/Tools/Repair Importers", priority = 25)]
        public static void RepairImporters()
        {
            int fixedN = 0;
            foreach (var g in AssetDatabase.FindAssets("", new[] { Root }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (AssetDatabase.IsValidFolder(p)) continue;
                var ext = Path.GetExtension(p).ToLowerInvariant();
                System.Type want = ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" ? typeof(TextureImporter)
                                 : ext == ".fbx" ? typeof(ModelImporter) : ext == ".wav" ? typeof(AudioImporter) : null;
                if (want == null) continue;
                var imp = AssetImporter.GetAtPath(p);
                if (imp != null && want.IsInstanceOfType(imp)) continue;
                Debug.LogWarning($"[NeonatalDelivery] {p} was imported with {(imp != null ? imp.GetType().Name : "no importer")} - regenerating its import settings.");
                var meta = NDMaterialBuilder.Abs(p) + ".meta";
                if (File.Exists(meta)) File.Delete(meta);
                AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                fixedN++;
            }
            foreach (var legacy in new[] { Models + "/Materials" })
                if (AssetDatabase.IsValidFolder(legacy)) { AssetDatabase.DeleteAsset(legacy); Debug.Log("[NeonatalDelivery] Removed legacy folder " + legacy); }
            foreach (var d in Directory.GetDirectories(NDMaterialBuilder.Abs(Models), "*.fbm"))
            {
                var rel = Models + "/" + Path.GetFileName(d);
                if (AssetDatabase.IsValidFolder(rel)) AssetDatabase.DeleteAsset(rel); else Directory.Delete(d, true);
                Debug.Log("[NeonatalDelivery] Removed legacy folder " + rel);
            }
            AssetDatabase.Refresh();
            Debug.Log($"[NeonatalDelivery] Importer check: {fixedN} asset(s) repaired.");
        }

        /// <summary>v4: assets that are no longer used (sounds/SFX removed, physics drape replaced by the user's gown).</summary>
        static void RemoveObsolete()
        {
            foreach (var p in new[] { Root + "/Resources/NeonatalDelivery/Audio", Root + "/Art/Textures/Drape" })
                if (AssetDatabase.IsValidFolder(p)) { AssetDatabase.DeleteAsset(p); Debug.Log("[NeonatalDelivery] Removed obsolete " + p); }
            var dm = NDMaterialBuilder.MatDir + "/ND_DrapeGreen.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(dm) != null) AssetDatabase.DeleteAsset(dm);
        }

        static void RunAfterPlay(PlayModeStateChange st)
        {
            if (st != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= RunAfterPlay;
            EditorApplication.delayCall += BuildEverything;
        }

        [MenuItem("Neonatal Delivery/Tools/Repair Materials (rebuild + reassign in open scene)", priority = 23)]
        public static void RepairMaterials()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[NeonatalDelivery] Stop Play mode first."); return; }
            foreach (var g in AssetDatabase.FindAssets("", new[] { Root + "/Art/Textures" })) { var p = AssetDatabase.GUIDToAssetPath(g); if (!AssetDatabase.IsValidFolder(p)) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); }
            NDMaterialBuilder.BuildAll();
            NDMaterialBuilder.RemapModels();
            int n = 0;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) n += NDMaterialBuilder.AssignByName(root);
            if (n > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[NeonatalDelivery] Repair: {n} slot(s) reassigned in the open scene. Save the scene (Ctrl+S).");
            DiagnoseMaterials();
        }

        [MenuItem("Neonatal Delivery/Tools/Diagnose Materials (log report)", priority = 24)]
        public static void DiagnoseMaterials()
        {
            var sb = new System.Text.StringBuilder("[NeonatalDelivery] MATERIAL REPORT\n");
            var mats = AssetDatabase.FindAssets("t:Material", new[] { NDMaterialBuilder.MatDir });
            sb.AppendLine($"  {mats.Length} material assets in {NDMaterialBuilder.MatDir}");
            var texs = AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Art/Textures" });
            sb.AppendLine($"  {texs.Length} textures in {Root}/Art/Textures");
            sb.AppendLine($"  shader NeonatalDelivery/Skin found: {Shader.Find("NeonatalDelivery/Skin") != null}, supported: {(Shader.Find("NeonatalDelivery/Skin") != null && Shader.Find("NeonatalDelivery/Skin").isSupported)}");
            foreach (var n in new[] { "Std_Skin_Body", "Std_Skin_Leg", "Std_Skin_Head", "ND_Gown", "ND_BabySkin" })
            {
                var m = NDMaterialBuilder.FindMaterial(n);
                if (m == null) { sb.AppendLine($"  {n}: MISSING .mat"); continue; }
                var t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                sb.AppendLine($"  {n}: shader={m.shader.name} baseMap={(t != null ? AssetDatabase.GetAssetPath(t) : "NONE")} color={(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString() : "-")}");
            }
            foreach (var g in AssetDatabase.FindAssets("t:Model", new[] { Models }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g); var mi = AssetImporter.GetAtPath(p) as ModelImporter; if (mi == null) continue;
                int ext = 0; foreach (var kv in mi.GetExternalObjectMap()) if (kv.Value is Material) ext++;
                sb.AppendLine($"  {Path.GetFileName(p)}: materialImportMode={mi.materialImportMode}, remapped slots={ext}");
            }
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (r.name == "M_Body" || r.name == "M_Gown" || r.name.StartsWith("B_Body")) sb.AppendLine("  scene " + NDMaterialBuilder.Describe(r));
            Debug.Log(sb.ToString());
        }

        [MenuItem("Neonatal Delivery/4. Open Delivery Scene", priority = 4)]
        public static void OpenScene() { if (File.Exists(NDMaterialBuilder.Abs(ScenePath))) EditorSceneManager.OpenScene(ScenePath); else Debug.LogWarning("[NeonatalDelivery] Run 'Build Everything' first."); }

        [MenuItem("Neonatal Delivery/5. Add Scene to Build Settings", priority = 5)]
        public static void AddToBuild()
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
            Debug.Log("[NeonatalDelivery] Scene added to Build Settings (existing scenes untouched).");
        }

        [MenuItem("Neonatal Delivery/Tools/Validate Fault Rules JSON", priority = 20)]
        public static void ValidateRules()
        {
            var p = "Assets/StreamingAssets/NeonatalDelivery/nd_fault_rules.json";
            if (!File.Exists(NDMaterialBuilder.Abs(p))) { Debug.LogError("Missing " + p); return; }
            var set = FaultRuleSet.Parse(File.ReadAllText(NDMaterialBuilder.Abs(p)));
            int bad = 0;
            foreach (var r in set.rules)
            {
                if (r.stepSet.Count == 0) { Debug.LogWarning($"{r.code}: no valid steps"); bad++; }
                if (r.zones != null && r.zones.Length != r.zoneSet.Count) { Debug.LogWarning($"{r.code}: unknown zone name"); bad++; }
                if (string.IsNullOrEmpty(r.remediation)) { Debug.LogWarning($"{r.code}: no remediation text"); bad++; }
            }
            Debug.Log($"[NeonatalDelivery] {set.rules.Count} fault rules checked, {bad} problem(s). Status: {set.status}");
        }

        [MenuItem("Neonatal Delivery/Tools/Open Session Results Folder", priority = 21)]
        public static void OpenResults() { Directory.CreateDirectory(SessionConfig.Folder); EditorUtility.RevealInFinder(SessionConfig.Folder); }

        [MenuItem("Neonatal Delivery/Tools/Reset Mannequin Calibration", priority = 22)]
        public static void ResetCalibration() { PlayerPrefs.DeleteKey("ND_StationPose_v1"); PlayerPrefs.DeleteKey("ND_StationAnchor_v1"); Debug.Log("[NeonatalDelivery] Calibration cleared."); }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }

    public static class NDSceneBuilder
    {
        // Mother bone positions in the base delivery pose, in the Blender "bed space" the assets were authored in
        // (x = mother's left, y = toward the bed head, z = up, origin = floor under the seat's foot edge).
        static readonly (string bone, Vector3 p)[] BedRef =
        {
            ("Hips", new Vector3(0f, 0.0483f, 0.9723f)), ("Chest", new Vector3(0f, 0.221f, 1.09f)), ("Head", new Vector3(0f, 0.4868f, 1.3728f)),
            ("LeftUpperArm", new Vector3(0.1636f, 0.4125f, 1.3049f)), ("RightUpperArm", new Vector3(-0.1636f, 0.4125f, 1.3049f))
        };
        static Quaternion _bedRot = Quaternion.identity; static Vector3 _bedPos; static Transform _station;
        // Bed space is right-handed (Blender) and Unity is left-handed, so the numeric mapping is improper (det -1):
        // mirror x first, then a proper rotation + translation fitted from the imported bones maps it into the station.
        static Vector3 L(Vector3 p) { return new Vector3(-p.x, p.y, p.z); }
        static Vector3 Bed(float x, float y, float z) { return _station.TransformPoint(_bedRot * L(new Vector3(x, y, z)) + _bedPos); }
        static Vector3 BedDir(float x, float y, float z) { return _station.TransformDirection(_bedRot * L(new Vector3(x, y, z))); }

        static T Mat<T>(string name) where T : Object { return AssetDatabase.LoadAssetAtPath<T>($"{NDMaterialBuilder.MatDir}/{name}.mat"); }

        static GameObject Inst(string path, Transform parent)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) { Debug.LogError("[NeonatalDelivery] Missing asset " + path); return new GameObject(Path.GetFileNameWithoutExtension(path)); }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static AnimationClip[] Clips(string fbx)
        {
            return AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        }

        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;   // never discard someone's unsaved scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // ---------- shared environment (teammate's ward prefab, referenced unmodified)
            var wardSrc = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MR_OperatingTheatre/Prefabs/MR_OperatingTheatre_Ward.prefab");
            GameObject env = wardSrc != null ? (GameObject)PrefabUtility.InstantiatePrefab(wardSrc) : null;
            Transform table = null, screen = null;
            if (env != null)
            {
                table = RigUtil.FindDeep(env.transform, "OperatingTable");
                screen = RigUtil.FindDeep(env.transform, "AnaestheticScreen");
                // scene-local overrides only: the delivery bed replaces the operating table in this module
                if (table != null) table.gameObject.SetActive(false);
                if (screen != null) screen.gameObject.SetActive(false);
                var mannequin = RigUtil.FindDeep(env.transform, "PatientMannequin"); if (mannequin != null) mannequin.gameObject.SetActive(false);
            }
            else
            {
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Floor"; floor.transform.localScale = Vector3.one * 2f;
                var l = new GameObject("Light").AddComponent<Light>(); l.type = LightType.Directional; l.transform.rotation = Quaternion.Euler(50, 30, 0);
            }

            // ---------- delivery station
            _station = new GameObject("ND_DeliveryStation").transform;
            var bed = Inst(NDSetup.Models + "/ND_DeliveryBed.fbx", _station);
            var mother = Inst(NDSetup.Models + "/ND_Mother.fbx", _station);
            var pathFrame = new GameObject("ND_BabyPathFrame").transform; pathFrame.SetParent(_station, false);
            var baby = Inst(NDSetup.Models + "/ND_Baby.fbx", pathFrame);
            var mClips = Clips(NDSetup.Models + "/ND_Mother.fbx"); var bClips = Clips(NDSetup.Models + "/ND_Baby.fbx");
            var baseClip = RigUtil.FindClip(mClips, "ND_M_Base"); if (baseClip != null) baseClip.SampleAnimation(mother, 0f);
            var birth = RigUtil.FindClip(bClips, "ND_B_Birth"); if (birth != null) birth.SampleAnimation(baby, 0f);
            // measure the bed-space -> station mapping from the imported rig (independent of FBX axis conventions)
            var a = new List<Vector3>(); var b = new List<Vector3>();
            foreach (var r in BedRef) { var t = RigUtil.FindDeep(mother.transform, r.bone); if (t != null) { a.Add(L(r.p)); b.Add(_station.InverseTransformPoint(t.position)); } }
            if (!RigUtil.RigidAlign(a, b, out _bedRot, out _bedPos)) Debug.LogWarning("[NeonatalDelivery] Could not measure bed space; using identity.");
            float fitErr = 0; for (int i = 0; i < a.Count; i++) fitErr += ((_bedRot * a[i] + _bedPos) - b[i]).magnitude; fitErr = fitErr / Mathf.Max(1, a.Count) * 1000f;
            if (fitErr > 5f) Debug.LogWarning($"[NeonatalDelivery] bed-space fit residual {fitErr:F1} mm (expected < 2 mm) - check the ND_Mother import (Generic rig, ND_M_Base clip).");
            else Debug.Log($"[NeonatalDelivery] bed-space fit residual {fitErr:F1} mm");

            // place the station where the operating table was: bed head toward the table head end
            if (table != null)
            {
                Vector3 headDir = Vector3.forward;
                var anchor = RigUtil.FindDeep(table, "ANCHOR_PatientHead");
                if (anchor != null) headDir = anchor.position - table.position;
                else if (screen != null) headDir = screen.position - table.position;
                else headDir = table.forward;
                headDir.y = 0; headDir.Normalize();
                Vector3 bedHead = BedDir(0, 1, 0); bedHead.y = 0; bedHead.Normalize();
                _station.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(bedHead, headDir, Vector3.up), Vector3.up) * _station.rotation;   // yaw only
                Vector3 center = Bed(0, 0.47f, 0);
                Vector3 target = table.position; target.y = table.position.y;
                _station.position += target - center;
            }
            float floorY = _station.position.y;

            // ---------- rig components
            var mc = mother.AddComponent<MotherController>(); mc.clips = mClips;
            mc.bodyRenderer = FindSMR(mother, "M_Body"); mc.eyelashRenderer = FindSMR(mother, "M_Eyelash");
            var canal = mother.AddComponent<BirthCanalDeformer>();
            var drapeSmr = FindSMR(mother, "ND_Drape");
            DrapeCloth drape = null;
            if (drapeSmr != null) { drape = drapeSmr.gameObject.AddComponent<DrapeCloth>(); drape.motherRoot = mother.transform; }
            else Debug.Log("[NeonatalDelivery] Mother wears the skinned green gown (M_Gown) - no physics drape needed.");
            var bc = baby.AddComponent<BabyController>(); bc.model = baby; bc.clips = bClips; bc.ghostMaterial = Mat<Material>("ND_Ghost");
            bc.skinRenderers = baby.GetComponentsInChildren<Renderer>(true);   // v5: unrigged baby (MeshRenderer)
            var anim = baby.GetComponent<Animator>(); if (anim != null) Object.DestroyImmediate(anim);
            var cordGo = new GameObject("ND_UmbilicalCord", typeof(MeshFilter), typeof(MeshRenderer));
            var cord = cordGo.AddComponent<UmbilicalCord>(); cord.material = Mat<Material>("ND_Cord");
            cord.placentaEnd = RigUtil.FindDeep(mother.transform, "ND_Anchor_Placenta");
            cord.babyEnd = RigUtil.FindDeep(baby.transform, "ND_Z_NavelSkin");
            cord.babyAxisLow = RigUtil.FindDeep(baby.transform, "Hips"); cord.babyAxisHigh = RigUtil.FindDeep(baby.transform, "Chest");
            cordGo.GetComponent<MeshRenderer>().sharedMaterial = cord.material;
            var perineum = new GameObject("ND_Zone_MaternalPerineum").transform; perineum.SetParent(_station, false);

            // ---------- instruments on a slim instrument stand at the trainee's right, clear of the raised legs
            // (the feet reach x=0.51 m at y=-0.48 m in bed space; the stand sits behind/right of the feet, within arm's reach)
            Vector3 standPos = Bed(0.68f, -0.88f, 0f); standPos.y = floorY;
            float trayTop = floorY + 0.93f;
            var stand = new GameObject("ND_InstrumentStand").transform; stand.SetParent(_station, false); stand.position = standPos;
            Prim(PrimitiveType.Cylinder, stand, new Vector3(0, 0.012f, 0), new Vector3(0.46f, 0.012f, 0.46f), "M_Plastic_Grey");      // weighted base
            Prim(PrimitiveType.Cylinder, stand, new Vector3(0, 0.47f, 0), new Vector3(0.028f, 0.45f, 0.028f), "M_Steel_Polished");     // pole
            Prim(PrimitiveType.Cube, stand, new Vector3(0, 0.915f, 0), new Vector3(0.40f, 0.012f, 0.28f), "M_Steel_Brushed");          // tray shelf
            stand.rotation = Quaternion.LookRotation(Flat(BedDir(0, 1, 0)), Vector3.up);
            var tray = Inst(NDSetup.Models + "/ND_DeliveryTray.fbx", _station); tray.name = "ND_DeliveryTray";
            tray.transform.position = new Vector3(standPos.x, trayTop + 0.002f, standPos.z); tray.transform.rotation = Quaternion.LookRotation(Flat(BedDir(0, 1, 0)), Vector3.up);
            var clampA = Tool(NDSetup.Models + "/ND_CordClamp.fbx", tray.transform, new Vector3(-0.12f, 0.012f, 0.07f), "ND_CordClamp_A", GrabStyle.Pinch).AddComponent<CordClampTool>();
            var clampB = Tool(NDSetup.Models + "/ND_CordClamp.fbx", tray.transform, new Vector3(-0.12f, 0.012f, -0.02f), "ND_CordClamp_B", GrabStyle.Pinch).AddComponent<CordClampTool>();
            var scissors = Tool(NDSetup.Models + "/ND_CordScissors.fbx", tray.transform, new Vector3(0.06f, 0.012f, 0.06f), "ND_CordScissors", GrabStyle.Pinch).AddComponent<CordScissorsTool>();
            var towelGo = Tool(NDSetup.Models + "/ND_Towel.fbx", _station, Vector3.zero, "ND_Towel", GrabStyle.Grip);
            towelGo.transform.position = Bed(0.29f, 0.17f, 0.926f); towelGo.transform.rotation = Quaternion.AngleAxis(90f, Vector3.up) * _station.rotation;   // folded on the seat beside the mother's thigh
            var towel = towelGo.AddComponent<TowelTool>();

            // ---------- FX + privacy veil + delivery lamp
            var blood = FX("ND_FX_Blood", Mat<Material>("ND_FX_Blood"), _station);
            var fluid = FX("ND_FX_Fluid", Mat<Material>("ND_FX_Fluid"), _station);
            var veil = GameObject.CreatePrimitive(PrimitiveType.Quad); veil.name = "ND_PrivacyVeil"; Object.DestroyImmediate(veil.GetComponent<Collider>());
            veil.transform.SetParent(_station, false); veil.GetComponent<Renderer>().sharedMaterial = Mat<Material>("ND_Veil"); veil.GetComponent<Renderer>().enabled = false;
            // materials: safety-net assignment by slot name (scene only) + a diagnostic line per key renderer in the console/_dev_log
            int fixedSlots = 0; foreach (var go in new[] { bed, mother, baby, tray, towelGo, clampA.gameObject, clampB.gameObject, scissors.gameObject }) fixedSlots += NDMaterialBuilder.AssignByName(go);
            if (fixedSlots > 0) Debug.Log($"[NeonatalDelivery] {fixedSlots} renderer material slot(s) were assigned by name in the scene.");
            foreach (var r in mother.GetComponentsInChildren<Renderer>(true)) if (r.name == "M_Body" || r.name == "M_Gown") Debug.Log("[NeonatalDelivery] material check " + NDMaterialBuilder.Describe(r));
            foreach (var r in baby.GetComponentsInChildren<Renderer>(true)) { Debug.Log("[NeonatalDelivery] material check " + NDMaterialBuilder.Describe(r)); break; }
            var lamp = new GameObject("ND_DeliveryLamp").AddComponent<Light>(); lamp.transform.SetParent(_station, false);
            lamp.type = LightType.Spot; lamp.spotAngle = 48f; lamp.range = 4f; lamp.intensity = 2.5f; lamp.color = new Color(1f, 0.97f, 0.92f); lamp.shadows = LightShadows.Soft;
            lamp.transform.position = Bed(0f, -0.55f, 2.25f); lamp.transform.rotation = Quaternion.LookRotation(Bed(0f, -0.03f, 0.98f) - lamp.transform.position);

            // ---------- XR rig with hands
            var rigGo = new GameObject("ND_XRRig"); var rig = rigGo.AddComponent<NDXRRig>(); rig.EnsureBuilt();
            var hands = rigGo.AddComponent<HandTrackingSource>(); hands.trackingSpace = rig.trackingSpace; hands.headCamera = rig.cam;
            rigGo.AddComponent<GrabInteractor>().hands = hands;
            var uiInt = rigGo.AddComponent<NDUIInteractor>(); uiInt.hands = hands; uiInt.cam = rig.cam;
            if (drape != null) drape.hands = hands;
            var gloveMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/MR_OperatingTheatre/Materials/M_Glove_Blue.mat") ?? Mat<Material>("ND_GloveNitrile");
            var gl = new GameObject("Glove_Left").AddComponent<GloveHandVisual>(); gl.transform.SetParent(rigGo.transform, false); gl.side = HandSide.Left; gl.material = gloveMat;
            var gr = new GameObject("Glove_Right").AddComponent<GloveHandVisual>(); gr.transform.SetParent(rigGo.transform, false); gr.side = HandSide.Right; gr.material = gloveMat;
            Vector3 eye = Bed(0f, -0.62f, 1.6f);
            Vector3 stand2 = eye; stand2.y = floorY; rigGo.transform.position = stand2; rigGo.transform.rotation = Quaternion.LookRotation(Flat(BedDir(0, 1, 0)), Vector3.up);

            // ---------- systems
            var sys = new GameObject("ND_Systems");
            var dir = sys.AddComponent<DeliveryDirector>();
            var eval = sys.AddComponent<HandPlacementEvaluator>();
            var cons = sys.AddComponent<ConsequenceEngine>(); cons.bloodFx = blood; cons.fluidFx = fluid;
            var cues = sys.AddComponent<CueSystem>(); cues.ghostMaterial = Mat<Material>("ND_Ghost");
            var censor = sys.AddComponent<CensorshipFilter>(); censor.privacyVeil = veil.GetComponent<Renderer>(); censor.motherSkin.Add(mc.bodyRenderer);
            var ghost = sys.AddComponent<GhostHandGuide>(); ghost.ghostMaterial = Mat<Material>("ND_Ghost");
            var fit = sys.AddComponent<HandAutoFit>(); fit.guide = ghost; fit.hands = hands;   // hands near the newborn snap into the correct pose
            NDAudio audioSys = null;   // v4: no sounds / SFX
            var pass = sys.AddComponent<PassthroughController>(); pass.rig = rig; pass.occluderMaterial = Mat<Material>("ND_Occluder");
            pass.hideInMR = env != null ? new[] { env, bed } : new[] { bed };
            pass.motherRenderers = mother.GetComponentsInChildren<Renderer>(true);
            var calib = sys.AddComponent<MannequinCalibration>();
            var replayGo = new GameObject("ND_Replay"); replayGo.transform.SetParent(sys.transform, false);
            var replay = replayGo.AddComponent<ReflectiveReplay>(); replay.replayHandMaterial = Mat<Material>("ND_ReplayHand"); replay.faultHandMaterial = Mat<Material>("ND_GhostFault");

            // ---------- world-space UI panels (content is built at runtime)
            var ui = new GameObject("ND_UI").transform;
            var edu = Panel("ND_EducatorPanel", ui, Bed(0.0f, 0.18f, 1.62f), eye, 960f).gameObject.AddComponent<EducatorPanel>(); edu.panel = edu.GetComponent<NDPanel>();
            var ins = Panel("ND_InstructionPanel", ui, Bed(-0.62f, -0.05f, 1.55f), eye, 900f).gameObject.AddComponent<InstructionPanel>(); ins.panel = ins.GetComponent<NDPanel>();
            var sum = Panel("ND_SummaryPanel", ui, Bed(0.0f, 0.18f, 1.62f), eye, 1000f).gameObject.AddComponent<SummaryPanel>(); sum.panel = sum.GetComponent<NDPanel>();
            var rp = Panel("ND_ReplayPanel", ui, Bed(0.62f, 0.02f, 1.6f), eye, 880f); replay.panel = rp;

            // ---------- wire the director
            dir.station = _station; dir.babyPathFrame = pathFrame; dir.mother = mc; dir.canal = canal; dir.baby = bc; dir.cord = cord;
            dir.perineumZone = perineum; dir.skinToSkinAnchor = RigUtil.FindDeep(mother.transform, "ND_Anchor_SkinToSkin");
            dir.environmentRoot = env; dir.deliveryBed = bed;
            dir.hands = hands; dir.evaluator = eval; dir.leftGlove = gl; dir.rightGlove = gr;
            dir.clampA = clampA; dir.clampB = clampB; dir.scissors = scissors; dir.towel = towel;
            dir.consequences = cons; dir.cues = cues; dir.replay = replay; dir.censorship = censor; dir.ghost = ghost; dir.autoFit = fit; dir.audioSys = audioSys;
            dir.rig = rig; dir.passthrough = pass; dir.calibration = calib;
            dir.educatorPanel = edu; dir.instructionPanel = ins; dir.summaryPanel = sum; dir.floorY = floorY;
            replay.viewTarget = perineum;

            if (env != null) ClearWorkArea(env.transform, bed, stand2, standPos);
            NDSetup.EnsureFolder(NDSetup.PrefabDir);
            PrefabUtility.SaveAsPrefabAssetAndConnect(_station.gameObject, NDSetup.PrefabDir + "/ND_DeliveryStation.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, NDSetup.ScenePath);
            Debug.Log("[NeonatalDelivery] Scene built: " + NDSetup.ScenePath + "  (press Play: desktop simulator; or build to Quest 3)");
        }

        // Scene-only: hide ward furniture that would stand inside the delivery bed, the trainee's standing space or the
        // instrument stand (e.g. the ward's own Mayo stand and step stool at the table foot). The ward prefab is untouched.
        static readonly string[] Keep = { "OT_RoomShell", "LaminarFlowCanopy", "SurgicalLight", "CeilingPendant", "MainTheatreDoor", "Door_" };
        static void ClearWorkArea(Transform ward, GameObject bed, Vector3 traineeFeet, Vector3 standPos)
        {
            var zones = new List<Bounds>();
            var bedR = bed.GetComponentsInChildren<Renderer>(); if (bedR.Length > 0) { var bb = bedR[0].bounds; foreach (var r in bedR) bb.Encapsulate(r.bounds); bb.Expand(0.05f); zones.Add(bb); }
            zones.Add(new Bounds(traineeFeet + Vector3.up * 1f, new Vector3(0.9f, 2f, 0.9f)));
            zones.Add(new Bounds(standPos + Vector3.up * 0.7f, new Vector3(0.6f, 1.4f, 0.6f)));
            var hidden = new List<string>();
            for (int i = 0; i < ward.childCount; i++)
            {
                var c = ward.GetChild(i); if (!c.gameObject.activeSelf) continue;
                if (Keep.Any(k => c.name.StartsWith(k))) continue;
                var rs = c.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.size.y > 2.6f) continue;   // room-scale geometry
                if (zones.Any(z => z.Intersects(b))) { c.gameObject.SetActive(false); hidden.Add(c.name); }
            }
            if (hidden.Count > 0) Debug.Log("[NeonatalDelivery] Hidden in this scene only (ward prefab unchanged): " + string.Join(", ", hidden));
        }

        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 localPos, Vector3 localScale, string material)
        {
            var g = GameObject.CreatePrimitive(t); g.name = t.ToString(); Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false); g.transform.localPosition = localPos; g.transform.localScale = localScale;
            var m = NDMaterialBuilder.FindMaterial(material); if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }

        static SkinnedMeshRenderer FindSMR(GameObject root, string name)
        {
            foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (s.name == name) return s;
            return null;
        }

        static GameObject Tool(string fbx, Transform parent, Vector3 localPos, string name, GrabStyle style)
        {
            var go = Inst(fbx, parent); go.name = name; go.transform.localPosition = localPos;
            var g = go.AddComponent<Grabbable>(); g.style = style; g.grabRadius = style == GrabStyle.Grip ? 0.09f : 0.045f;
            return go;
        }

        static ParticleSystem FX(string name, Material m, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.startLifetime = 1.4f; main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.004f, 0.011f); main.gravityModifier = 1f; main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 400;
            var em = ps.emission; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.01f;
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.World; col.dampen = 0.8f; col.bounce = 0.05f; col.lifetimeLoss = 0.2f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = m;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

        static NDPanel Panel(string name, Transform parent, Vector3 pos, Vector3 eye, float width)
        {
            var p = NDPanel.Create(name, parent, width, 0.0006f);
            p.transform.position = pos; p.transform.rotation = Quaternion.LookRotation(pos - eye, Vector3.up);
            return p;
        }
    }
}
