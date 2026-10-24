// Classifies each attempt into the fault taxonomy (FR3): evaluates the active step's rules on every evaluation frame and
// reports the exact time interval in which each fault occurred. Event-type rules are raised by the tools (clamp/scissors).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public class FaultClassifier
    {
        class RuleState { public float since = -1; public float accum; public bool fired; public FaultEvent open; }
        readonly Dictionary<string, RuleState> _state = new Dictionary<string, RuleState>();
        public FaultRuleSet Rules { get; private set; }
        public Action<FaultEvent> OnFault;
        public HandSide dominant = HandSide.Right;
        public int attempt = 1;

        public FaultClassifier(FaultRuleSet rules) { Rules = rules; }

        public void ResetForStep() { _state.Clear(); }

        RuleState S(FaultRule r) { if (!_state.TryGetValue(r.code, out var s)) _state[r.code] = s = new RuleState(); return s; }

        public struct Context
        {
            public DeliveryStep step; public float time; public float dt; public float progress; public float progressRate;
            public EvaluationFrame eval; public bool babyHeld; public bool babyBorn; public float contraction;
        }

        public void Evaluate(Context c)
        {
            if (Rules == null) return;
            foreach (var r in Rules.rules)
            {
                if (!r.ActiveIn(c.step)) continue;
                if (c.progress < r.progressMin || c.progress > r.progressMax) { Close(r, c.time); S(r).since = -1; continue; }
                bool cond = Condition(r, c, out string ev);
                var st = S(r);
                if (cond)
                {
                    if (st.since < 0) st.since = c.time;
                    st.accum += c.dt;
                    if (!st.fired && c.time - st.since >= r.minDuration)
                    {
                        st.fired = true;
                        st.open = Make(r, c.step, st.since, c.time, ev);
                        OnFault?.Invoke(st.open);
                    }
                    else if (st.open != null) st.open.endTime = c.time;
                }
                else { st.since = -1; Close(r, c.time); }
            }
        }

        void Close(FaultRule r, float t) { var st = S(r); if (st.open != null) { st.open.endTime = t; st.open = null; } }

        public FaultEvent Make(FaultRule r, DeliveryStep step, float start, float end, string evidence)
        {
            return new FaultEvent
            {
                code = r.code, name = r.name, step = step, startTime = start, endTime = end, severity = r.severity,
                evidence = evidence, complication = r.complication, explanation = r.explanation, remediation = r.remediation, attempt = attempt
            };
        }

        /// <summary>Raise an event-type fault (clamp/cut/drop) immediately if a matching rule exists for the step.</summary>
        public FaultEvent RaiseEvent(string detector, DeliveryStep step, float time, string evidence, float value = 0f)
        {
            if (Rules == null) return null;
            foreach (var r in Rules.rules)
            {
                if (r.detector != detector) continue;
                if (r.stepSet != null && r.stepSet.Count > 0 && !r.ActiveIn(step)) continue;
                if (detector == Detector.ClampPosition && value >= r.minCm && value <= r.maxCm) continue;
                var st = S(r); if (st.fired) continue; st.fired = true;
                var f = Make(r, step, Mathf.Max(0, time - 1.5f), time, evidence);
                OnFault?.Invoke(f); return f;
            }
            return null;
        }

        HandEval Pick(FaultRule r, EvaluationFrame e, out HandEval other)
        {
            var dom = e.Hand(dominant); var non = e.Hand(dominant == HandSide.Left ? HandSide.Right : HandSide.Left);
            if (r.hands == "NonDominant") { other = dom; return non; }
            other = non; return dom;
        }

        bool AnyZone(FaultRule r, HandEval h) { foreach (var z in r.zoneSet) if (h.Has(z)) return true; return false; }

        bool Condition(FaultRule r, Context c, out string ev)
        {
            ev = ""; var e = c.eval; if (e == null) return false;
            switch (r.detector)
            {
                case Detector.NoContactOnZones:
                {
                    bool any = AnyZone(r, e.left) || AnyZone(r, e.right);
                    if (r.hands == "Dominant") any = AnyZone(r, e.Hand(dominant));
                    if (r.hands == "NonDominant") any = AnyZone(r, e.Hand(dominant == HandSide.Left ? HandSide.Right : HandSide.Left));
                    bool cond = !any && (r.threshold <= 0f || c.progressRate > r.threshold);
                    ev = $"no hand on {string.Join("/", r.zones ?? new string[0])} (progress rate {c.progressRate * 100f:F1}%/s)";
                    return cond;
                }
                case Detector.ContactOnZones:
                {
                    foreach (var h in new[] { e.left, e.right })
                    {
                        if (!AnyZone(r, h)) continue;
                        if (r.requireGrip && !h.engaged) continue;
                        if (r.threshold > 0 && h.force < r.threshold && h.traction.magnitude < r.threshold) continue;
                        ev = $"{h.side} hand on {h.zone} (force {h.force:F1} N, traction {h.traction.magnitude:F1} N)";
                        return true;
                    }
                    return false;
                }
                case Detector.ForceAbove:
                {
                    foreach (var h in new[] { e.left, e.right })
                    {
                        if (r.zoneSet.Count > 0 && !AnyZone(r, h)) continue;
                        float fz = Mathf.Max(h.force, h.traction.magnitude);
                        if (fz > r.threshold) { ev = $"{h.side} hand {fz:F1} N > {r.threshold:F0} N on {h.zone}"; return true; }
                    }
                    return false;
                }
                case Detector.TractionAxis:
                {
                    if (e.tractionMagnitude < r.threshold) return false;
                    float ang = AxisAngle(r.expectedAxis, c, e);
                    ev = $"traction {e.tractionMagnitude:F1} N at {ang:F0} deg from the {r.expectedAxis} axis (limit {r.maxAngleDeg:F0} deg)";
                    return ang > r.maxAngleDeg;
                }
                case Detector.LateralForceAbove:
                    ev = $"lateral traction {e.lateralComponent:F1} N > {r.threshold:F0} N";
                    return e.lateralComponent > r.threshold;
                case Detector.TractionBeforeProgress:
                    ev = $"traction {e.tractionMagnitude:F1} N before external rotation completed (progress {c.progress:P0})";
                    return e.tractionMagnitude > r.threshold;
                case Detector.ReleaseDuring:
                {
                    bool supported = r.hands == "Both" ? e.fullySupported : e.bodySupported;
                    ev = supported ? "" : "baby not supported by " + (r.hands == "Both" ? "both hands" : "any hand");
                    return !supported && (c.babyHeld || !c.babyBorn);
                }
                case Detector.ProgressRateAbove:
                    ev = $"progress rate {c.progressRate * 100f:F1}%/s > {r.threshold * 100f:F1}%/s";
                    return c.progressRate > r.threshold && !e.headControl;
            }
            return false;
        }

        static float AxisAngle(string axis, Context c, EvaluationFrame e)
        {
            Vector3 t = e.tractionCanal.normalized;     // x lateral, y outward, z anterior
            Vector3 exp;
            switch (axis)
            {
                case "Anterior": exp = new Vector3(0, 0.55f, 0.83f); break;        // following the curve of Carus upward
                case "Posterior": exp = new Vector3(0, 0.80f, -0.60f); break;       // traditional downward
                case "Outward": exp = new Vector3(0, 1, 0); break;
                default: return e.angleFromAxial;                                    // "Axial": along the fetal spine
            }
            return Vector3.Angle(t, exp.normalized);
        }
    }
}
