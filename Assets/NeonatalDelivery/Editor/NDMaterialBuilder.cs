// Builds URP / NeonatalDelivery materials from Art/ND_MaterialManifest.json and remaps the FBX material slots.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NeonatalDelivery.EditorTools
{
    public static class NDMaterialBuilder
    {
        public const string Art = "Assets/NeonatalDelivery/Art";
        public const string MatDir = Art + "/Materials";

        [Serializable] public class Entry { public string alphaMode; public float alphaCutoff; public bool doubleSided; public float[] baseColorFactor; public float metallic; public float roughness;
            public string baseColor; public string metalRough; public string normal; public string occlusion; public float normalScale = 1; public bool baseHasAlpha; public string procedural; }

        /// <summary>Absolute path for a project-relative path (independent of the Editor's working directory).</summary>
        public static string Abs(string rel) => Path.Combine(Path.GetDirectoryName(Application.dataPath), rel);

        static Shader Lit => Shader.Find("Universal Render Pipeline/Lit");
        static Shader Skin => Shader.Find("NeonatalDelivery/Skin") ?? Lit;

        public static void BuildAll()
        {
            NDSetup.EnsureFolder(MatDir);
            // absolute path: never depends on the Editor's working directory
            var manifestPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), Art, "ND_MaterialManifest.json");
            var text = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null;
            if (text == null) Debug.LogWarning("[NeonatalDelivery] Material manifest not found at " + manifestPath + " - using texture naming convention.");
            var entries = text != null ? ParseManifest(text) : new Dictionary<string, Entry>();
            ConventionFallback(entries);
            int textured = 0;
            foreach (var kv in entries) { Build(kv.Key, kv.Value); if (!string.IsNullOrEmpty(kv.Value.baseColor) && Tex(kv.Value.baseColor) != null) textured++; }
            Debug.Log($"[NeonatalDelivery] Built {entries.Count} model materials ({textured} with a base-colour texture) in {MatDir}");
            if (textured == 0)
            {
                string sample = null; foreach (var kv in entries) if (!string.IsNullOrEmpty(kv.Value.baseColor)) { sample = Art + "/" + kv.Value.baseColor; break; }
                var imp = sample != null ? AssetImporter.GetAtPath(sample) : null;
                Debug.LogError("[NeonatalDelivery] No base-colour textures could be loaded from " + Art + "/Textures. Sample " + sample +
                    ": exists=" + (sample != null && File.Exists(Abs(sample))) + ", importer=" + (imp != null ? imp.GetType().Name : "none") +
                    ", main asset=" + (sample != null ? AssetDatabase.GetMainAssetTypeAtPath(sample)?.Name : "-") + ". Run Neonatal Delivery > Tools > Repair Importers.");
            }
            // module materials
            Ghost("ND_Ghost", new Color(0.30f, 0.85f, 1f, 0.42f));
            Ghost("ND_GhostFault", new Color(1f, 0.25f, 0.2f, 0.5f));
            Ghost("ND_ReplayHand", new Color(1f, 0.78f, 0.3f, 0.45f));
            Simple("ND_Veil", Shader.Find("NeonatalDelivery/Veil"), Color.white);
            Simple("ND_Occluder", Shader.Find("NeonatalDelivery/Occluder"), Color.black);
            var glove = Lit_("ND_GloveNitrile", new Color(0.36f, 0.56f, 0.86f), 0.55f);
            Particle("ND_FX_Blood", new Color(0.45f, 0.02f, 0.03f, 0.9f));
            Particle("ND_FX_Fluid", new Color(0.85f, 0.9f, 0.92f, 0.45f));
            Cord();
            AssetDatabase.SaveAssets();
        }

        // JsonUtility cannot read a dictionary: parse the top-level keys manually and feed each object to JsonUtility.
        static Dictionary<string, Entry> ParseManifest(string json)
        {
            var d = new Dictionary<string, Entry>();
            int i = json.IndexOf('{') + 1;
            while (i > 0 && i < json.Length)
            {
                int q1 = json.IndexOf('"', i); if (q1 < 0) break; int q2 = json.IndexOf('"', q1 + 1);
                string key = json.Substring(q1 + 1, q2 - q1 - 1);
                int b = json.IndexOf('{', q2); int depth = 0, j = b;
                for (; j < json.Length; j++) { if (json[j] == '{') depth++; else if (json[j] == '}') { depth--; if (depth == 0) break; } }
                d[key] = JsonUtility.FromJson<Entry>(json.Substring(b, j - b + 1));
                i = j + 1;
            }
            return d;
        }

        // Any "<Material>_base.(jpg|png)" in Art/Textures/** defines a material even without the manifest.
        static void ConventionFallback(Dictionary<string, Entry> entries)
        {
            foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { Art + "/Textures" }))
            {
                var p = AssetDatabase.GUIDToAssetPath(g); var file = Path.GetFileNameWithoutExtension(p);
                if (!file.EndsWith("_base")) continue;
                var name = file.Substring(0, file.Length - 5); var rel = p.Substring(Art.Length + 1);
                if (!entries.TryGetValue(name, out var e)) { e = new Entry { roughness = 0.6f, baseColorFactor = new float[] { 1, 1, 1, 1 } }; entries[name] = e; }
                if (string.IsNullOrEmpty(e.baseColor)) e.baseColor = rel;
                var dir = Path.GetDirectoryName(rel).Replace('\\', '/');
                foreach (var ext in new[] { ".png", ".jpg" })
                {
                    if (string.IsNullOrEmpty(e.normal) && File.Exists(Abs(Path.Combine(Art, dir, name + "_normal" + ext)))) e.normal = dir + "/" + name + "_normal" + ext;
                    if (string.IsNullOrEmpty(e.metalRough) && File.Exists(Abs(Path.Combine(Art, dir, name + "_metalrough" + ext)))) e.metalRough = dir + "/" + name + "_metalrough" + ext;
                    if (string.IsNullOrEmpty(e.occlusion) && File.Exists(Abs(Path.Combine(Art, dir, name + "_occlusion" + ext)))) e.occlusion = dir + "/" + name + "_occlusion" + ext;
                }
                if (name.Contains("Hair") || name.Contains("Skullcap") || name.Contains("Eyelash")) e.baseHasAlpha = true;
            }
        }

        static Texture2D Tex(string rel) { return string.IsNullOrEmpty(rel) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "/" + rel); }

        static Material GetOrCreate(string name, Shader sh)
        {
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh) { name = name }; AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }

        static void Build(string name, Entry e)
        {
            bool skin = name.StartsWith("Std_Skin") || name == "ND_BabySkin" || e.procedural == "mucosa";
            if (skin)
            {
                var m = GetOrCreate(name, Skin);
                if (e.procedural == "mucosa")
                {
                    m.SetColor("_BaseColor", new Color(0.66f, 0.24f, 0.27f)); m.SetFloat("_Smoothness", 0.86f); m.SetFloat("_Wetness", 0.9f);
                    m.SetColor("_SSSColor", new Color(0.9f, 0.2f, 0.2f)); m.SetFloat("_Cull", 0f); m.SetFloat("_StencilRef", 1f);
                }
                else
                {
                    m.SetTexture("_BaseMap", Tex(e.baseColor)); m.SetTexture("_BumpMap", Tex(e.normal)); m.SetFloat("_BumpScale", e.normalScale > 0 ? e.normalScale : 1f);
                    bool baby = name == "ND_BabySkin";
                    m.SetFloat("_StencilRef", baby ? 0f : 1f);
                    m.SetFloat("_Smoothness", baby ? 0.5f : 0.42f);
                    m.SetColor("_SSSColor", baby ? new Color(0.9f, 0.38f, 0.35f) : new Color(0.85f, 0.35f, 0.28f));
                    if (baby && !string.IsNullOrEmpty(e.metalRough)) { m.SetTexture("_MetalRoughMap", Tex(e.metalRough)); m.SetFloat("_UseMetalRough", 1f); m.SetFloat("_Metallic", 0f); }
                }
                EditorUtility.SetDirty(m); return;
            }
            if (e.procedural == "towel") { Lit_(name, new Color(0.95f, 0.95f, 0.93f), 0.08f); return; }
            var lit = GetOrCreate(name, Lit);
            lit.SetTexture("_BaseMap", Tex(e.baseColor));
            var c = e.baseColorFactor != null && e.baseColorFactor.Length == 4 ? new Color(e.baseColorFactor[0], e.baseColorFactor[1], e.baseColorFactor[2], e.baseColorFactor[3]) : Color.white;
            lit.SetColor("_BaseColor", c);
            var n = Tex(e.normal); if (n != null) { lit.SetTexture("_BumpMap", n); lit.SetFloat("_BumpScale", e.normalScale > 0 ? e.normalScale : 1f); lit.EnableKeyword("_NORMALMAP"); }
            var ao = Tex(e.occlusion); if (ao != null) { lit.SetTexture("_OcclusionMap", ao); lit.SetFloat("_OcclusionStrength", 1f); lit.EnableKeyword("_OCCLUSIONMAP"); }
            lit.SetFloat("_Metallic", 0f);
            float smooth = Mathf.Clamp01(1f - (e.roughness > 0 ? e.roughness : 0.6f));
            if (name.Contains("Eye") && !name.Contains("lash")) smooth = 0.85f;
            if (name.Contains("Teeth")) smooth = 0.6f; if (name.Contains("Tongue")) smooth = 0.7f; if (name.Contains("Nails")) smooth = 0.65f;
            if (name == "ND_DrapeGreen" || name == "ND_Gown") { smooth = name == "ND_Gown" ? Mathf.Min(smooth, 0.25f) : 0.18f; lit.SetFloat("_Cull", 0f); lit.doubleSidedGI = true; }   // single-layer cloth: render both faces
            lit.SetFloat("_Smoothness", smooth);
            bool hair = name.Contains("Hair") || name.Contains("Skullcap") || name.Contains("Eyelash");
            bool cornea = name.Contains("Cornea");
            if (hair)
            {
                lit.SetFloat("_AlphaClip", 1f); lit.SetFloat("_Cutoff", 0.35f); lit.EnableKeyword("_ALPHATEST_ON");
                lit.SetFloat("_Cull", 0f); lit.SetFloat("_Smoothness", 0.35f); lit.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                lit.SetOverrideTag("RenderType", "TransparentCutout");
            }
            if (cornea)
            {
                lit.SetFloat("_Surface", 1f); lit.SetFloat("_Blend", 0f); lit.SetOverrideTag("RenderType", "Transparent");
                lit.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); lit.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                lit.SetFloat("_ZWrite", 0f); lit.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); lit.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                lit.SetColor("_BaseColor", new Color(1, 1, 1, 0.08f)); lit.SetFloat("_Smoothness", 0.96f);
            }
            EditorUtility.SetDirty(lit);
        }

        static Material Lit_(string name, Color c, float smooth)
        {
            var m = GetOrCreate(name, Lit); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0f); EditorUtility.SetDirty(m); return m;
        }
        static void Ghost(string name, Color c) { var m = GetOrCreate(name, Shader.Find("NeonatalDelivery/Ghost") ?? Lit); m.SetColor("_BaseColor", c); EditorUtility.SetDirty(m); }
        static void Simple(string name, Shader sh, Color c) { if (sh == null) return; var m = GetOrCreate(name, sh); EditorUtility.SetDirty(m); }
        static void Particle(string name, Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Lit;
            var m = GetOrCreate(name, sh); m.SetColor("_BaseColor", c);
            m.SetFloat("_Surface", 1f); m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetFloat("_ZWrite", 0f);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; EditorUtility.SetDirty(m);
        }

        static void Cord()
        {
            var texPath = Art + "/Textures/ND_CordProcedural.png";
            if (!File.Exists(Abs(texPath)))
            {
                var t = UmbilicalCord.CordTexture(); File.WriteAllBytes(Abs(texPath), t.EncodeToPNG()); AssetDatabase.ImportAsset(texPath);
            }
            var m = GetOrCreate("ND_Cord", Skin);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetFloat("_Smoothness", 0.78f); m.SetFloat("_Wetness", 0.85f); m.SetColor("_SSSColor", new Color(0.75f, 0.45f, 0.5f)); m.SetFloat("_StencilRef", 0f);
            EditorUtility.SetDirty(m);
        }

        /// <summary>Find the project material for an FBX slot name: ours first, then the shared ward materials, then anywhere.</summary>
        public static Material FindMaterial(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            name = name.Replace(" (Instance)", "");
            var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{name}.mat");
            if (m == null) m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/MR_OperatingTheatre/Materials/{name}.mat");
            if (m == null)
                foreach (var g in AssetDatabase.FindAssets(name + " t:Material"))
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    if (Path.GetFileNameWithoutExtension(p) == name) { m = AssetDatabase.LoadAssetAtPath<Material>(p); break; }
                }
            return m;
        }

        /// <summary>Explicitly remap every FBX material slot to the project material of the same name (deterministic AddRemap).</summary>
        public static void RemapModels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Art + "/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mi = AssetImporter.GetAtPath(path) as ModelImporter; if (mi == null) continue;
                var names = new HashSet<string>();
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Material) names.Add(o.name);
                foreach (var kv in mi.GetExternalObjectMap()) if (kv.Key.type == typeof(Material)) names.Add(kv.Key.name);
                var missing = new List<string>(); int ok = 0;
                foreach (var n in names)
                {
                    var mat = FindMaterial(n);
                    if (mat != null) { mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), mat); ok++; }
                    else missing.Add(n);
                }
                mi.SaveAndReimport();
                Debug.Log($"[NeonatalDelivery] {Path.GetFileName(path)}: {ok}/{names.Count} material slots remapped" + (missing.Count > 0 ? " - no project material for: " + string.Join(", ", missing) : ""));
            }
        }

        /// <summary>Safety net for the scene: assign project materials by slot name on every renderer under root.</summary>
        public static int AssignByName(GameObject root)
        {
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var m = FindMaterial(mats[i].name);
                    if (m != null && m != mats[i]) { mats[i] = m; changed = true; n++; }
                }
                if (changed) r.sharedMaterials = mats;
            }
            return n;
        }

        public static string Describe(Renderer r)
        {
            var sb = new System.Text.StringBuilder(r.name + ": ");
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) { sb.Append("<null> "); continue; }
                var t = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                sb.Append($"{m.name} [{(m.shader != null ? m.shader.name : "no shader")}, map={(t != null ? t.name : "none")}, path={AssetDatabase.GetAssetPath(m)}]  ");
            }
            return sb.ToString();
        }
    }
}
