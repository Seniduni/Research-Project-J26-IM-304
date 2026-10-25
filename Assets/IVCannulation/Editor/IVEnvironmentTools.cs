using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IVSim.EditorTools
{
    /// <summary>
    /// Tools for placing the IV training station inside another environment (e.g. a teammate's operating theatre).
    /// Menu: IV Cannulation > Environment > 1..6 (use them in order).
    /// </summary>
    public static class IVEnvironmentTools
    {
        const string StationName = "IVStation";
        const string RoomName = "DefaultRoom (hidden)";
        const string TheatrePrefix = "TheatreRoom";
        static readonly string[] KeepFromRoom = { "ProcedureTable", "PatientBed", "ExamLight" };
        static readonly HashSet<string> PlayerTypes = new HashSet<string>
        {
            "XROrigin", "XRInteractionManager", "EventSystem", "CharacterController",
            "TeleportationProvider", "LocomotionMediator", "XRDeviceSimulator", "InputSystemUIInputModule", "StandaloneInputModule"
        };

        // =====================================================================================
        // 1. Prepare
        // =====================================================================================

        [MenuItem("IV Cannulation/Environment/1. Prepare Scene for Custom Environment", false, 10)]
        public static void Prepare()
        {
            if (!TrainingSceneOpen()) return;
            var existing = GameObject.Find(StationName);
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                EditorUtility.DisplayDialog("Already prepared", "This scene already has '" + StationName + "' (now selected).\n\nNext: IV Cannulation > Environment > 2. Bring In Environment From Scene.", "OK");
                return;
            }
            var station = CreateStation(true);
            Selection.activeGameObject = station;
            EditorUtility.DisplayDialog("Step 1 done",
                "Everything for the IV training is now inside 'IVStation'.\nThe plain floor, walls and sun are in '" + RoomName + "' (disabled).\n\n" +
                "Next: IV Cannulation > Environment > 2. Bring In Environment From Scene.", "OK");
        }

        static GameObject CreateStation(bool undo)
        {
            var scene = SceneManager.GetActiveScene();
            var station = new GameObject(StationName);
            if (undo) Undo.RegisterCreatedObjectUndo(station, "Prepare IV station");
            // Pivot = floor level, under the centre of the procedure table.
            station.transform.position = new Vector3(0f, 0f, 0.15f);

            GameObject room = null;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == station) continue;
                if (go.name == "Environment" || go.name == RoomName) { room = go; continue; }
                if (go.name.StartsWith(TheatrePrefix)) continue;
                if (undo) Undo.SetTransformParent(go.transform, station.transform, "Prepare IV station");
                else go.transform.SetParent(station.transform, true);
            }
            if (room != null)
            {
                foreach (var keep in KeepFromRoom)
                {
                    var t = room.transform.Find(keep);
                    if (t == null) continue;
                    if (undo) Undo.SetTransformParent(t, station.transform, "Prepare IV station");
                    else t.SetParent(station.transform, true);
                }
                if (undo) Undo.RecordObject(room, "Prepare IV station");
                room.name = RoomName;
                room.SetActive(false);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            return station;
        }

        // =====================================================================================
        // 2. Bring in another scene's environment
        // =====================================================================================

        [MenuItem("IV Cannulation/Environment/2. Bring In Environment From Scene...", false, 11)]
        public static void BringIn()
        {
            if (!TrainingSceneOpen()) return;
            var active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path))
            {
                EditorUtility.DisplayDialog("Save first", "Save this scene first (File > Save As...).", "OK");
                return;
            }

            // Which scene? Use the scene selected in the Project window, or ask.
            string rel = null;
            var selScene = Selection.activeObject as SceneAsset;
            if (selScene != null) rel = AssetDatabase.GetAssetPath(selScene);
            if (string.IsNullOrEmpty(rel))
            {
                string abs = EditorUtility.OpenFilePanel("Choose the theatre scene (inside this project's Assets folder)", Application.dataPath, "unity");
                if (string.IsNullOrEmpty(abs)) return;
                abs = abs.Replace('\\', '/');
                string data = Application.dataPath.Replace('\\', '/');
                if (!abs.StartsWith(data))
                {
                    EditorUtility.DisplayDialog("Wrong location", "The scene must be inside THIS project's Assets folder.\nCopy your teammate's theatre folder into Assets first.", "OK");
                    return;
                }
                rel = "Assets" + abs.Substring(data.Length);
            }
            if (rel == active.path)
            {
                EditorUtility.DisplayDialog("Same scene", "Choose the theatre scene, not the scene that is open now.", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Bring in environment",
                "Copy everything from\n" + rel + "\ninto this scene?\n\nThe teammate's scene FILE is not changed. Cameras, player rigs and HUD canvases in it will be switched off automatically.\n\n" +
                "This step cannot be undone with Ctrl+Z (keep your backup copy of the scene).", "Bring in", "Cancel"))
                return;

            if (GameObject.Find(StationName) == null) CreateStation(false);
            var report = new StringBuilder();
            var holderT = BringInSilently(rel, report);
            if (holderT == null) { EditorUtility.DisplayDialog("Could not open", rel, "OK"); return; }
            var holder = holderT.gameObject;
            int disabled = report.Length > 0 ? report.ToString().Split('\n').Length - 1 : 0;

            Bounds rb;
            string scaleNote = "";
            if (CombinedBounds(holder.transform, out rb))
            {
                if (rb.size.y > 10f || rb.size.y < 1.5f)
                    scaleNote = "\n\nWARNING: the room is about " + rb.size.y.ToString("0.0") + " m tall. It may use a different scale. " +
                                "Scale the TheatreRoom object (never IVStation) until a door is about 2 m tall.";
            }

            EditorSceneManager.MarkSceneDirty(active);
            Selection.activeGameObject = holder;
            EditorUtility.DisplayDialog("Step 2 done",
                "The theatre is now in '" + holder.name + "'.\n\n" +
                (disabled > 0 ? "Switched off " + disabled + " object(s)/component(s) that would conflict:\n" + report : "No cameras/player rigs found.") +
                scaleNote +
                "\n\nNext: in the Scene view CLICK the operating table (the spot where the patient should be), then\nIV Cannulation > Environment > 3. Move Station To Selected Object.", "OK");
        }

        /// <summary>Opens a scene additively, moves all its objects into a TheatreRoom holder in the active scene, closes it unsaved.</summary>
        public static Transform BringInSilently(string scenePath, StringBuilder report)
        {
            var active = SceneManager.GetActiveScene();
            var other = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            if (!other.IsValid()) return null;

            SceneManager.SetActiveScene(other);
            var L = new LightingCopy();
            L.Read();
            SceneManager.SetActiveScene(active);
            L.Apply();

            var holder = new GameObject(TheatrePrefix + " (" + other.name + ")");
            foreach (var root in other.GetRootGameObjects())
            {
                SceneManager.MoveGameObjectToScene(root, active);
                root.transform.SetParent(holder.transform, true);
            }
            EditorSceneManager.CloseScene(other, true);   // closes WITHOUT saving: the theatre scene file is untouched
            DisableConflicts(holder.transform, report);
            EditorSceneManager.MarkSceneDirty(active);
            return holder.transform;
        }

        public static GameObject CreateStationSilently() { return CreateStation(false); }

        public static List<GameObject> ClearSpaceSilently(Transform station, Transform theatre)
        {
            var toHide = new List<GameObject>();
            Bounds sb;
            if (!StationFootprint(station, out sb)) return toHide;
            float floor = station.position.y;
            var area = new Bounds();
            area.SetMinMax(new Vector3(sb.min.x - 0.25f, floor + 0.03f, sb.min.z - 0.25f),
                           new Vector3(sb.max.x + 0.25f, floor + 2.0f, sb.max.z + 0.25f));
            foreach (var r in theatre.GetComponentsInChildren<Renderer>(false))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!r.enabled || !area.Intersects(r.bounds)) continue;
                if (IsStructural(r.bounds)) continue;
                var u = Unit(r.transform, theatre).gameObject;
                if (!toHide.Contains(u)) toHide.Add(u);
            }
            foreach (var g in toHide)
            {
                Undo.RecordObject(g, "Clear space around IV station");
                g.SetActive(false);
            }
            return toHide;
        }

        public static bool Combined(Transform t, out Bounds b) { return CombinedBounds(t, out b); }
        public static Transform UnitOf(Transform t, Transform root) { return Unit(t, root); }
        public static bool Structural(Bounds b) { return IsStructural(b); }

        /// <summary>Station footprint without dialogs.</summary>
        public static bool StationFootprint(Transform station, out Bounds b)
        {
            if (!CombinedBounds(station, out b)) return false;
            Transform stand = null;
            foreach (var t in station.GetComponentsInChildren<Transform>(true))
                if (t.name == "StandPoint") { stand = t; break; }
            if (stand != null)
            {
                b.Encapsulate(stand.position + new Vector3(0.4f, 0f, 0.4f));
                b.Encapsulate(stand.position - new Vector3(0.4f, 0f, 0.4f));
            }
            return true;
        }

        static int DisableConflicts(Transform root, StringBuilder report)
        {
            int n = 0;
            var handled = new HashSet<GameObject>();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;   // missing script
                var go = c.gameObject;
                if (handled.Contains(go)) continue;
                string tn = c.GetType().Name;

                if (c is Camera)
                {
                    var cam = (Camera)c;
                    if (cam.targetTexture != null) continue;   // screens/mirrors are fine
                    n += SwitchOff(go, cam, report, "camera"); handled.Add(go);
                }
                else if (c is AudioListener)
                {
                    var al = (AudioListener)c;
                    if (al.enabled) { Undo.RecordObject(al, "Disable"); al.enabled = false; report.Append("- Audio Listener on ").Append(go.name).Append('\n'); n++; }
                }
                else if (c is Canvas && ((Canvas)c).renderMode != RenderMode.WorldSpace && ((Canvas)c).isRootCanvas)
                {
                    n += SwitchOff(go, null, report, "screen HUD"); handled.Add(go);
                }
                else if (PlayerTypes.Contains(tn))
                {
                    n += SwitchOff(go, c as Behaviour, report, tn); handled.Add(go);
                }
            }
            return n;
        }

        /// <summary>Turns off a whole object, unless that would also hide a big part of the room (then only the component).</summary>
        static int SwitchOff(GameObject go, Behaviour comp, StringBuilder report, string what)
        {
            if (!go.activeSelf) return 0;
            int renderers = go.GetComponentsInChildren<MeshRenderer>(true).Length;
            if (renderers > 25 && comp != null)
            {
                if (!comp.enabled) return 0;
                comp.enabled = false;
                report.Append("- ").Append(what).Append(" component on ").Append(go.name).Append('\n');
                return 1;
            }
            if (renderers > 25) return 0;
            go.SetActive(false);
            report.Append("- ").Append(go.name).Append(" (").Append(what).Append(")\n");
            return 1;
        }

        class LightingCopy
        {
            Material skybox;
            UnityEngine.Rendering.AmbientMode ambientMode;
            Color sky, equator, ground;
            float ambientIntensity, reflectionIntensity;
            bool fog; FogMode fogMode; Color fogColor; float fogDensity, fogStart, fogEnd;

            public void Read()
            {
                skybox = RenderSettings.skybox;
                ambientMode = RenderSettings.ambientMode;
                sky = RenderSettings.ambientSkyColor; equator = RenderSettings.ambientEquatorColor; ground = RenderSettings.ambientGroundColor;
                ambientIntensity = RenderSettings.ambientIntensity;
                reflectionIntensity = RenderSettings.reflectionIntensity;
                fog = RenderSettings.fog; fogMode = RenderSettings.fogMode; fogColor = RenderSettings.fogColor;
                fogDensity = RenderSettings.fogDensity; fogStart = RenderSettings.fogStartDistance; fogEnd = RenderSettings.fogEndDistance;
            }

            public void Apply()
            {
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = equator; RenderSettings.ambientGroundColor = ground;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.reflectionIntensity = reflectionIntensity;
                RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity; RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd;
            }
        }

        // =====================================================================================
        // 3. Move station to the selected object (e.g. the operating table)
        // =====================================================================================

        [MenuItem("IV Cannulation/Environment/3. Move Station To Selected Object", false, 12)]
        public static void MoveToSelected()
        {
            var station = GameObject.Find(StationName);
            if (station == null) { EditorUtility.DisplayDialog("No IVStation", "Run step 1 first.", "OK"); return; }
            var sel = Selection.activeTransform;
            if (sel == null || sel.IsChildOf(station.transform))
            {
                EditorUtility.DisplayDialog("Select a target",
                    "In the Scene view, CLICK the object in the middle of the theatre where the patient should be (e.g. the operating table), then run this again.", "OK");
                return;
            }
            Transform theatre = FindTheatreRoot();
            Transform unit = Unit(sel, theatre);
            Bounds ub;
            if (!CombinedBounds(unit, out ub)) { EditorUtility.DisplayDialog("No visible mesh", "That object has no visible mesh. Click a visible object.", "OK"); return; }
            Bounds sb;
            if (!StationBounds(station.transform, out sb)) return;

            Vector3 offset = sb.center - station.transform.position;
            offset.y = 0f;
            Undo.RecordObject(station.transform, "Move IV station");
            station.transform.position = new Vector3(ub.center.x - offset.x, ub.min.y, ub.center.z - offset.z);
            EditorSceneManager.MarkSceneDirty(station.scene);
            Selection.activeGameObject = station;
            SceneView.FrameLastActiveSceneView();
            EditorUtility.DisplayDialog("Step 3 done",
                "IVStation moved onto '" + unit.name + "' (floor height " + ub.min.y.ToString("0.00") + " m).\n\n" +
                "Look at it in the Scene view:\n- If the bed points the wrong way, run 4. Rotate Station 90° (repeat as needed).\n" +
                "- To nudge it, use the Move tool (W) on IVStation.\n\nThen run 5. Clear Space Around Station.", "OK");
        }

        // =====================================================================================
        // 4. Rotate station 90 degrees around its own centre
        // =====================================================================================

        [MenuItem("IV Cannulation/Environment/4. Rotate Station 90°", false, 13)]
        public static void Rotate90()
        {
            var station = GameObject.Find(StationName);
            if (station == null) { EditorUtility.DisplayDialog("No IVStation", "Run step 1 first.", "OK"); return; }
            Bounds sb;
            if (!StationBounds(station.transform, out sb)) return;
            Undo.RecordObject(station.transform, "Rotate IV station");
            Vector3 pivot = new Vector3(sb.center.x, station.transform.position.y, sb.center.z);
            station.transform.RotateAround(pivot, Vector3.up, 90f);
            Vector3 e = station.transform.eulerAngles;
            station.transform.eulerAngles = new Vector3(0f, Mathf.Round(e.y), 0f);
            EditorSceneManager.MarkSceneDirty(station.scene);
            Selection.activeGameObject = station;
        }

        // =====================================================================================
        // 5. Hide theatre objects that overlap the station
        // =====================================================================================

        [MenuItem("IV Cannulation/Environment/5. Clear Space Around Station", false, 14)]
        public static void ClearSpace()
        {
            var station = GameObject.Find(StationName);
            Transform theatre = FindTheatreRoot();
            if (station == null || theatre == null)
            {
                EditorUtility.DisplayDialog("Not ready", "Run steps 1 and 2 first.", "OK");
                return;
            }
            var toHide = ClearSpaceSilently(station.transform, theatre);
            if (toHide.Count == 0)
            {
                EditorUtility.DisplayDialog("Nothing to clear", "No theatre objects overlap the station.", "OK");
                return;
            }
            var names = new StringBuilder();
            foreach (var g in toHide) names.Append("- ").Append(g.name).Append('\n');
            EditorSceneManager.MarkSceneDirty(station.scene);
            EditorUtility.DisplayDialog("Step 5 done",
                "Hidden " + toHide.Count + " object(s) that were in the way (switched off, not deleted - tick them again in the Hierarchy if you want one back, or press Ctrl+Z):\n\n" +
                names + "\nNext: 6. Check Scene for Conflicts, then Ctrl+S.", "OK");
        }

        // =====================================================================================
        // 6. Check
        // =====================================================================================

        [MenuItem("IV Cannulation/Environment/6. Check Scene for Conflicts", false, 15)]
        public static void CheckConflicts()
        {
            var station = GameObject.Find(StationName);
            var report = new StringBuilder();
            int problems = 0;

            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (station != null && cam.transform.IsChildOf(station.transform)) continue;
                if (cam.targetTexture != null) continue;
                report.Append("- Extra camera: ").Append(PathOf(cam.transform)).Append("  (untick it)\n");
                problems++;
            }
            foreach (var al in Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!al.enabled || (station != null && al.transform.IsChildOf(station.transform))) continue;
                report.Append("- Extra Audio Listener: ").Append(PathOf(al.transform)).Append("  (untick the component)\n");
                problems++;
            }
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (mb == null || !mb.enabled) continue;
                string tn = mb.GetType().Name;
                if (!PlayerTypes.Contains(tn)) continue;
                if (station != null && mb.transform.IsChildOf(station.transform)) continue;
                report.Append("- ").Append(tn).Append(" on ").Append(PathOf(mb.transform)).Append("  (another player rig - untick it)\n");
                problems++;
            }
            var arm = Object.FindAnyObjectByType<ArmModel>();
            if (arm != null)
            {
                Physics.SyncTransforms();
                Vector3 c = arm.SurfacePoint(0.3f, 0f);
                foreach (var col in Physics.OverlapSphere(c, 0.25f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (station != null && col.transform.IsChildOf(station.transform)) continue;
                    report.Append("- Collider near the arm: ").Append(PathOf(col.transform)).Append("  (blocks clicks/grabs - untick it)\n");
                    problems++;
                }
            }
            if (station != null && station.transform.lossyScale != Vector3.one)
            {
                report.Append("- IVStation is scaled. Set its Scale back to 1,1,1 (scale the theatre instead).\n");
                problems++;
            }
            EditorUtility.DisplayDialog("Scene check", problems == 0 ? "No conflicts found. Save with Ctrl+S and press Play." : problems + " possible problem(s):\n\n" + report, "OK");
        }

        // =====================================================================================
        // helpers
        // =====================================================================================

        static bool TrainingSceneOpen()
        {
            if (Object.FindAnyObjectByType<IVSession>() != null) return true;
            EditorUtility.DisplayDialog("IV training scene not open",
                "Open your IV training scene first (Project window > Assets/IVCannulation/Scenes).", "OK");
            return false;
        }

        static Transform FindTheatreRoot()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go.name.StartsWith(TheatrePrefix)) return go.transform;
            return null;
        }

        /// <summary>Walls, floors and ceilings: very wide or very tall.</summary>
        static bool IsStructural(Bounds b)
        {
            return b.size.x > 2.6f || b.size.z > 2.6f || b.size.y > 2.6f;
        }

        /// <summary>The whole piece of furniture a mesh belongs to (climbs up while the group is still furniture-sized).</summary>
        static Transform Unit(Transform t, Transform root)
        {
            Transform best = t;
            Transform cur = t;
            while (cur.parent != null && cur.parent != root)
            {
                Bounds pb;
                if (!CombinedBounds(cur.parent, out pb) || IsStructural(pb)) break;
                best = cur.parent;
                cur = cur.parent;
            }
            return best;
        }

        static bool CombinedBounds(Transform t, out Bounds b)
        {
            b = new Bounds();
            bool any = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(false))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>Footprint of the station: bed, table, tools, plus room for the learner to stand.</summary>
        static bool StationBounds(Transform station, out Bounds b)
        {
            if (!CombinedBounds(station, out b))
            {
                EditorUtility.DisplayDialog("Empty station", "IVStation has no visible objects.", "OK");
                return false;
            }
            var stand = station.Find("StandPoint");
            if (stand == null)
            {
                foreach (var t in station.GetComponentsInChildren<Transform>(true))
                    if (t.name == "StandPoint") { stand = t; break; }
            }
            if (stand != null)
            {
                b.Encapsulate(stand.position + new Vector3(0.4f, 0f, 0.4f));
                b.Encapsulate(stand.position - new Vector3(0.4f, 0f, 0.4f));
            }
            return true;
        }

        static string PathOf(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }
    }
}
