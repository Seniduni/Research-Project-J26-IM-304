// Ring-buffer motion capture of both hands (26 joints) + newborn state at >= 60 Hz, used by the reflective replay,
// the telemetry export and expert motion capture (reference set for the classifier, methodology 3.2).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NeonatalDelivery
{
    [Serializable]
    public class MotionFrame
    {
        public float t;
        public float progress;
        public int babyMode;
        public Vector3 babyRootPos; public Quaternion babyRootRot;
        public bool lTracked, rTracked;
        public Vector3[] lJ = new Vector3[J.Count], rJ = new Vector3[J.Count];
        public int lZone, rZone; public float lForce, rForce; public Vector3 traction;
    }

    public class MotionRecorder
    {
        public float rate = 60f;
        public float seconds = 40f;
        MotionFrame[] _buf; int _head, _count; float _last = -1f;
        public int Count => _count;

        public MotionRecorder(float seconds = 40f, float rate = 60f)
        {
            this.seconds = seconds; this.rate = rate;
            _buf = new MotionFrame[Mathf.CeilToInt(seconds * rate) + 8];
            for (int i = 0; i < _buf.Length; i++) _buf[i] = new MotionFrame();
        }

        public void Clear() { _head = 0; _count = 0; _last = -1; }

        public void Record(float t, float progress, BabyController baby, TrackedHand l, TrackedHand r, EvaluationFrame e)
        {
            if (_last >= 0 && t - _last < 1f / rate * 0.95f) return;
            _last = t;
            var f = _buf[_head]; _head = (_head + 1) % _buf.Length; _count = Mathf.Min(_count + 1, _buf.Length);
            f.t = t; f.progress = progress;
            if (baby != null && baby.model != null) { f.babyMode = (int)baby.Mode; f.babyRootPos = baby.model.transform.position; f.babyRootRot = baby.model.transform.rotation; }
            f.lTracked = l != null && l.tracked; f.rTracked = r != null && r.tracked;
            if (f.lTracked) Array.Copy(l.pos, f.lJ, J.Count);
            if (f.rTracked) Array.Copy(r.pos, f.rJ, J.Count);
            if (e != null) { f.lZone = (int)e.left.zone; f.rZone = (int)e.right.zone; f.lForce = e.left.force; f.rForce = e.right.force; f.traction = e.tractionWorld; }
        }

        /// <summary>Copy of frames between t0 and t1 (oldest first).</summary>
        public List<MotionFrame> Range(float t0, float t1)
        {
            var list = new List<MotionFrame>();
            for (int i = 0; i < _count; i++)
            {
                var f = _buf[(_head - _count + i + _buf.Length) % _buf.Length];
                if (f.t >= t0 && f.t <= t1) list.Add(Clone(f));
            }
            return list;
        }

        static MotionFrame Clone(MotionFrame s)
        {
            var f = new MotionFrame { t = s.t, progress = s.progress, babyMode = s.babyMode, babyRootPos = s.babyRootPos, babyRootRot = s.babyRootRot, lTracked = s.lTracked, rTracked = s.rTracked,
                lZone = s.lZone, rZone = s.rZone, lForce = s.lForce, rForce = s.rForce, traction = s.traction };
            Array.Copy(s.lJ, f.lJ, J.Count); Array.Copy(s.rJ, f.rJ, J.Count); return f;
        }
    }
}
