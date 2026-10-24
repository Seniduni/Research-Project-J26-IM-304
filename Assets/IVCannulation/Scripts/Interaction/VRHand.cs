using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace IVSim
{
    /// <summary>
    /// A VR controller hand driven directly by UnityEngine.XR.InputDevices (works with OpenXR on Quest / PC VR,
    /// no XR Interaction Toolkit required). Must be a child of the XR rig root (tracking space).
    ///  Grip         grab / release tools
    ///  Trigger      use the object you are touching, palpate the skin (empty hand),
    ///               advance the catheter (while holding the inserted cannula), hold syringes/gauze
    ///  A / X        show / hide hints
    ///  B / Y        recentre the view at the bedside
    /// </summary>
    public class VRHand : IVHand
    {
        public XRNode node = XRNode.RightHand;
        [Tooltip("Fingertip / pinch point used for touching, poking and palpating")] public Transform interactionPoint;
        public float hoverRadius = 0.045f;
        public MeshRenderer[] handRenderers;
        public Material bareMaterial;
        public Material gloveMaterial;
        public XRRigController rig;

        public override bool IsVR { get { return true; } }
        public bool Tracked { get; private set; }
        public float Trigger { get; private set; }
        public float Grip { get; private set; }
        public Vector3 PokePoint { get { return interactionPoint != null ? interactionPoint.position : transform.position; } }

        InputDevice device;
        bool gripDown, triggerDown, primaryPrev, secondaryPrev;
        bool lastGloves;
        Vector3 grabLocalPos;
        Quaternion grabLocalRot = Quaternion.identity;
        readonly Collider[] overlap = new Collider[32];

        protected override void OnEnable()
        {
            base.OnEnable();
            lastGloves = !lastGloves;   // force a visual refresh
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (!device.isValid) device = InputDevices.GetDeviceAtXRNode(node);

            Vector3 pos; Quaternion rot;
            Tracked = device.isValid
                      && device.TryGetFeatureValue(CommonUsages.devicePosition, out pos)
                      && device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
            if (Tracked)
            {
                device.TryGetFeatureValue(CommonUsages.devicePosition, out pos);
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out rot);
                transform.localPosition = pos;
                transform.localRotation = rot;
            }

            float grip = 0f, trig = 0f;
            bool primary = false, secondary = false;
            if (device.isValid)
            {
                device.TryGetFeatureValue(CommonUsages.grip, out grip);
                device.TryGetFeatureValue(CommonUsages.trigger, out trig);
                device.TryGetFeatureValue(CommonUsages.primaryButton, out primary);
                device.TryGetFeatureValue(CommonUsages.secondaryButton, out secondary);
            }
            Grip = grip;
            Trigger = trig;

            UpdateGloves();

            var s = IVSession.Instance;
            if (primary && !primaryPrev && s != null) s.HintsVisible = !s.HintsVisible;
            if (secondary && !secondaryPrev && rig != null) rig.Recenter();
            primaryPrev = primary;
            secondaryPrev = secondary;

            UpdateHover();

            // ---- grip: grab / release
            if (!gripDown && grip > 0.6f)
            {
                gripDown = true;
                var g = Hovered as Grabbable;
                if (Held == null && g != null && g.CanGrab(this))
                {
                    grabLocalPos = Quaternion.Inverse(transform.rotation) * (g.transform.position - transform.position);
                    grabLocalRot = Quaternion.Inverse(transform.rotation) * g.transform.rotation;
                    Grab(g);
                }
            }
            else if (gripDown && grip < 0.35f)
            {
                gripDown = false;
                if (Held != null) Release();
            }

            if (Held != null)
            {
                Held.DriveFromVR(transform.position + transform.rotation * grabLocalPos, transform.rotation * grabLocalRot, this);
            }

            // ---- trigger: use / palpate
            if (!triggerDown && trig > 0.55f)
            {
                triggerDown = true;
                if (Hovered != null && Hovered.IsUsable && !(Hovered is Grabbable)) BeginUse(Hovered);
                else if (Held != null) Held.OnHeldUseStart(this);
            }
            else if (triggerDown && trig < 0.3f)
            {
                triggerDown = false;
                EndUse();
                if (Held != null) Held.OnHeldUseEnd(this);
                StopPalpating();
            }
            if (triggerDown)
            {
                if (activeUse != null) HoldUse(dt);
                else if (Held == null) TryPalpate(dt);
                else Held.OnHeldUseHold(this, dt);
            }
        }

        void TryPalpate(float dt)
        {
            var arm = ArmModel.Instance;
            if (arm == null) return;
            Vector3 sp, n; float sd;
            if (arm.ProbeSurface(PokePoint, 0.03f, out sp, out n, out sd) && sd < 0.015f)
                Palpate(sp, n, dt);
            else
                StopPalpating();
        }

        void UpdateHover()
        {
            Vector3 p = Held != null ? Held.Tip.position : PokePoint;
            int count = Physics.OverlapSphereNonAlloc(p, hoverRadius, overlap, ~0, QueryTriggerInteraction.Collide);
            Interactable best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var c = overlap[i];
                if (c == null) continue;
                if (Held != null && c.transform.IsChildOf(Held.transform)) continue;
                var it = c.GetComponentInParent<Interactable>();
                if (it == null || !it.IsAvailable) continue;
                if (Held != null && (it.IsGrabbable || !it.IsUsable)) continue;   // holding: only usable targets (bottle, bin)
                if (Held == null && !it.IsGrabbable && !it.IsUsable) continue;
                float d = (c.ClosestPoint(p) - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = it; }
            }
            SetHovered(best);
        }

        void UpdateGloves()
        {
            var s = IVSession.Instance;
            bool gloves = s != null && s.GlovesOn;
            if (gloves == lastGloves) return;
            lastGloves = gloves;
            if (handRenderers == null) return;
            var m = gloves ? gloveMaterial : bareMaterial;
            if (m == null) return;
            foreach (var r in handRenderers) if (r != null) r.sharedMaterial = m;
        }

        public override void Haptic(float amplitude, float seconds)
        {
            if (device.isValid) device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), seconds);
        }
    }
}
