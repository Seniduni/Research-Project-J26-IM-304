using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace IVSim.EditorTools
{
    /// <summary>
    /// Menu: IV Cannulation > Build Training Scene
    /// Generates every 3D asset (arm, veins, cannula, swab, bottle, syringes, tourniquet...), the materials,
    /// the treatment-room scene with desktop + VR rigs, and wires all components together.
    /// Safe to run again: assets are overwritten in place.
    /// </summary>
    public static class IVSceneBuilder
    {
        const string Root = "Assets/IVCannulation";
        const string MatDir = Root + "/Materials";
        const string MeshDir = Root + "/Generated";
        const string PrefabDir = Root + "/Prefabs";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/IVCannulationTraining.unity";

        class Mats
        {
            public Material skin, skinTransparent, sleeve, vein, artery, floor, wall, table, metal, pillow, sheet,
                steel, bevel, catheter, hubPink, wingWhite, chamber, blood, bloodBright, cotton, cottonWet, cottonUsed, wood,
                bottle, bottleBlue, tourniquet, buckle, gloveBox, sharps, sharpsLid, slot, barrel, plunger, rubber, saline,
                dressingPack, film, gauze, tray, markerIdeal, markerOk, markerAvoid, wet, hematoma, bruise, lineUnlit,
                handBare, handGlove, buttonBase, btnGreen, btnYellow, btnRed, btnBlue, btnGray, board, skinFlat, veinFlat;
        }

        static int skinLayer = -1, insetLayer = -1;
        static Shader lit, unlit;

        [MenuItem("IV Cannulation/Build Training Scene", false, 0)]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog("Build IV Cannulation scene",
                "This creates (or overwrites) the scene, models and materials under Assets/IVCannulation.\n\nContinue?", "Build", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            string targetPath = ScenePath;
            if (File.Exists(ScenePath))
            {
                int choice = EditorUtility.DisplayDialogComplex("Scene already exists",
                    ScenePath + " already exists.\n\nIf you customised it (for example added a theatre environment), overwriting will ERASE those changes.",
                    "Save as a new copy", "Cancel", "Overwrite");
                if (choice == 1) return;
                if (choice == 0) targetPath = AssetDatabase.GenerateUniqueAssetPath(ScenePath);
            }

            lit = Shader.Find("Universal Render Pipeline/Lit");
            unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (lit == null || GraphicsSettings.defaultRenderPipeline == null)
            {
                EditorUtility.DisplayDialog("URP not found", "This builder expects a Universal Render Pipeline (URP) project. " +
                    "Materials will fall back to the Standard shader and may look wrong.", "OK");
                if (lit == null) lit = Shader.Find("Standard");
                if (unlit == null) unlit = Shader.Find("Unlit/Color");
            }

            EnsureFolders();
            skinLayer = EnsureLayer("IVSkin");
            insetLayer = EnsureLayer("IVInset");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var m = CreateMaterials();

            BuildEnvironment(m);
            var arm = BuildArm(m);
            var cleaning = arm.gameObject.AddComponent<SiteCleaning>();
            cleaning.wetMaterial = m.wet;

            var cannula = BuildCannula(m, new Vector3(0.25f, 0.8615f, 0.17f), Quaternion.identity);
            var swab = BuildSwab(m, new Vector3(0.31f, 0.8655f, 0.03f), Quaternion.LookRotation(Vector3.back, Vector3.up));
            BuildAlcoholBottle(m, new Vector3(0.39f, 0.858f, 0.22f));
            BuildSyringe(m, "BloodSyringe", ActionKind.BloodSyringe, new Vector3(0.235f, 0.8665f, -0.06f));
            BuildSyringe(m, "SalineSyringe", ActionKind.SalineSyringe, new Vector3(0.235f, 0.8665f, -0.12f));
            BuildDressing(m, new Vector3(0.385f, 0.858f, -0.03f));
            BuildGauze(m, new Vector3(0.385f, 0.858f, -0.12f));
            BuildGloves(m, new Vector3(-0.33f, 0.85f, 0.33f));
            BuildTourniquet(m, arm, new Vector3(-0.23f, 0.8515f, 0.02f));
            BuildSharpsBin(m, new Vector3(0.44f, 0.85f, 0.48f));
            var board = BuildAngleBoard(m, new Vector3(-0.36f, 0.85f, -0.06f));
            BuildButtons(m);

            // ---- rigs
            var focus = new GameObject("CameraFocus");
            focus.transform.position = new Vector3(0f, 0.92f, 0.12f);
            var desktop = BuildDesktopRig(focus.transform);
            var xr = BuildXRRig(m);

            // ---- UI helpers
            var sideView = BuildSideView(m);
            var gauge = BuildAngleGauge(m);

            // ---- managers
            var mgr = new GameObject("IVTrainer");
            var session = mgr.AddComponent<IVSession>();
            session.arm = arm;
            session.cleaning = cleaning;
            session.cannula = cannula;
            session.skinOpaque = m.skin;
            session.skinTransparent = m.skinTransparent;
            session.bloodMaterial = m.blood;
            session.hematomaMaterial = m.hematoma;
            session.bruiseMaterial = m.bruise;
            session.angleReferenceBoard = board;

            var boot = mgr.AddComponent<RigBootstrap>();
            boot.desktopRig = desktop;
            boot.xrRig = xr;

            var panelAnchor = new GameObject("TrainerPanelAnchor");
            panelAnchor.transform.position = new Vector3(0f, 1.33f, 0.80f);
            var toastAnchor = new GameObject("ToastAnchor");
            toastAnchor.transform.position = new Vector3(0f, 1.02f, 0.08f);
            var ui = mgr.AddComponent<TrainerUI>();
            ui.panelAnchor = panelAnchor.transform;
            ui.toastAnchor = toastAnchor.transform;
            ui.sideView = sideView;
            panelAnchor.transform.SetParent(mgr.transform, true);
            toastAnchor.transform.SetParent(mgr.transform, true);
            sideView.transform.SetParent(mgr.transform, true);
            gauge.transform.SetParent(mgr.transform, true);

            // ---- prefabs (reusable assets for the rest of the team)
            SavePrefab(arm.gameObject, "IV_PatientArm");
            SavePrefab(cannula.gameObject, "IV_Cannula_20G");
            SavePrefab(swab.gameObject, "IV_CottonSwab");

            EditorSceneManager.SaveScene(scene, targetPath);
            AddSceneToBuild(targetPath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = arm.gameObject;
            EditorUtility.DisplayDialog("Done", "Scene saved to " + targetPath + ".\n\nPress Play to test with mouse & keyboard, " +
                "or build to your headset (see README).", "OK");
        }

        [MenuItem("IV Cannulation/Export Arm as OBJ (for Blender)", false, 20)]
        public static void ExportArmObj()
        {
            var arm = Object.FindAnyObjectByType<ArmModel>();
            if (arm == null) { EditorUtility.DisplayDialog("No arm", "Open the training scene first.", "OK"); return; }
            string path = EditorUtility.SaveFilePanel("Export arm + veins as OBJ", "", "IV_Arm.obj", "obj");
            if (string.IsNullOrEmpty(path)) return;
            ObjExporter.Export(arm.transform, path);
            EditorUtility.RevealInFinder(path);
        }

        // =====================================================================================
        // Setup helpers
        // =====================================================================================

        static void EnsureFolders()
        {
            foreach (var p in new[] { MatDir, MeshDir, PrefabDir, SceneDir })
            {
                if (AssetDatabase.IsValidFolder(p)) continue;
                string parent = Path.GetDirectoryName(p).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(p));
            }
        }

        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return -1;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = name;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    return i;
                }
            }
            Debug.LogWarning("[IVSim] No free layer for " + name + " - the side view will be disabled.");
            return -1;
        }

        static void AddSceneToBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        static Mesh SaveMesh(Mesh mesh, string name)
        {
            string path = MeshDir + "/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = name;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static void SavePrefab(GameObject go, string name)
        {
            string path = PrefabDir + "/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
        }

        // =====================================================================================
        // Materials
        // =====================================================================================

        static Material Mat(string name, Color c, float smooth = 0.35f, float metal = 0f, bool transparent = false, Shader shader = null)
        {
            string path = MatDir + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader sh = shader != null ? shader : lit;
            if (mat == null)
            {
                mat = new Material(sh);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != sh) mat.shader = sh;
            mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal);
            if (transparent) MakeTransparent(mat); else MakeOpaque(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("DepthOnly", false);
            mat.SetShaderPassEnabled("ShadowCaster", false);
        }

        static void MakeOpaque(Material mat)
        {
            mat.SetFloat("_Surface", 0f);
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.Zero);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
            mat.SetFloat("_ZWrite", 1f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = -1;
            mat.SetShaderPassEnabled("DepthOnly", true);
            mat.SetShaderPassEnabled("ShadowCaster", true);
        }

        static Mats CreateMaterials()
        {
            var skinTone = new Color(0.71f, 0.52f, 0.40f, 1f);
            var m = new Mats();
            m.skin = Mat("Skin", skinTone, 0.42f);
            var st = skinTone; st.a = 0.35f;
            m.skinTransparent = Mat("SkinTransparent", st, 0.5f, 0f, true);
            m.sleeve = Mat("GownSleeve", new Color(0.55f, 0.7f, 0.82f), 0.15f);
            m.vein = Mat("Vein", new Color(0.22f, 0.32f, 0.78f), 0.6f);
            m.artery = Mat("Artery", new Color(0.82f, 0.16f, 0.16f), 0.6f);
            m.floor = Mat("Floor", new Color(0.62f, 0.66f, 0.64f), 0.3f);
            m.wall = Mat("Wall", new Color(0.88f, 0.9f, 0.88f), 0.1f);
            m.table = Mat("TableTop", new Color(0.82f, 0.84f, 0.86f), 0.45f, 0.1f);
            m.metal = Mat("Metal", new Color(0.7f, 0.72f, 0.75f), 0.7f, 0.9f);
            m.pillow = Mat("ArmBoard", new Color(0.42f, 0.55f, 0.62f), 0.2f);
            m.sheet = Mat("BedSheet", new Color(0.95f, 0.95f, 0.97f), 0.15f);
            m.steel = Mat("NeedleSteel", new Color(0.85f, 0.87f, 0.9f), 0.92f, 1f);
            m.bevel = Mat("BevelMark", new Color(0.25f, 0.25f, 0.28f), 0.8f, 1f);
            m.catheter = Mat("CatheterPlastic", new Color(0.95f, 0.97f, 1f, 0.55f), 0.85f, 0f, true);
            m.hubPink = Mat("Hub20G_Pink", new Color(1f, 0.55f, 0.72f, 0.88f), 0.7f, 0f, true);
            m.wingWhite = Mat("CannulaWhite", new Color(0.96f, 0.96f, 0.96f), 0.5f);
            m.chamber = Mat("FlashChamber", new Color(0.92f, 0.95f, 1f, 0.35f), 0.9f, 0f, true);
            m.blood = Mat("Blood", new Color(0.42f, 0.03f, 0.05f), 0.75f);
            m.bloodBright = Mat("BloodArterial", new Color(0.95f, 0.08f, 0.08f), 0.8f);
            m.cotton = Mat("CottonDry", new Color(0.98f, 0.98f, 0.97f), 0.05f);
            m.cottonWet = Mat("CottonWet", new Color(0.72f, 0.85f, 0.95f), 0.7f);
            m.cottonUsed = Mat("CottonUsed", new Color(0.78f, 0.74f, 0.66f), 0.2f);
            m.wood = Mat("SwabStick", new Color(0.86f, 0.78f, 0.6f), 0.2f);
            m.bottle = Mat("BottleWhite", new Color(0.93f, 0.95f, 0.97f), 0.6f);
            m.bottleBlue = Mat("BottleBlue", new Color(0.12f, 0.36f, 0.78f), 0.5f);
            m.tourniquet = Mat("Tourniquet", new Color(0.18f, 0.45f, 0.85f), 0.35f);
            m.buckle = Mat("Buckle", new Color(0.15f, 0.15f, 0.17f), 0.5f);
            m.gloveBox = Mat("GloveBox", new Color(0.42f, 0.38f, 0.8f), 0.3f);
            m.sharps = Mat("SharpsYellow", new Color(0.98f, 0.82f, 0.1f), 0.4f);
            m.sharpsLid = Mat("SharpsRed", new Color(0.82f, 0.12f, 0.12f), 0.4f);
            m.slot = Mat("SharpsSlot", new Color(0.08f, 0.08f, 0.08f), 0.2f);
            m.barrel = Mat("SyringeBarrel", new Color(0.95f, 0.97f, 1f, 0.32f), 0.9f, 0f, true);
            m.plunger = Mat("SyringePlunger", new Color(0.97f, 0.97f, 0.97f), 0.5f);
            m.rubber = Mat("RubberStopper", new Color(0.12f, 0.12f, 0.14f), 0.4f);
            m.saline = Mat("Saline", new Color(0.75f, 0.88f, 1f, 0.55f), 0.9f, 0f, true);
            m.dressingPack = Mat("DressingPack", new Color(0.97f, 0.97f, 0.95f), 0.3f);
            m.film = Mat("DressingFilm", new Color(0.95f, 0.98f, 1f, 0.35f), 0.95f, 0f, true);
            m.gauze = Mat("Gauze", new Color(0.99f, 0.99f, 0.98f), 0.05f);
            m.tray = Mat("Tray", new Color(0.78f, 0.8f, 0.82f), 0.75f, 0.85f);
            m.markerIdeal = Mat("MarkerIdeal", new Color(0.2f, 0.9f, 0.4f, 0.4f), 0.2f, 0f, true);
            m.markerOk = Mat("MarkerAcceptable", new Color(1f, 0.8f, 0.2f, 0.4f), 0.2f, 0f, true);
            m.markerAvoid = Mat("MarkerAvoid", new Color(1f, 0.25f, 0.25f, 0.4f), 0.2f, 0f, true);
            m.wet = Mat("WetAntiseptic", new Color(0.85f, 0.93f, 1f, 0.45f), 0.95f, 0f, true);
            m.hematoma = Mat("Haematoma", new Color(0.35f, 0.05f, 0.2f, 0.75f), 0.5f, 0f, true);
            m.bruise = Mat("Bruise", new Color(0.32f, 0.18f, 0.42f, 0.5f), 0.3f, 0f, true);
            m.lineUnlit = Mat("LineUnlit", new Color(1f, 1f, 1f, 1f), 0f, 0f, true, unlit);
            m.handBare = Mat("HandBare", new Color(0.86f, 0.68f, 0.56f), 0.35f);
            m.handGlove = Mat("NitrileGlove", new Color(0.35f, 0.55f, 0.95f), 0.55f);
            m.buttonBase = Mat("ButtonBase", new Color(0.18f, 0.2f, 0.24f), 0.4f);
            m.btnGreen = Mat("ButtonGreen", new Color(0.25f, 0.72f, 0.42f), 0.5f);
            m.btnYellow = Mat("ButtonYellow", new Color(0.92f, 0.7f, 0.2f), 0.5f);
            m.btnRed = Mat("ButtonRed", new Color(0.85f, 0.3f, 0.28f), 0.5f);
            m.btnBlue = Mat("ButtonBlue", new Color(0.28f, 0.52f, 0.9f), 0.5f);
            m.btnGray = Mat("ButtonGray", new Color(0.55f, 0.58f, 0.62f), 0.5f);
            m.board = Mat("Whiteboard", new Color(0.97f, 0.97f, 0.96f), 0.3f);
            m.skinFlat = Mat("DiagramSkin", new Color(0.83f, 0.6f, 0.45f), 0.2f);
            m.veinFlat = Mat("DiagramVein", new Color(0.25f, 0.35f, 0.85f), 0.2f);
            return m;
        }

        // =====================================================================================
        // Generic object helpers
        // =====================================================================================

        static GameObject Empty(string name, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            return go;
        }

        static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat, Quaternion localRot, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat, bool keepCollider = false)
        {
            return Prim(PrimitiveType.Cube, name, parent, localPos, size, mat, Quaternion.identity, keepCollider);
        }

        /// <summary>Cylinder between two points in the parent's local space.</summary>
        static GameObject Cyl(string name, Transform parent, Vector3 from, Vector3 to, float diameter, Material mat)
        {
            Vector3 d = to - from;
            var go = Prim(PrimitiveType.Cylinder, name, parent, (from + to) * 0.5f,
                          new Vector3(diameter, d.magnitude * 0.5f, diameter), mat, Quaternion.FromToRotation(Vector3.up, d.normalized));
            return go;
        }

        static GameObject MeshObj(string name, Transform parent, Mesh mesh, Material mat, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static BoxCollider FitBox(GameObject root, float pad, bool trigger)
        {
            var b = new Bounds();
            bool first = true;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = root.transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; }
                    else b.Encapsulate(p);
                }
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size + Vector3.one * pad;
            col.isTrigger = trigger;
            return col;
        }

        static Text WorldText(Transform parent, string text, Vector3 localPos, Quaternion localRot, Vector2 sizePx, float mpp, int fontSize, Color col, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var c = UIFactory.WorldCanvas("Label", parent, sizePx, mpp);
            c.transform.localPosition = localPos;
            c.transform.localRotation = localRot;
            var t = UIFactory.Label("Text", c.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontSize, align, col, FontStyle.Bold);
            t.text = text;
            return t;
        }

        static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        // =====================================================================================
        // Environment
        // =====================================================================================

        static void BuildEnvironment(Mats m)
        {
            var env = new GameObject("Environment").transform;

            var sun = new GameObject("Sun");
            sun.transform.SetParent(env);
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.1f;
            l.color = new Color(1f, 0.97f, 0.92f);
            l.shadows = LightShadows.Soft;

            var lamp = new GameObject("ExamLight");
            lamp.transform.SetParent(env);
            lamp.transform.position = new Vector3(0.1f, 1.9f, 0.1f);
            var pl = lamp.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.range = 3f;
            pl.intensity = 1.2f;
            pl.color = new Color(1f, 0.98f, 0.95f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.76f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.6f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.33f, 0.32f, 0.31f);

            Box("Floor", env, new Vector3(0f, -0.01f, 0.5f), new Vector3(8f, 0.02f, 8f), m.floor, true);
            Box("BackWall", env, new Vector3(0f, 1.5f, 2.4f), new Vector3(8f, 3f, 0.1f), m.wall, true);
            Box("LeftWall", env, new Vector3(-2.6f, 1.5f, 0.5f), new Vector3(0.1f, 3f, 8f), m.wall, true);
            Box("RightWall", env, new Vector3(2.6f, 1.5f, 0.5f), new Vector3(0.1f, 3f, 8f), m.wall, true);

            // procedure table
            var table = Empty("ProcedureTable", env, Vector3.zero, Quaternion.identity).transform;
            Box("Top", table, new Vector3(0f, 0.835f, 0.15f), new Vector3(1.1f, 0.03f, 0.95f), m.table, true);
            foreach (var p in new[] { new Vector2(-0.5f, -0.28f), new Vector2(0.5f, -0.28f), new Vector2(-0.5f, 0.58f), new Vector2(0.5f, 0.58f) })
                Cyl("Leg", table, new Vector3(p.x, 0f, p.y), new Vector3(p.x, 0.82f, p.y), 0.035f, m.metal);
            Box("ArmBoard", table, new Vector3(0f, 0.861f, 0.12f), new Vector3(0.15f, 0.022f, 0.64f), m.pillow, true);
            Box("Tray", table, new Vector3(0.31f, 0.854f, 0.06f), new Vector3(0.24f, 0.008f, 0.46f), m.tray, true);

            // patient bed (stylised)
            var bed = Empty("PatientBed", env, Vector3.zero, Quaternion.identity).transform;
            Box("Frame", bed, new Vector3(0.1f, 0.42f, 1.2f), new Vector3(2.1f, 0.5f, 0.95f), m.metal, true);
            Box("Mattress", bed, new Vector3(0.1f, 0.73f, 1.2f), new Vector3(2.05f, 0.14f, 0.92f), m.sheet, true);
            Box("Blanket", bed, new Vector3(0.2f, 0.86f, 1.08f), new Vector3(1.35f, 0.14f, 0.62f), m.sheet, true);
            Box("Pillow", bed, new Vector3(0.95f, 0.86f, 1.08f), new Vector3(0.28f, 0.1f, 0.45f), m.sheet, true);
            var head = Prim(PrimitiveType.Sphere, "Head", bed, new Vector3(0.96f, 1.0f, 1.05f), new Vector3(0.19f, 0.2f, 0.23f), m.skin, Quaternion.identity);
            head.name = "PatientHead";
        }

        // =====================================================================================
        // Arm and veins
        // =====================================================================================

        struct VP { public float x, th, depthMm; public VP(float x, float th, float d) { this.x = x; this.th = th; depthMm = d; } }

        static ArmModel BuildArm(Mats m)
        {
            var armGO = new GameObject("PatientArm");
            armGO.transform.position = new Vector3(0f, 0.872f, 0.40f);
            armGO.transform.rotation = Quaternion.Euler(0f, 90f, 0f);   // local +X (toward fingers) -> world -Z (toward the learner)
            var arm = armGO.AddComponent<ArmModel>();

            var skinMesh = SaveMesh(arm.BuildSkinMesh(), "IV_ArmSkin");
            var skin = MeshObj("Skin", armGO.transform, skinMesh, m.skin, Vector3.zero, Quaternion.identity);
            skin.layer = skinLayer >= 0 ? skinLayer : 0;
            var mc = skin.AddComponent<MeshCollider>();
            mc.sharedMesh = skinMesh;
            arm.skinFilter = skin.GetComponent<MeshFilter>();
            arm.skinRenderer = skin.GetComponent<MeshRenderer>();
            arm.skinCollider = mc;

            var sleeveMesh = SaveMesh(arm.BuildSleeveMesh(-0.34f, 0.075f, 0.007f), "IV_GownSleeve");
            MeshObj("GownSleeve", armGO.transform, sleeveMesh, m.sleeve, Vector3.zero, Quaternion.identity);

            var veins = Empty("Vessels", armGO.transform, Vector3.zero, Quaternion.identity).transform;

            AddVessel(arm, veins, m, "Cephalic vein", VesselType.Vein, 0.0017f, SiteRating.Ideal,
                "Runs along the thumb (radial) side of the forearm. Straight forearm segments are ideal.",
                new[] { new VP(0.47f, 64, 3.0f), new VP(0.43f, 58, 3.2f), new VP(0.38f, 52, 3.3f), new VP(0.33f, 48, 3.4f), new VP(0.28f, 46, 3.6f),
                        new VP(0.235f, 46, 3.8f), new VP(0.19f, 50, 4.0f), new VP(0.14f, 55, 4.3f), new VP(0.08f, 60, 4.8f), new VP(0.01f, 64, 5.2f) });

            AddVessel(arm, veins, m, "Basilic vein", VesselType.Vein, 0.0018f, SiteRating.Acceptable,
                "Runs along the little-finger (ulnar) side. Good size but tends to roll - anchor the skin.",
                new[] { new VP(0.47f, -72, 3.0f), new VP(0.42f, -68, 3.2f), new VP(0.37f, -64, 3.3f), new VP(0.31f, -58, 3.5f), new VP(0.26f, -52, 3.7f),
                        new VP(0.21f, -48, 3.9f), new VP(0.17f, -52, 4.4f), new VP(0.12f, -56, 5.6f), new VP(0.06f, -58, 8f), new VP(0.01f, -60, 10f) });

            AddVessel(arm, veins, m, "Median cubital vein", VesselType.Vein, 0.0021f, SiteRating.Acceptable,
                "Large vein at the front of the elbow. Easy to access but over a joint - mainly for blood sampling or emergencies.",
                new[] { new VP(0.236f, 46, 3.8f), new VP(0.223f, 26, 3.5f), new VP(0.206f, 2, 3.4f), new VP(0.19f, -24, 3.6f), new VP(0.177f, -48, 4.2f) });

            AddVessel(arm, veins, m, "Median antebrachial vein", VesselType.Vein, 0.0012f, SiteRating.Acceptable,
                "Smaller vein in the middle of the forearm - suits a smaller gauge cannula.",
                new[] { new VP(0.44f, 10, 2.8f), new VP(0.39f, 6, 2.9f), new VP(0.34f, 2, 3.0f), new VP(0.29f, -2, 3.1f), new VP(0.245f, -4, 3.3f), new VP(0.208f, 0, 3.4f) });

            AddVessel(arm, veins, m, "Radial artery", VesselType.Artery, 0.0013f, SiteRating.NotSuitable,
                "Artery - pulsating. Never cannulate for IV access.",
                new[] { new VP(0.465f, 38, 4.5f), new VP(0.41f, 34, 6.5f), new VP(0.35f, 28, 9f), new VP(0.29f, 20, 11.5f), new VP(0.23f, 8, 13.5f) });

            AddVessel(arm, veins, m, "Brachial artery", VesselType.Artery, 0.0019f, SiteRating.NotSuitable,
                "Artery deep to the antecubital fossa - a steep or deep insertion here can hit it.",
                new[] { new VP(0.25f, -12, 9.5f), new VP(0.21f, -16, 8.2f), new VP(0.17f, -24, 8.8f), new VP(0.11f, -34, 10.5f), new VP(0.04f, -42, 12f) });

            var zones = Empty("SiteZones", armGO.transform, Vector3.zero, Quaternion.identity).transform;
            AddZone(arm, zones, m, "Forearm cephalic", SiteRating.Ideal, 0.335f, 49f, 0.035f, "Straight, palpable forearm vein away from joints - an ideal site.");
            AddZone(arm, zones, m, "Forearm basilic", SiteRating.Ideal, 0.33f, -60f, 0.028f, "Good forearm site - anchor the skin because this vein rolls.");
            AddZone(arm, zones, m, "Mid forearm", SiteRating.Acceptable, 0.335f, 2f, 0.024f, "Smaller vein - acceptable, consider a smaller gauge.");
            AddZone(arm, zones, m, "Antecubital fossa", SiteRating.Acceptable, 0.205f, 0f, 0.03f,
                "Over the elbow joint: bending kinks the cannula and the brachial artery/median nerve are close. Use other sites first.");
            AddZone(arm, zones, m, "Radial wrist", SiteRating.Avoid, 0.445f, 60f, 0.022f, "Avoid: superficial radial nerve injury risk and wrist movement.");
            AddZone(arm, zones, m, "Palmar wrist", SiteRating.Avoid, 0.445f, 15f, 0.02f, "Avoid: radial artery, nerves and tendons lie close to the surface.");

            return arm;
        }

        static void AddVessel(ArmModel arm, Transform parent, Mats m, string name, VesselType type, float radius, SiteRating rating, string note, VP[] pts)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var vp = go.AddComponent<VeinPath>();
            vp.displayName = name;
            vp.vesselType = type;
            vp.radius = radius;
            vp.defaultRating = rating;
            vp.siteNote = note;
            vp.palpable = true;
            if (type == VesselType.Artery) { vp.engorgedScale = 1f; vp.startRadiusScale = 1f; }
            for (int i = 0; i < pts.Length; i++)
            {
                var p = new GameObject("P" + i);
                p.transform.SetParent(go.transform, false);
                p.transform.localPosition = arm.SurfacePointLocal(pts[i].x, pts[i].th, pts[i].depthMm / 1000f);
            }
            vp.meshFilter = go.AddComponent<MeshFilter>();
            vp.meshRenderer = go.AddComponent<MeshRenderer>();
            vp.meshRenderer.sharedMaterial = type == VesselType.Artery ? m.artery : m.vein;
            vp.meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            vp.RebuildSamples();
            var mesh = vp.RebuildMesh(false);
            vp.meshFilter.sharedMesh = SaveMesh(mesh, "IV_" + name.Replace(" ", ""));
        }

        static void AddZone(ArmModel arm, Transform parent, Mats m, string name, SiteRating rating, float x, float th, float radius, string note)
        {
            var go = new GameObject("Zone - " + name);
            go.transform.SetParent(parent, false);
            go.transform.position = arm.SurfacePoint(x, th, 0f);
            var z = go.AddComponent<SiteZone>();
            z.zoneName = name;
            z.rating = rating;
            z.radius = radius;
            z.note = note;
            if (rating == SiteRating.Ideal || rating == SiteRating.Avoid || rating == SiteRating.Acceptable)
            {
                Vector3 n = arm.SurfaceNormal(x, th);
                var disc = SaveMesh(MeshBuilder.Disc("Disc"), "IV_Disc");
                var marker = new GameObject("Marker");
                marker.transform.SetParent(go.transform, false);
                marker.transform.position = go.transform.position + n * 0.0012f;
                marker.transform.rotation = Quaternion.FromToRotation(Vector3.up, n);
                marker.transform.localScale = new Vector3(radius * 1.1f, 1f, radius * 1.1f);
                marker.AddComponent<MeshFilter>().sharedMesh = disc;
                var r = marker.AddComponent<MeshRenderer>();
                r.sharedMaterial = rating == SiteRating.Ideal ? m.markerIdeal : rating == SiteRating.Acceptable ? m.markerOk : m.markerAvoid;
                r.shadowCastingMode = ShadowCastingMode.Off;
                z.marker = marker;
            }
        }

        // =====================================================================================
        // Cannula (20G, pink) - root origin = needle tip, +Z = direction of insertion, +Y = bevel up
        // =====================================================================================

        static Cannula BuildCannula(Mats m, Vector3 tipPos, Quaternion rot)
        {
            const float tipGap = 0.0015f, cathLen = 0.032f, needleLen = 0.035f;
            float hubFront = -(tipGap + cathLen);      // -0.0335
            float hubBack = hubFront - 0.011f;           // -0.0445
            float chamberBack = hubBack - 0.0135f;       // -0.058

            var root = new GameObject("Cannula_20G");
            root.transform.SetPositionAndRotation(tipPos, rot);
            var c = root.AddComponent<Cannula>();
            c.displayName = "IV cannula (20G)";
            c.hoverHint = "grab to pick up";
            c.needleLength = needleLen;
            c.catheterLength = cathLen;
            c.tipGap = tipGap;

            // ---- needle
            var needle = Empty("NeedleAssembly", root.transform, Vector3.zero, Quaternion.identity).transform;
            Cyl("NeedleShaft", needle, new Vector3(0, 0, -needleLen), new Vector3(0, 0, -0.0015f), 0.0009f, m.steel);
            var cone = SaveMesh(MeshBuilder.Cone("NeedleTip", 0.00045f, 0.0015f, 12), "IV_NeedleTip");
            MeshObj("BevelTip", needle, cone, m.steel, new Vector3(0, 0, -0.0015f), Quaternion.identity);
            Box("BevelMark", needle, new Vector3(0f, 0.00042f, -0.0009f), new Vector3(0.0005f, 0.0001f, 0.0011f), m.bevel);
            Cyl("FlashChamber", needle, new Vector3(0, 0, hubBack - 0.0005f), new Vector3(0, 0, chamberBack), 0.0068f, m.chamber);
            Cyl("NeedleGrip", needle, new Vector3(0, 0, chamberBack), new Vector3(0, 0, chamberBack - 0.011f), 0.0074f, m.wingWhite);
            Box("ThumbTab", needle, new Vector3(0f, 0.0042f, chamberBack - 0.004f), new Vector3(0.0022f, 0.004f, 0.012f), m.wingWhite);
            var flash = Empty("FlashFill", needle, new Vector3(0, 0, hubBack - 0.001f), Quaternion.identity).transform;
            var flashMesh = Cyl("Blood", flash, new Vector3(0, 0, 0f), new Vector3(0, 0, -0.012f), 0.0046f, m.blood);
            flash.localScale = new Vector3(1f, 1f, 0.01f);
            flash.gameObject.SetActive(false);

            // ---- catheter
            var cath = Empty("CatheterAssembly", root.transform, Vector3.zero, Quaternion.identity).transform;
            var tubeGO = new GameObject("CatheterTube");
            tubeGO.transform.SetParent(cath, false);
            var tubeMF = tubeGO.AddComponent<MeshFilter>();
            var tubeMR = tubeGO.AddComponent<MeshRenderer>();
            tubeMR.sharedMaterial = m.catheter;
            var straight = new List<Vector3> { new Vector3(0, 0, hubFront), new Vector3(0, 0, hubFront + cathLen * 0.5f), new Vector3(0, 0, -tipGap) };
            var radii = new List<float> { 0.00055f, 0.00055f, 0.0004f };
            tubeMF.sharedMesh = SaveMesh(MeshBuilder.TubeMesh("CatheterStraight", straight, radii, 10, false, false), "IV_CatheterStraight");

            Cyl("HubNose", cath, new Vector3(0, 0, hubFront + 0.0035f), new Vector3(0, 0, hubFront - 0.001f), 0.0028f, m.hubPink);
            Cyl("Hub", cath, new Vector3(0, 0, hubFront - 0.001f), new Vector3(0, 0, hubBack), 0.0058f, m.hubPink);
            Box("WingL", cath, new Vector3(-0.0088f, -0.0006f, hubFront - 0.0045f), new Vector3(0.012f, 0.0008f, 0.0085f), m.hubPink);
            Box("WingR", cath, new Vector3(0.0088f, -0.0006f, hubFront - 0.0045f), new Vector3(0.012f, 0.0008f, 0.0085f), m.hubPink);
            Cyl("InjectionPort", cath, new Vector3(0, 0.0025f, hubFront - 0.0045f), new Vector3(0, 0.0072f, hubFront - 0.0045f), 0.0038f, m.hubPink);
            Cyl("PortCap", cath, new Vector3(0, 0.0072f, hubFront - 0.0045f), new Vector3(0, 0.0082f, hubFront - 0.0045f), 0.0046f, m.wingWhite);
            var hubBlood = Cyl("HubBlood", cath, new Vector3(0, 0, hubFront - 0.0015f), new Vector3(0, 0, hubBack + 0.001f), 0.0036f, m.blood);
            hubBlood.SetActive(false);
            var port = Empty("HubPort", cath, new Vector3(0, 0, hubBack), Quaternion.identity).transform;

            var removeGO = Empty("PlacedCannulaHandle", cath, new Vector3(0, 0.002f, (hubFront + hubBack) * 0.5f), Quaternion.identity);
            var rc = removeGO.AddComponent<BoxCollider>();
            rc.size = new Vector3(0.03f, 0.016f, 0.03f);
            rc.isTrigger = true;
            var remove = removeGO.AddComponent<ProcedureAction>();
            remove.kind = ActionKind.RemoveCannula;
            remove.displayName = "Placed cannula";
            remove.hoverHint = "use to remove";
            removeGO.SetActive(false);

            // grab collider around everything
            FitBox(root, 0.012f, true);

            c.needleAssembly = needle;
            c.catheterAssembly = cath;
            c.catheterTube = tubeMF;
            c.flashbackFill = flash;
            c.flashbackRenderer = flashMesh.GetComponent<MeshRenderer>();
            c.hubBlood = hubBlood;
            c.hubPort = port;
            c.removeAction = remove;
            c.bloodDark = m.blood;
            c.bloodBright = m.bloodBright;
            return c;
        }

        // =====================================================================================
        // Swab, bottle, syringes, dressing, gauze, gloves, tourniquet, sharps
        // =====================================================================================

        static Swab BuildSwab(Mats m, Vector3 tipPos, Quaternion rot)
        {
            var root = new GameObject("CottonSwab");
            root.transform.SetPositionAndRotation(tipPos, rot);
            var cotton = Prim(PrimitiveType.Sphere, "Cotton", root.transform, new Vector3(0, 0, -0.003f), new Vector3(0.009f, 0.009f, 0.016f), m.cotton, Quaternion.identity);
            Cyl("Stick", root.transform, new Vector3(0, 0, -0.008f), new Vector3(0, 0, -0.15f), 0.0028f, m.wood);
            FitBox(root, 0.014f, true);
            var s = root.AddComponent<Swab>();
            s.displayName = "Cotton swab";
            s.hoverHint = "grab, dip in alcohol, then scrub the site";
            s.cottonRenderer = cotton.GetComponent<MeshRenderer>();
            s.dryMaterial = m.cotton;
            s.wetMaterial = m.cottonWet;
            s.usedMaterial = m.cottonUsed;
            s.functionalPoint = root.transform;
            return s;
        }

        static void BuildAlcoholBottle(Mats m, Vector3 basePos)
        {
            var root = new GameObject("AlcoholBottle");
            root.transform.position = basePos;
            var t = root.transform;
            Cyl("Body", t, new Vector3(0, 0, 0), new Vector3(0, 0.11f, 0), 0.05f, m.bottle);
            Cyl("Label", t, new Vector3(0, 0.03f, 0), new Vector3(0, 0.08f, 0), 0.0508f, m.bottleBlue);
            Cyl("Shoulder", t, new Vector3(0, 0.11f, 0), new Vector3(0, 0.118f, 0), 0.036f, m.bottle);
            Cyl("Neck", t, new Vector3(0, 0.118f, 0), new Vector3(0, 0.128f, 0), 0.02f, m.bottle);
            Cyl("FlipCap", t, new Vector3(0, 0.128f, 0), new Vector3(0, 0.14f, 0), 0.026f, m.bottleBlue);
            var mouth = Empty("Mouth", t, new Vector3(0, 0.145f, 0), Quaternion.identity).transform;
            WorldText(t, "70% ALCOHOL\nantiseptic", new Vector3(0, 0.055f, -0.0262f), Quaternion.identity, new Vector2(300, 120), 0.00014f, 34, Color.white);
            FitBox(root, 0.01f, true);
            var a = root.AddComponent<ProcedureAction>();
            a.kind = ActionKind.AlcoholBottle;
            a.displayName = "Alcohol antiseptic";
            var b = root.AddComponent<AlcoholBottle>();
            b.mouth = mouth;
        }

        static void BuildSyringe(Mats m, string name, ActionKind kind, Vector3 nozzlePos)
        {
            bool blood = kind == ActionKind.BloodSyringe;
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(nozzlePos, Quaternion.LookRotation(Vector3.left, Vector3.up));
            var t = root.transform;
            Cyl("Nozzle", t, new Vector3(0, 0, 0), new Vector3(0, 0, -0.008f), 0.0032f, m.barrel);
            Cyl("Barrel", t, new Vector3(0, 0, -0.008f), new Vector3(0, 0, -0.078f), 0.016f, m.barrel);
            Box("Flange", t, new Vector3(0, 0, -0.077f), new Vector3(0.03f, 0.004f, 0.0025f), m.plunger);
            Cyl("ColourBand", t, new Vector3(0, 0, -0.012f), new Vector3(0, 0, -0.016f), 0.0165f, blood ? m.sharpsLid : m.bottleBlue);

            var fill = Empty("Fill", t, new Vector3(0, 0, -0.009f), Quaternion.identity).transform;
            Cyl("Fluid", fill, new Vector3(0, 0, 0), new Vector3(0, 0, -0.035f), 0.0145f, blood ? m.blood : m.saline);
            if (blood) fill.localScale = new Vector3(1f, 1f, 0.001f);

            float plungerZ = blood ? -0.010f : -0.045f;
            var plunger = Empty("Plunger", t, new Vector3(0, 0, plungerZ), Quaternion.identity).transform;
            Cyl("Stopper", plunger, new Vector3(0, 0, 0.0f), new Vector3(0, 0, -0.004f), 0.0148f, m.rubber);
            Box("Rod", plunger, new Vector3(0, 0, -0.035f), new Vector3(0.004f, 0.004f, 0.062f), m.plunger);
            Cyl("ThumbPress", plunger, new Vector3(0, 0, -0.066f), new Vector3(0, 0, -0.068f), 0.018f, m.plunger);

            FitBox(root, 0.012f, true);
            var a = root.AddComponent<ProcedureAction>();
            a.kind = kind;
            a.displayName = blood ? "Syringe (blood sample)" : "Saline flush 0.9%";
            a.movable = t;
            a.fill = fill;
            a.plunger = plunger;
            a.plungerTravel = 0.035f;
            a.holdSeconds = 2.5f;
        }

        static void BuildDressing(Mats m, Vector3 pos)
        {
            var root = new GameObject("DressingPack");
            root.transform.position = pos;
            var pack = Box("Pack", root.transform, new Vector3(0, 0.003f, 0), new Vector3(0.06f, 0.005f, 0.07f), m.dressingPack, true);
            pack.GetComponent<Collider>().isTrigger = true;
            Box("Stripe", pack.transform, new Vector3(0, 0.55f, 0.3f), new Vector3(1f, 0.1f, 0.15f), m.bottleBlue);
            var film = new GameObject("DressingOnArm");
            film.transform.SetParent(root.transform, false);
            Box("Film", film.transform, Vector3.zero, new Vector3(0.045f, 0.0008f, 0.06f), m.film);
            Box("Border", film.transform, new Vector3(0, 0.0001f, -0.028f), new Vector3(0.045f, 0.0009f, 0.005f), m.dressingPack);
            film.SetActive(false);
            var a = root.AddComponent<ProcedureAction>();
            a.kind = ActionKind.Dressing;
            a.displayName = "Transparent dressing";
            a.trayVisual = pack;
            a.appliedVisual = film;
        }

        static void BuildGauze(Mats m, Vector3 pos)
        {
            var root = new GameObject("Gauze");
            root.transform.position = pos;
            Box("Pad", root.transform, new Vector3(0, 0.003f, 0), new Vector3(0.05f, 0.006f, 0.05f), m.gauze);
            FitBox(root, 0.012f, true);
            var a = root.AddComponent<ProcedureAction>();
            a.kind = ActionKind.Gauze;
            a.displayName = "Sterile gauze";
            a.movable = root.transform;
        }

        static void BuildGloves(Mats m, Vector3 pos)
        {
            var root = new GameObject("HandHygiene_Gloves");
            root.transform.position = pos;
            Box("GloveBox", root.transform, new Vector3(0, 0.035f, 0), new Vector3(0.24f, 0.07f, 0.12f), m.gloveBox);
            Box("Opening", root.transform, new Vector3(0, 0.0705f, 0), new Vector3(0.12f, 0.002f, 0.04f), m.slot);
            Prim(PrimitiveType.Sphere, "GloveTuft", root.transform, new Vector3(0, 0.074f, 0), new Vector3(0.05f, 0.015f, 0.025f), m.handGlove, Quaternion.identity);
            Cyl("HandRub", root.transform, new Vector3(0.16f, 0f, 0f), new Vector3(0.16f, 0.13f, 0f), 0.045f, m.bottle);
            Cyl("Pump", root.transform, new Vector3(0.16f, 0.13f, 0f), new Vector3(0.16f, 0.15f, 0f), 0.012f, m.slot);
            WorldText(root.transform, "Hand rub + gloves", new Vector3(0.05f, 0.04f, -0.061f), Quaternion.identity, new Vector2(400, 60), 0.00025f, 36, Color.white);
            FitBox(root, 0.01f, true);
            var a = root.AddComponent<ProcedureAction>();
            a.kind = ActionKind.HandHygiene;
            a.displayName = "Hand hygiene & gloves";
        }

        static void BuildTourniquet(Mats m, ArmModel arm, Vector3 trayPos)
        {
            var root = new GameObject("Tourniquet");
            var strip = Box("TrayStrip", root.transform, trayPos, new Vector3(0.026f, 0.003f, 0.42f), m.tourniquet, true);
            strip.GetComponent<Collider>().isTrigger = true;
            Box("Clip", strip.transform, new Vector3(0, 1.2f, 0.45f), new Vector3(1.4f, 2.5f, 0.06f), m.buckle);

            var band = new GameObject("ArmBand");
            band.transform.SetParent(root.transform, false);
            band.transform.SetPositionAndRotation(arm.transform.position, arm.transform.rotation);
            float tx = 0.12f;
            var bandMesh = SaveMesh(arm.BuildTourniquetBandMesh(tx, 0.025f, 0.003f), "IV_TourniquetBand");
            band.AddComponent<MeshFilter>().sharedMesh = bandMesh;
            band.AddComponent<MeshRenderer>().sharedMaterial = m.tourniquet;
            var bc = band.AddComponent<BoxCollider>();
            bc.center = bandMesh.bounds.center;
            bc.size = bandMesh.bounds.size + Vector3.one * 0.01f;
            bc.isTrigger = true;
            Vector3 top = arm.SurfacePointLocal(tx, 0f, 0f);
            Box("Buckle", band.transform, top + new Vector3(0f, 0.004f, 0f), new Vector3(0.028f, 0.006f, 0.02f), m.buckle);
            band.SetActive(false);

            var a = root.AddComponent<ProcedureAction>();
            a.kind = ActionKind.Tourniquet;
            a.displayName = "Tourniquet";
            a.trayVisual = strip;
            a.appliedVisual = band;
        }

        static void BuildSharpsBin(Mats m, Vector3 basePos)
        {
            var root = new GameObject("SharpsContainer");
            root.transform.position = basePos;
            var t = root.transform;
            Box("Body", t, new Vector3(0, 0.08f, 0), new Vector3(0.13f, 0.16f, 0.11f), m.sharps);
            Box("Lid", t, new Vector3(0, 0.17f, 0), new Vector3(0.135f, 0.02f, 0.115f), m.sharpsLid);
            Box("Slot", t, new Vector3(0, 0.1805f, -0.01f), new Vector3(0.07f, 0.002f, 0.025f), m.slot);
            WorldText(t, "SHARPS", new Vector3(0, 0.09f, -0.0555f), Quaternion.identity, new Vector2(300, 80), 0.0003f, 48, new Color(0.15f, 0.1f, 0.05f));
            var opening = Empty("Opening", t, new Vector3(0, 0.19f, 0), Quaternion.identity).transform;
            FitBox(root, 0.02f, true);
            var a = root.AddComponent<ProcedureAction>();
            a.kind = ActionKind.SharpsBin;
            a.displayName = "Sharps container";
            var s = root.AddComponent<SharpsBin>();
            s.opening = opening;
        }

        // =====================================================================================
        // Angle reference board
        // =====================================================================================

        static GameObject BuildAngleBoard(Mats m, Vector3 pos)
        {
            var root = new GameObject("AngleReferenceBoard");
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, -18f, 0f));
            var t = root.transform;
            Box("Stand", t, new Vector3(0, 0.004f, 0.02f), new Vector3(0.12f, 0.008f, 0.05f), m.buttonBase);
            var face = Empty("Face", t, new Vector3(0, 0.095f, 0f), Quaternion.Euler(12f, 0f, 0f)).transform;
            Box("Board", face, new Vector3(0, 0, 0.003f), new Vector3(0.26f, 0.16f, 0.006f), m.board);
            WorldText(face, "INSERTION ANGLE  (bevel up)", new Vector3(0, 0.064f, -0.001f), Quaternion.identity, new Vector2(900, 70), 0.00025f, 44, new Color(0.1f, 0.15f, 0.25f));

            var cols = new[] { -0.085f, 0f, 0.085f };
            var angles = new[] { 6f, 22f, 50f };
            var labels = new[] { "<6° too shallow\nglides above vein", "15-30° IDEAL\nenters the lumen", ">35° too steep\npierces back wall" };
            var colors = new[] { new Color(0.75f, 0.5f, 0f), new Color(0.05f, 0.55f, 0.2f), new Color(0.8f, 0.1f, 0.1f) };
            for (int i = 0; i < 3; i++)
            {
                float cx = cols[i];
                const float skinY = -0.004f;
                Vector3 veinC = new Vector3(cx + 0.012f, -0.016f, -0.0008f);
                Box("Skin", face, new Vector3(cx, skinY, -0.0008f), new Vector3(0.072f, 0.0025f, 0.001f), m.skinFlat);
                Prim(PrimitiveType.Cylinder, "Vein", face, veinC, new Vector3(0.012f, 0.0005f, 0.012f), m.veinFlat, Quaternion.Euler(90f, 0f, 0f));
                float a = angles[i] * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(a), -Mathf.Sin(a), 0f);           // direction into the skin
                Vector3 tipPt = i == 0 ? new Vector3(cx + 0.004f, -0.0062f, 0f)      // stays above the vein
                              : i == 1 ? new Vector3(veinC.x, veinC.y, 0f)            // in the lumen
                                       : new Vector3(veinC.x, veinC.y - 0.0075f, 0f); // through the back wall
                var needle = Box("Needle", face, tipPt - d * 0.022f + new Vector3(0, 0, -0.0013f), new Vector3(0.044f, 0.0018f, 0.001f), m.steel);
                needle.transform.localRotation = Quaternion.Euler(0f, 0f, -angles[i]);
                WorldText(face, labels[i], new Vector3(cx, -0.047f, -0.001f), Quaternion.identity, new Vector2(330, 110), 0.00024f, 34, colors[i]);
            }
            return root;
        }

        // =====================================================================================
        // Menu buttons
        // =====================================================================================

        static void BuildButtons(Mats m)
        {
            var root = new GameObject("ControlButtons");
            root.transform.position = new Vector3(-0.38f, 0.85f, -0.21f);
            Box("Base", root.transform, new Vector3(0, 0.005f, 0), new Vector3(0.3f, 0.01f, 0.17f), m.buttonBase, true);
            AddButton(root.transform, m, "EASY", ButtonAction.StartEasy, new Vector3(-0.095f, 0.01f, 0.04f), m.btnGreen, true, false, false);
            AddButton(root.transform, m, "MEDIUM", ButtonAction.StartMedium, new Vector3(0f, 0.01f, 0.04f), m.btnYellow, true, false, false);
            AddButton(root.transform, m, "HARD", ButtonAction.StartHard, new Vector3(0.095f, 0.01f, 0.04f), m.btnRed, true, false, false);
            AddButton(root.transform, m, "RESTART", ButtonAction.Restart, new Vector3(-0.095f, 0.01f, -0.04f), m.btnBlue, false, true, true);
            AddButton(root.transform, m, "MENU", ButtonAction.Menu, new Vector3(0f, 0.01f, -0.04f), m.btnGray, false, true, true);
            AddButton(root.transform, m, "HINTS", ButtonAction.ToggleHints, new Vector3(0.095f, 0.01f, -0.04f), m.btnGray, false, true, false);
        }

        static void AddButton(Transform parent, Mats m, string label, ButtonAction action, Vector3 localPos, Material capMat, bool menu, bool running, bool report)
        {
            var go = Empty("Button_" + label, parent, localPos, Quaternion.identity);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.015f, 0);
            col.size = new Vector3(0.085f, 0.04f, 0.065f);
            col.isTrigger = true;
            var visual = Empty("Visual", go.transform, Vector3.zero, Quaternion.identity);
            Box("Plinth", visual.transform, new Vector3(0, 0.004f, 0), new Vector3(0.085f, 0.008f, 0.065f), m.buttonBase);
            var cap = Box("Cap", visual.transform, new Vector3(0, 0.013f, 0), new Vector3(0.072f, 0.012f, 0.05f), capMat);
            WorldText(cap.transform.parent, label, new Vector3(0, 0.0195f, 0), Quaternion.Euler(90f, 0f, 0f), new Vector2(300, 90), 0.00022f, 52, Color.white);
            var b = go.AddComponent<PushButton>();
            b.displayName = label;
            b.action = action;
            b.visibleInMenu = menu;
            b.visibleInRunning = running;
            b.visibleInReport = report;
            b.visual = visual;
            b.pressCollider = col;
            b.cap = cap.transform;
        }

        // =====================================================================================
        // Rigs
        // =====================================================================================

        static GameObject BuildDesktopRig(Transform focus)
        {
            var rig = new GameObject("DesktopRig");
            var camGO = new GameObject("DesktopCamera");
            camGO.transform.SetParent(rig.transform, false);
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;
            cam.fieldOfView = 55f;
            camGO.AddComponent<AudioListener>();
            var hand = camGO.AddComponent<DesktopHand>();
            hand.cam = cam;
            var dr = rig.AddComponent<DesktopRig>();
            dr.cam = cam;
            dr.focus = focus;
            camGO.transform.position = focus.position + Quaternion.Euler(40f, 0f, 0f) * Vector3.back * 0.7f;
            camGO.transform.rotation = Quaternion.Euler(40f, 0f, 0f);
            return rig;
        }

        static GameObject BuildXRRig(Mats m)
        {
            var rig = new GameObject("XRRig");
            rig.transform.position = new Vector3(0f, 0f, -0.55f);
            var ctrl = rig.AddComponent<XRRigController>();

            var head = new GameObject("Head");
            head.transform.SetParent(rig.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            head.tag = "MainCamera";
            var cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 50f;
            head.AddComponent<AudioListener>();
            ctrl.head = cam;

            var stand = new GameObject("StandPoint");
            stand.transform.position = new Vector3(0f, 0f, -0.5f);
            stand.transform.rotation = Quaternion.identity;
            ctrl.standPoint = stand.transform;

            BuildVRHand(rig.transform, ctrl, m, "LeftHand", UnityEngine.XR.XRNode.LeftHand, -1f);
            BuildVRHand(rig.transform, ctrl, m, "RightHand", UnityEngine.XR.XRNode.RightHand, 1f);
            rig.SetActive(false);
            return rig;
        }

        static void BuildVRHand(Transform rig, XRRigController ctrl, Mats m, string name, UnityEngine.XR.XRNode node, float side)
        {
            var go = new GameObject(name);
            go.transform.SetParent(rig, false);
            go.transform.localPosition = new Vector3(0.2f * side, 1.1f, 0.3f);
            var visual = Empty("Visual", go.transform, Vector3.zero, Quaternion.identity).transform;
            var renderers = new List<MeshRenderer>();
            renderers.Add(Box("Palm", visual, new Vector3(0f, -0.01f, 0.0f), new Vector3(0.075f, 0.026f, 0.085f), m.handBare).GetComponent<MeshRenderer>());
            renderers.Add(Prim(PrimitiveType.Capsule, "Index", visual, new Vector3(0.02f * side, -0.012f, 0.06f), new Vector3(0.017f, 0.025f, 0.017f), m.handBare, Quaternion.Euler(90f, 0f, 0f)).GetComponent<MeshRenderer>());
            renderers.Add(Prim(PrimitiveType.Capsule, "Thumb", visual, new Vector3(0.035f * side, 0.0f, 0.03f), new Vector3(0.018f, 0.02f, 0.018f), m.handBare, Quaternion.Euler(70f, 30f * side, 0f)).GetComponent<MeshRenderer>());
            renderers.Add(Box("Fingers", visual, new Vector3(-0.012f * side, -0.016f, 0.05f), new Vector3(0.05f, 0.022f, 0.04f), m.handBare).GetComponent<MeshRenderer>());
            var tip = Empty("InteractionPoint", go.transform, new Vector3(0.02f * side, -0.012f, 0.085f), Quaternion.identity).transform;
            var h = go.AddComponent<VRHand>();
            h.node = node;
            h.interactionPoint = tip;
            h.handRenderers = renderers.ToArray();
            h.bareMaterial = m.handBare;
            h.gloveMaterial = m.handGlove;
            h.rig = ctrl;
        }

        // =====================================================================================
        // Side view + angle gauge
        // =====================================================================================

        static SideViewCamera BuildSideView(Mats m)
        {
            var root = new GameObject("SideView");
            var camGO = new GameObject("InsetCamera");
            camGO.transform.SetParent(root.transform, false);
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.enabled = false;
            cam.depth = -5;
            var lineGO = new GameObject("SkinProfile");
            lineGO.transform.SetParent(root.transform, false);
            if (insetLayer >= 0) lineGO.layer = insetLayer;
            var lr = lineGO.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.0007f;
            lr.sharedMaterial = m.lineUnlit;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            var lineMat = Mat("SideViewSkinLine", new Color(0.55f, 0.32f, 0.22f, 1f), 0f, 0f, false, unlit);
            lr.sharedMaterial = lineMat;
            var sv = root.AddComponent<SideViewCamera>();
            sv.insetCam = cam;
            sv.skinLine = lr;
            return sv;
        }

        static AngleGauge BuildAngleGauge(Mats m)
        {
            var root = new GameObject("AngleGauge");
            var g = root.AddComponent<AngleGauge>();
            g.lineMaterial = m.lineUnlit;
            g.skinLine = Line(root.transform, "SkinRef", 0.0006f, m.lineUnlit);
            g.needleLine = Line(root.transform, "NeedleRef", 0.0007f, m.lineUnlit);
            g.arcLine = Line(root.transform, "Arc", 0.0005f, m.lineUnlit);
            g.idealMinLine = Line(root.transform, "IdealMin", 0.0004f, m.lineUnlit);
            g.idealMaxLine = Line(root.transform, "IdealMax", 0.0004f, m.lineUnlit);
            return g;
        }

        static LineRenderer Line(Transform parent, string name, float width, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = width;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.positionCount = 0;
            lr.numCapVertices = 2;
            return lr;
        }
    }
}
