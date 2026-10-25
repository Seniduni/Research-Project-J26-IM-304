// CordScissorsTool (v5): hold the scissors (pinch) and bring the blades to the cord - they close and cut automatically.
// The blades open wide while held; when the cutting point touches the cord they close over 'closeTime' and the cord is cut
// at that point (the director then checks it was between the two clamps). Moving away before the blades close cancels.
using System;
using UnityEngine;

namespace NeonatalDelivery
{
    public class CordScissorsTool : MonoBehaviour
    {
        public UmbilicalCord cord;
        public Transform bladeA, bladeB, tip;   // tip = point between the blades where the cord is cut
        public float maxOpen = 22f;
        [Header("Automatic cut")]
        public bool autoCut = true;
        [Tooltip("Cutting point within this distance of the cord starts the cut (m).")] public float cutReach = 0.022f;
        [Tooltip("Seconds the blades take to close on the cord before it is cut.")] public float closeTime = 0.3f;
        public event Action<float, Vector3> Cut;  // (distance from navel m, world pos)
        public bool Cutting => _close > 0f;
        Grabbable _g; float _open = 0.15f, _close; Quaternion _aRest, _bRest;

        void Awake()
        {
            _g = GetComponent<Grabbable>();
            if (bladeA == null) bladeA = RigUtil.FindDeep(transform, "ND_CordScissors_BladeA");
            if (bladeB == null) bladeB = RigUtil.FindDeep(transform, "ND_CordScissors_BladeB");
            if (bladeA != null) _aRest = bladeA.localRotation; if (bladeB != null) _bRest = bladeB.localRotation;
            if (tip == null) { tip = new GameObject("CutPoint").transform; tip.SetParent(transform, false); tip.localPosition = new Vector3(0.045f, 0, 0); }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (_g == null || !_g.IsHeld) { _close = 0f; _open = Mathf.MoveTowards(_open, 0.15f, dt * 4f); SetOpen(_open); return; }
            float d = (autoCut && cord != null) ? cord.DistanceTo(tip.position) : float.PositiveInfinity;
            if (d < cutReach)
            {
                _close += dt / Mathf.Max(0.05f, closeTime);              // blades close on the cord
                if (_close >= 1f)
                {
                    float s = cord.TryCut(tip.position, cutReach + 0.01f);
                    if (s >= 0) { NDAudio.Play("scissor_snip", tip.position, 1f); Cut?.Invoke(s, tip.position); }
                    _close = 0f; _open = 0f;                              // closed after the cut, reopens below
                }
            }
            else _close = Mathf.MoveTowards(_close, 0f, dt * 4f);      // moved away -> cancel
            float target = cord != null && cord.IsCut ? 0.35f : 1f;      // open wide while approaching the cord
            _open = Mathf.MoveTowards(_open, target, dt * 3f);
            SetOpen(_open * (1f - Mathf.Clamp01(_close)));
        }

        void SetOpen(float o)
        {
            float a = maxOpen * Mathf.Clamp01(o) * 0.5f;
            if (bladeA != null) bladeA.localRotation = _aRest * Quaternion.AngleAxis(a, Vector3.forward);
            if (bladeB != null) bladeB.localRotation = _bRest * Quaternion.AngleAxis(-a, Vector3.forward);
        }
        public void ResetTool() { _close = 0f; _open = 0.15f; if (_g != null) _g.ReturnToRest(); }
    }
}
