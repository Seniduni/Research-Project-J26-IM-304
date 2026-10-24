// MR Operating Theatre - scene bootstraps for the ward demo and the asset gallery.
using UnityEngine;

namespace MROT
{
    /// <summary>Ward demo (Progress Presentation 1): walk the ward, use the hands-free door, inspect every asset.</summary>
    public class WardDemoBootstrap : MonoBehaviour
    {
        public bool startInCorridor = true;
        void Start()
        {
            var ward = WardSetup.Instance;
            if (ward == null) { Debug.LogError("[MROT] Add the MR_OperatingTheatre_Ward prefab to the scene."); return; }
            Vector3 pos; float yaw;
            if (startInCorridor) { pos = ward.BlenderToWorld(0f, -5.4f, 0f); yaw = WardSpace.YawFromBlenderFacing(0, 1); }
            else { pos = ward.BlenderToWorld(-1.6f, -2.2f, 0f); yaw = WardSpace.YawFromBlenderFacing(1.6f, 2.4f); }
            PlayerRig.Create(pos, yaw);
            gameObject.AddComponent<WardExplorer>();
        }
    }
}
