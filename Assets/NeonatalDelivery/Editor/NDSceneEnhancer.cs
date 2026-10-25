using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NeonatalDelivery.EditorTools
{
    public static class NDSceneEnhancer
    {
        const string NurseAsset = "Assets/NeonatalDelivery/Art/Models/ND_Nurse.fbx";

        [MenuItem("Neonatal Delivery/6. Add Nurse + Extra Sheet + Face Light", priority = 6)]
        public static void ApplyToOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[NeonatalDelivery] Stop Play mode first.");
                return;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogError("[NeonatalDelivery] Open the delivery scene first.");
                return;
            }

            var station = GameObject.Find("ND_Station");
            var mother = GameObject.Find("ND_Mother") ?? Object.FindObjectsByType<MotherController>(FindObjectsSortMode.None).FirstOrDefault()?.gameObject;
            if (station == null || mother == null)
            {
                Debug.LogError("[NeonatalDelivery] Could not find ND_Station or ND_Mother in the open scene.");
                return;
            }

            AddOrUpdateNurse(station.transform, mother.transform);
            AddOrUpdateFaceLight(station.transform, mother.transform);
            AddOrUpdateExtraSheet(mother.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[NeonatalDelivery] Nurse, extra green sheet and face light added to the open scene.");
        }

        public static void ApplyIfPossible()
        {
            var station = GameObject.Find("ND_Station");
            var mother = GameObject.Find("ND_Mother") ?? Object.FindObjectsByType<MotherController>(FindObjectsSortMode.None).FirstOrDefault()?.gameObject;
            if (station == null || mother == null) return;
            AddOrUpdateNurse(station.transform, mother.transform);
            AddOrUpdateFaceLight(station.transform, mother.transform);
            AddOrUpdateExtraSheet(mother.transform);
        }

        static void AddOrUpdateNurse(Transform station, Transform mother)
        {
            var existing = station.Find("ND_Nurse");
            GameObject nurse;
            if (existing != null) nurse = existing.gameObject;
            else
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NurseAsset);
                if (prefab != null) nurse = (GameObject)PrefabUtility.InstantiatePrefab(prefab, station.gameObject.scene);
                else nurse = new GameObject("ND_Nurse");
                nurse.name = "ND_Nurse";
                nurse.transform.SetParent(station, true);
            }

            var motherPos = mother.position;
            var motherRight = mother.right; motherRight.y = 0f; if (motherRight.sqrMagnitude < 1e-5f) motherRight = Vector3.right; motherRight.Normalize();
            var motherForward = mother.forward; motherForward.y = 0f; if (motherForward.sqrMagnitude < 1e-5f) motherForward = Vector3.forward; motherForward.Normalize();
            var pos = motherPos - motherForward * 0.20f - motherRight * 0.78f;
            pos.y = station.position.y;
            nurse.transform.position = pos;
            nurse.transform.rotation = Quaternion.LookRotation((motherPos - pos).normalized, Vector3.up);

            var bounds = CombinedBounds(nurse);
            if (bounds.size.y > 0.01f)
            {
                // normalize height roughly to an adult standing nurse
                float targetHeight = 1.62f;
                float scaleMul = targetHeight / bounds.size.y;
                nurse.transform.localScale *= scaleMul;
                bounds = CombinedBounds(nurse);
                float yOffset = station.position.y - bounds.min.y;
                nurse.transform.position += Vector3.up * yOffset;
            }

            foreach (var a in nurse.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        }

        static void AddOrUpdateFaceLight(Transform station, Transform mother)
        {
            var head = RigUtil.FindDeep(mother, "Head") ?? RigUtil.FindDeep(mother, "Neck") ?? mother;
            var existing = station.Find("ND_MotherFaceLight");
            Light light;
            if (existing != null) light = existing.GetComponent<Light>();
            else
            {
                var go = new GameObject("ND_MotherFaceLight");
                go.transform.SetParent(station, false);
                light = go.AddComponent<Light>();
            }
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.95f, 0.90f);
            light.spotAngle = 52f;
            light.range = 3.0f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.Soft;
            light.renderMode = LightRenderMode.Auto;
            light.transform.position = head.position + new Vector3(0f, 0.48f, 0.72f);
            light.transform.rotation = Quaternion.LookRotation(head.position - light.transform.position, Vector3.up);
        }

        static void AddOrUpdateExtraSheet(Transform mother)
        {
            var root = mother.Find("ND_ExtraSheet")?.gameObject;
            if (root == null)
            {
                root = new GameObject("ND_ExtraSheet");
                root.transform.SetParent(mother, false);
            }

            var chest = RigUtil.FindDeep(mother, "Chest") ?? RigUtil.FindDeep(mother, "UpperChest") ?? mother;
            var spine = RigUtil.FindDeep(mother, "Spine") ?? chest;
            var leftShoulder = RigUtil.FindDeep(mother, "LeftUpperArm") ?? chest;
            var rightShoulder = RigUtil.FindDeep(mother, "RightUpperArm") ?? chest;

            root.transform.position = chest.position + (mother.forward * 0.03f) + Vector3.up * 0.05f;
            root.transform.rotation = Quaternion.LookRotation(mother.forward, Vector3.up);
            root.transform.localScale = Vector3.one;

            var mf = root.GetComponent<MeshFilter>(); if (mf == null) mf = root.AddComponent<MeshFilter>();
            var mr = root.GetComponent<MeshRenderer>(); if (mr == null) mr = root.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildSheetMesh(chest, spine, leftShoulder, rightShoulder, mother);

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/NeonatalDelivery/Art/Materials/ND_Gown.mat")
                      ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/NeonatalDelivery/Art/Materials/ND_Towel.mat");
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;
        }

        static Mesh BuildSheetMesh(Transform chest, Transform spine, Transform leftShoulder, Transform rightShoulder, Transform mother)
        {
            float width = Mathf.Clamp(Vector3.Distance(leftShoulder.position, rightShoulder.position) * 1.35f, 0.46f, 0.72f);
            float length = Mathf.Clamp(Vector3.Distance(chest.position, spine.position) * 2.4f, 0.42f, 0.72f);

            const int xCount = 9;
            const int yCount = 7;
            var verts = new Vector3[xCount * yCount];
            var uvs = new Vector2[verts.Length];
            var tris = new int[(xCount - 1) * (yCount - 1) * 6];

            for (int y = 0; y < yCount; y++)
            {
                float vy = y / (float)(yCount - 1);
                for (int x = 0; x < xCount; x++)
                {
                    float vx = x / (float)(xCount - 1);
                    float px = (vx - 0.5f) * width;
                    float pz = -(vy * length);
                    float edgeLift = Mathf.Abs(vx - 0.5f) * 0.02f;
                    float centerFold = Mathf.Cos(vx * Mathf.PI * 2f) * 0.008f;
                    float frontLift = Mathf.SmoothStep(0.015f, 0.0f, vy);
                    float py = 0.015f + edgeLift + centerFold + frontLift;
                    verts[y * xCount + x] = new Vector3(px, py, pz);
                    uvs[y * xCount + x] = new Vector2(vx, vy);
                }
            }

            int ti = 0;
            for (int y = 0; y < yCount - 1; y++)
            for (int x = 0; x < xCount - 1; x++)
            {
                int i = y * xCount + x;
                tris[ti++] = i;
                tris[ti++] = i + xCount;
                tris[ti++] = i + 1;
                tris[ti++] = i + 1;
                tris[ti++] = i + xCount;
                tris[ti++] = i + xCount + 1;
            }

            var mesh = new Mesh();
            mesh.name = "ND_ExtraSheetMesh";
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Bounds CombinedBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1)) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
