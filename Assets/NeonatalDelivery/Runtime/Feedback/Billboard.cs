// Billboard: split from CueSystem.cs (Unity needs one MonoBehaviour per file, named like the class).
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace NeonatalDelivery
{
    /// <summary>Keeps world-space labels facing the viewer.</summary>
    public class Billboard : MonoBehaviour
    {
        public Vector3 offset;
        Vector3 _base; bool _init;
        void LateUpdate()
        {
            var cam = Camera.main; if (cam == null) return;
            if (!_init) { _base = transform.localPosition; _init = true; }
            if (transform.parent != null) transform.localPosition = _base;
            transform.position += offset;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position, Vector3.up);
        }
    }
}
