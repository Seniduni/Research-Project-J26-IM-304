using UnityEngine;

namespace IVSim
{
    /// <summary>Marks the alcohol bottle so a VR swab can be wetted by touching its mouth.</summary>
    public class AlcoholBottle : MonoBehaviour
    {
        public static AlcoholBottle Instance { get; private set; }
        public Transform mouth;
        public float wetRadius = 0.03f;

        void OnEnable() { Instance = this; }

        public bool IsAtMouth(Vector3 p)
        {
            Transform m = mouth != null ? mouth : transform;
            return Vector3.Distance(p, m.position) < wetRadius;
        }
    }
}
