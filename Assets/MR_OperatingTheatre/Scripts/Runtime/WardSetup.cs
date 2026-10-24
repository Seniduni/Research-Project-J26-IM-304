// MR Operating Theatre - shared ward runtime (H.M.H. Lakshan, IT23404564)
// Attach WardSetup to the imported ward model (MR_OperatingTheatre_Ward.fbx). At runtime it:
//   * tags every piece of equipment (EquipmentTag + collider) from the equipment catalogue
//   * creates the real-time lights at the positions modelled in Blender (LIGHT_* empties)
//   * makes walls/floor solid, sets up a reflection probe for stainless steel
//   * wires the hands-free sliding door and its foot sensors
//   * brings the monitors to life (live clock, sweeping vital-sign trace)
// Other team members can drop the "MR_OperatingTheatre_Ward" prefab into their own scenes.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MROT
{
    public class WardSetup : MonoBehaviour
    {
        public static WardSetup Instance { get; private set; }
        public bool showPatientMannequin = false;
        public bool createLights = true;
        public bool liveScreens = true;
        [HideInInspector] public bool ambientSound = false;   // background hum removed (distracting for learners)
        [Range(0.2f, 3f)] public float lightIntensityScale = 1f;
        public readonly Dictionary<string, EquipmentTag> Tags = new Dictionary<string, EquipmentTag>();
        public HandsFreeDoor MainDoor { get; private set; }
        bool _done;

        void Awake() { Instance = this; Setup(); }

        public void Setup()
        {
            if (_done) return; _done = true;
            var all = GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                var n = t.name;
                if (n.StartsWith("LIGHT_") && createLights) MakeLight(t);
            }
            foreach (var t in all)
            {
                if (t == transform) continue;
                var n = t.name;
                if (n == "PatientMannequin") t.gameObject.SetActive(showPatientMannequin);
                if (n.StartsWith("Instr_") && !n.Contains("_On")) { if (t.parent == transform || t.parent.parent == transform) t.gameObject.SetActive(false); continue; }
                if (n == "PreviewCamera") { t.gameObject.SetActive(false); continue; }
                if (EquipmentCatalog.Get(n) != null || n.EndsWith("_OnMayo") || n.EndsWith("_OnTrolley"))
                {
                    string id = n.Replace("_OnMayo", "").Replace("_OnTrolley", "");
                    if (n == "OT_RoomShell") { SetupRoom(t); continue; }
                    if (Tags.ContainsKey(n)) continue;
                    var tag = t.gameObject.AddComponent<EquipmentTag>();
                    tag.id = id;
                    Tags[n] = tag;
                    if (n != "MainTheatreDoor" && n != "LaminarFlowCanopy") AddBoxCollider(t, n.StartsWith("Instr_"));
                }
            }
            var door = transform.FindDeep("MainTheatreDoor");
            if (door != null) MainDoor = door.gameObject.AddComponent<HandsFreeDoor>();
            if (liveScreens)
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                    if (r.name.Contains("_Screen_")) r.gameObject.AddComponent<LiveScreen>();
            SetupEnvironment();
            // no background ambience: the looping hum/beeps distracted from Nila's voice
        }

        void SetupRoom(Transform room)
        {
            foreach (var mf in room.GetComponentsInChildren<MeshFilter>(true))
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mf.gameObject.AddComponent<WalkableFloor>();
            }
        }

        static void AddBoxCollider(Transform t, bool small)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var bc = t.gameObject.AddComponent<BoxCollider>();
            // bounds are world-space; convert to local (assumes no scale on the ward)
            bc.center = t.InverseTransformPoint(b.center);
            var size = t.InverseTransformVector(b.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            if (small) size = Vector3.Max(size, new Vector3(0.03f, 0.03f, 0.03f));
            bc.size = size;
            bc.isTrigger = small;
        }

        void MakeLight(Transform t)
        {
            var n = t.name;
            var l = t.gameObject.AddComponent<Light>();
            if (n.StartsWith("LIGHT_Surgical"))
            {
                l.type = LightType.Spot;
                l.spotAngle = 42f; l.innerSpotAngle = 22f;
                l.range = 4.5f;
                l.intensity = (n.Contains("Main") ? 6f : 4.5f) * lightIntensityScale;
                l.color = new Color(1f, 0.98f, 0.94f);
                l.shadows = LightShadows.Soft; l.shadowStrength = 0.65f;
                var table = transform.FindDeep("OperatingTable");
                var aim = table != null ? table.position + Vector3.up * 0.95f : t.position + Vector3.down;
                t.rotation = Quaternion.LookRotation(aim - t.position);
            }
            else
            {
                l.type = LightType.Point;
                bool corridor = n.Contains("Corridor");
                l.range = corridor ? 4.5f : 5.5f;
                l.intensity = (corridor ? 0.45f : 0.75f) * lightIntensityScale;
                l.color = corridor ? new Color(1f, 0.96f, 0.9f) : new Color(0.95f, 0.97f, 1f);
                l.shadows = LightShadows.None;
                if (n.StartsWith("LIGHT_Canopy")) { l.intensity *= 0.6f; }
            }
        }

        void SetupEnvironment()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.45f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.34f, 0.36f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.22f, 0.23f);
            RenderSettings.fog = false;
            var probeGo = new GameObject("TheatreReflectionProbe");
            probeGo.transform.SetParent(transform, false);
            probeGo.transform.position = transform.TransformPoint(new Vector3(0, 1.5f, 0));
            var p = probeGo.AddComponent<ReflectionProbe>();
            p.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            p.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            p.size = new Vector3(7.4f, 3.2f, 11f);
            p.boxProjection = true;
            p.resolution = 256;
            p.intensity = 1f;
            StartCoroutine(RenderProbe(p));
            SetupPostProcessing();
        }

        void SetupPostProcessing()
        {
            var go = new GameObject("TheatrePostProcessing");
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<UnityEngine.Rendering.Volume>();
            v.isGlobal = true; v.priority = 10;
            var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            var tm = prof.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
            tm.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
            var bloom = prof.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.Override(1.1f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.6f);
            var ca = prof.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            ca.postExposure.Override(0.15f); ca.contrast.Override(8f); ca.saturation.Override(4f);
            var vg = prof.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vg.intensity.Override(0.18f);
            v.profile = prof;
        }
        IEnumerator RenderProbe(ReflectionProbe p) { yield return null; yield return null; p.RenderProbe(); }

        public EquipmentTag Tag(string id)
        {
            EquipmentTag t;
            if (Tags.TryGetValue(id, out t)) return t;
            foreach (var kv in Tags) if (kv.Value.id == id) return kv.Value;
            return null;
        }
        public Vector3 BlenderToWorld(float x, float y, float z) { return WardSpace.World(transform, x, y, z); }
    }
}
