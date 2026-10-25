using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// Mouse + keyboard "hand" for testing without a headset.
    ///  Left click        pick up a tool / use an object / (empty hand on skin) palpate
    ///  Left hold         swab: scrub   |  syringe/gauze: hold
    ///  Right click / G   put the tool back
    ///  Cannula: W/S angle, A/D direction, Q/E bevel roll, Space advance, X withdraw, F advance catheter, Shift = fine
    /// </summary>
    public class DesktopHand : IVHand
    {
        public Camera cam;
        public override bool IsVR { get { return false; } }

        public Vector3 HoverPoint { get; private set; }
        public Vector3 HoverNormal { get; private set; }
        public bool HoverOnSkin { get; private set; }

        Vector2 rightDownPos;
        bool leftHeldOnHeld;
        readonly List<RaycastHit> hitBuffer = new List<RaycastHit>();

        void Update()
        {
            if (cam == null || !cam.isActiveAndEnabled) return;
            float dt = Time.deltaTime;
            ComputeHover();
            HandleKeys();

            // ---- left button
            if (IVInput.MouseButtonDown(0))
            {
                if (Held == null)
                {
                    var g = Hovered as Grabbable;
                    // clicking on or near a cannula that is in the arm picks it up again
                    var inArm = InsertedCannulaNear(HoverPoint);
                    if (g == null && inArm != null) g = inArm;
                    if (g != null && g.CanGrab(this)) Grab(g);
                    else if (Hovered != null && Hovered.IsUsable) BeginUse(Hovered);
                    else if (HoverOnSkin) { palpating = false; Palpate(HoverPoint, HoverNormal, 0f); }
                }
                else
                {
                    if (Hovered != null && Hovered.IsUsable && !(Hovered is Grabbable)) BeginUse(Hovered);
                    else { Held.OnHeldUseStart(this); leftHeldOnHeld = true; }
                }
            }
            if (IVInput.MouseButton(0))
            {
                if (activeUse != null) HoldUse(dt);
                else if (Held == null && HoverOnSkin) Palpate(HoverPoint, HoverNormal, dt);
                else if (Held != null && leftHeldOnHeld) Held.OnHeldUseHold(this, dt);
            }
            if (IVInput.MouseButtonUp(0))
            {
                EndUse();
                StopPalpating();
                if (Held != null && leftHeldOnHeld) Held.OnHeldUseEnd(this);
                leftHeldOnHeld = false;
            }

            // ---- right click (without dragging) puts the tool down - but never drops a cannula that is in the arm
            // (right-dragging to look around was letting go of it by accident). Use G to let go of an inserted cannula.
            if (IVInput.MouseButtonDown(1)) rightDownPos = IVInput.MousePosition;
            bool heldInArm = Held is Cannula && ((Cannula)Held).IsInSkin;
            if (IVInput.MouseButtonUp(1) && (IVInput.MousePosition - rightDownPos).magnitude < 6f && Held != null && !heldInArm) Release();
            if ((IVInput.GetKeyDown(IVKey.G) || IVInput.GetKeyDown(IVKey.Escape)) && Held != null) Release();

            if (Held != null) Held.DriveFromDesktop(this);
        }

        static Cannula InsertedCannulaNear(Vector3 p)
        {
            var s = IVSession.Instance;
            var c = s != null ? s.cannula : null;
            if (c == null || !c.IsInSkin || c.IsHeld) return null;
            float d = Mathf.Min(Vector3.Distance(p, c.EntryPoint), Vector3.Distance(p, c.transform.position - c.transform.forward * 0.04f));
            return d < 0.035f ? c : null;
        }

        void HandleKeys()
        {
            var s = IVSession.Instance;
            if (s == null) return;
            if (IVInput.GetKeyDown(IVKey.H)) s.HintsVisible = !s.HintsVisible;
            if (s.State != SessionState.Running)
            {
                if (IVInput.GetKeyDown(IVKey.Alpha1)) s.StartSession(Difficulty.Easy);
                if (IVInput.GetKeyDown(IVKey.Alpha2)) s.StartSession(Difficulty.Medium);
                if (IVInput.GetKeyDown(IVKey.Alpha3)) s.StartSession(Difficulty.Hard);
            }
        }

        void ComputeHover()
        {
            Ray ray = cam.ScreenPointToRay(IVInput.MousePosition);
            var hits = Physics.RaycastAll(ray, 5f, ~0, QueryTriggerInteraction.Collide);
            hitBuffer.Clear();
            hitBuffer.AddRange(hits);
            hitBuffer.Sort((a, b) => a.distance.CompareTo(b.distance));

            var arm = ArmModel.Instance;
            Interactable found = null;
            bool gotSurface = false;
            HoverOnSkin = false;

            foreach (var h in hitBuffer)
            {
                if (Held != null && h.collider.transform.IsChildOf(Held.transform)) continue;
                if (arm != null && h.collider == arm.skinCollider)
                {
                    HoverOnSkin = true;
                    HoverPoint = h.point;
                    HoverNormal = h.normal;
                    gotSurface = true;
                    break;
                }
                var it = h.collider.GetComponentInParent<Interactable>();
                if (it != null)
                {
                    if (!it.IsAvailable) continue;
                    found = it;
                    HoverPoint = h.point;
                    HoverNormal = Vector3.up;
                    gotSurface = true;
                    break;
                }
                if (h.collider.isTrigger) continue;
                HoverPoint = h.point;
                HoverNormal = h.normal;
                gotSurface = true;
                break;
            }
            if (!gotSurface)
            {
                HoverPoint = ray.origin + ray.direction * 0.6f;
                HoverNormal = Vector3.up;
            }
            SetHovered(found);
        }
    }
}
