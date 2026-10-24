// Mother rig driver: blends the baked delivery clips (Playables), drives facial morphs (pain, strain, relief), jaw, gaze,
// breathing and post-delivery belly change. All bone outputs are deterministic and evaluated from DeliveryDirector.Tick.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace NeonatalDelivery
{
    public enum MotherPose { Rest = 0, Push = 1, Pant = 2, Reach = 3, Cradle = 4 }

    public class MotherController : MonoBehaviour
    {
        public Animator animator;
        public AnimationClip[] clips;              // assigned by the editor builder (ND_M_Idle, ND_M_Push, ND_M_Pant, ND_M_Reach, ND_M_Cradle, ND_M_Base)
        public SkinnedMeshRenderer bodyRenderer, eyelashRenderer;
        public Transform lookTarget;               // trainee head
        [Range(0, 1)] public float pain, strain, relief;
        public float blendSpeed = 2.5f;

        PlayableGraph _graph; AnimationMixerPlayable _mixer;
        readonly AnimationClipPlayable[] _p = new AnimationClipPlayable[5];
        readonly float[] _w = new float[5], _target = new float[5];
        float _pushPhase; MotherPose _pose = MotherPose.Rest;
        public MotherPose Pose => _pose;
        Dictionary<string, Transform> _bones;
        public Transform Hips, Head, Neck, Jaw, LeftEye, RightEye, Belly, Spine, Chest;
        Quaternion _jawRest, _eyeLRest, _eyeRRest; float _jawSign = 1f; Vector3 _bellyRestScale = Vector3.one;
        readonly Dictionary<string, int> _morph = new Dictionary<string, int>();
        readonly Dictionary<string, int> _lashMorph = new Dictionary<string, int>();
        float _blinkTimer = 2f, _blink, _deflate, _breathPhase;
        public bool Ready { get; private set; }
        public Matrix4x4 HipsReferenceLocal { get; private set; }   // hips relative to the station in the base pose
        public Transform station;

        public void Init(Transform stationRoot)
        {
            station = stationRoot;
            _bones = RigUtil.MapBones(transform);
            Hips = B("Hips"); Head = B("Head"); Neck = B("Neck"); Jaw = B("Jaw"); LeftEye = B("LeftEye"); RightEye = B("RightEye");
            Belly = B("Belly"); Spine = B("Spine"); Chest = B("Chest");
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator == null) animator = gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create("ND_Mother");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(_graph, "Mother", animator);
            _mixer = AnimationMixerPlayable.Create(_graph, 5);
            string[] names = { "ND_M_Idle", "ND_M_Push", "ND_M_Pant", "ND_M_Reach", "ND_M_Cradle" };
            for (int i = 0; i < 5; i++)
            {
                var c = RigUtil.FindClip(clips, names[i]) ?? RigUtil.FindClip(clips, "ND_M_Base");
                if (c == null) { ND.Error("Mother clip missing: " + names[i]); continue; }
                _p[i] = AnimationClipPlayable.Create(_graph, c);
                if (i == 1) _p[i].SetSpeed(0);      // push is scrubbed by the contraction model
                _graph.Connect(_p[i], 0, _mixer, i);
            }
            output.SetSourcePlayable(_mixer);
            _w[0] = _target[0] = 1f; ApplyWeights();
            _graph.Evaluate(0f);
            if (Hips != null && station != null) HipsReferenceLocal = station.worldToLocalMatrix * Hips.localToWorldMatrix;
            if (Jaw != null) _jawRest = Jaw.localRotation;
            if (LeftEye != null) _eyeLRest = LeftEye.localRotation;
            if (RightEye != null) _eyeRRest = RightEye.localRotation;
            if (Belly != null) _bellyRestScale = Belly.localScale;
            CacheMorphs(bodyRenderer, _morph); CacheMorphs(eyelashRenderer, _lashMorph);
            CalibrateJaw();
            Ready = true;
        }

        Transform B(string n) { return _bones != null && _bones.TryGetValue(n, out var t) ? t : null; }

        static void CacheMorphs(SkinnedMeshRenderer r, Dictionary<string, int> d)
        {
            d.Clear(); if (r == null || r.sharedMesh == null) return;
            for (int i = 0; i < r.sharedMesh.blendShapeCount; i++) d[r.sharedMesh.GetBlendShapeName(i)] = i;
        }

        void CalibrateJaw()
        {
            if (Jaw == null || Head == null || LeftEye == null || RightEye == null) return;
            Vector3 up = (Head.position - (Neck != null ? Neck.position : Head.position - Vector3.up)).normalized;
            Vector3 lat = (LeftEye.position - RightEye.position).normalized;
            Vector3 probe = Jaw.position + Vector3.Cross(lat, up) * 0.05f;     // a point in front/below
            Vector3 rel = probe - Jaw.position;
            Vector3 after = Quaternion.AngleAxis(10f, lat) * rel;
            _jawSign = Vector3.Dot(after - rel, up) < 0f ? 1f : -1f;           // positive angle must move the chin down
        }

        public void SetPose(MotherPose p) { _pose = p; for (int i = 0; i < 5; i++) _target[i] = i == (int)p ? 1f : 0f; }
        /// <summary>Push clip phase 0..1 (inhale, curl, sustained push, release) driven by the contraction model.</summary>
        public void SetPushPhase(float phase01) { _pushPhase = Mathf.Clamp01(phase01); }
        public void SetDelivered(bool delivered) { _deflateTarget = delivered ? 1f : 0f; }
        float _deflateTarget;

        void ApplyWeights()
        {
            float s = 0; for (int i = 0; i < 5; i++) s += _w[i];
            for (int i = 0; i < 5; i++) if (_p[i].IsValid()) _mixer.SetInputWeight(i, s > 0 ? _w[i] / s : (i == 0 ? 1 : 0));
        }

        /// <summary>Evaluate the animation for this frame and apply procedural overlays. Call once per frame (LateUpdate order).</summary>
        public void Tick(float dt)
        {
            if (!Ready) return;
            for (int i = 0; i < 5; i++) _w[i] = Mathf.MoveTowards(_w[i], _target[i], dt * blendSpeed);
            ApplyWeights();
            if (_p[1].IsValid()) _p[1].SetTime(_pushPhase * _p[1].GetAnimationClip().length);
            _graph.Evaluate(dt);
            Overlays(dt);
        }

        void Overlays(float dt)
        {
            // jaw: vocalising/strain opens slightly, panting opens rhythmically
            if (Jaw != null && Head != null && LeftEye != null && RightEye != null)
            {
                _breathPhase += dt * (_pose == MotherPose.Pant ? 2f : 0.25f) * Mathf.PI * 2f;
                float open = pain * 8f + (_pose == MotherPose.Pant ? 6f + 5f * Mathf.Sin(_breathPhase) : 0f) + relief * 4f;
                Vector3 lat = (LeftEye.position - RightEye.position).normalized;
                Jaw.localRotation = _jawRest; Jaw.rotation = Quaternion.AngleAxis(open * _jawSign, lat) * Jaw.rotation;
            }
            // eyes: look at the trainee, blink
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f) { _blink = 1f; _blinkTimer = Random.Range(2.2f, 5.5f); }
            _blink = Mathf.MoveTowards(_blink, 0f, dt * 7f);
            if (lookTarget != null)
            {
                Aim(LeftEye, _eyeLRest); Aim(RightEye, _eyeRRest);
            }
            // facial morphs (0..100)
            float eyesShut = Mathf.Max(_blink, strain * 0.85f, pain * 0.45f);
            Set("ND_BrowLower_L", strain * 70 + pain * 50); Set("ND_BrowLower_R", strain * 70 + pain * 50);
            Set("ND_BrowRaiseInner_L", pain * 55); Set("ND_BrowRaiseInner_R", pain * 55);
            Set("ND_EyeClose_L", eyesShut * 100); Set("ND_EyeClose_R", eyesShut * 100);
            Set("ND_EyeSquint_L", pain * 60); Set("ND_EyeSquint_R", pain * 60);
            Set("ND_NoseSneer_L", strain * 55 + pain * 40); Set("ND_NoseSneer_R", strain * 55 + pain * 40);
            Set("ND_CheekRaise_L", strain * 50); Set("ND_CheekRaise_R", strain * 50);
            Set("ND_MouthStretch_L", pain * 70); Set("ND_MouthStretch_R", pain * 70);
            Set("ND_MouthPress", strain * 90);
            Set("ND_MouthOpenTeeth", pain * 55 + (_pose == MotherPose.Pant ? 35 : 0));
            Set("ND_NeckStrain_L", strain * 90); Set("ND_NeckStrain_R", strain * 90);
            Set("ND_MouthSmile_L", relief * 65); Set("ND_MouthSmile_R", relief * 65);
            // belly deflates after delivery (uterus contracts to the umbilicus)
            _deflate = Mathf.MoveTowards(_deflate, _deflateTarget, dt * 0.25f);
            if (Belly != null) Belly.localScale = Vector3.Scale(_bellyRestScale, Vector3.one * Mathf.Lerp(1f, 0.82f, _deflate));
        }

        void Aim(Transform eye, Quaternion rest)
        {
            if (eye == null || eye.parent == null) return;
            eye.localRotation = rest;
            Vector3 cur = (eye.position - (Head != null ? Head.position : eye.parent.position));
            Vector3 fwd = Vector3.ProjectOnPlane(cur, (Head.position - Neck.position).normalized);
            Vector3 to = lookTarget.position - eye.position;
            if (fwd.sqrMagnitude < 1e-6f) return;
            float ang = Vector3.Angle(fwd, to); float k = Mathf.Clamp01(25f / Mathf.Max(1e-3f, ang));
            var q = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(fwd.normalized, to.normalized), k);
            eye.rotation = q * eye.rotation;
        }

        void Set(string n, float v)
        {
            v = Mathf.Clamp(v, 0f, 100f);
            if (bodyRenderer != null && _morph.TryGetValue(n, out int i)) bodyRenderer.SetBlendShapeWeight(i, v);
            if (eyelashRenderer != null && _lashMorph.TryGetValue(n, out int j)) eyelashRenderer.SetBlendShapeWeight(j, v);
        }

        /// <summary>Current hips motion relative to the base pose, expressed in station space (moves the birth-path frame with the pelvis).</summary>
        public Matrix4x4 HipsDeltaLocal()
        {
            if (Hips == null || station == null) return Matrix4x4.identity;
            var now = station.worldToLocalMatrix * Hips.localToWorldMatrix;
            return now * HipsReferenceLocal.inverse;
        }

        void OnDestroy() { if (_graph.IsValid()) _graph.Destroy(); }
    }
}
