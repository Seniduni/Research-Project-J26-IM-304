using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    public enum CannulaPhase { Ready, InSkin, NeedleOnly, Disposed }

    /// <summary>
    /// Peripheral IV cannula (catheter-over-needle). The root transform's origin is the NEEDLE TIP and +Z is the
    /// direction of travel, local +Y is the bevel-up side.
    ///
    /// How insertion works:
    ///  1. While above the skin the cannula follows the hand freely. The live angle to the skin is measured.
    ///  2. When the tip is pushed through the skin along its axis it becomes constrained to that entry point:
    ///     the learner can push/pull along the needle axis and pivot (lower/raise the angle) around the entry point.
    ///  3. Each frame the tip is tested against every vein's lumen: entering = flashback; leaving through the far
    ///     side = back-wall puncture (haematoma); entering an artery = arterial puncture.
    ///  4. After flashback, lowering the angle and advancing 1-2 mm brings the catheter tip (1.5 mm behind the needle
    ///     tip) into the lumen; the catheter can then be slid off the needle and bends along the vein.
    ///  5. Pulling the needle back out leaves the catheter in the arm.
    /// </summary>
    public class Cannula : Grabbable
    {
        [Header("Parts")]
        public Transform needleAssembly;
        public Transform catheterAssembly;
        public MeshFilter catheterTube;
        public Transform flashbackFill;
        public MeshRenderer flashbackRenderer;
        public GameObject hubBlood;
        [Tooltip("Rear opening of the catheter hub (syringes connect here). Child of catheterAssembly.")]
        public Transform hubPort;
        public ProcedureAction removeAction;
        public Material bloodDark;
        public Material bloodBright;

        [Header("Dimensions (m) - 20G, 32 mm")]
        public float needleLength = 0.035f;
        public float catheterLength = 0.032f;
        public float tipGap = 0.0015f;
        public float catheterRadius = 0.00055f;

        [Header("Feel")]
        [Tooltip("VR: fraction of hand movement that becomes needle movement once in the skin (finer control)")]
        public float vrDepthScale = 0.6f;
        public float maxAdvanceSpeed = 0.03f;
        public float maxTurnSpeed = 60f;
        public float threadSpeed = 0.012f;
        [Tooltip("Depth without flashback after which a 'missed vein' hint appears")]
        public float missDepth = 0.012f;

        // ---------------- state
        public CannulaPhase Phase { get; private set; }
        public bool IsInSkin { get { return Phase == CannulaPhase.InSkin; } }
        public bool IsUsed { get; private set; }
        public bool CatheterPlaced { get; private set; }
        public bool CatheterInVein { get; private set; }
        public bool Flashback { get { return flashback; } }
        public bool InLumen { get { return inLumen; } }
        public bool Transfixed { get { return transfixed; } }
        public float Depth { get { return depth; } }
        public float ThreadFraction { get { float m = SMax(); return m > 0f ? Mathf.Clamp01(threadS / m) : 0f; } }
        public Vector3 EntryPoint { get { return entryPoint; } }
        public Vector3 EntryNormal { get { return entryNormal; } }
        public Vector3 PlacedDirection { get { return placedDir; } }

        // live readouts for UI / gauge
        public bool LiveValid { get; private set; }
        public float LiveAngle { get; private set; }
        public float LiveRoll { get; private set; }
        public Vector3 LivePoint { get; private set; }
        public Vector3 LiveNormal { get; private set; }
        public float LiveDistance { get; private set; }

        Vector3 entryPoint, entryNormal, entryFlat, dir, placedDir;
        float depth, prevDepth;
        bool flashback, inLumen, transfixed, arteryHit, missedReported, loweredReported;
        float flashAngle, flashDepth;
        VeinPath lumenVessel;
        VeinPath.Hit lastHit;
        float threadS, threadDepth;
        bool threadStarted, threadComplete, catheterInVeinFlag;
        VeinPath catheterVessel;
        float flashFill, flashTarget;
        Mesh catheterMesh;
        Vector3 catheterHomeLocalPos;
        Quaternion catheterHomeLocalRot;
        Transform catheterHomeParent;

        // lower-and-advance (step 7) tracking
        Vector3 flashTip;
        bool catheterTipInsideNow;

        /// <summary>True while the learner should lower the angle and advance 1-2 mm (after flashback).</summary>
        public bool AwaitingLowerAdvance { get { return Phase == CannulaPhase.InSkin && flashback && !loweredReported && !transfixed && !arteryHit && threadS <= 0f; } }
        public bool LowerAdvanceDone { get { return loweredReported; } }
        public float FlashAngle { get { return flashAngle; } }
        public float LowerTargetAngle
        {
            get
            {
                var s = IVSession.Instance;
                float by = s != null ? s.clinical.lowerAngleBy : 4f;
                return Mathf.Max(5f, flashAngle - by);
            }
        }
        public bool IsLowered { get { return flashback && (CurrentAngle <= LowerTargetAngle + 0.05f || CurrentAngle <= 12f); } }
        /// <summary>How far (m) the needle tip has moved forward since flashback.</summary>
        public float AdvanceSinceFlash { get { return flashback && Phase == CannulaPhase.InSkin ? Mathf.Max(0f, Vector3.Dot(transform.position - flashTip, dir)) : 0f; } }
        /// <summary>Advance needed after flashback: at least the 1.5 mm gap between needle tip and catheter tip (= "advance 1-2 mm").</summary>
        public float AdvanceTarget { get { var s = IVSession.Instance; return Mathf.Max(s != null ? s.clinical.advanceAfterFlash : 0.0008f, tipGap); } }
        public bool CatheterTipInside { get { return catheterTipInsideNow; } }

        // VR: hand movement is mapped relative to where it was when the needle entered / was re-grabbed
        bool vrAnchored;
        float vrAnchorDepth, vrAnchorHand;

        // desktop control state
        float deskPitch = 25f, deskYaw, deskRoll, deskPush;

        protected override void Awake()
        {
            base.Awake();
            if (catheterAssembly != null)
            {
                catheterHomeParent = catheterAssembly.parent;
                catheterHomeLocalPos = catheterAssembly.localPosition;
                catheterHomeLocalRot = catheterAssembly.localRotation;
            }
            catheterMesh = new Mesh();
            catheterMesh.name = "CatheterTube_runtime";
            if (catheterTube != null) catheterTube.sharedMesh = catheterMesh;
            BuildStraightCatheter();
        }

        public override bool IsAvailable { get { return isActiveAndEnabled && Phase != CannulaPhase.Disposed; } }
        public override bool CanGrab(IVHand hand) { return Phase != CannulaPhase.Disposed; }

        public override string HoverText(IVHand hand)
        {
            if (IsUsed && Phase != CannulaPhase.InSkin) return "Used needle - dispose in the sharps container";
            return base.HoverText(hand);
        }

        // =====================================================================================
        // Driving
        // =====================================================================================

        public override void DriveFromVR(Vector3 rootPos, Quaternion rootRot, VRHand hand)
        {
            float dt = Time.deltaTime;
            if (Phase == CannulaPhase.InSkin)
            {
                Vector3 desiredDir = rootRot * Vector3.forward;
                Vector3 c = ConstrainDir(desiredDir);
                float handProj = Vector3.Dot(rootPos - entryPoint, c);
                if (!vrAnchored) { vrAnchorDepth = depth; vrAnchorHand = handProj; vrAnchored = true; }
                float raw = vrAnchorDepth + (handProj - vrAnchorHand) * vrDepthScale;
                UpdateInserted(desiredDir, raw, rootRot * Vector3.up, dt, true);
                if (hand.Trigger > 0.15f && Phase == CannulaPhase.InSkin) AdvanceCatheter(hand.Trigger * threadSpeed * dt);
            }
            else
            {
                ApplyFreePose(rootPos, rootRot, true);
            }
        }

        public override void DriveFromDesktop(DesktopHand hand)
        {
            float dt = Time.deltaTime;
            float slow = IVInput.GetKey(IVKey.LeftShift) ? 0.35f : 1f;
            bool fine = Phase == CannulaPhase.InSkin && flashback;          // after flashback: slower, precise control
            float pitchSpeed = fine ? 12f : 30f;
            float advSpeed = fine ? 0.0015f : 0.006f;
            float dPitch = ((IVInput.GetKey(IVKey.W) ? 1f : 0f) - (IVInput.GetKey(IVKey.S) ? 1f : 0f)) * pitchSpeed * dt * slow;
            float dYaw = ((IVInput.GetKey(IVKey.D) ? 1f : 0f) - (IVInput.GetKey(IVKey.A) ? 1f : 0f)) * 50f * dt * slow;
            float dRoll = ((IVInput.GetKey(IVKey.E) ? 1f : 0f) - (IVInput.GetKey(IVKey.Q) ? 1f : 0f)) * 90f * dt * slow;
            float adv = ((IVInput.GetKey(IVKey.Space) ? 1f : 0f) - (IVInput.GetKey(IVKey.X) ? 1f : 0f)) * advSpeed * dt * slow;

            if (Phase == CannulaPhase.InSkin)
            {
                DesktopInsertedControl(dPitch, dYaw, dRoll, adv, dt);
                if (IVInput.GetKey(IVKey.F) && Phase == CannulaPhase.InSkin) AdvanceCatheter(threadSpeed * dt);
                deskPitch = CurrentAngle;
                return;
            }

            deskPitch = Mathf.Clamp(deskPitch + dPitch, 0f, 85f);
            deskYaw = Mathf.Repeat(deskYaw + dYaw + 180f, 360f) - 180f;
            deskRoll = Mathf.Repeat(deskRoll + dRoll + 180f, 360f) - 180f;
            if (adv > 0f) deskPush += adv; else deskPush = Mathf.Max(0f, deskPush + adv);

            Vector3 n = hand.HoverOnSkin ? hand.HoverNormal : Vector3.up;
            Vector3 A = ArmModel.Instance != null ? ArmModel.Instance.ProximalDirection : Vector3.forward;
            Vector3 flat = A - Vector3.Dot(A, n) * n;
            if (flat.sqrMagnitude < 1e-6f) flat = Vector3.Cross(n, Vector3.right);
            flat = Quaternion.AngleAxis(deskYaw, n) * flat.normalized;
            float pr = deskPitch * Mathf.Deg2Rad;
            Vector3 d = (Mathf.Cos(pr) * flat - Mathf.Sin(pr) * n).normalized;
            Quaternion rot = Quaternion.AngleAxis(deskRoll, d) * Quaternion.LookRotation(d, n);
            float lift = hand.HoverOnSkin ? 0.0025f : 0.03f;
            if (!hand.HoverOnSkin) deskPush = 0f;
            Vector3 tip = hand.HoverPoint + n * lift + d * deskPush;
            ApplyFreePose(tip, rot, deskPush > 0f && adv > 0f);
            if (Phase == CannulaPhase.InSkin) deskPush = 0f;
        }

        public float DesktopPitch { get { return deskPitch; } }

        public override void OnGrab(IVHand hand)
        {
            base.OnGrab(hand);
            deskPitch = 25f; deskYaw = 0f; deskRoll = 0f; deskPush = 0f;
            vrAnchored = false;
        }

        public override void OnRelease(IVHand hand)
        {
            if (Phase == CannulaPhase.InSkin)
            {
                // stays where it is in the arm; it can be grabbed again
                base.ClearHolder();
                return;
            }
            if (IsUsed)
            {
                var bin = SharpsBin.Instance;
                if (bin != null && bin.IsNear(transform.position))
                {
                    base.ClearHolder();
                    Dispose();
                    return;
                }
                if (IVSession.Instance != null && IVSession.Instance.State == SessionState.Running) IVSession.Instance.OnUnsafeSharps();
            }
            base.OnRelease(hand);
        }

        // =====================================================================================
        // Free movement (above the skin)
        // =====================================================================================

        public void ApplyFreePose(Vector3 tipTarget, Quaternion rot, bool allowPierce)
        {
            var arm = ArmModel.Instance;
            Vector3 prev = transform.position;
            Vector3 move = tipTarget - prev;
            Vector3 d = rot * Vector3.forward;

            if (arm != null && move.sqrMagnitude > 1e-10f && move.magnitude < 0.08f)
            {
                RaycastHit hit;
                if (arm.RaycastSkin(new Ray(prev, move.normalized), move.magnitude, out hit))
                {
                    bool axial = Vector3.Angle(move, d) < 60f;
                    bool pointsIn = Vector3.Dot(d, hit.normal) < -Mathf.Sin(2f * Mathf.Deg2Rad);
                    var s = IVSession.Instance;
                    if (allowPierce && Phase == CannulaPhase.Ready && axial && pointsIn && s != null && s.CanPierce(this))
                    {
                        BeginInsertion(hit, d, rot, tipTarget);
                        return;
                    }
                    tipTarget = hit.point + hit.normal * 0.0008f;
                }
            }

            // guard against ending up inside the arm without a valid crossing (fast hand moves, grabs)
            if (arm != null)
            {
                float x, th, r;
                arm.ToArmCoords(tipTarget, out x, out th, out r);
                if (x > 0.005f && x < arm.handEndX - 0.01f && r < 0.985f)
                {
                    Vector3 n;
                    Vector3 sp = arm.ProjectToSurface(tipTarget, out n);
                    tipTarget = sp + n * 0.0008f;
                }
            }

            transform.SetPositionAndRotation(tipTarget, rot);
            UpdateLiveReadout(rot);
        }

        void UpdateLiveReadout(Quaternion rot)
        {
            LiveValid = false;
            var arm = ArmModel.Instance;
            if (arm == null) return;
            Vector3 d = rot * Vector3.forward;
            RaycastHit hit;
            Vector3 origin = transform.position - d * 0.02f;
            if (arm.RaycastSkin(new Ray(origin, d), 0.07f, out hit))
            {
                LiveValid = true;
                LivePoint = hit.point;
                LiveNormal = hit.normal;
                LiveAngle = Mathf.Asin(Mathf.Clamp(-Vector3.Dot(d, hit.normal), -1f, 1f)) * Mathf.Rad2Deg;
                LiveRoll = ComputeRoll(rot, hit.normal);
                LiveDistance = hit.distance - 0.02f;
            }
        }

        static float ComputeRoll(Quaternion rot, Vector3 n)
        {
            Vector3 d = rot * Vector3.forward;
            Vector3 up = rot * Vector3.up;
            Vector3 nPerp = n - Vector3.Dot(n, d) * d;
            if (nPerp.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(nPerp.normalized, up, d);
        }

        // =====================================================================================
        // Insertion
        // =====================================================================================

        void BeginInsertion(RaycastHit hit, Vector3 d, Quaternion rot, Vector3 tipTarget)
        {
            Phase = CannulaPhase.InSkin;
            IsUsed = true;
            vrAnchored = false;
            entryPoint = hit.point;
            entryNormal = hit.normal;
            Vector3 flat = d - Vector3.Dot(d, entryNormal) * entryNormal;
            entryFlat = flat.sqrMagnitude > 1e-6f ? flat.normalized : Vector3.Cross(entryNormal, Vector3.right).normalized;
            dir = d;
            depth = Mathf.Clamp(Vector3.Dot(tipTarget - entryPoint, dir), 0.0002f, 0.004f);
            prevDepth = depth;
            flashback = inLumen = transfixed = arteryHit = missedReported = loweredReported = false;
            lumenVessel = null;
            threadS = 0f; threadStarted = threadComplete = false; catheterInVeinFlag = false;
            flashFill = flashTarget = 0f;
            SetFlashMaterial(false);
            UpdateFlashVisual();

            float angle = Mathf.Asin(Mathf.Clamp(-Vector3.Dot(dir, entryNormal), -1f, 1f)) * Mathf.Rad2Deg;
            float roll = ComputeRoll(rot, entryNormal);
            transform.SetPositionAndRotation(entryPoint + dir * depth, rot);
            if (Holder != null) Holder.Haptic(0.35f, 0.04f);
            if (IVSession.Instance != null) IVSession.Instance.OnPierce(entryPoint, entryNormal, angle, roll);
            LiveValid = true; LivePoint = entryPoint; LiveNormal = entryNormal; LiveAngle = angle; LiveRoll = roll;
            UpdateTissue(transform.position);
        }

        public float CurrentAngle
        {
            get
            {
                if (Phase != CannulaPhase.InSkin) return LiveAngle;
                return Mathf.Asin(Mathf.Clamp(-Vector3.Dot(dir, entryNormal), -1f, 1f)) * Mathf.Rad2Deg;
            }
        }

        /// <summary>
        /// Turns 'from' toward 'to' by at most maxRad radians. (Unity's Vector3.RotateTowards ignores turns smaller than
        /// ~0.26 degrees when maxMagnitudeDelta is 0, which froze small per-frame angle changes from the W/S keys.)
        /// </summary>
        static Vector3 TurnTowards(Vector3 from, Vector3 to, float maxRad)
        {
            from = from.normalized; to = to.normalized;
            float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(from, to), -1f, 1f));
            if (ang <= maxRad || ang < 1e-6f) return to;
            Vector3 axis = Vector3.Cross(from, to);
            if (axis.sqrMagnitude < 1e-12f) return to;
            return (Quaternion.AngleAxis(maxRad * Mathf.Rad2Deg, axis) * from).normalized;
        }

        Vector3 ConstrainDir(Vector3 d)
        {
            Vector3 n = entryNormal;
            float along = Mathf.Clamp(-Vector3.Dot(d, n), -1f, 1f);
            float angle = Mathf.Clamp(Mathf.Asin(along) * Mathf.Rad2Deg, 0.5f, 75f);
            Vector3 flat = d - Vector3.Dot(d, n) * n;
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : entryFlat;
            float yaw = Mathf.Clamp(Vector3.SignedAngle(entryFlat, flat, n), -25f, 25f);
            flat = Quaternion.AngleAxis(yaw, n) * entryFlat;
            float ar = angle * Mathf.Deg2Rad;
            return (Mathf.Cos(ar) * flat - Mathf.Sin(ar) * n).normalized;
        }

        void DesktopInsertedControl(float dPitch, float dYaw, float dRoll, float dDepth, float dt)
        {
            Vector3 n = entryNormal;
            float a = CurrentAngle + dPitch;
            Vector3 flat = dir - Vector3.Dot(dir, n) * n;
            flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : entryFlat;
            flat = Quaternion.AngleAxis(dYaw, n) * flat;
            float ar = a * Mathf.Deg2Rad;
            Vector3 desired = (Mathf.Cos(ar) * flat - Mathf.Sin(ar) * n).normalized;
            Vector3 up = Quaternion.AngleAxis(dRoll, dir) * transform.up;
            UpdateInserted(desired, depth + dDepth, up, dt, false);
        }

        void UpdateInserted(Vector3 desiredDir, float rawDepth, Vector3 upHint, float dt, bool rateLimited)
        {
            var s = IVSession.Instance;
            if (threadS > 0.002f)
            {
                rawDepth = Mathf.Min(rawDepth, threadDepth + 0.0005f);   // needle held still while the catheter moves
            }
            else
            {
                Vector3 oldTip = entryPoint + dir * depth;
                Vector3 oldDir = dir;
                Vector3 c = ConstrainDir(desiredDir);
                float maxRad = (rateLimited ? maxTurnSpeed : 720f) * Mathf.Deg2Rad * dt;
                dir = TurnTowards(dir, c, maxRad);

                // After flashback the needle pivots around its TIP (as when a nurse lowers the hub): the tip stays in the
                // vein and the entry point slides along the skin. Before flashback it pivots at the skin, which is what
                // makes a too-steep angle go through the back wall.
                if (flashback && !transfixed && !arteryHit && Vector3.Angle(oldDir, dir) > 1e-4f)
                {
                    float h = -Vector3.Dot(oldTip - entryPoint, entryNormal);   // tip depth under the skin
                    float sn = -Vector3.Dot(dir, entryNormal);
                    if (h > 0.0001f && sn > 0.03f)
                    {
                        float anchoredDepth = h / sn;
                        if (anchoredDepth < needleLength - 0.004f)
                        {
                            float delta = anchoredDepth - depth;
                            entryPoint = oldTip - dir * anchoredDepth;
                            rawDepth += delta;
                            vrAnchorDepth += delta;
                            depth = anchoredDepth;
                        }
                        else dir = oldDir;   // cannot go flatter without the hub touching the skin
                    }
                }
            }

            float newDepth = rateLimited ? Mathf.MoveTowards(depth, rawDepth, maxAdvanceSpeed * dt) : rawDepth;
            newDepth = Mathf.Min(newDepth, needleLength - 0.004f);

            // withdrawing the needle once the catheter has been advanced
            if (threadStarted && !CatheterPlaced && newDepth < threadDepth - 0.001f)
            {
                if (!threadComplete && threadS >= 0.8f * SMax())
                {
                    threadS = SMax();
                    threadComplete = true;
                    if (catheterAssembly != null) catheterAssembly.localPosition = catheterHomeLocalPos + Vector3.forward * threadS;
                    UpdateCatheterVisual();
                    if (s != null) s.OnThreadComplete(catheterInVeinFlag);
                }
                if (threadComplete)
                {
                    if (s == null || s.CanWithdrawNeedleWithCatheter()) DetachCatheter();
                    else newDepth = Mathf.Max(newDepth, threadDepth);
                }
            }

            if (newDepth <= 0f)
            {
                ExitSkin();
                return;
            }

            prevDepth = depth;
            depth = newDepth;
            Vector3 up = upHint - Vector3.Dot(upHint, dir) * dir;
            if (up.sqrMagnitude < 1e-6f) up = entryNormal;
            Quaternion rot = Quaternion.LookRotation(dir, up.normalized);
            transform.SetPositionAndRotation(entryPoint + dir * depth, rot);
            LiveValid = true; LivePoint = entryPoint; LiveNormal = entryNormal;
            LiveAngle = Mathf.Asin(Mathf.Clamp(-Vector3.Dot(dir, entryNormal), -1f, 1f)) * Mathf.Rad2Deg;
            LiveRoll = ComputeRoll(rot, entryNormal);
            LiveDistance = -depth;

            if (!CatheterPlaced)
            {
                UpdateTissue(transform.position);
                UpdateLowerAdvance();
                if (threadS > 0f) UpdateCatheterVisual();
            }
        }

        void UpdateTissue(Vector3 tip)
        {
            var s = IVSession.Instance;
            VeinPath.Hit h;
            bool any = VascularSystem.Query(tip, out h);
            bool inside = any && h.Inside;

            if (inside && !inLumen)
            {
                if (h.vessel.vesselType == VesselType.Artery)
                {
                    if (!arteryHit)
                    {
                        arteryHit = true;
                        flashTarget = 1f;
                        SetFlashMaterial(true);
                        if (Holder != null) Holder.Haptic(0.9f, 0.15f);
                        if (s != null) s.OnArteryPuncture();
                        BloodFX.Hematoma(h.closestPoint, entryNormal);
                    }
                }
                else if (!transfixed && !flashback)
                {
                    flashback = true;
                    flashAngle = CurrentAngle;
                    flashDepth = depth;
                    flashTip = tip;
                    flashTarget = 1f;
                    if (Holder != null) Holder.Haptic(0.5f, 0.06f);
                    if (s != null) s.OnFlashback(h.vessel, flashAngle);
                }
                lumenVessel = h.vessel;
            }
            else if (!inside && inLumen && lumenVessel != null)
            {
                Vector3 v = tip - lastHit.closestPoint;
                bool below = Vector3.Dot(v, entryNormal) < 0f;
                bool advancing = depth >= prevDepth - 1e-5f;
                if (below && advancing && lumenVessel.vesselType == VesselType.Vein && !transfixed && !threadStarted)
                {
                    transfixed = true;
                    if (Holder != null) Holder.Haptic(0.6f, 0.1f);
                    if (s != null) s.OnTransfix(flashback ? flashAngle : CurrentAngle);
                    BloodFX.Hematoma(lastHit.closestPoint - entryNormal * lastHit.radius, entryNormal);
                }
                else if (!below && s != null)
                {
                    s.FeedbackOnce("tipout", "The needle tip has left the vein (angle lowered too early or pulled back).", Severity.Warning, FeedbackKind.Technique, 4f);
                }
            }
            inLumen = inside;
            if (any) lastHit = h;

            if (!flashback && !arteryHit && depth > missDepth && !missedReported)
            {
                missedReported = true;
                if (s != null) s.OnMissedVein();
            }
        }

        void UpdateLowerAdvance()
        {
            var s = IVSession.Instance;
            VeinPath.Hit ch;
            catheterTipInsideNow = flashback && CatheterTipInVein(out ch);
            if (s == null || !flashback || transfixed || arteryHit || loweredReported) return;
            var a = s.Metrics.CurrentAttempt;
            bool lowered = IsLowered;
            bool advanced = AdvanceSinceFlash >= AdvanceTarget;
            if (a != null)
            {
                if (lowered) a.lowered = true;
                if (advanced) a.advancedAfterFlash = true;
            }
            if (lowered && advanced && catheterTipInsideNow)
            {
                loweredReported = true;
                if (Holder != null) Holder.Haptic(0.35f, 0.06f);
                s.OnLoweredAndAdvanced();
            }
            else if (lowered && advanced && !catheterTipInsideNow)
            {
                s.FeedbackOnce("advmore", "Almost - advance a tiny bit more so the plastic catheter tip is inside the vein too.", Severity.Info, FeedbackKind.Guidance, 5f);
            }
        }

        Vector3 CatheterTipStraight { get { return transform.position - dir * tipGap + dir * threadS; } }

        bool CatheterTipInVein(out VeinPath.Hit hit)
        {
            hit = default(VeinPath.Hit);
            return VascularSystem.Query(CatheterTipStraight, out hit) && hit.Inside && hit.vessel.vesselType == VesselType.Vein;
        }

        float SMax() { return catheterLength + tipGap - (threadStarted ? threadDepth : depth); }

        /// <summary>Slides the plastic catheter forward off the needle (amount in metres).</summary>
        public void AdvanceCatheter(float amount)
        {
            if (Phase != CannulaPhase.InSkin || CatheterPlaced || threadComplete) return;
            var s = IVSession.Instance;
            if (!threadStarted)
            {
                if (transfixed || arteryHit)
                {
                    if (s != null) s.FeedbackOnce("nothread", "The vessel wall is damaged - withdraw the cannula, apply pressure and use a new site.", Severity.Warning, FeedbackKind.Event, 3f);
                    return;
                }
                bool strict = s == null || s.Profile.enforceStepOrder;
                if (!flashback)
                {
                    if (s != null) s.FeedbackOnce("noflash", "No flashback yet - the needle is not in a vein.", Severity.Warning, FeedbackKind.Technique, 3f);
                    if (strict) return;
                }
                VeinPath.Hit ch;
                bool catheterIn = CatheterTipInVein(out ch);
                if (!catheterIn)
                {
                    if (strict)
                    {
                        if (s != null) s.FeedbackOnce("cathout",
                            inLumen ? "Only the needle tip is in the vein - lower the angle and advance 1-2 mm so the catheter tip is in too."
                                    : "The needle tip is not in the vein.",
                            Severity.Warning, FeedbackKind.Event, 3f);
                        return;
                    }
                }
                if (s != null && !s.RequestAction(StepId.ThreadCatheter)) return;
                catheterInVeinFlag = catheterIn;
                catheterVessel = catheterIn ? ch.vessel : null;
                CatheterInVein = catheterIn;
                threadStarted = true;
                threadDepth = depth;
            }
            float max = SMax();
            threadS = Mathf.Min(max, threadS + amount);
            if (catheterAssembly != null) catheterAssembly.localPosition = catheterHomeLocalPos + Vector3.forward * threadS;
            UpdateCatheterVisual();
            if (threadS >= max - 0.0003f)
            {
                threadComplete = true;
                if (Holder != null) Holder.Haptic(0.3f, 0.05f);
                if (s != null) s.OnThreadComplete(catheterInVeinFlag);
                if (s != null) s.Feedback(catheterInVeinFlag ? "Catheter fully advanced. Release the tourniquet, then withdraw the needle." :
                                                               "Catheter advanced - but it was not in the vein.", Severity.Info, FeedbackKind.Guidance);
            }
        }

        void DetachCatheter()
        {
            if (CatheterPlaced || catheterAssembly == null) return;
            CatheterPlaced = true;
            placedDir = dir;
            var arm = ArmModel.Instance;
            catheterAssembly.SetParent(arm != null ? arm.transform : null, true);
            if (hubBlood != null) hubBlood.SetActive(catheterInVeinFlag);
            if (removeAction != null) removeAction.gameObject.SetActive(true);
            RefreshRenderers();
            if (IVSession.Instance != null) IVSession.Instance.OnCatheterPlaced(hubPort != null ? hubPort.position : entryPoint);
        }

        void ExitSkin()
        {
            var s = IVSession.Instance;
            bool placed = CatheterPlaced;
            if (!placed && threadS > 0f)
            {
                threadS = 0f;
                threadStarted = threadComplete = false;
                if (catheterAssembly != null) catheterAssembly.localPosition = catheterHomeLocalPos;
                BuildStraightCatheter();
            }
            Phase = placed ? CannulaPhase.NeedleOnly : CannulaPhase.Ready;
            inLumen = false;
            depth = 0f;
            transform.position = entryPoint + entryNormal * 0.001f;
            if (!placed && (flashback || arteryHit) && !transfixed) BloodFX.Spill(entryPoint, entryNormal, 0.006f);
            if (s != null) s.OnNeedleOut(placed);
        }

        // =====================================================================================
        // Visuals
        // =====================================================================================

        void Update()
        {
            if (Mathf.Abs(flashFill - flashTarget) > 0.001f)
            {
                flashFill = Mathf.MoveTowards(flashFill, flashTarget, Time.deltaTime / (arteryHit ? 0.25f : 0.8f));
                UpdateFlashVisual();
            }
            if (arteryHit && flashbackFill != null)
            {
                float pulse = 1f + 0.08f * Mathf.Max(0f, Mathf.Sin(Time.time * 7.5f));
                Vector3 sc = flashbackFill.localScale;
                sc.x = sc.y = pulse;
                flashbackFill.localScale = sc;
            }
            if (!IsHeld && Phase != CannulaPhase.InSkin) LiveValid = false;
        }

        void UpdateFlashVisual()
        {
            if (flashbackFill == null) return;
            flashbackFill.gameObject.SetActive(flashFill > 0.01f);
            Vector3 sc = flashbackFill.localScale;
            sc.z = Mathf.Max(0.01f, flashFill);
            flashbackFill.localScale = sc;
        }

        void SetFlashMaterial(bool bright)
        {
            if (flashbackRenderer == null) return;
            var m = bright ? bloodBright : bloodDark;
            if (m != null) flashbackRenderer.sharedMaterial = m;
        }

        readonly List<Vector3> pathWorld = new List<Vector3>();

        void BuildStraightCatheter()
        {
            if (catheterTube == null) return;
            var pts = new List<Vector3>();
            var radii = new List<float>();
            float z0 = -(tipGap + catheterLength);
            int n = 6;
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                pts.Add(new Vector3(0f, 0f, z0 + t * catheterLength));
                radii.Add(i == n ? catheterRadius * 0.75f : catheterRadius);
            }
            WriteTube(pts, radii, catheterTube.transform, false);
        }

        void UpdateCatheterVisual()
        {
            if (catheterTube == null) return;
            pathWorld.Clear();
            Vector3 tip = transform.position;
            Vector3 hubFront = tip - dir * (tipGap + catheterLength) + dir * threadS;
            float straight = catheterLength + tipGap - threadS;
            pathWorld.Add(hubFront);
            if (threadS <= tipGap || !catheterInVeinFlag || catheterVessel == null)
            {
                pathWorld.Add(hubFront + dir * (catheterLength * 0.5f));
                pathWorld.Add(hubFront + dir * catheterLength);
            }
            else
            {
                pathWorld.Add(hubFront + dir * (straight * 0.5f));
                pathWorld.Add(tip);
                VeinPath.Hit h;
                if (catheterVessel.Closest(tip, out h))
                    catheterVessel.WalkAlong(h, dir, threadS - tipGap, pathWorld);
            }
            var local = new List<Vector3>();
            var radii = new List<float>();
            Transform tf = catheterTube.transform;
            for (int i = 0; i < pathWorld.Count; i++)
            {
                if (i > 0 && (pathWorld[i] - pathWorld[i - 1]).sqrMagnitude < 1e-10f) continue;
                local.Add(tf.InverseTransformPoint(pathWorld[i]));
                radii.Add(catheterRadius);
            }
            if (radii.Count > 0) radii[radii.Count - 1] = catheterRadius * 0.75f;
            if (local.Count >= 2) WriteTube(local, radii, tf, true);
        }

        void WriteTube(List<Vector3> pts, List<float> radii, Transform space, bool alreadyLocal)
        {
            var b = new MeshBuilder.Builder();
            MeshBuilder.Tube(b, pts, radii, 10, false, false);
            b.WriteTo(catheterMesh);
        }

        // =====================================================================================
        // After placement
        // =====================================================================================

        public Pose HubPortPose
        {
            get
            {
                if (hubPort == null) return new Pose(entryPoint, Quaternion.LookRotation(placedDir));
                return new Pose(hubPort.position, hubPort.rotation);
            }
        }

        public void RemovePlacedCatheter(System.Action onDone)
        {
            if (!CatheterPlaced || catheterAssembly == null) { if (onDone != null) onDone(); return; }
            StartCoroutine(RemoveRoutine(onDone));
        }

        IEnumerator RemoveRoutine(System.Action onDone)
        {
            if (removeAction != null) removeAction.gameObject.SetActive(false);
            Vector3 start = catheterAssembly.position;
            Vector3 end = start - placedDir * (catheterLength + 0.03f) + entryNormal * 0.02f;
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / 1.4f);
                catheterAssembly.position = Vector3.Lerp(start, end, t * t);
                yield return null;
            }
            catheterAssembly.gameObject.SetActive(false);
            if (onDone != null) onDone();
        }

        public void Dispose()
        {
            var s = IVSession.Instance;
            bool afterPlacement = CatheterPlaced;
            if (afterPlacement)
            {
                Phase = CannulaPhase.Disposed;
                if (needleAssembly != null) needleAssembly.gameObject.SetActive(false);
                transform.SetPositionAndRotation(homePos, homeRot);
                if (s != null) s.OnNeedleDisposed(true);
            }
            else
            {
                ResetFresh();
                if (s != null) s.OnNeedleDisposed(false);
            }
        }

        void ResetFresh()
        {
            StopAllCoroutines();
            StopMove();
            Phase = CannulaPhase.Ready;
            IsUsed = false;
            CatheterPlaced = false;
            CatheterInVein = false;
            flashback = inLumen = transfixed = arteryHit = missedReported = loweredReported = false;
            threadS = 0f; threadStarted = threadComplete = false; catheterInVeinFlag = false;
            catheterVessel = null;
            depth = 0f;
            flashFill = flashTarget = 0f;
            SetFlashMaterial(false);
            UpdateFlashVisual();
            if (flashbackFill != null) flashbackFill.localScale = new Vector3(1f, 1f, 0.01f);
            if (catheterAssembly != null)
            {
                catheterAssembly.gameObject.SetActive(true);
                catheterAssembly.SetParent(catheterHomeParent != null ? catheterHomeParent : transform, false);
                catheterAssembly.localPosition = catheterHomeLocalPos;
                catheterAssembly.localRotation = catheterHomeLocalRot;
            }
            if (needleAssembly != null) needleAssembly.gameObject.SetActive(true);
            if (hubBlood != null) hubBlood.SetActive(false);
            if (removeAction != null) removeAction.gameObject.SetActive(false);
            BuildStraightCatheter();
            RefreshRenderers();
            transform.SetPositionAndRotation(homePos, homeRot);
        }

        public override void ResetForSession()
        {
            if (Holder != null) Holder.ForceRelease();
            ClearHolder();
            ResetFresh();
        }
    }
}
