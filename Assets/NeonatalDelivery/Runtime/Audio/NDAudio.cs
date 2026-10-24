// Spatial audio for the delivery: mother's breathing/pushing/relief, newborn cry, fetal heart doppler, instrument sounds.
// Clips are loaded from Resources/NeonatalDelivery/Audio (synthesised placeholders - replace with recorded audio freely).
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    public class NDAudio : MonoBehaviour
    {
        public static NDAudio Instance { get; private set; }
        public AudioSource motherVoice, babyVoice, monitor, ui;
        public AudioLowPassFilter motherLowPass;
        public float motherVolume = 1f, babyVolume = 1f, masterVolume = 1f;
        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        float _fhr = 140f, _fhrTimer;
        public bool fetalMonitorOn = true;

        void Awake()
        {
            Instance = this;
            foreach (var c in Resources.LoadAll<AudioClip>("NeonatalDelivery/Audio")) _clips[c.name] = c;
        }

        public static AudioSource MakeSource(Transform parent, string name, bool spatial)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var s = go.AddComponent<AudioSource>(); s.playOnAwake = false; s.spatialBlend = spatial ? 1f : 0f;
            s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = 0.4f; s.maxDistance = 12f; s.dopplerLevel = 0f;
            return s;
        }

        public AudioClip Clip(string n) { return _clips.TryGetValue(n, out var c) ? c : null; }

        public static void Play(string clip, Vector3 pos, float vol = 1f)
        {
            var a = Instance; if (a == null) return; var c = a.Clip(clip); if (c == null) return;
            AudioSource.PlayClipAtPoint(c, pos, vol * a.masterVolume);
        }

        public void Mother(string clip, float vol = 1f, bool interrupt = true)
        {
            if (motherVoice == null) return; var c = Clip(clip); if (c == null) return;
            if (!interrupt && motherVoice.isPlaying) return;
            motherVoice.clip = c; motherVoice.volume = vol * motherVolume * masterVolume; motherVoice.Play();
        }

        public void Baby(string clip, bool loop, float vol = 1f)
        {
            if (babyVoice == null) return; var c = Clip(clip); if (c == null) { babyVoice.Stop(); return; }
            if (babyVoice.clip == c && babyVoice.isPlaying) { babyVoice.volume = vol * babyVolume * masterVolume; return; }
            babyVoice.clip = c; babyVoice.loop = loop; babyVoice.volume = vol * babyVolume * masterVolume; babyVoice.Play();
        }
        public void StopBaby() { if (babyVoice != null) babyVoice.Stop(); }

        public void UI(string clip, float vol = 0.7f) { if (ui != null) { var c = Clip(clip); if (c != null) ui.PlayOneShot(c, vol * masterVolume); } }

        /// <summary>Fetal heart doppler: one "whoosh" per beat at the current rate.</summary>
        public void SetFetalHeartRate(float bpm) { _fhr = bpm; }

        void Update()
        {
            if (!fetalMonitorOn || monitor == null) return;
            _fhrTimer -= Time.deltaTime;
            if (_fhrTimer <= 0f)
            {
                _fhrTimer = 60f / Mathf.Max(40f, _fhr);
                var c = Clip("fhr_beat"); if (c != null) monitor.PlayOneShot(c, 0.35f * masterVolume);
            }
        }
    }
}
