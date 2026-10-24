// NDButton: split from NDUI.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonatalDelivery
{
    public class NDButton : MonoBehaviour
    {
        public static readonly List<NDButton> All = new List<NDButton>();
        public Image image; public TMP_Text label; public BoxCollider box;
        public Action onClick;
        public bool toggled;
        public bool interactable = true;
        float _cool; bool _hover;
        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }
        public RectTransform Rect => (RectTransform)transform;
        public void SetHover(bool h) { _hover = h; }
        public void Press()
        {
            if (!interactable || Time.unscaledTime < _cool) return;
            _cool = Time.unscaledTime + 0.35f;
            NDAudio.Instance?.UI("ui_click", 0.6f);
            HandTrackingSource.Instance?.Haptic(HandSide.Right, 0.3f, 0.05f);
            onClick?.Invoke();
        }
        void LateUpdate()
        {
            if (box != null) { var r = Rect.rect; box.size = new Vector3(r.width, r.height, 30f); box.center = new Vector3(r.center.x, r.center.y, 0); }
            if (image != null) image.color = !interactable ? new Color(0.2f, 0.2f, 0.2f, 0.6f) : toggled ? NDTheme.ButtonOn : (_hover ? NDTheme.ButtonHover : NDTheme.Button);
        }
        /// <summary>Position of a world point in this button's rect space (x,y px) and its signed distance in front of the panel (m).</summary>
        public bool Contains(Vector3 world, out float frontDistance)
        {
            Vector3 l = Rect.InverseTransformPoint(world);
            frontDistance = -l.z * Rect.lossyScale.z;
            return Rect.rect.Contains(new Vector2(l.x, l.y));
        }
    }
}
