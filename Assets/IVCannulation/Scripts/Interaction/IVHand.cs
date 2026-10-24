using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// Shared logic for a "hand" that can hover, grab, use and palpate.
    /// DesktopHand (mouse) and VRHand (controller) derive from it.
    /// </summary>
    public abstract class IVHand : MonoBehaviour
    {
        public static readonly List<IVHand> All = new List<IVHand>();

        public Grabbable Held { get; private set; }
        public Interactable Hovered { get; private set; }
        public abstract bool IsVR { get; }

        /// <summary>What the fingertip last felt (shown in the UI).</summary>
        public string PalpationText { get; protected set; }
        public float PalpationTime { get; protected set; }

        protected Interactable activeUse;
        protected bool palpating;
        protected float palpateHeld;
        protected Vector3 lastPalpatePoint;

        protected virtual void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable()
        {
            All.Remove(this);
            SetHovered(null);
            if (Held != null) Release();
        }

        public virtual void Haptic(float amplitude, float seconds) { }

        protected void SetHovered(Interactable i)
        {
            if (Hovered == i) return;
            if (Hovered != null) Hovered.SetHighlight(false);
            Hovered = i;
            if (Hovered != null) Hovered.SetHighlight(true);
        }

        public void Grab(Grabbable g)
        {
            if (g == null) return;
            if (Held != null) Release();
            if (g.Holder != null && g.Holder != this) g.Holder.ForceRelease();
            SetHovered(null);
            Held = g;
            g.OnGrab(this);
            Haptic(0.25f, 0.05f);
        }

        public void Release()
        {
            var g = Held;
            Held = null;
            if (g != null) g.OnRelease(this);
        }

        /// <summary>Drop the reference without telling the tool (used by resets).</summary>
        public void ForceRelease()
        {
            if (Held != null) Held.ClearHolder();
            Held = null;
            activeUse = null;
        }

        protected void BeginUse(Interactable target)
        {
            activeUse = target;
            if (activeUse != null) activeUse.OnUseStart(this);
        }

        protected void HoldUse(float dt)
        {
            if (activeUse != null) activeUse.OnUseHold(this, dt);
        }

        protected void EndUse()
        {
            if (activeUse != null) activeUse.OnUseEnd(this);
            activeUse = null;
        }

        /// <summary>Feeling the skin with a fingertip at a surface point.</summary>
        protected void Palpate(Vector3 surfacePoint, Vector3 normal, float dt)
        {
            var s = IVSession.Instance;
            if (s == null) return;
            if (!palpating || Vector3.Distance(surfacePoint, lastPalpatePoint) > 0.006f)
            {
                palpateHeld = 0f;
                lastPalpatePoint = surfacePoint;
            }
            palpating = true;
            palpateHeld += dt;
            PalpationText = s.Palpate(surfacePoint, normal, palpateHeld);
            PalpationTime = Time.time;

            var res = VascularSystem.Palpate(surfacePoint, normal);
            if (res.found)
            {
                if (res.vessel.vesselType == VesselType.Artery)
                {
                    float beat = Mathf.Repeat(Time.time * 1.2f, 1f);
                    if (beat < dt * 1.2f) Haptic(0.8f, 0.08f);
                }
                else
                {
                    float strength = Mathf.Clamp01(1f - res.lateralDistance / 0.006f) * (s.TourniquetOn ? 1f : 0.4f);
                    if (Time.frameCount % 6 == 0) Haptic(0.1f + 0.35f * strength, 0.03f);
                }
            }
        }

        protected void StopPalpating()
        {
            palpating = false;
            palpateHeld = 0f;
        }
    }
}
