// ND New Baby installer (Unity 6): bind an artist-supplied, UNRIGGED OBJ to the existing ND_Baby FBX skeleton.
// IMPORTANT: automatic nearest-surface weight transfer is an APPROXIMATION, not a professionally weight-painted rig.
// We keep all of the original bone transforms, procedural delivery path, clinical markers and clips intact.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NeonatalDelivery.EditorTools
{
    public static class NDNewBabyInstaller
    {
        const string Root = "Assets/NeonatalDelivery/Art";
        const string NewSource = Root + "/Models/ND_NewBaby_Source.obj";
        const string Generated = Root + "/Generated";
        const string SkinnedPath = Generated + "/ND_NewBaby_Skinned.asset";
        const string MaterialPath = Generated + "/ND_NewBabySkin.mat";
        const string TexRoot = Root + "/Textures/NewBaby/";

        [MenuItem("Neonatal Delivery/4. Apply New Baby To Open Scene", priority = 4)]
        public static void ApplyToOpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("New Baby", "Exit Play Mode before installing a skinned mesh.", "OK");
                return;
            }
            int changed = 0;
            foreach (var bc in UnityEngine.Object.FindObjectsByType<BabyController>(FindObjectsSortMode.None))
                if (bc.gameObject.scene.IsValid() && Install(bc.gameObject)) changed++;
            if (changed == 0) Debug.LogWarning("[ND] No compatible BabyController found in the open scene. Open ND_NeonatalDelivery.unity first.");
            else
            {
                foreach (var bc in UnityEngine.Object.FindObjectsByType<BabyController>(FindObjectsSortMode.None))
                    EditorSceneManager.MarkSceneDirty(bc.gameObject.scene);
                EditorSceneManager.SaveOpenScenes();
                Debug.Log("[ND] Updated " + changed + " baby instance(s). Please test animation and inspect limb deformation in Play Mode.");
            }
        }

        // Invoked by NDSceneBuilder.Build, and by the explicit scene-patch menu for existing scenes.
        public static bool Install(GameObject baby)
        {
            EnsureReadable(Root + "/Models/ND_Baby.fbx");
            EnsureReadable(NewSource);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(NewSource);
            if (source == null) { Debug.LogError("[ND] Missing " + NewSource); return false; }
            var sourceMf = source.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(x => x.sharedMesh != null);
            if (sourceMf == null || sourceMf.sharedMesh == null) return false;
            var sourceMesh = sourceMf.sharedMesh;
            var allSkinned = baby.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var old = allSkinned.FirstOrDefault(r => r.sharedMesh != null && r.bones.Length > 0 &&
                (r.name == "B_Body" || r.name.EndsWith("Body"))) ??
                allSkinned.FirstOrDefault(r => r.sharedMesh != null && r.bones.Length > 0 && !r.name.Contains("ND_NewBaby_Visual"));
            if (old == null) { Debug.LogError("[ND] The original ND_Baby skeleton/skinned body is missing. Keep ND_Baby.fbx!" ); return false; }
            Mesh original = old.sharedMesh;
            if (!sourceMesh.isReadable || !original.isReadable)
            {
                Debug.LogError("[ND] NewBaby OBJ and ND_Baby FBX need Read/Write Enabled. Check NDAssetPostprocessor and reimport both.");
                return false;
            }
            if (original.boneWeights.Length != original.vertexCount || original.bindposes.Length == 0)
            {
                Debug.LogError("[ND] Original source has no usable bone weights/bind poses.");
                return false;
            }
            // Place new geometry in EXACTLY the mesh-local space of the original SkinnedMeshRenderer.
            // This ensures original bind poses and authored animation clips remain valid.
            var srcBounds = sourceMesh.bounds;
            var dstBounds = original.bounds;
            // Preserve proportional silhouette while fitting height; broaden slightly to approximate the old shoulder spacing.
            var srcExtent = srcBounds.size;
            var dstExtent = dstBounds.size;
            var scale = new Vector3(
                dstExtent.x / Mathf.Max(srcExtent.x, 1e-4f),
                dstExtent.y / Mathf.Max(srcExtent.y, 1e-4f),
                dstExtent.z / Mathf.Max(srcExtent.z, 1e-4f));
            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] mapped = new Vector3[sourceVertices.Length];
            for (int i = 0; i < mapped.Length; i++)
                mapped[i] = dstBounds.center + Vector3.Scale(sourceVertices[i] - srcBounds.center, scale);
            var originalV = original.vertices;
            var originalW = original.boneWeights;
            // Grid index: O(N) expected, avoiding a multimillion-comparison mesh-to-mesh scan.
            float cell = Mathf.Max(0.008f, dstBounds.size.magnitude / 70f);
            var cells = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < originalV.Length; i++)
            {
                var key = Cell(originalV[i], cell);
                if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<int>(8);
                list.Add(i);
            }
            var weights = new BoneWeight[mapped.Length];
            for (int i = 0; i < mapped.Length; i++)
                weights[i] = BlendedWeight(mapped[i], originalV, originalW, cells, cell);
            var result = UnityEngine.Object.Instantiate(sourceMesh);
            result.name = "ND_NewBaby_Skinned";
            result.vertices = mapped;
            result.boneWeights = weights;
            result.bindposes = original.bindposes;
            // OBJ normals are rederived to match the nonuniform alignment axes.
            result.RecalculateNormals();
            if (result.uv != null && result.uv.Length == result.vertexCount) result.RecalculateTangents();
            result.RecalculateBounds();
            NDSetup.EnsureFolder(Generated);
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(SkinnedPath);
            if (saved == null) AssetDatabase.CreateAsset(result, SkinnedPath);
            else { EditorUtility.CopySerialized(result, saved); UnityEngine.Object.DestroyImmediate(result); result = saved; EditorUtility.SetDirty(saved); }
            Material skin = MakeMaterial();
            var existing = old.transform.Find("ND_NewBaby_Visual");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var child = new GameObject("ND_NewBaby_Visual");
            child.transform.SetParent(old.transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            var r = child.AddComponent<SkinnedMeshRenderer>();
            r.sharedMesh = result;
            r.sharedMaterial = skin;
            r.bones = old.bones;
            r.rootBone = old.rootBone;
            r.localBounds = result.bounds;
            r.updateWhenOffscreen = true;
            r.quality = SkinQuality.Bone4;
            // Never destroy rig meshes/transforms. They carry the bones, markers and original clips.
            foreach (var rend in baby.GetComponentsInChildren<Renderer>(true))
                if (rend != r) rend.enabled = false;
            r.enabled = true;
            var controller = baby.GetComponent<BabyController>();
            if (controller != null) controller.skinRenderers = new Renderer[] { r };
            EditorUtility.SetDirty(baby);
            AssetDatabase.SaveAssets();
            return true;
        }

        static void EnsureReadable(string path)
        {
            var mi = AssetImporter.GetAtPath(path) as ModelImporter;
            if (mi != null && !mi.isReadable)
            {
                mi.isReadable = true;
                mi.SaveAndReimport();
            }
        }

        static Vector3Int Cell(Vector3 p, float d) => new Vector3Int(
            Mathf.FloorToInt(p.x / d), Mathf.FloorToInt(p.y / d), Mathf.FloorToInt(p.z / d));
        static int Closest(Vector3 p, Vector3[] v, Dictionary<Vector3Int, List<int>> grid, float cell)
        {
            var key = Cell(p, cell);
            float closest = float.PositiveInfinity;
            int best = 0;
            for (int r = 0; r <= 9; r++)
            {
                for (int x = -r; x <= r; x++) for (int y = -r; y <= r; y++) for (int z = -r; z <= r; z++)
                {
                    // Only inspect newly reached shell cells.
                    if (r > 0 && Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z)) != r) continue;
                    if (!grid.TryGetValue(key + new Vector3Int(x,y,z), out var candidates)) continue;
                    foreach (int i in candidates)
                    {
                        float d = (p - v[i]).sqrMagnitude;
                        if (d < closest) { closest = d; best = i; }
                    }
                }
                if (closest < (r * cell) * (r * cell)) return best;
            }
            // Rare outliers far from original silhouette.
            for (int i = 0; i < v.Length; i++)
            {
                var d = (p - v[i]).sqrMagnitude;
                if (d < closest) { closest = d; best = i; }
            }
            return best;
        }

        static BoneWeight BlendedWeight(Vector3 p, Vector3[] v, BoneWeight[] bw, Dictionary<Vector3Int, List<int>> grid, float cell)
        {
            var candidates = Nearby(p, v, grid, cell, 8);
            if (candidates.Count == 0) return bw[Closest(p, v, grid, cell)];

            var accum = new Dictionary<int, float>(16);
            float norm = 0f;
            foreach (int i in candidates)
            {
                float d = Vector3.Distance(p, v[i]);
                float k = 1f / Mathf.Max(1e-4f, d * d);
                norm += k;
                Add(accum, bw[i].boneIndex0, bw[i].weight0 * k);
                Add(accum, bw[i].boneIndex1, bw[i].weight1 * k);
                Add(accum, bw[i].boneIndex2, bw[i].weight2 * k);
                Add(accum, bw[i].boneIndex3, bw[i].weight3 * k);
            }
            if (norm <= 1e-6f) return bw[candidates[0]];
            var top = accum.OrderByDescending(kv => kv.Value).Take(4).ToArray();
            float sum = top.Sum(kv => kv.Value);
            if (sum <= 1e-6f) return bw[candidates[0]];
            var result = new BoneWeight();
            if (top.Length > 0) { result.boneIndex0 = top[0].Key; result.weight0 = top[0].Value / sum; }
            if (top.Length > 1) { result.boneIndex1 = top[1].Key; result.weight1 = top[1].Value / sum; }
            if (top.Length > 2) { result.boneIndex2 = top[2].Key; result.weight2 = top[2].Value / sum; }
            if (top.Length > 3) { result.boneIndex3 = top[3].Key; result.weight3 = top[3].Value / sum; }
            return result;
        }

        static void Add(Dictionary<int, float> d, int key, float value)
        {
            if (key < 0 || value <= 0f) return;
            d[key] = d.TryGetValue(key, out var old) ? old + value : value;
        }

        static List<int> Nearby(Vector3 p, Vector3[] v, Dictionary<Vector3Int, List<int>> grid, float cell, int target)
        {
            var key = Cell(p, cell);
            var found = new List<int>(target * 2);
            for (int r = 0; r <= 9 && found.Count < target; r++)
            {
                for (int x = -r; x <= r; x++) for (int y = -r; y <= r; y++) for (int z = -r; z <= r; z++)
                {
                    if (r > 0 && Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z)) != r) continue;
                    if (!grid.TryGetValue(key + new Vector3Int(x, y, z), out var candidates)) continue;
                    found.AddRange(candidates);
                }
            }
            if (found.Count > target)
                found = found.OrderBy(i => (p - v[i]).sqrMagnitude).Take(target).ToList();
            return found;
        }

        static Material MakeMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("NeonatalDelivery/Skin") ?? Shader.Find("Universal Render Pipeline/Lit"));
                mat.name = "ND_NewBabySkin";
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexRoot + "ND_NewBaby_color.png"));
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexRoot + "ND_NewBaby_normal.png"));
            mat.SetFloat("_BumpScale", 0.65f);
            mat.SetFloat("_Smoothness", 0.58f);
            mat.SetFloat("_UseMetalRough", 0f);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetColor("_SSSColor", new Color(0.88f,0.42f,0.36f,1f));
            mat.SetFloat("_StencilRef", 0f);
            mat.SetTexture("_OcclusionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexRoot + "ND_NewBaby_occlusion.png"));
            mat.SetFloat("_OcclusionStrength", 0.55f);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
