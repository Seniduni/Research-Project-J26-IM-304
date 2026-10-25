using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// Orbit camera for desktop testing. Right-drag to orbit, scroll to zoom, middle-drag to pan,
    /// C to re-centre on the selected site.
    /// </summary>
    public class DesktopRig : MonoBehaviour
    {
        public Camera cam;
        public Transform focus;
        public float distance = 0.7f;
        public float yaw = 0f;
        public float pitch = 40f;
        public float minDistance = 0.12f, maxDistance = 1.6f;

        Vector3 target;
        Vector2 lastMouse;

        void Start()
        {
            target = focus != null ? focus.position : new Vector3(0f, 0.9f, 0.1f);
            if (focus != null) yaw += focus.eulerAngles.y;   // works when the whole station is rotated in a new environment
            Apply();
        }

        void LateUpdate()
        {
            if (cam == null) return;
            Vector2 m = IVInput.MousePosition;
            Vector2 delta = m - lastMouse;
            lastMouse = m;

            if (IVInput.MouseButton(1) && !IVInput.MouseButtonDown(1))
            {
                yaw += delta.x * 0.25f;
                pitch = Mathf.Clamp(pitch - delta.y * 0.25f, -10f, 85f);
            }
            if (IVInput.MouseButton(2) && !IVInput.MouseButtonDown(2))
            {
                float k = distance * 0.0015f;
                target -= cam.transform.right * delta.x * k + cam.transform.up * delta.y * k;
            }
            float scroll = IVInput.ScrollSteps;
            if (scroll != 0f) distance = Mathf.Clamp(distance * (scroll > 0f ? 0.88f : 1.13f), minDistance, maxDistance);

            if (IVInput.GetKeyDown(IVKey.C))
            {
                var s = IVSession.Instance;
                if (s != null && s.SiteSelected) target = s.SitePoint;
                else if (focus != null) target = focus.position;
            }
            Apply();
        }

        void Apply()
        {
            if (cam == null) return;
            Quaternion q = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.position = target - q * Vector3.forward * distance;
            cam.transform.rotation = q;
        }
    }
}
