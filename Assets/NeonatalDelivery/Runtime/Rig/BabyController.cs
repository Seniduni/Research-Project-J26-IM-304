// Newborn driver: birth path (ND_B_Birth scrubbed by delivery progress), hand-held carry, skin-to-skin placement,
// landmark zones and skin-shader state. v5: the newborn is UNRIGGED (the user's sculpt as a static mesh under an animated
// 'Root'); posture clips (Calm/Cry/Limp/ErbsPalsy) are optional and simply skipped when the model has none.
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public enum BabyMode { Delivering, Held, OnChest, Falling, Resting }

    public class BabyController : MonoBehaviour
    {
        public GameObject model;                    // FBX instance root (clips sample relative to this)
        public AnimationClip[] clips;
        public Renderer[] skinRenderers;
        public Material ghostMaterial;
        public float babyScale = 1f;                // babyLengthCm / 47
        public BabyMode Mode { get; private set; } = BabyMode.Delivering;
        [Range(0, 1)] public float wetness = 1f, vernix = 0.7f, blood = 0.35f, cyanosis = 0.9f;
        [Range(0, 1)] public float crying, limp, erbsPalsy;
        public float Progress { get; private set; }
        public bool Born => Progress >= 0.999f;

        AnimationClip _birth, _calm, _cry, _limp, _erb;
        Dictionary<string, Transform> _b;
        Transform _rootBone; Vector3 _rootRestPos; Quaternion _rootRestRot; Vector3 _rootRestScale;
        public Transform HeadCenter, Vertex, Occiput, Face, Neck, ChestFront, Back, Buttocks, Navel, HipsBone, NeckBone, HeadBone;
        public readonly List<LandmarkZone> zones = new List<LandmarkZone>();
        float _calmT, _cryT;
        MaterialPropertyBlock _mpb;
        Vector3 _clipOrigin, _clipAxis = Vector3.forward; float _clipRadius = 0.05f, _clipSlope = 1.2f, _clipEnabled = 1f;
        // carry/placement
        Vector3 _tgtPos; Quaternion _tgtRot = Quaternion.identity; bool _hasTarget; Vector3 _vel;

        static readonly int P_Wet = Shader.PropertyToID("_Wetness"), P_Vernix = Shader.PropertyToID("_Vernix"), P_Blood = Shader.PropertyToID("_Blood"),
            P_Cyan = Shader.PropertyToID("_Cyanosis"), P_ClipOn = Shader.PropertyToID("_ClipEnabled"), P_ClipO = Shader.PropertyToID("_ClipOrigin"),
            P_ClipN = Shader.PropertyToID("_ClipNormal"), P_ClipR = Shader.PropertyToID("_ClipRadius"), P_ClipS = Shader.PropertyToID("_ClipSlope"), P_Censor = Shader.PropertyToID("_CensorGore");
        public float censorGore;

        public void Init(float scale)
        {
            babyScale = scale;
            if (model == null) model = gameObject;
            _b = RigUtil.MapBones(model.transform);
            _birth = RigUtil.FindClip(clips, "ND_B_Birth"); _calm = RigUtil.FindClip(clips, "ND_B_Calm"); _cry = RigUtil.FindClip(clips, "ND_B_Cry");
            _limp = RigUtil.FindClip(clips, "ND_B_Limp"); _erb = RigUtil.FindClip(clips, "ND_B_ErbsPalsy");
            if (_birth == null) ND.Error("ND_B_Birth clip missing on baby");
            _rootBone = T("Root");
            if (_rootBone != null) { _rootRestPos = _rootBone.localPosition; _rootRestRot = _rootBone.localRotation; _rootRestScale = _rootBone.localScale; }
            HeadCenter = T("ND_Z_HeadCenter"); Vertex = T("ND_Z_Vertex"); Occiput = T("ND_Z_Occiput"); Face = T("ND_Z_Face"); Neck = T("ND_Z_Neck");
            ChestFront = T("ND_Z_ChestFront"); Back = T("ND_Z_Back"); Buttocks = T("ND_Z_Buttocks"); Navel = T("ND_Z_NavelSkin");
            HipsBone = T("Hips"); NeckBone = T("Neck"); HeadBone = T("Head");
            if (skinRenderers == null || skinRenderers.Length == 0) skinRenderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in skinRenderers) if (r is SkinnedMeshRenderer smr) { smr.updateWhenOffscreen = true; }
            _mpb = new MaterialPropertyBlock();
            BuildZones();
        }

        Transform T(string n) { return _b != null && _b.TryGetValue(n, out var t) ? t : null; }

        void BuildZones()
        {
            zones.Clear();
            float s = babyScale;
            Z("ND_Z_Occiput", LandmarkZoneId.Occiput, 0.034f * s);
            Z("ND_Z_Vertex", LandmarkZoneId.Vertex, 0.032f * s);
            Z("ND_Z_ParietalL", LandmarkZoneId.ParietalLeft, 0.030f * s);
            Z("ND_Z_ParietalR", LandmarkZoneId.ParietalRight, 0.030f * s);
            Z("ND_Z_Face", LandmarkZoneId.Face, 0.030f * s);
            var neck = Z("ND_Z_Neck", LandmarkZoneId.Neck, 0.026f * s);
            Z("ND_Z_ShoulderL", LandmarkZoneId.ShoulderPosterior, 0.026f * s);   // LOA: baby's left shoulder delivers posteriorly
            Z("ND_Z_ShoulderR", LandmarkZoneId.ShoulderAnterior, 0.026f * s);    // baby's right shoulder is anterior (under the pubis)
            Z("ND_Z_AxillaL", LandmarkZoneId.Axilla, 0.018f * s); Z("ND_Z_AxillaR", LandmarkZoneId.Axilla, 0.018f * s);
            Z("ND_Z_Back", LandmarkZoneId.Back, 0.045f * s);
            Z("ND_Z_ChestFront", LandmarkZoneId.Chest, 0.04f * s);
            Z("ND_Z_Buttocks", LandmarkZoneId.Buttocks, 0.042f * s);
            Z("ND_Z_ThighL", LandmarkZoneId.Legs, 0.03f * s); Z("ND_Z_ThighR", LandmarkZoneId.Legs, 0.03f * s);
        }

        LandmarkZone Z(string marker, LandmarkZoneId id, float r)
        {
            var t = T(marker); if (t == null) { ND.Warn("Marker missing: " + marker); return null; }
            var z = NDUtil.GetOrAdd<LandmarkZone>(t.gameObject);
            z.id = id; z.radius = r; z.isBaby = true; zones.Add(z); return z;
        }

        public float HeadRadius => 0.048f * babyScale;     // v4 baby: biparietal ~9.5 cm
        public Vector3 SpineAxisOut
        {
            get
            {
                if (HeadCenter == null || Buttocks == null) return transform.forward;
                return (HeadCenter.position - (HipsBone != null ? HipsBone.position : Buttocks.position)).normalized;
            }
        }

        public void SetMode(BabyMode m) { Mode = m; if (m != BabyMode.Delivering) _hasTarget = false; }

        /// <summary>Delivering: sample the birth path at normalised progress.</summary>
        public void SetProgress(float p) { Progress = Mathf.Clamp01(p); }

        public void SetTarget(Vector3 chestPos, Quaternion bodyRot) { _tgtPos = chestPos; _tgtRot = bodyRot; _hasTarget = true; }

        public void Tick(float dt, BirthCanalDeformer canal)
        {
            if (model == null) return;
            if (Mode == BabyMode.Delivering && _birth != null)
            {
                _birth.SampleAnimation(model, Progress * _birth.length);
                if (_rootBone != null && Mathf.Abs(babyScale - 1f) > 0.001f)
                {
                    // keep the leading part (head) on the authored path when the baby is resized to match the silicone doll
                    Vector3 h1 = HeadCenter != null ? HeadCenter.position : _rootBone.position;
                    _rootBone.localScale = _rootRestScale * babyScale;
                    Vector3 h2 = HeadCenter != null ? HeadCenter.position : _rootBone.position;
                    _rootBone.position += h1 - h2;
                }
                // after birth the newborn starts moving its limbs
                if (Progress > 0.92f && _calm != null) { }
            }
            else
            {
                // postures (clips exclude the Root bone)
                if (_rootBone != null) { _rootBone.localPosition = _rootRestPos; _rootBone.localRotation = _rootRestRot; _rootBone.localScale = _rootRestScale * babyScale; }
                _calmT += dt; _cryT += dt;
                var post = limp > 0.5f ? _limp : (crying > 0.4f ? _cry : _calm);
                if (post != null) post.SampleAnimation(model, post == _limp ? 0f : ((post == _cry ? _cryT : _calmT) % post.length));
                if (erbsPalsy > 0.5f && _erb != null) _erb.SampleAnimation(model, 0f);
                if (_hasTarget) Place(dt);
            }
            UpdateShader(canal);
        }

        /// <summary>Rotate/translate the model so the baby's chest sits at the target with the body frame matching the target rotation.</summary>
        void Place(float dt)
        {
            if (ChestFront == null || Back == null || HeadCenter == null || HipsBone == null) return;
            Vector3 chest = (ChestFront.position + Back.position) * 0.5f;
            Vector3 up = (HeadCenter.position - HipsBone.position).normalized;
            Vector3 back = Vector3.ProjectOnPlane(Back.position - ChestFront.position, up).normalized;
            Quaternion cur = Quaternion.LookRotation(back, up);
            Quaternion delta = _tgtRot * Quaternion.Inverse(cur);
            float k = 1f - Mathf.Exp(-dt * 10f);
            delta = Quaternion.Slerp(Quaternion.identity, delta, k);
            var mt = model.transform;
            mt.rotation = delta * mt.rotation;
            mt.position = chest + delta * (mt.position - chest);
            chest = (ChestFront.position + Back.position) * 0.5f;
            Vector3 np = Vector3.SmoothDamp(chest, _tgtPos, ref _vel, 0.08f, 3f, dt);
            mt.position += np - chest;
        }

        /// <summary>Current body frame of the newborn: forward = toward its back, up = toward its head.</summary>
        public Quaternion BodyRotation
        {
            get
            {
                if (ChestFront == null || Back == null || HeadCenter == null || HipsBone == null) return transform.rotation;
                Vector3 up = (HeadCenter.position - HipsBone.position).normalized;
                Vector3 back = Vector3.ProjectOnPlane(Back.position - ChestFront.position, up);
                return back.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(back.normalized, up) : transform.rotation;
            }
        }

        public Vector3 ChestCenter => ChestFront != null && Back != null ? (ChestFront.position + Back.position) * 0.5f : transform.position;

        public void ConfigureClip(BirthCanalDeformer canal, bool enabled)
        {
            _clipEnabled = enabled ? 1f : 0f;
        }

        void UpdateShader(BirthCanalDeformer canal)
        {
            if (canal != null && canal.Ready)
            {
                _clipOrigin = canal.IntroitusCenter; _clipAxis = canal.OutwardAxis;
                _clipRadius = canal.IntroitusRadius + 0.004f;
            }
            if (Born && Mode != BabyMode.Delivering) _clipEnabled = 0f;
            foreach (var r in skinRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(P_Wet, wetness); _mpb.SetFloat(P_Vernix, vernix); _mpb.SetFloat(P_Blood, blood); _mpb.SetFloat(P_Cyan, cyanosis);
                _mpb.SetFloat(P_ClipOn, _clipEnabled); _mpb.SetVector(P_ClipO, _clipOrigin); _mpb.SetVector(P_ClipN, _clipAxis);
                _mpb.SetFloat(P_ClipR, _clipRadius); _mpb.SetFloat(P_ClipS, _clipSlope); _mpb.SetFloat(P_Censor, censorGore);
                r.SetPropertyBlock(_mpb);
            }
        }

        /// <summary>Spheres approximating the baby's girth for the canal deformer (head, shoulders, hips).</summary>
        public void RegisterProbes(BirthCanalDeformer canal)
        {
            canal.probes.Clear();
            if (HeadCenter != null) canal.probes.Add(new BirthCanalDeformer.Probe { t = HeadCenter, radius = HeadRadius * 0.98f });
            var sh = T("Chest"); if (sh != null) canal.probes.Add(new BirthCanalDeformer.Probe { t = sh, radius = 0.052f * babyScale });
            if (HipsBone != null) canal.probes.Add(new BirthCanalDeformer.Probe { t = HipsBone, radius = 0.045f * babyScale });
            canal.SetMaxRadiusFromHead(HeadRadius);
        }

        public void SetVisible(bool v) { foreach (var r in skinRenderers) if (r != null) r.enabled = v; }
    }
}
