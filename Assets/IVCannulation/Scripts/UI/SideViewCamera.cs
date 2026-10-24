using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// "Ultrasound-style" cross-section view: an orthographic camera looks at the needle from the side, with the
    /// skin hidden, so learners can see the needle tip relative to the vein. Shown on the trainer panel in Easy mode.
    /// Requires layers "IVSkin" (arm skin) and "IVInset" (profile line) - the scene builder creates them.
    /// </summary>
    public class SideViewCamera : MonoBehaviour
    {
        public Camera insetCam;
        public LineRenderer skinLine;
        public int textureSize = 512;
        public float viewHalfHeight = 0.011f;
        public float slabHalfWidth = 0.012f;

        RenderTexture rt;
        public Texture Texture { get { return rt; } }
        public bool Active { get; private set; }

        void Awake()
        {
            rt = new RenderTexture(textureSize, textureSize, 24);
            rt.name = "IVSideView";
            if (insetCam != null)
            {
                insetCam.targetTexture = rt;
                insetCam.stereoTargetEye = StereoTargetEyeMask.None;
                insetCam.orthographic = true;
                insetCam.orthographicSize = viewHalfHeight;
                insetCam.clearFlags = CameraClearFlags.SolidColor;
                insetCam.backgroundColor = new Color(0.86f, 0.70f, 0.64f, 1f);
                int skin = LayerMask.NameToLayer("IVSkin");
                insetCam.cullingMask = skin >= 0 ? ~(1 << skin) : ~0;
            }
        }

        void OnDestroy()
        {
            if (rt != null) rt.Release();
        }

        void LateUpdate()
        {
            var s = IVSession.Instance;
            bool show = s != null && s.Profile != null && s.Profile.showSideView && LayerMask.NameToLayer("IVSkin") >= 0;
            if (insetCam != null && insetCam.enabled != show) insetCam.enabled = show;
            if (skinLine != null) skinLine.enabled = show;
            Active = show;
            if (!show || insetCam == null) return;

            var arm = ArmModel.Instance;
            if (arm == null) return;

            Vector3 P, n, d;
            var c = s.cannula;
            if (c != null && (c.IsInSkin || (c.IsHeld && c.LiveValid)))
            {
                P = c.IsInSkin ? c.EntryPoint : c.LivePoint;
                n = c.IsInSkin ? c.EntryNormal : c.LiveNormal;
                d = c.transform.forward;
            }
            else if (c != null && c.CatheterPlaced)
            {
                P = c.EntryPoint; n = c.EntryNormal; d = c.PlacedDirection;
            }
            else if (s.SiteSelected)
            {
                P = s.SitePoint;
                arm.ProjectToSurface(P, out n);
                d = arm.ProximalDirection;
            }
            else
            {
                P = arm.SurfacePoint(0.33f, 0f);
                n = arm.SurfaceNormal(0.33f, 0f);
                d = arm.ProximalDirection;
            }

            Vector3 flat = d - Vector3.Dot(d, n) * n;
            if (flat.sqrMagnitude < 1e-6f) flat = arm.ProximalDirection;
            flat.Normalize();
            Vector3 side = Vector3.Cross(n, flat).normalized;
            float dist = 0.12f;
            Vector3 center = P + flat * 0.008f - n * 0.003f;
            insetCam.transform.position = center + side * dist;
            insetCam.transform.rotation = Quaternion.LookRotation(-side, n);
            insetCam.nearClipPlane = dist - slabHalfWidth;
            insetCam.farClipPlane = dist + slabHalfWidth;

            if (skinLine != null)
            {
                int count = 25;
                skinLine.positionCount = count;
                for (int i = 0; i < count; i++)
                {
                    float off = Mathf.Lerp(-0.02f, 0.036f, i / (float)(count - 1));
                    Vector3 q = P + flat * off;
                    RaycastHit hit;
                    Vector3 pt = q;
                    if (arm.RaycastSkin(new Ray(q + n * 0.04f, -n), 0.08f, out hit)) pt = hit.point;
                    skinLine.SetPosition(i, pt + side * 0.0f);
                }
            }
        }
    }
}
