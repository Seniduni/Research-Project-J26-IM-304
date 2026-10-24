// Reflective practice loop (FR5/FR6): rebuilds a third-person replay of the faulty interval with an annotation that marks
// the exact moment and nature of the fault, then requires the trainee to repeat the isolated step.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonatalDelivery
{
    public class ReflectiveReplay : MonoBehaviour
    {
        public Camera thirdPersonCamera;
        public RenderTexture target;
        public Material replayHandMaterial, faultHandMaterial;
        public BabyController baby;
        public NDPanel panel;                       // world-space panel with the replay view and annotations
        public Transform viewTarget;                // what the 3rd-person camera looks at (introitus / baby head)
        public bool Active { get; private set; }
        public float playbackSpeed = 0.5f;

        List<MotionFrame> _frames; FaultEvent _fault; float _t0, _t1, _clock; Action _onRetry;
        GloveHandVisual _lv, _rv; readonly TrackedHand _lh = new TrackedHand(HandSide.Left), _rh = new TrackedHand(HandSide.Right);
        RawImage _view; TMP_Text _title, _body, _time; NDBar _bar;
        float _savedProgress; BabyMode _savedMode; Vector3 _savedPos; Quaternion _savedRot;

        public void Build(TMP_FontAsset font)
        {
            if (target == null) { target = new RenderTexture(1024, 640, 24) { name = "ND_ReplayRT", antiAliasing = 2 }; }
            if (thirdPersonCamera == null)
            {
                var go = new GameObject("ND_ReplayCamera"); go.transform.SetParent(transform, false);
                thirdPersonCamera = go.AddComponent<Camera>(); thirdPersonCamera.fieldOfView = 38f; thirdPersonCamera.nearClipPlane = 0.02f; thirdPersonCamera.farClipPlane = 20f;
            }
            thirdPersonCamera.targetTexture = target; thirdPersonCamera.enabled = false;
            var lgo = new GameObject("ReplayHandL"); lgo.transform.SetParent(transform, false); _lv = lgo.AddComponent<GloveHandVisual>(); _lv.side = HandSide.Left; _lv.isGhost = true; _lv.material = replayHandMaterial;
            var rgo = new GameObject("ReplayHandR"); rgo.transform.SetParent(transform, false); _rv = rgo.AddComponent<GloveHandVisual>(); _rv.side = HandSide.Right; _rv.isGhost = true; _rv.material = replayHandMaterial;
            if (panel != null)
            {
                _title = panel.AddText("Reflective replay", 34, FontStyles.Bold);
                _view = panel.AddRawImage(target, new Vector2(820, 512));
                _bar = panel.AddBar(new Vector2(820, 16));
                _time = panel.AddText("", 22, FontStyles.Normal);
                _body = panel.AddText("", 24, FontStyles.Normal);
                var row = panel.AddRow();
                panel.AddButton(row, "Replay again", () => { _clock = 0f; });
                panel.AddButton(row, "Repeat this step", () => Finish());
                panel.gameObject.SetActive(false);
            }
        }

        public void Begin(FaultEvent fault, List<MotionFrame> frames, Action onRetry)
        {
            _fault = fault; _frames = frames; _onRetry = onRetry; Active = true; _clock = 0f;
            _t0 = frames.Count > 0 ? frames[0].t : fault.startTime; _t1 = frames.Count > 0 ? frames[frames.Count - 1].t : fault.endTime;
            if (baby != null) { _savedProgress = baby.Progress; _savedMode = baby.Mode; _savedPos = baby.model.transform.position; _savedRot = baby.model.transform.rotation; }
            if (thirdPersonCamera != null)
            {
                thirdPersonCamera.enabled = true;
                Vector3 c = viewTarget != null ? viewTarget.position : transform.position;
                Vector3 side = viewTarget != null ? viewTarget.right : Vector3.right;
                thirdPersonCamera.transform.position = c + side * 0.75f + Vector3.up * 0.35f - (viewTarget != null ? viewTarget.forward : Vector3.forward) * 0.25f;
                thirdPersonCamera.transform.LookAt(c + Vector3.up * 0.03f);
            }
            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                _title.text = $"Reflective replay - {fault.name}";
                _body.text = $"<b>What happened:</b> {fault.evidence}\n<b>Clinical consequence:</b> {fault.complication}\n<b>Why:</b> {fault.explanation}\n<b>Do instead:</b> {fault.remediation}";
                if (_bar != null) _bar.SetMarker(Mathf.InverseLerp(_t0, _t1, fault.startTime), Mathf.InverseLerp(_t0, _t1, fault.endTime));
            }
            NDAudio.Instance?.UI("replay_start", 0.6f);
        }

        void Update()
        {
            if (!Active || _frames == null || _frames.Count == 0) return;
            float dur = Mathf.Max(0.1f, _t1 - _t0);
            bool inFault = _t0 + _clock >= _fault.startTime && _t0 + _clock <= _fault.endTime + 0.3f;
            _clock += Time.unscaledDeltaTime * (inFault ? playbackSpeed * 0.6f : playbackSpeed);
            if (_clock > dur + 0.8f) _clock = 0f;
            float t = _t0 + Mathf.Min(_clock, dur);
            var f = Sample(t);
            if (f == null) return;
            Apply(f, _lh, f.lJ, f.lTracked, _fault.hand == HandSide.Left && inFault);
            Apply(f, _rh, f.rJ, f.rTracked, _fault.hand == HandSide.Right && inFault);
            _lv.material = (_fault.hand == HandSide.Left && inFault) ? faultHandMaterial : replayHandMaterial;
            _rv.material = (_fault.hand == HandSide.Right && inFault) ? faultHandMaterial : replayHandMaterial;
            _lv.Draw(_lh); _rv.Draw(_rh);
            if (baby != null)
            {
                if ((BabyMode)f.babyMode == BabyMode.Delivering) { baby.SetMode(BabyMode.Delivering); baby.SetProgress(f.progress); }
                else { baby.model.transform.SetPositionAndRotation(f.babyRootPos, f.babyRootRot); }
            }
            if (_bar != null) _bar.SetValue(Mathf.Clamp01(_clock / dur));
            if (_time != null) _time.text = $"t = {t:F1} s   {(inFault ? "<color=#ff5050>FAULT</color>" : "")}   (fault {_fault.startTime:F1}-{_fault.endTime:F1} s)";
        }

        MotionFrame Sample(float t)
        {
            MotionFrame best = null; float bd = 9f;
            foreach (var f in _frames) { float d = Mathf.Abs(f.t - t); if (d < bd) { bd = d; best = f; } }
            return best;
        }

        static void Apply(MotionFrame f, TrackedHand h, Vector3[] j, bool tracked, bool fault)
        {
            h.tracked = tracked; if (!tracked) return;
            Array.Copy(j, h.pos, J.Count); h.UpdateDerived(f.t);
        }

        public void Finish()
        {
            if (!Active) return;
            Active = false;
            if (thirdPersonCamera != null) thirdPersonCamera.enabled = false;
            if (panel != null) panel.gameObject.SetActive(false);
            _lh.tracked = _rh.tracked = false; _lv.Draw(_lh); _rv.Draw(_rh);
            if (baby != null) { baby.SetMode(_savedMode); baby.SetProgress(_savedProgress); if (_savedMode != BabyMode.Delivering) baby.model.transform.SetPositionAndRotation(_savedPos, _savedRot); }
            var cb = _onRetry; _onRetry = null; cb?.Invoke();
        }
    }
}
