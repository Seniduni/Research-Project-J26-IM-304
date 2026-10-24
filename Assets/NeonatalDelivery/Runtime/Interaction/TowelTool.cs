// TowelTool: split from DeliveryTools.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using UnityEngine;

namespace NeonatalDelivery
{
    public class TowelTool : MonoBehaviour
    {
        public BabyController baby;
        public float dryRate = 0.18f;          // wetness removed per metre of rubbing
        public float RubbedMetres { get; private set; }
        Grabbable _g; Vector3 _prev; bool _has;

        void Awake() { _g = GetComponent<Grabbable>(); }

        void Update()
        {
            if (_g == null || !_g.IsHeld || baby == null) { _has = false; return; }
            Vector3 p = transform.position;
            if (_has)
            {
                float move = Vector3.Distance(p, _prev);
                bool touching = false;
                foreach (var z in baby.zones) if (z != null && z.SignedDistance(p) < 0.035f) { touching = true; break; }
                if (touching && move > 0.0005f)
                {
                    RubbedMetres += move;
                    baby.wetness = Mathf.Max(0.12f, baby.wetness - move * dryRate * 4f);
                    baby.vernix = Mathf.Max(0.25f, baby.vernix - move * dryRate * 1.5f);
                    baby.blood = Mathf.Max(0f, baby.blood - move * dryRate * 2f);
                }
            }
            _prev = p; _has = true;
        }
        public void ResetTool() { RubbedMetres = 0; if (_g != null) _g.ReturnToRest(); }
    }
}
