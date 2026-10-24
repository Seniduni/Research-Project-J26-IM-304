// Sensitive-situation censorship (FR8): lecturer-selected graded visual attenuation and audio damping (4 levels).
// The teaching content (head position, hand landmarks, traction axis, cord, clamps) is never hidden at any level.
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public class CensorshipFilter : MonoBehaviour
    {
        public CensorshipLevel level = CensorshipLevel.Moderate;
        public Renderer privacyVeil;              // frosted veil quad over the maternal perineum (ND/Veil shader, stencil-masked to the mother)
        public List<Renderer> motherSkin = new List<Renderer>();
        public BabyController baby;
        public NDAudio audioSys;
        public BirthCanalDeformer canal;
        public Transform veilAnchor;              // follows the introitus

        static readonly int P_Gore = Shader.PropertyToID("_CensorGore"), P_Stylize = Shader.PropertyToID("_Stylize"), P_VeilAlpha = Shader.PropertyToID("_VeilOpacity");
        MaterialPropertyBlock _mpb;

        public float BloodScale => level == CensorshipLevel.FullRealism ? 1f : level == CensorshipLevel.Moderate ? 0.5f : level == CensorshipLevel.Clinical ? 0.12f : 0f;
        public float GoreDesaturation => level == CensorshipLevel.FullRealism ? 0f : level == CensorshipLevel.Moderate ? 0.35f : level == CensorshipLevel.Clinical ? 0.8f : 1f;
        public float VeilOpacity => level == CensorshipLevel.Clinical ? 0.55f : level == CensorshipLevel.Minimal ? 0.85f : 0f;
        public float MotherStylize => level == CensorshipLevel.Minimal ? 0.75f : 0f;
        public float VocalVolume => level == CensorshipLevel.FullRealism ? 1f : level == CensorshipLevel.Moderate ? 0.6f : level == CensorshipLevel.Clinical ? 0.35f : 0.15f;
        public float VocalLowPass => level == CensorshipLevel.FullRealism ? 22000f : level == CensorshipLevel.Moderate ? 7000f : level == CensorshipLevel.Clinical ? 2600f : 1200f;
        public bool ShowTearAsOutline => level >= CensorshipLevel.Clinical;

        public void Apply(CensorshipLevel l) { level = l; Refresh(); }

        void Refresh()
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            foreach (var r in motherSkin)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb); _mpb.SetFloat(P_Gore, GoreDesaturation); _mpb.SetFloat(P_Stylize, MotherStylize); r.SetPropertyBlock(_mpb);
            }
            if (baby != null) baby.censorGore = GoreDesaturation;
            if (audioSys != null)
            {
                audioSys.motherVolume = VocalVolume;
                if (audioSys.motherLowPass != null) audioSys.motherLowPass.cutoffFrequency = VocalLowPass;
            }
            if (privacyVeil != null)
            {
                privacyVeil.enabled = VeilOpacity > 0.01f;
                privacyVeil.GetPropertyBlock(_mpb); _mpb.SetFloat(P_VeilAlpha, VeilOpacity); privacyVeil.SetPropertyBlock(_mpb);
            }
        }

        void LateUpdate()
        {
            if (privacyVeil != null && privacyVeil.enabled && canal != null && canal.Ready)
            {
                var t = privacyVeil.transform;
                t.position = canal.IntroitusCenter + canal.OutwardAxis * 0.03f;
                t.rotation = Quaternion.LookRotation(-canal.OutwardAxis, canal.AnteriorAxis);
                t.localScale = Vector3.one * 0.30f;
            }
        }

        void Start() { Refresh(); }
    }
}
