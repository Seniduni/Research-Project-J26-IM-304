using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// A tool that can be picked up. When released it glides back to its place on the tray
    /// (no physics needed, which keeps things predictable for training).
    /// </summary>
    public class Grabbable : Interactable, IResettable
    {
        [Tooltip("The working end of the tool (needle tip, swab tip...). Defaults to this transform.")]
        public Transform functionalPoint;
        [Tooltip("Height the functional point hovers above surfaces in desktop mode (m)")]
        public float desktopLift = 0.02f;

        protected Vector3 homePos;
        protected Quaternion homeRot;

        public IVHand Holder { get; private set; }
        public bool IsHeld { get { return Holder != null; } }
        public override bool IsGrabbable { get { return true; } }
        public override bool IsUsable { get { return false; } }

        public Transform Tip { get { return functionalPoint != null ? functionalPoint : transform; } }

        protected override void Awake()
        {
            base.Awake();
            homePos = transform.position;
            homeRot = transform.rotation;
        }

        protected virtual void Start()
        {
            if (IVSession.Instance != null) IVSession.Instance.RegisterResettable(this);
        }

        public virtual bool CanGrab(IVHand hand) { return true; }

        public virtual void OnGrab(IVHand hand)
        {
            StopMove();
            Holder = hand;
        }

        public virtual void OnRelease(IVHand hand)
        {
            Holder = null;
            ReturnHome();
        }

        /// <summary>Clears the holder without callbacks (used when resetting).</summary>
        public void ClearHolder() { Holder = null; }

        public void ReturnHome(float seconds = 0.45f)
        {
            MoveTo(transform, homePos, homeRot, seconds);
        }

        public void SetHome(Vector3 pos, Quaternion rot) { homePos = pos; homeRot = rot; }

        /// <summary>VR: the hand provides the pose this tool's root should take.</summary>
        public virtual void DriveFromVR(Vector3 rootPos, Quaternion rootRot, VRHand hand)
        {
            transform.SetPositionAndRotation(rootPos, rootRot);
        }

        /// <summary>Desktop: put the functional point above the mouse hover point.</summary>
        public virtual void DriveFromDesktop(DesktopHand hand)
        {
            Vector3 target = hand.HoverPoint + hand.HoverNormal * desktopLift;
            Quaternion rot = transform.rotation;
            PlaceFunctionalPoint(target, rot);
        }

        /// <summary>Moves the root so the functional point lands at 'tipPos' with the root rotation 'rot'.</summary>
        public void PlaceFunctionalPoint(Vector3 tipPos, Quaternion rot)
        {
            Vector3 localTip = transform.InverseTransformPoint(Tip.position);
            Vector3 offset = rot * Vector3.Scale(localTip, transform.lossyScale);
            transform.SetPositionAndRotation(tipPos - offset, rot);
        }

        public virtual void OnHeldUseStart(IVHand hand) { }
        public virtual void OnHeldUseHold(IVHand hand, float dt) { }
        public virtual void OnHeldUseEnd(IVHand hand) { }

        public virtual void ResetForSession()
        {
            if (Holder != null) Holder.ForceRelease();
            Holder = null;
            StopMove();
            transform.SetPositionAndRotation(homePos, homeRot);
        }
    }
}
