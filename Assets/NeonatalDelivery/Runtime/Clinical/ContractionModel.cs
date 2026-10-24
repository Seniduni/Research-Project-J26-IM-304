// Second-stage labour model: contraction cycle, maternal pushing effort, fetal heart rate (CTG-like).
using UnityEngine;

namespace NeonatalDelivery
{
    public class ContractionModel
    {
        public float restSeconds = 14f;          // compressed (real: 2-3 min) - scaled by SessionConfig.timeScale
        public float contractionSeconds = 12f;   // compressed (real: 45-90 s); maps onto the ND_M_Push clip
        public float baselineFHR = 142f;
        public bool Active { get; private set; }
        public float Phase01 { get; private set; }          // 0..1 within the current contraction
        public float Intensity { get; private set; }        // 0..1 uterine pressure
        public float PushEffort { get; private set; }       // 0..1 maternal bearing-down
        public float FetalHeartRate { get; private set; } = 142f;
        public bool Panting { get; set; }                   // instructed to pant (stop pushing) at crowning
        public bool Bradycardia { get; set; }               // consequence state (hypoxia)
        public bool Running = true;
        public int Count { get; private set; }
        float _t; float _scale = 1f;
        public System.Action<int> OnContractionStart;

        public void SetTimeScale(float s) { _scale = Mathf.Max(0.2f, s); }
        public void StartSoon(float delay = 2f) { Active = false; _t = restSeconds * _scale - delay; }

        public void Tick(float dt)
        {
            if (!Running) { PushEffort = Mathf.MoveTowards(PushEffort, 0, dt); Intensity = Mathf.MoveTowards(Intensity, 0, dt * 0.5f); UpdateFHR(dt); return; }
            _t += dt;
            if (!Active)
            {
                Intensity = Mathf.MoveTowards(Intensity, 0f, dt * 0.4f); PushEffort = Mathf.MoveTowards(PushEffort, 0f, dt * 2f); Phase01 = 0f;
                if (_t >= restSeconds * _scale) { Active = true; _t = 0f; Count++; OnContractionStart?.Invoke(Count); NDEvents.RaiseContraction(1f); }
            }
            else
            {
                Phase01 = Mathf.Clamp01(_t / contractionSeconds);
                Intensity = Mathf.Sin(Phase01 * Mathf.PI);                               // bell-shaped uterine pressure
                float bearing = Mathf.Clamp01((Phase01 - 0.15f) / 0.1f) * Mathf.Clamp01((0.85f - Phase01) / 0.1f);
                PushEffort = Mathf.MoveTowards(PushEffort, Panting ? 0.1f * bearing : bearing, dt * 3f);
                if (_t >= contractionSeconds) { Active = false; _t = 0f; }
            }
            UpdateFHR(dt);
        }

        void UpdateFHR(float dt)
        {
            float target = baselineFHR + 4f * Mathf.Sin(Time.time * 0.9f) + 2f * Mathf.Sin(Time.time * 2.3f);
            if (Active) target -= 18f * Intensity;                                        // early decelerations (head compression) mirror contractions
            if (Bradycardia) target = 88f + 3f * Mathf.Sin(Time.time);
            FetalHeartRate = Mathf.MoveTowards(FetalHeartRate, target, dt * 12f);
        }
    }
}
