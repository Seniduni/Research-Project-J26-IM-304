// Per-step rubric and competency state machine (decides whether the trainee may continue).
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public class ScoringRubric
    {
        public float faultPenaltyPerSeverity = 30f;
        public float retryPenalty = 5f;
        public float competentThreshold = 80f, developingThreshold = 60f;
        readonly Dictionary<DeliveryStep, List<FaultEvent>> _faults = new Dictionary<DeliveryStep, List<FaultEvent>>();
        readonly Dictionary<DeliveryStep, int> _attempts = new Dictionary<DeliveryStep, int>();

        public void Attempt(DeliveryStep s) { _attempts[s] = Attempts(s) + 1; }
        public int Attempts(DeliveryStep s) { return _attempts.TryGetValue(s, out var a) ? a : 0; }
        public void Fault(FaultEvent f) { if (!_faults.TryGetValue(f.step, out var l)) _faults[f.step] = l = new List<FaultEvent>(); l.Add(f); }

        /// <summary>Score of the final (successful) attempt penalised by faults made on the way and by the number of retries.</summary>
        public float StepScore(DeliveryStep s, float timeQuality01 = 1f)
        {
            float score = 100f;
            if (_faults.TryGetValue(s, out var l)) foreach (var f in l) score -= f.severity * faultPenaltyPerSeverity;
            score -= Mathf.Max(0, Attempts(s) - 1) * retryPenalty;
            score -= (1f - Mathf.Clamp01(timeQuality01)) * 10f;
            return Mathf.Clamp(score, 0f, 100f);
        }

        public float OverallScore(SessionResult r)
        {
            if (r.steps.Count == 0) return 0f;
            float sum = 0; int n = 0;
            foreach (var s in r.steps) if (s.completed) { sum += s.score; n++; }
            return n > 0 ? sum / n : 0f;
        }

        public CompetencyStatus Competency(SessionResult r)
        {
            if (r.steps.Count == 0) return CompetencyStatus.NotAttempted;
            float o = OverallScore(r);
            bool severe = false; foreach (var f in r.faults) if (f.severity >= 0.8f) severe = true;
            if (o >= competentThreshold && !severe) return CompetencyStatus.Competent;
            if (o >= developingThreshold) return CompetencyStatus.Developing;
            return CompetencyStatus.Novice;
        }
    }
}
