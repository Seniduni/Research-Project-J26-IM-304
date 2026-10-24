using UnityEngine;

namespace IVSim
{
    /// <summary>Scales an object from zero to a target size over time.</summary>
    public class FXGrow : MonoBehaviour
    {
        Vector3 target;
        float duration = 1f;
        float t;

        public void Init(Vector3 targetScale, float seconds)
        {
            target = targetScale;
            duration = Mathf.Max(0.05f, seconds);
            transform.localScale = target * 0.05f;
        }

        void Update()
        {
            if (t >= 1f) return;
            t = Mathf.Min(1f, t + Time.deltaTime / duration);
            float e = 1f - (1f - t) * (1f - t);
            transform.localScale = Vector3.Lerp(target * 0.05f, target, e);
        }
    }
}
