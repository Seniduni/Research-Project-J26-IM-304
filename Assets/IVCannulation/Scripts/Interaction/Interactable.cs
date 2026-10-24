using System.Collections;
using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// Anything the learner can point at / touch. Works with both the desktop mouse hand and the VR hands.
    /// Needs at least one collider on itself or its children (usually a trigger).
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public string displayName = "Item";
        [TextArea(1, 3)] public string hoverHint = "";

        MeshRenderer[] renderers;
        MaterialPropertyBlock mpb;
        bool highlighted;

        protected virtual void Awake()
        {
            renderers = GetComponentsInChildren<MeshRenderer>(true);
            mpb = new MaterialPropertyBlock();
        }

        /// <summary>Can it be grabbed (picked up)?</summary>
        public virtual bool IsGrabbable { get { return false; } }

        /// <summary>Can it be used with trigger/click right now?</summary>
        public virtual bool IsUsable { get { return true; } }

        /// <summary>Should hands consider it at all right now?</summary>
        public virtual bool IsAvailable { get { return isActiveAndEnabled; } }

        public virtual void OnUseStart(IVHand hand) { }
        public virtual void OnUseHold(IVHand hand, float dt) { }
        public virtual void OnUseEnd(IVHand hand) { }

        public virtual string HoverText(IVHand hand)
        {
            return string.IsNullOrEmpty(hoverHint) ? displayName : displayName + " - " + hoverHint;
        }

        public void SetHighlight(bool on)
        {
            if (highlighted == on) return;
            highlighted = on;
            RefreshHighlight();
        }

        public void RefreshHighlight()
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!highlighted)
                {
                    r.SetPropertyBlock(null);
                    continue;
                }
                var m = r.sharedMaterial;
                if (m == null || !m.HasProperty("_BaseColor")) continue;
                Color c = m.GetColor("_BaseColor");
                Color h = Color.Lerp(c, new Color(1f, 1f, 0.75f, c.a), 0.35f);
                h.a = c.a;
                mpb.Clear();
                mpb.SetColor("_BaseColor", h);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>Re-read child renderers (call after adding/removing visuals).</summary>
        public void RefreshRenderers()
        {
            bool h = highlighted;
            SetHighlight(false);
            renderers = GetComponentsInChildren<MeshRenderer>(true);
            SetHighlight(h);
        }

        // ---------------------------------------------------------------- small tween helper

        Coroutine moveRoutine;

        public void MoveTo(Transform t, Vector3 pos, Quaternion rot, float seconds)
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            if (!isActiveAndEnabled || seconds <= 0f)
            {
                t.SetPositionAndRotation(pos, rot);
                return;
            }
            moveRoutine = StartCoroutine(MoveRoutine(t, pos, rot, seconds));
        }

        public void StopMove()
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = null;
        }

        IEnumerator MoveRoutine(Transform t, Vector3 pos, Quaternion rot, float seconds)
        {
            Vector3 p0 = t.position;
            Quaternion r0 = t.rotation;
            float k = 0f;
            while (k < 1f)
            {
                k = Mathf.Min(1f, k + Time.deltaTime / seconds);
                float e = k * k * (3f - 2f * k);
                t.SetPositionAndRotation(Vector3.Lerp(p0, pos, e), Quaternion.Slerp(r0, rot, e));
                yield return null;
            }
            moveRoutine = null;
        }
    }
}
