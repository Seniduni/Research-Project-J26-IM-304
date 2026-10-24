// NDLazyFollow: split from NDUI.cs (Unity needs one MonoBehaviour per file, named like the class).
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NeonatalDelivery
{
    /// <summary>Keeps a panel comfortably in front of the user (lazy follow).</summary>
    public class NDLazyFollow : MonoBehaviour
    {
        public Transform head; public float distance = 0.7f, height = -0.1f, speed = 2f, angleThreshold = 30f;
        void LateUpdate()
        {
            if (head == null) { var c = Camera.main; if (c == null) return; head = c.transform; }
            Vector3 f = head.forward; f.y = 0; if (f.sqrMagnitude < 1e-4f) return; f.Normalize();
            Vector3 target = head.position + f * distance + Vector3.up * height;
            Vector3 toP = transform.position - head.position; toP.y = 0;
            if (Vector3.Angle(toP, f) > angleThreshold || Vector3.Distance(transform.position, target) > 0.4f)
                transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * speed);
            transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
        }
    }
}
