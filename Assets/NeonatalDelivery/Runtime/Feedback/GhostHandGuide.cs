// Guided practice mode (FR12): ghosted reference hands demonstrate the correct placement for the current step.
// Uses expert motion-capture recordings when available (MocapLibrary), otherwise anatomical target poses.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NeonatalDelivery
{
    [Serializable]
    public class MocapClip
    {
        public string step; public float rate = 60f; public string recordedBy; public string recordedUtc;
        public List<MocapSample> samples = new List<MocapSample>();
    }
    [Serializable]
    public class MocapSample { public float t; public bool l, r; public Vector3[] lJ, rJ; }

    /// <summary>Expert motion capture: record hand joints relative to the newborn's head frame; save/load per step.</summary>
    public static class MocapLibrary
    {
        public static string Folder => Path.Combine(SessionConfig.Folder, "Mocap");
        static readonly Dictionary<DeliveryStep, MocapClip> s_Cache = new Dictionary<DeliveryStep, MocapClip>();

        public static MocapClip Load(DeliveryStep s)
        {
            if (s_Cache.TryGetValue(s, out var c)) return c;
            c = null;
            try
            {
                var p = Path.Combine(Folder, s + ".json");
                if (File.Exists(p)) c = JsonUtility.FromJson<MocapClip>(File.ReadAllText(p));
                else { var ta = Resources.Load<TextAsset>("NeonatalDelivery/Mocap/" + s); if (ta != null) c = JsonUtility.FromJson<MocapClip>(ta.text); }
            }
            catch (Exception e) { ND.Warn("Mocap load failed: " + e.Message); }
            s_Cache[s] = c; return c;
        }

        public static void Save(MocapClip c)
        {
            try { Directory.CreateDirectory(Folder); File.WriteAllText(Path.Combine(Folder, c.step + ".json"), JsonUtility.ToJson(c)); s_Cache.Remove((DeliveryStep)Enum.Parse(typeof(DeliveryStep), c.step)); ND.Log("Expert mocap saved for " + c.step); }
            catch (Exception e) { ND.Error("Mocap save failed: " + e.Message); }
        }
    }

    public class GhostHandGuide : MonoBehaviour
    {
        public BabyController baby;
        public BirthCanalDeformer canal;
        public Material ghostMaterial;
        public HandSide dominant = HandSide.Right;
        public bool Recording { get; private set; }
        GloveHandVisual _l, _r; readonly TrackedHand _lh = new TrackedHand(HandSide.Left), _rh = new TrackedHand(HandSide.Right);
        DeliveryStep _step; bool _show; MocapClip _clip; float _clock;
        MocapClip _rec; float _recClock;

        void Awake()
        {
            _l = new GameObject("GhostL").AddComponent<GloveHandVisual>(); _l.transform.SetParent(transform, false); _l.side = HandSide.Left; _l.isGhost = true; _l.material = ghostMaterial;
            _r = new GameObject("GhostR").AddComponent<GloveHandVisual>(); _r.transform.SetParent(transform, false); _r.side = HandSide.Right; _r.isGhost = true; _r.material = ghostMaterial;
        }

        public void Show(DeliveryStep step, bool on) { _step = step; _show = on; _clip = on ? MocapLibrary.Load(step) : null; _clock = 0f; if (!on) { _lh.tracked = _rh.tracked = false; } }

        Transform Frame => baby != null ? baby.HeadCenter : null;

        void LateUpdate()
        {
            if (_l.material != ghostMaterial) { _l.material = ghostMaterial; _r.material = ghostMaterial; }
            if (_show && baby != null)
            {
                if (_clip != null && _clip.samples.Count > 0 && Frame != null) PlayClip(); else Targets();
            }
            else { _lh.tracked = _rh.tracked = false; }
            _l.Draw(_lh); _r.Draw(_rh);
        }

        void PlayClip()
        {
            _clock += Time.deltaTime; float dur = _clip.samples[_clip.samples.Count - 1].t + 1f; if (_clock > dur) _clock = 0f;
            MocapSample s = _clip.samples[0];
            foreach (var x in _clip.samples) { if (x.t > _clock) break; s = x; }
            Fill(_lh, s.l, s.lJ); Fill(_rh, s.r, s.rJ);
        }
        void Fill(TrackedHand h, bool tr, Vector3[] local)
        {
            h.tracked = tr && local != null && local.Length == J.Count; if (!h.tracked) return;
            for (int i = 0; i < J.Count; i++) h.pos[i] = Frame.TransformPoint(local[i]);
            h.UpdateDerived(Time.time);
        }

        /// <summary>Anatomical target poses for each step when no expert recording exists.</summary>
        void Targets()
        {
            _lh.tracked = _rh.tracked = false;
            if (canal == null || !canal.Ready) return;
            Vector3 outw = canal.OutwardAxis, ant = canal.AnteriorAxis, lat = canal.LateralAxis;
            var dom = dominant == HandSide.Left ? _lh : _rh; var non = dominant == HandSide.Left ? _rh : _lh;
            switch (_step)
            {
                case DeliveryStep.CrowningHeadControl:
                {
                    // dominant: flat fingers on the occiput (anterior aspect at the pubic arch), pressing gently inward to keep flexion
                    Vector3 occ = baby.Occiput != null ? baby.Occiput.position : canal.IntroitusCenter;
                    Vector3 p = occ + outw * 0.035f + ant * 0.025f;
                    ProceduralHand.Generate(dom, p, Quaternion.LookRotation(-ant * 0.3f - outw, ant), 0.15f, 0f);
                    // non-dominant: palm with a pad supporting the perineum (posterior), fingers across
                    Vector3 per = canal.IntroitusCenter - ant * (canal.IntroitusRadius + 0.035f) + outw * 0.02f;
                    ProceduralHand.Generate(non, per, Quaternion.LookRotation(lat * (dominant == HandSide.Right ? 1 : -1), -ant + outw * 0.4f), 0.25f, 0f);
                    break;
                }
                case DeliveryStep.NuchalCordCheck:
                {
                    if (baby.Neck == null) return;
                    ProceduralHand.Generate(dom, baby.Neck.position + outw * 0.03f + ant * 0.035f, Quaternion.LookRotation(-outw, ant), 0.35f, 0f);
                    break;
                }
                case DeliveryStep.AnteriorShoulder:
                case DeliveryStep.PosteriorShoulder:
                {
                    var pl = RigUtil.FindDeep(baby.model.transform, "ND_Z_ParietalL"); var pr = RigUtil.FindDeep(baby.model.transform, "ND_Z_ParietalR");
                    if (pl == null || pr == null) return;
                    Vector3 across = (pl.position - pr.position).normalized;
                    ProceduralHand.Generate(_lh.side == HandSide.Left ? _lh : _rh, pl.position + across * 0.03f, Quaternion.LookRotation(outw, across), 0.3f, 0f);
                    ProceduralHand.Generate(_rh, pr.position - across * 0.03f, Quaternion.LookRotation(outw, -across), 0.3f, 0f);
                    break;
                }
                case DeliveryStep.BodyDelivery:
                case DeliveryStep.SkinToSkin:
                {
                    if (baby.Neck == null || baby.Buttocks == null) return;
                    Vector3 down = -ant;
                    ProceduralHand.Generate(dom, baby.Neck.position + down * 0.04f, Quaternion.LookRotation(outw, -down), 0.35f, 0f);
                    ProceduralHand.Generate(non, baby.Buttocks.position + down * 0.04f, Quaternion.LookRotation(outw, -down), 0.45f, 0f);
                    break;
                }
            }
        }

        // ---------------- expert capture
        public void StartRecording(DeliveryStep step, string expert) { _rec = new MocapClip { step = step.ToString(), recordedBy = expert, recordedUtc = DateTime.UtcNow.ToString("o") }; _recClock = 0f; Recording = true; }
        public void RecordFrame(TrackedHand l, TrackedHand r)
        {
            if (!Recording || Frame == null) return;
            _recClock += Time.deltaTime;
            var s = new MocapSample { t = _recClock, l = l.tracked, r = r.tracked, lJ = new Vector3[J.Count], rJ = new Vector3[J.Count] };
            for (int i = 0; i < J.Count; i++) { s.lJ[i] = Frame.InverseTransformPoint(l.pos[i]); s.rJ[i] = Frame.InverseTransformPoint(r.pos[i]); }
            _rec.samples.Add(s);
        }
        public void StopRecording(bool save) { Recording = false; if (save && _rec != null && _rec.samples.Count > 10) MocapLibrary.Save(_rec); _rec = null; }
    }
}
