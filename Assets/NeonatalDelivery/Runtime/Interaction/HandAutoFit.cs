// Hand auto-fit: when the trainee brings a hand close to its correct place on the newborn (crowning head control,
// nuchal-cord check, shoulders, body delivery, skin-to-skin), the hand smoothly snaps into the correct supporting pose
// and follows the baby there. Moving the hand away (beyond releaseRadius) releases it again.
// The fitted pose is written into the TrackedHand itself, so the gloves, grabbing and the placement evaluator all use it.
// Switch off with SessionConfig.handAutoFit (e.g. for assessment sessions).
using UnityEngine;

namespace NeonatalDelivery
{
    [DefaultExecutionOrder(-150)]   // after HandTrackingSource (-200), before evaluation and visuals
    public class HandAutoFit : MonoBehaviour
    {
        public HandTrackingSource hands;
        public GhostHandGuide guide;
        public bool autoFit = true;
        [Tooltip("Palm within this distance of the target -> fully fitted (m).")] public float snapRadius = 0.06f;
        [Tooltip("Fitting starts inside this distance (m).")] public float captureRadius = 0.12f;
        [Tooltip("A fitted hand is released when the real palm moves this far away (m).")] public float releaseRadius = 0.16f;
        public float blendSpeed = 6f;
        readonly float[] _w = new float[2];
        readonly bool[] _fitted = new bool[2];
        readonly Vector3[] _lastTarget = new Vector3[2];
        public bool IsFitted(HandSide s) => _fitted[(int)s] && _w[(int)s] > 0.9f;

        void Update()
        {
            if (hands == null) hands = HandTrackingSource.Instance;
            if (hands == null || guide == null) return;
            Fit(hands.Left, 0); Fit(hands.Right, 1);
        }

        void Fit(TrackedHand h, int i)
        {
            float dt = Time.deltaTime;
            TrackedHand t = null;
            bool hasTarget = autoFit && h.tracked && h.source != HandSourceKind.Replay && guide.TryGetTarget(h.side, out t) && t != null;
            float target = 0f;
            if (hasTarget)
            {
                float d = Vector3.Distance(h.palmPos, t.palmPos);              // raw (tracked) palm, before fitting
                if (_fitted[i]) _fitted[i] = d < releaseRadius;
                else _fitted[i] = d < captureRadius;
                if (_fitted[i]) target = d <= snapRadius ? 1f : Mathf.Clamp01(1f - (d - snapRadius) / Mathf.Max(1e-3f, releaseRadius - snapRadius));
            }
            else _fitted[i] = false;
            _w[i] = Mathf.MoveTowards(_w[i], target, dt * blendSpeed);
            if (t == null) return;
            Vector3 tVel = dt > 1e-5f ? (t.palmPos - _lastTarget[i]) / dt : Vector3.zero; _lastTarget[i] = t.palmPos;
            if (_w[i] <= 0.001f) return;
            Vector3 rawPalm = h.palmPos, rawVel = h.velocity;
            float w = _w[i] * _w[i] * (3f - 2f * _w[i]);
            for (int j = 0; j < J.Count; j++)
            {
                h.pos[j] = Vector3.Lerp(h.pos[j], t.pos[j], w);
                h.rot[j] = Quaternion.Slerp(h.rot[j], t.rot[j], w);
            }
            h.UpdateDerived(Time.time);
            h.OverrideMotion(rawPalm, Vector3.Lerp(rawVel, tVel, w));
        }
    }
}
