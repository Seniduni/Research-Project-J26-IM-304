using UnityEngine;

namespace IVSim
{
    public enum ButtonAction { StartEasy, StartMedium, StartHard, Restart, Menu, ToggleHints }

    /// <summary>
    /// A physical 3D button. Click it (desktop), or poke it / point at it and pull the trigger (VR).
    /// It hides itself in session states where it is not relevant.
    /// </summary>
    public class PushButton : Interactable
    {
        public ButtonAction action;
        public bool visibleInMenu = true;
        public bool visibleInRunning = false;
        public bool visibleInReport = false;
        [Tooltip("Child that holds the visible parts (hidden when not relevant)")] public GameObject visual;
        public Collider pressCollider;
        public Transform cap;

        float cooldown;
        bool pokeInside;
        Vector3 capHome;

        protected override void Awake()
        {
            base.Awake();
            if (cap != null) capHome = cap.localPosition;
        }

        bool Visible
        {
            get
            {
                var s = IVSession.Instance;
                if (s == null) return true;
                switch (s.State)
                {
                    case SessionState.Menu: return visibleInMenu;
                    case SessionState.Running: return visibleInRunning;
                    default: return visibleInReport;
                }
            }
        }

        public override bool IsAvailable { get { return isActiveAndEnabled && Visible; } }

        void Update()
        {
            bool v = Visible;
            if (visual != null && visual.activeSelf != v) visual.SetActive(v);
            if (pressCollider != null) pressCollider.enabled = v;
            cooldown -= Time.deltaTime;

            if (cap != null) cap.localPosition = Vector3.Lerp(cap.localPosition, capHome, Time.deltaTime * 10f);
            if (!v || pressCollider == null) return;

            // VR poke
            bool anyInside = false;
            foreach (var h in IVHand.All)
            {
                var vr = h as VRHand;
                if (vr == null || !vr.Tracked) continue;
                if (pressCollider.bounds.Contains(vr.PokePoint)) { anyInside = true; break; }
            }
            if (anyInside && !pokeInside) Press(null);
            pokeInside = anyInside;
        }

        public override void OnUseStart(IVHand hand) { Press(hand); }

        void Press(IVHand hand)
        {
            if (cooldown > 0f) return;
            cooldown = 0.6f;
            if (cap != null) cap.localPosition = capHome - Vector3.up * 0.006f;
            if (hand != null) hand.Haptic(0.3f, 0.04f);
            else foreach (var h in IVHand.All) if (h is VRHand) h.Haptic(0.2f, 0.03f);

            var s = IVSession.Instance;
            if (s == null) return;
            switch (action)
            {
                case ButtonAction.StartEasy: s.StartSession(Difficulty.Easy); break;
                case ButtonAction.StartMedium: s.StartSession(Difficulty.Medium); break;
                case ButtonAction.StartHard: s.StartSession(Difficulty.Hard); break;
                case ButtonAction.Restart: s.RestartSession(); break;
                case ButtonAction.Menu: s.ReturnToMenu(); break;
                case ButtonAction.ToggleHints: s.HintsVisible = !s.HintsVisible; break;
            }
        }
    }
}
