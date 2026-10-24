using UnityEngine;

namespace IVSim
{
    /// <summary>Marks the sharps container. Releasing a used needle near it disposes of it.</summary>
    public class SharpsBin : MonoBehaviour
    {
        public static SharpsBin Instance { get; private set; }
        public Transform opening;
        public float radius = 0.15f;

        void OnEnable() { Instance = this; }

        public bool IsNear(Vector3 p)
        {
            Transform o = opening != null ? opening : transform;
            return Vector3.Distance(p, o.position) < radius;
        }
    }
}
