using UnityEngine;

namespace IVSim
{
    public enum SwabState { Dry, Wet, Used }

    /// <summary>
    /// Cotton-tipped swab. Touch it to the alcohol bottle to wet it, then rub the skin around the chosen site.
    /// The root's functionalPoint should be the centre of the cotton tip; local +Z points from stick to tip.
    /// </summary>
    public class Swab : Grabbable
    {
        public MeshRenderer cottonRenderer;
        public Material dryMaterial;
        public Material wetMaterial;
        public Material usedMaterial;
        [Tooltip("Distance from the skin that counts as contact (m)")] public float contactDistance = 0.005f;
        [Tooltip("Radius of the cotton tip (m)")] public float tipRadius = 0.004f;

        public SwabState State { get; private set; }
        public bool InContact { get; private set; }

        Vector3 lastContact;
        bool hadContact;
        bool desktopScrubbing;

        protected override void Start()
        {
            base.Start();
            SetState(SwabState.Dry);
        }

        public void SetState(SwabState s)
        {
            State = s;
            if (cottonRenderer == null) return;
            Material m = s == SwabState.Wet ? wetMaterial : s == SwabState.Used ? usedMaterial : dryMaterial;
            if (m != null) cottonRenderer.sharedMaterial = m;
            RefreshHighlight();
        }

        public void Wet()
        {
            var session = IVSession.Instance;
            if (State == SwabState.Used)
            {
                if (session != null) session.FeedbackOnce("usedswab", "This swab has been used. Each swab is single-use.", Severity.Info, FeedbackKind.Guidance, 3f);
                return;
            }
            if (State != SwabState.Wet)
            {
                SetState(SwabState.Wet);
                if (Holder != null) Holder.Haptic(0.2f, 0.05f);
                if (session != null) session.Feedback("Swab soaked with alcohol antiseptic.", Severity.Info, FeedbackKind.Event);
            }
        }

        public override void DriveFromVR(Vector3 rootPos, Quaternion rootRot, VRHand hand)
        {
            transform.SetPositionAndRotation(rootPos, rootRot);
            var bottle = AlcoholBottle.Instance;
            if (bottle != null && bottle.IsAtMouth(Tip.position)) Wet();
            ContactCheck(Time.deltaTime);
        }

        public override void DriveFromDesktop(DesktopHand hand)
        {
            Vector3 n = hand.HoverOnSkin ? hand.HoverNormal : Vector3.up;
            Vector3 toCam = hand.cam != null ? (hand.cam.transform.position - hand.HoverPoint).normalized : Vector3.up;
            // stick leans back toward the viewer, cotton tip points into the skin
            Vector3 tipDir = (-n * 0.8f - toCam * 0.25f).normalized;
            Vector3 upHint = Vector3.Cross(tipDir, hand.cam != null ? hand.cam.transform.right : Vector3.right);
            if (upHint.sqrMagnitude < 1e-6f) upHint = Vector3.up;
            Quaternion rot = Quaternion.LookRotation(tipDir, upHint);
            float lift = hand.HoverOnSkin && desktopScrubbing ? tipRadius * 0.6f : 0.012f;
            PlaceFunctionalPoint(hand.HoverPoint + n * lift, rot);
            ContactCheck(Time.deltaTime);
        }

        public override void OnHeldUseStart(IVHand hand) { desktopScrubbing = true; }
        public override void OnHeldUseEnd(IVHand hand) { desktopScrubbing = false; }

        void ContactCheck(float dt)
        {
            InContact = false;
            var arm = ArmModel.Instance;
            var cleaning = IVSession.Instance != null ? IVSession.Instance.cleaning : null;
            if (arm == null) return;
            Vector3 sp, n; float sd;
            if (!arm.ProbeSurface(Tip.position, 0.03f, out sp, out n, out sd)) { hadContact = false; return; }
            if (sd > tipRadius + contactDistance) { hadContact = false; return; }
            InContact = true;

            var s = IVSession.Instance;
            if (State == SwabState.Dry)
            {
                if (s != null) s.FeedbackOnce("dryswab", "Wet the swab with alcohol first (touch it to the bottle).", Severity.Info, FeedbackKind.Guidance, 3f);
                hadContact = false;
                return;
            }
            if (State == SwabState.Used) { hadContact = false; return; }
            if (s == null || !s.CanClean()) { hadContact = false; return; }

            float speed = hadContact && dt > 0f ? Vector3.Distance(sp, lastContact) / dt : 0f;
            lastContact = sp;
            hadContact = true;
            if (cleaning != null)
            {
                cleaning.Scrub(sp, n, speed, dt);
                if (cleaning.IsComplete) SetState(SwabState.Used);
            }
            if (Holder != null && speed > 0.02f && Time.frameCount % 5 == 0) Holder.Haptic(0.08f, 0.02f);
        }

        public override void ResetForSession()
        {
            base.ResetForSession();
            SetState(SwabState.Dry);
            desktopScrubbing = false;
            hadContact = false;
        }

        public override void OnRelease(IVHand hand)
        {
            desktopScrubbing = false;
            base.OnRelease(hand);
            // a used swab is replaced by a fresh one when it goes back to the tray
            if (State == SwabState.Used) SetState(SwabState.Dry);
        }
    }
}
