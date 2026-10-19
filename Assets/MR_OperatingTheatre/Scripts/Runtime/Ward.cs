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
using Text = TMPro.TextMeshProUGUI;

namespace MROT
{
    // ====================================================================== data
    [Serializable] public class EquipmentInfo
    {
        public string id, name, category, summary, nurseRole;
        public string[] points, lo, aliases;
    }
    [Serializable] class EquipmentList { public EquipmentInfo[] items; }

    public static class EquipmentCatalog
    {
        static Dictionary<string, EquipmentInfo> _map;
        public static Dictionary<string, EquipmentInfo> Map { get { if (_map == null) Load(); return _map; } }
        static void Load()
        {
            _map = new Dictionary<string, EquipmentInfo>();
            var ta = Resources.Load<TextAsset>("MROT/Data/equipment_catalog");
            if (ta == null) { Debug.LogWarning("[MROT] equipment_catalog.json not found"); return; }
            var list = JsonUtility.FromJson<EquipmentList>(ta.text);
            foreach (var e in list.items) _map[e.id] = e;
        }
        public static EquipmentInfo Get(string id)
        {
            EquipmentInfo e;
            if (id != null && Map.TryGetValue(id, out e)) return e;
            return null;
        }
    }

    /// <summary>Blender (Z-up, right-handed) -> Unity ward-local coordinates.
    /// FBX export: axis_forward=-Z, axis_up=Y, bake transforms; Unity flips X on import.</summary>
    public static class WardSpace
    {
        public static Vector3 FromBlender(float x, float y, float z) { return new Vector3(-x, z, -y); }
        public static Vector3 World(Transform ward, float bx, float by, float bz)
        {
            return ward != null ? ward.TransformPoint(FromBlender(bx, by, bz)) : FromBlender(bx, by, bz);
        }
        /// <summary>Blender yaw (degrees, CCW around Z, 0 = facing -Y) -> Unity yaw.</summary>
        public static float YawFromBlenderFacing(float bx, float by)
        {
            var u = new Vector3(-bx, 0, -by);
            return Quaternion.LookRotation(u).eulerAngles.y;
        }
    }

    // ====================================================================== equipment tag + highlight
    /// <summary>Pulsing emissive highlight (MaterialPropertyBlock) + floating marker arrow.</summary>
    public class Highlighter : MonoBehaviour
    {
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        Renderer[] _rs;
        MaterialPropertyBlock _mpb;
        bool _hover, _focus;
        float _t;
        GameObject _arrow;
        public Color hoverColor = new Color(0.10f, 0.45f, 0.55f);
        public Color focusColor = new Color(0.10f, 0.75f, 0.70f);

        void Awake() { _rs = GetComponentsInChildren<Renderer>(); _mpb = new MaterialPropertyBlock(); }
        public void Hover(bool on) { _hover = on; Apply(); }
        public void Focus(bool on)
        {
            _focus = on; Apply();
            if (on && _arrow == null) _arrow = MarkerArrow.Create(transform, GetComponent<EquipmentTag>());
            if (_arrow != null) _arrow.SetActive(on);
        }
        void Update() { if (_focus) { _t += Time.deltaTime; Apply(); } }
        void Apply()
        {
            if (_rs == null) return;
            Color c = Color.black;
            if (_focus) c = focusColor * (0.35f + 0.35f * (0.5f + 0.5f * Mathf.Sin(_t * 4f)));
            if (_hover) c = Color.Lerp(c, hoverColor, 0.7f);
            foreach (var r in _rs)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                r.GetPropertyBlock(_mpb);
                if (c == Color.black) _mpb.Clear(); else _mpb.SetColor(EmissionId, c);
                r.SetPropertyBlock(_mpb);
            }
        }
        void OnDisable() { _hover = _focus = false; Apply(); if (_arrow) _arrow.SetActive(false); }
    }

    /// <summary>Bobbing arrow + glowing ring that points at the focused equipment.</summary>
    public class MarkerArrow : MonoBehaviour
    {
        Vector3 _base; float _t;
        public static GameObject Create(Transform target, EquipmentTag tag)
        {
            var b = tag != null ? tag.WorldBounds : new Bounds(target.position, Vector3.one * 0.3f);
            var go = new GameObject("FocusMarker");
            go.transform.position = new Vector3(b.center.x, b.max.y + 0.18f, b.center.z);
            var cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.Destroy(cone.GetComponent<Collider>());
            cone.transform.SetParent(go.transform, false);
            cone.transform.localScale = new Vector3(0.06f, 0.05f, 0.06f);
            cone.transform.localPosition = new Vector3(0, 0.08f, 0);
            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            UnityEngine.Object.Destroy(tip.GetComponent<Collider>());
            tip.transform.SetParent(go.transform, false);
            tip.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
            var mat = RuntimeMaterials.Unlit(new Color(0.15f, 0.95f, 0.85f, 0.95f), false);
            cone.GetComponent<Renderer>().sharedMaterial = mat; tip.GetComponent<Renderer>().sharedMaterial = mat;
            // floor ring
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.Destroy(ring.GetComponent<Collider>());
            ring.name = "FocusRing";
            ring.transform.SetParent(go.transform, true);
            float r = Mathf.Max(b.extents.x, b.extents.z) * 2.2f + 0.2f;
            ring.transform.position = new Vector3(b.center.x, target.root.position.y + 0.006f, b.center.z);
            ring.transform.localScale = new Vector3(r, 0.002f, r);
            ring.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Unlit(new Color(0.15f, 0.95f, 0.85f, 0.18f));
            go.AddComponent<MarkerArrow>();
            return go;
        }
        void Start() { _base = transform.position; }
        void Update()
        {
            _t += Time.deltaTime;
            var p = _base; p.y += Mathf.Sin(_t * 3f) * 0.04f;
            transform.position = p;
            transform.GetChild(0).Rotate(0, 90 * Time.deltaTime, 0);
        }
    }

    // ====================================================================== ward setup
    public static class TransformExt
    {
        public static Transform FindDeep(this Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t) { var r = c.FindDeep(name); if (r != null) return r; }
            return null;
        }
    }

    // ====================================================================== hands-free door
    /// <summary>Hermetic sliding theatre door. Opens ONLY via the foot sensors (virtual foot / F key / A-X button).
    /// Touching the leaf or handle with a hand/pointer raises OnWrongAttempt (used for the tutorial).</summary>
    public class HandsFreeDoor : MonoBehaviour
    {
        public float openTime = 1.6f, stayOpen = 6f, sensorRadius = 0.55f;
        public bool locked;
        public event Action OnOpened, OnWrongAttempt, OnSensorFocus;
        public bool IsOpen { get; private set; }
        public Transform Leaf { get; private set; }
        public List<Transform> Sensors = new List<Transform>();
        Vector3 _closedPos, _openPos;
        float _openAmount, _timer;
        AudioSource _audio;
        Renderer[] _sensorLeds;

        void Start()
        {
            Leaf = transform.FindDeep("MainDoor_Leaf");
            foreach (var n in new[] { "MainDoor_FootSensor", "MainDoor_FootSensorInside" })
            {
                var s = transform.FindDeep(n); if (s == null) continue;
                Sensors.Add(s);
                var c = s.gameObject.AddComponent<BoxCollider>();
                var fs = s.gameObject.AddComponent<FootSensorTarget>(); fs.door = this;
            }
            if (Leaf != null)
            {
                _closedPos = Leaf.localPosition;
                // slides toward Blender +X  ->  Unity -X (ward space)
                var dirWorld = transform.root.TransformDirection(Vector3.left);
                _openPos = _closedPos + Leaf.parent.InverseTransformDirection(dirWorld) * 1.65f;
                var bc = Leaf.gameObject.AddComponent<BoxCollider>();
                var leafTouch = Leaf.gameObject.AddComponent<DoorTouchTarget>(); leafTouch.door = this;
            }
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 1f; _audio.minDistance = 1f; _audio.maxDistance = 12f; _audio.playOnAwake = false;
        }

        public void HandTouched()
        {
            if (IsOpen) return;
            UIAudio.Play("error", 0.8f);
            if (OnWrongAttempt != null) OnWrongAttempt();
        }

        public void SensorActivated()
        {
            if (locked) { UIAudio.Play("error", 0.6f); return; }
            _timer = stayOpen;
            if (IsOpen) return;
            IsOpen = true;
            var c = UIAudio.Clip("door_slide"); if (c != null) _audio.PlayOneShot(c);
            UIAudio.Play("sensor_beep", 0.8f);
            if (OnOpened != null) OnOpened();
        }

        /// <summary>True when the user stands in front of the doorway (either side), within ~2 m.</summary>
        bool NearDoorway(Vector3 foot)
        {
            var c = Leaf != null ? Leaf.position : transform.position;
            var flat = new Vector3(foot.x - c.x, 0, foot.z - c.z);
            return flat.magnitude < 2.0f;
        }

        public Transform NearestSensor(Vector3 p)
        {
            Transform best = null; float bd = 1e9f;
            foreach (var s in Sensors) { float d = Vector3.Distance(s.position, p); if (d < bd) { bd = d; best = s; } }
            return best;
        }

        void Update()
        {
            var rig = PlayerRig.Instance;
            if (rig != null && Sensors.Count > 0)
            {
                var foot = rig.FootPosition;
                var s = NearestSensor(foot);
                float d = Vector3.Distance(new Vector3(foot.x, s.position.y, foot.z), s.position);
                if (d < sensorRadius * 0.45f) SensorActivated();                       // stepping the (virtual) foot into the sensor zone
                else if (InputBridge.FootTapDown && (d < sensorRadius * 2.2f || NearDoorway(foot))) SensorActivated(); // deliberate foot tap at the sensor / in front of the door
                if (d < sensorRadius * 2.2f && OnSensorFocus != null) OnSensorFocus();
            }
            if (IsOpen)
            {
                bool someoneInDoorway = rig != null && Leaf != null && Vector3.Distance(new Vector3(rig.FloorPosition.x, 0, rig.FloorPosition.z), new Vector3(transform.position.x, 0, transform.position.z)) < 1.0f;
                if (!someoneInDoorway) _timer -= Time.deltaTime;
                if (_timer <= 0) { IsOpen = false; var c = UIAudio.Clip("door_slide"); if (c != null) _audio.PlayOneShot(c, 0.8f); }
            }
            _openAmount = Mathf.MoveTowards(_openAmount, IsOpen ? 1f : 0f, Time.deltaTime / openTime);
            if (Leaf != null) Leaf.localPosition = Vector3.Lerp(_closedPos, _openPos, Mathf.SmoothStep(0, 1, _openAmount));
        }
    }

    public class FootSensorTarget : MonoBehaviour, IPointerTarget
    {
        public HandsFreeDoor door;
        public bool IsInteractable { get { return true; } }
        public void OnHoverEnter(PointerEvent e) { }
        public void OnHoverExit(PointerEvent e) { }
        public void OnSelect(PointerEvent e)
        {
            // Pointing a hand/mouse at the sensor is NOT a foot touch - remind the user.
            var rig = PlayerRig.Instance;
            if (rig != null && Vector3.Distance(rig.FloorPosition, transform.position) < 1.4f && e.source == PointerSource.Mouse && !rig.IsVR)
                door.SensorActivated();          // desktop: clicking the sensor while standing at it = foot tap
            else door.HandTouched();
        }
    }

    public class DoorTouchTarget : MonoBehaviour, IPointerTarget
    {
        public HandsFreeDoor door;
        public bool IsInteractable { get { return door != null && !door.IsOpen; } }
        public void OnHoverEnter(PointerEvent e) { }
        public void OnHoverExit(PointerEvent e) { }
        public void OnSelect(PointerEvent e) { door.HandTouched(); }
        void OnTriggerEnter(Collider other) { }
        void Update()
        {
            // VR: a controller physically touching the door leaf counts as a hand push
            var rig = PlayerRig.Instance;
            if (rig == null || !rig.IsVR || door.IsOpen) return;
            var col = GetComponent<Collider>(); if (col == null) return;
            foreach (var h in new[] { rig.LeftHand, rig.RightHand })
                if (h.gameObject.activeInHierarchy && col.bounds.Contains(h.position)) { door.HandTouched(); break; }
        }
    }

    // ====================================================================== live screens
    /// <summary>Overlays live content on modelled screens: real clock, sweeping vitals trace, blinking alarms.</summary>
    public class LiveScreen : MonoBehaviour
    {
        string _key; Text _clock, _timer, _hr; RectTransform _sweep; float _t; float _start;
        void Start()
        {
            _key = name.Substring(name.IndexOf("_Screen_") + 8);
            var mf = GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) { enabled = false; return; }
            var mesh = mf.sharedMesh;
            var b = mesh.bounds;
            var n = mesh.normals.Length > 0 ? mesh.normals[0] : Vector3.back;
            // build a canvas on the screen face
            var worldN = transform.TransformDirection(n).normalized;
            var c = UIKit.WorldCanvas("Live_" + _key, Vector2.one * 100, transform, false);
            c.transform.position = transform.TransformPoint(b.center) + worldN * 0.002f;
            c.transform.rotation = Quaternion.LookRotation(-worldN, Vector3.up);
            // size: project bounds extents onto the screen plane
            var size = transform.TransformVector(b.size);
            var right = Vector3.Cross(Vector3.up, -worldN).normalized;
            float w = Mathf.Abs(Vector3.Dot(size, right)) * 1000f, h = Mathf.Abs(size.y) * 1000f;
            if (w < 10) w = size.magnitude * 1000f * 0.8f;
            ((RectTransform)c.transform).sizeDelta = new Vector2(w, h);
            c.sortingOrder = 1;
            _start = Time.time - UnityEngine.Random.Range(1500f, 2600f);
            if (_key == "vitals")
            {
                var sw = UIKit.Panel(c.transform, "Sweep", new Color(0.02f, 0.03f, 0.04f, 1f), null);
                sw.sprite = null; sw.type = Image.Type.Simple;
                _sweep = sw.rectTransform;
                _sweep.anchorMin = new Vector2(0.02f, 0.07f); _sweep.anchorMax = new Vector2(0.05f, 0.94f);
                _sweep.offsetMin = _sweep.offsetMax = Vector2.zero;
            }
            else if (_key == "clock")
            {
                var bg = UIKit.Panel(c.transform, "Bg", new Color(0.02f, 0.03f, 0.05f, 1f), null); bg.sprite = null; bg.type = Image.Type.Simple;
                _clock = UIKit.Label(c.transform, "", Mathf.RoundToInt(h * 0.36f), new Color(1f, 0.25f, 0.2f), TextAnchor.UpperCenter, UIKit.SemiBold);
                ((RectTransform)_clock.transform).offsetMax = new Vector2(0, -h * 0.08f);
                _timer = UIKit.Label(c.transform, "", Mathf.RoundToInt(h * 0.09f), new Color(0.3f, 0.9f, 0.5f), TextAnchor.LowerCenter, UIKit.SemiBold);
                ((RectTransform)_timer.transform).offsetMin = new Vector2(0, h * 0.1f);
            }
        }
        void Update()
        {
            _t += Time.deltaTime;
            if (_sweep != null)
            {
                float x = Mathf.Repeat(_t * 0.25f, 0.66f) + 0.02f;
                _sweep.anchorMin = new Vector2(x, 0.07f); _sweep.anchorMax = new Vector2(x + 0.025f, 0.94f);
            }
            if (_clock != null)
            {
                _clock.text = DateTime.Now.ToString("HH:mm:ss");
                var el = TimeSpan.FromSeconds(Time.time - _start);
                var el2 = TimeSpan.FromSeconds((Time.time - _start) * 0.8f);
                _timer.text = string.Format("ANAESTHESIA  {0:hh\\:mm\\:ss}      SURGERY  {1:hh\\:mm\\:ss}", el, el2);
            }
        }
    }

    // ====================================================================== ambience
    /// <summary>Quiet theatre ambience: ventilation hum loop + monitor beep at the patient monitor.</summary>
    public class TheatreAmbience : MonoBehaviour
    {
        public float volume = 0.35f;
        AudioSource _hum, _beep; float _next;
        void Start()
        {
            var hum = Resources.Load<AudioClip>("MROT/Audio/ambience_theatre");
            if (hum != null)
            {
                _hum = gameObject.AddComponent<AudioSource>();
                _hum.clip = hum; _hum.loop = true; _hum.volume = volume; _hum.spatialBlend = 0f; _hum.Play();
            }
            var mon = transform.FindDeep("PatientMonitor");
            if (mon != null)
            {
                _beep = mon.gameObject.AddComponent<AudioSource>();
                _beep.clip = Resources.Load<AudioClip>("MROT/Audio/monitor_beep");
                _beep.spatialBlend = 1f; _beep.minDistance = 0.8f; _beep.maxDistance = 10f; _beep.volume = 0.35f;
            }
        }
        void Update()
        {
            if (_beep == null || _beep.clip == null) return;
            if (Time.time > _next) { _beep.Play(); _next = Time.time + 60f / 72f; }
        }
    }

    // ====================================================================== guided navigation marker
    /// <summary>Glowing floor target used for bot-paced guided navigation.</summary>
    public class NavigationMarker : MonoBehaviour
    {
        public float radius = 0.45f;
        public event Action Reached;
        Transform _ring, _beam; float _t; bool _reached;
        public static NavigationMarker Create(Vector3 floorPos)
        {
            var go = new GameObject("NavigationMarker");
            go.transform.position = floorPos + Vector3.up * 0.01f;
            var m = go.AddComponent<NavigationMarker>();
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(go.transform, false);
            ring.transform.localScale = new Vector3(0.9f, 0.003f, 0.9f);
            ring.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Unlit(new Color(0.2f, 0.95f, 0.8f, 0.35f));
            m._ring = ring.transform;
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            UnityEngine.Object.Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(go.transform, false);
            beam.transform.localScale = new Vector3(0.5f, 0.6f, 0.5f);
            beam.transform.localPosition = new Vector3(0, 0.6f, 0);
            beam.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Unlit(new Color(0.2f, 0.95f, 0.8f, 0.08f));
            m._beam = beam.transform;
            return m;
        }
        void Update()
        {
            _t += Time.deltaTime;
            float s = 0.9f + Mathf.Sin(_t * 3f) * 0.08f;
            _ring.localScale = new Vector3(s, 0.003f, s);
            var rig = PlayerRig.Instance;
            if (!_reached && rig != null)
            {
                var p = rig.FloorPosition; p.y = transform.position.y;
                if (Vector3.Distance(p, transform.position) < radius)
                {
                    _reached = true;
                    UIAudio.Play("reach", 0.7f);
                    if (Reached != null) Reached();
                }
            }
        }
    }
}
