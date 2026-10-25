using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IVSim.EditorTools
{
    /// <summary>
    /// IV Cannulation > ONE CLICK: Put Station Into Theatre
    /// Copies the training scene, brings in the theatre scene found in the project, switches off its cameras/player,
    /// places the station on the operating table, hides the furniture in the middle, saves, and sets it as the start scene.
    /// Nothing existing is overwritten: the original training scene and the theatre scene files stay unchanged.
    /// </summary>
    public static class IVOneClickTheatre
    {
        const string TrainingScene = "Assets/IVCannulation/Scenes/IVCannulationTraining.unity";
        const string OutputScene = "Assets/IVCannulation/Scenes/IVCannulationTheatre.unity";
        static readonly string[] TableWords = { "operat", "surg", "ot_table", "ot table", "otable", "bed", "table", "stretcher" };

        [MenuItem("IV Cannulation/ONE CLICK: Put Station Into Theatre", false, 1)]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Stop Play mode", "Press Stop first, then try again.", "OK"); return; }
            if (!File.Exists(TrainingScene))
            {
                EditorUtility.DisplayDialog("Training scene missing", "Run IV Cannulation > Build Training Scene first.", "OK");
                return;
            }

            // ---- find the theatre scene
            string theatre = FindTheatreScene();
            if (theatre == null) return;
            if (!EditorUtility.DisplayDialog("Put station into theatre",
                "Theatre scene:\n" + theatre + "\n\nA NEW scene will be made:\n" + OutputScene + "\n\nYour training scene and the theatre scene are not changed.", "Go", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // ---- open a fresh copy of the training scene
            string outPath = File.Exists(OutputScene) ? AssetDatabase.GenerateUniqueAssetPath(OutputScene) : OutputScene;
            var scene = EditorSceneManager.OpenScene(TrainingScene, OpenSceneMode.Single);
            if (!EditorSceneManager.SaveScene(scene, outPath, false)) { EditorUtility.DisplayDialog("Could not save", outPath, "OK"); return; }
            scene = EditorSceneManager.OpenScene(outPath, OpenSceneMode.Single);

            // ---- steps 1 + 2
            EditorUtility.DisplayProgressBar("IV station", "Grouping the station...", 0.2f);
            var station = GameObject.Find("IVStation");
            if (station == null) station = IVEnvironmentTools.CreateStationSilently();
            EditorUtility.DisplayProgressBar("IV station", "Bringing in the theatre...", 0.4f);
            var report = new StringBuilder();
            Transform room = IVEnvironmentTools.BringInSilently(theatre, report);
            if (room == null) { EditorUtility.ClearProgressBar(); EditorUtility.DisplayDialog("Could not open the theatre scene", theatre, "OK"); return; }

            // ---- step 3 + 4: place on the operating table
            EditorUtility.DisplayProgressBar("IV station", "Placing the station...", 0.6f);
            string placedOn = PlaceStation(station.transform, room);

            // ---- step 5: clear the middle
            EditorUtility.DisplayProgressBar("IV station", "Clearing space...", 0.8f);
            var hidden = IVEnvironmentTools.ClearSpaceSilently(station.transform, room);

            // ---- save + start scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(s => s.path == outPath);
            foreach (var s in list) if (s.path == TrainingScene) s.enabled = false;
            list.Insert(0, new EditorBuildSettingsScene(outPath, true));
            EditorBuildSettings.scenes = list.ToArray();
            EditorUtility.ClearProgressBar();

            Selection.activeGameObject = station;
            SceneView.FrameLastActiveSceneView();
            EditorUtility.DisplayDialog("Done!",
                "Saved as " + outPath + " (now the start scene).\n\n" +
                "Station placed on: " + placedOn + "\n" +
                "Furniture hidden from the middle: " + hidden.Count + "\n" +
                (report.Length > 0 ? "Switched off from the theatre:\n" + report : "") +
                "\nPress PLAY to test.\n\nIf the bed faces the wrong way: IV Cannulation > Environment > 4. Rotate Station 90°, then Ctrl+S.\n" +
                "If the room looks dark: Window > Rendering > Lighting > Generate Lighting.", "OK");
        }

        static string FindTheatreScene()
        {
            var candidates = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                string lower = p.ToLowerInvariant();
                if (lower.StartsWith("assets/ivcannulation/")) continue;
                if (lower.StartsWith("assets/samples/") || lower.Contains("/xri") || lower.Contains("tutorialinfo")) continue;
                if (lower == "assets/scenes/samplescene.unity" || lower.EndsWith("/basicscene.unity")) continue;
                candidates.Add(p);
            }
            if (candidates.Count == 1) return candidates[0];

            string start = candidates.Count > 0 ? Path.GetDirectoryName(candidates[0]) : "Assets";
            string abs = EditorUtility.OpenFilePanel(candidates.Count == 0
                    ? "No theatre scene found automatically - choose it (it must be inside this project's Assets folder)"
                    : "Several scenes found - choose the THEATRE scene", start, "unity");
            if (string.IsNullOrEmpty(abs)) return null;
            abs = abs.Replace('\\', '/');
            string data = Application.dataPath.Replace('\\', '/');
            if (!abs.StartsWith(data))
            {
                EditorUtility.DisplayDialog("Wrong location", "Copy the theatre folder into this project's Assets folder first.", "OK");
                return null;
            }
            return "Assets" + abs.Substring(data.Length);
        }

        /// <summary>Finds the operating table (or the room centre) and moves/rotates the station onto it.</summary>
        static string PlaceStation(Transform station, Transform room)
        {
            Bounds roomB;
            if (!IVEnvironmentTools.Combined(room, out roomB)) return "(empty theatre)";
            Vector3 centre = roomB.center;

            Transform best = null;
            Bounds bestB = new Bounds();
            float bestScore = float.MaxValue;
            foreach (var r in room.GetComponentsInChildren<Renderer>(false))
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                var unit = IVEnvironmentTools.UnitOf(r.transform, room);
                string n = (unit.name + " " + r.name).ToLowerInvariant();
                bool named = false;
                foreach (var w in TableWords) if (n.Contains(w)) { named = true; break; }
                if (!named) continue;
                Bounds ub;
                if (!IVEnvironmentTools.Combined(unit, out ub) || IVEnvironmentTools.Structural(ub)) continue;
                if (ub.size.x < 0.4f && ub.size.z < 0.4f) continue;   // too small to be a table
                Vector2 d = new Vector2(ub.center.x - centre.x, ub.center.z - centre.z);
                float score = d.magnitude - (n.Contains("operat") || n.Contains("surg") ? 1.5f : 0f);
                if (score < bestScore) { bestScore = score; best = unit; bestB = ub; }
            }

            Vector3 target;
            float floor;
            string label;
            if (best != null)
            {
                target = bestB.center;
                floor = bestB.min.y;
                label = best.name;
                // match the long side of the table with the bed
                bool tableAlongZ = bestB.size.z > bestB.size.x;
                station.rotation = Quaternion.Euler(0f, tableAlongZ ? 90f : 0f, 0f);
            }
            else
            {
                target = centre;
                floor = FloorBelow(centre, roomB.min.y);
                label = "centre of the room (no table found)";
            }

            Bounds sb;
            if (!IVEnvironmentTools.StationFootprint(station, out sb)) return label;
            Vector3 offset = sb.center - station.position;
            offset.y = 0f;
            station.position = new Vector3(target.x - offset.x, floor, target.z - offset.z);
            return label;
        }

        static float FloorBelow(Vector3 p, float fallback)
        {
            Physics.SyncTransforms();
            RaycastHit hit;
            if (Physics.Raycast(new Vector3(p.x, p.y + 1f, p.z), Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return fallback;
        }
    }
}
