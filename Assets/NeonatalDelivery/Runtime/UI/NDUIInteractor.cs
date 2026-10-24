// NDUIInteractor: split from NDUI.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonatalDelivery
{
    /// <summary>Finger poke + pinch-ray + mouse interaction with NDButtons.</summary>
    public class NDUIInteractor : MonoBehaviour
    {
        public HandTrackingSource hands;
        public Camera cam;
        readonly float[] _prevFront = { 1f, 1f };
        readonly bool[] _prevPinch = new bool[2];
        LineRenderer[] _rays = new LineRenderer[2];

        void Start()
        {
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject("UIRay" + i); go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>(); lr.widthMultiplier = 0.003f; lr.positionCount = 2; lr.enabled = false;
                lr.material = RigUtil.MakeMaterial("Universal Render Pipeline/Unlit", new Color(0.6f, 0.9f, 1f, 0.8f)); _rays[i] = lr;
            }
        }

        void Update()
        {
            if (hands == null) hands = HandTrackingSource.Instance;
            if (cam == null) cam = Camera.main;
            foreach (var b in NDButton.All) b.SetHover(false);
            // finger poke
            if (hands != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    var h = i == 0 ? hands.Left : hands.Right;
                    _rays[i].enabled = false;
                    if (!h.tracked) { _prevFront[i] = 1f; continue; }
                    Vector3 tip = h.IndexTip; float best = 1f; NDButton bb = null;
                    foreach (var b in NDButton.All)
                    {
                        if (!b.isActiveAndEnabled) continue;
                        if (b.Contains(tip, out float fd) && fd > -0.03f && fd < 0.06f && fd < best) { best = fd; bb = b; }
                    }
                    if (bb != null) { bb.SetHover(true); if (_prevFront[i] > 0.004f && best <= 0.004f) bb.Press(); }
                    _prevFront[i] = bb != null ? best : 1f;
                    // pinch ray for distant panels (index knuckle -> index tip direction)
                    if (bb == null && hands.ActiveSource != HandSourceKind.DesktopSimulator)
                    {
                        Vector3 o = h.pos[J.IndexProx]; Vector3 d = (h.palmForward + (h.IndexTip - o).normalized).normalized;
                        if (Physics.Raycast(o, d, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Collide))
                        {
                            var btn = hit.collider.GetComponentInParent<NDButton>();
                            if (btn != null && btn.isActiveAndEnabled)
                            {
                                btn.SetHover(true); _rays[i].enabled = true; _rays[i].SetPosition(0, o); _rays[i].SetPosition(1, hit.point);
                                bool p = h.pinch > 0.85f; if (p && !_prevPinch[i]) btn.Press(); _prevPinch[i] = p; continue;
                            }
                        }
                        _prevPinch[i] = h.pinch > 0.85f;
                    }
                }
            }
            // mouse (desktop): hold Ctrl + left click, or middle click, to click UI so it doesn't clash with the hand simulator
#if ENABLE_INPUT_SYSTEM && ND_INPUTSYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current; var kb = UnityEngine.InputSystem.Keyboard.current;
            if (cam != null && mouse != null)
            {
                var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Collide))
                {
                    var b = hit.collider.GetComponentInParent<NDButton>();
                    if (b != null)
                    {
                        b.SetHover(true);
                        bool ctrl = kb != null && kb[UnityEngine.InputSystem.Key.LeftCtrl].isPressed;
                        if (mouse.middleButton.wasPressedThisFrame || (ctrl && mouse.leftButton.wasPressedThisFrame)) b.Press();
                    }
                }
            }
#endif
        }
    }
}
