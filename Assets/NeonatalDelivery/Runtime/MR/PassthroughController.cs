// Mixed reality: Meta Quest 3 colour passthrough (AR Foundation + Unity OpenXR: Meta), with the virtual ward hidden and the
// virtual mother rendered as visible / depth-only occluder / hidden over the physical silicone birthing mannequin.
using System.Collections.Generic;
using UnityEngine;
#if ND_ARFOUNDATION
using UnityEngine.XR.ARFoundation;
#endif

namespace NeonatalDelivery
{
    public class PassthroughController : MonoBehaviour
    {
        public NDXRRig rig;
        public GameObject[] hideInMR;          // virtual ward, delivery bed (the real bed is used)
        public Renderer[] motherRenderers;
        public Material occluderMaterial;      // NeonatalDelivery/Occluder (depth only)
        public bool MRActive { get; private set; }
        readonly Dictionary<Renderer, Material[]> _orig = new Dictionary<Renderer, Material[]>();
        CameraClearFlags _flags; Color _bg; bool _hdr;
#if ND_ARFOUNDATION
        ARSession _session; ARCameraManager _camMgr;
#endif
        public static bool PassthroughSupported
        {
            get
            {
#if ND_ARFOUNDATION && ND_META_OPENXR
                return true;
#else
                return false;
#endif
            }
        }

        public void SetMixedReality(bool on, MotherRenderMode motherMode)
        {
            if (rig == null || rig.cam == null) return;
            var cam = rig.cam;
            if (on && !MRActive) { _flags = cam.clearFlags; _bg = cam.backgroundColor; _hdr = cam.allowHDR; }
            MRActive = on;
            foreach (var g in hideInMR) if (g != null) g.SetActive(!on);
#if ND_ARFOUNDATION
            if (on)
            {
                if (_session == null) { _session = FindAnyObjectByType<ARSession>(); if (_session == null) _session = new GameObject("ND_ARSession").AddComponent<ARSession>(); }
                if (_camMgr == null) _camMgr = NDUtil.GetOrAdd<ARCameraManager>(cam.gameObject);
                _session.enabled = true; _camMgr.enabled = true;
            }
            else if (_camMgr != null) _camMgr.enabled = false;
#else
            if (on) ND.Warn("Passthrough needs AR Foundation + Unity OpenXR: Meta (menu: Neonatal Delivery > Install Dependencies). Running as VR.");
#endif
            if (on)
            {
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0); cam.allowHDR = false;
#if ND_URP
                var d = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (d != null) d.renderPostProcessing = false;
#endif
            }
            else
            {
                cam.clearFlags = _flags == 0 ? CameraClearFlags.Skybox : _flags; cam.backgroundColor = _bg; cam.allowHDR = _hdr;
#if ND_URP
                var d = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); if (d != null) d.renderPostProcessing = true;
#endif
            }
            SetMotherMode(on ? motherMode : MotherRenderMode.Visible);
        }

        public void SetMotherMode(MotherRenderMode m)
        {
            foreach (var r in motherRenderers)
            {
                if (r == null) continue;
                if (!_orig.ContainsKey(r)) _orig[r] = r.sharedMaterials;
                r.enabled = m != MotherRenderMode.Hidden;
                if (m == MotherRenderMode.Occluder && occluderMaterial != null)
                {
                    var arr = new Material[_orig[r].Length]; for (int i = 0; i < arr.Length; i++) arr[i] = occluderMaterial; r.sharedMaterials = arr;
                }
                else r.sharedMaterials = _orig[r];
            }
        }
    }
}
