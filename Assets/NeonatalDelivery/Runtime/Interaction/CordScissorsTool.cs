// CordScissorsTool: split from DeliveryTools.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using UnityEngine;

namespace NeonatalDelivery
{
    public class CordScissorsTool : MonoBehaviour
    {
        public UmbilicalCord cord;
        public Transform bladeA, bladeB, tip;   // tip = point between the blades where the cord is cut
        public float maxOpen = 22f;
        public event Action<float, Vector3> Cut;  // (distance from navel m, world pos)
        Grabbable _g; float _prevPinch; Quaternion _aRest, _bRest;

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
            if (_g == null || !_g.IsHeld) { SetOpen(0.15f); _prevPinch = 0; return; }
            float pinch = _g.heldBy.pinch;
            SetOpen(1f - pinch);
            if (_prevPinch < 0.8f && pinch >= 0.8f && cord != null)
            {
                float s = cord.TryCut(tip.position, 0.02f);
                if (s >= 0) { NDAudio.Play("scissor_snip", tip.position, 1f); Cut?.Invoke(s, tip.position); }
            }
            _prevPinch = pinch;
        }

        void SetOpen(float o)
        {
            float a = maxOpen * Mathf.Clamp01(o) * 0.5f;
            if (bladeA != null) bladeA.localRotation = _bRestOr(_aRest) * Quaternion.AngleAxis(a, Vector3.forward);
            if (bladeB != null) bladeB.localRotation = _bRestOr(_bRest) * Quaternion.AngleAxis(-a, Vector3.forward);
        }
        static Quaternion _bRestOr(Quaternion q) => q;
        public void ResetTool() { if (_g != null) _g.ReturnToRest(); }
    }
}
