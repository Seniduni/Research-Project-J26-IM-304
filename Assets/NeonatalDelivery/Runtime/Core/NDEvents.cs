// Global events so other platform modules (AI tutor, performance tracking service, dashboard) can listen without coupling.
using System;

namespace NeonatalDelivery
{
    public static class NDEvents
    {
        public static event Action<DeliveryStep, DeliveryStep> StepChanged;   // (previous, next)
        public static event Action<FaultEvent> FaultDetected;
        public static event Action<FaultEvent> ConsequenceRendered;
        public static event Action<DeliveryStep, int> StepRetryRequired;       // (step, attempt)
        public static event Action<DeliveryStep, float> StepCompleted;         // (step, score 0..100)
        public static event Action<SessionResult> SessionFinished;
        public static event Action<string> Instruction;                        // subtitle/instruction text
        public static event Action<float> ContractionStarted;                  // intensity 0..1
        public static event Action BabyBorn;
        public static event Action CordCut;

        public static void RaiseStepChanged(DeliveryStep a, DeliveryStep b) { StepChanged?.Invoke(a, b); }
        public static void RaiseFault(FaultEvent f) { FaultDetected?.Invoke(f); }
        public static void RaiseConsequence(FaultEvent f) { ConsequenceRendered?.Invoke(f); }
        public static void RaiseRetry(DeliveryStep s, int attempt) { StepRetryRequired?.Invoke(s, attempt); }
        public static void RaiseStepCompleted(DeliveryStep s, float score) { StepCompleted?.Invoke(s, score); }
        public static void RaiseSessionFinished(SessionResult r) { SessionFinished?.Invoke(r); }
        public static void RaiseInstruction(string t) { Instruction?.Invoke(t); }
        public static void RaiseContraction(float i) { ContractionStarted?.Invoke(i); }
        public static void RaiseBabyBorn() { BabyBorn?.Invoke(); }
        public static void RaiseCordCut() { CordCut?.Invoke(); }
    }
}
