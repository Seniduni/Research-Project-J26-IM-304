// Session performance store (FR9/FR10/FR11): attempts, faults, timings, per-step scores, trajectories.
// Results are written in the platform's common result structure and handed to a pluggable evaluation service.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NeonatalDelivery
{
    [Serializable]
    public class StepRecord
    {
        public string step;
        public int attempts;
        public float firstStartTime = -1;
        public float completedTime = -1;
        public float durationSeconds;
        public float timeToCorrectPlacement = -1;   // seconds from step start until correct placement was first achieved
        public float score;                         // 0..100
        public int faultCount;
        public bool completed;
    }

    [Serializable]
    public class TrajectorySample
    {
        public float t;            // session time
        public float progress;     // delivery progress 0..1
        public string step;
        public Vector3 leftPalm, rightPalm;         // station-local
        public Quaternion leftRot, rightRot;
        public string leftZone, rightZone;
        public float leftForce, rightForce;          // estimated N
        public Vector3 traction;                     // canal-frame (x lateral, y outward, z anterior), N
        public float leftGrip, rightGrip;
    }

    /// <summary>Common platform result structure: student code, module code, attempt records, per-step scores, competency status.</summary>
    [Serializable]
    public class SessionResult
    {
        public string schema = "J26-IM-304/session-result/1";
        public string studentCode;
        public string moduleCode = ND.ModuleCode;
        public string moduleVersion = ND.Version;
        public string startedUtc;
        public string finishedUtc;
        public float totalSeconds;
        public string difficulty;
        public string censorship;
        public bool ablationMode;
        public bool mixedReality;
        public string dominantHand;
        public bool scenarioNuchalCord;
        public List<StepRecord> steps = new List<StepRecord>();
        public List<FaultEvent> faults = new List<FaultEvent>();
        public int totalAttempts;
        public int rePracticeAttempts;
        public float overallScore;
        public string competency;
        public string summaryText;
        public string trajectoryFile;
        public string device;
    }

    /// <summary>Contract with the shared Performance Tracking & Evaluation Service. Replace with the platform implementation at integration.</summary>
    public interface IEvaluationService
    {
        void Submit(SessionResult result, string json);
    }

    /// <summary>Default implementation: writes JSON to persistentDataPath/NeonatalDelivery/Sessions.</summary>
    public class FileEvaluationService : IEvaluationService
    {
        public void Submit(SessionResult result, string json)
        {
            try
            {
                var dir = Path.Combine(SessionConfig.Folder, "Sessions"); Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{Sanitize(result.studentCode)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json");
                File.WriteAllText(path, json);
                ND.Log("Session result written: " + path);
            }
            catch (Exception e) { ND.Error("Could not write session result: " + e.Message); }
        }
        public static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "anon";
            var sb = new StringBuilder(); foreach (var c in s) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.ToString();
        }
    }

    public class SessionStore
    {
        public static IEvaluationService Service = new FileEvaluationService();   // integration point (FR11)

        public SessionResult Result { get; private set; }
        readonly Dictionary<DeliveryStep, StepRecord> _steps = new Dictionary<DeliveryStep, StepRecord>();
        readonly List<TrajectorySample> _trajectory = new List<TrajectorySample>(20000);
        float _lastSample = -1;
        public float SampleInterval = 1f / 60f;   // FR1/objective 1: >= 60 samples per second
        public int TrajectoryCount => _trajectory.Count;

        public void Begin(SessionConfig cfg)
        {
            Result = new SessionResult
            {
                studentCode = cfg.studentCode,
                startedUtc = DateTime.UtcNow.ToString("o"),
                difficulty = cfg.difficulty.ToString(),
                censorship = cfg.censorship.ToString(),
                ablationMode = cfg.ablationMode,
                mixedReality = cfg.mixedReality,
                dominantHand = cfg.dominantHand.ToString(),
                scenarioNuchalCord = cfg.scenarioNuchalCord,
                device = SystemInfo.deviceModel
            };
            _steps.Clear(); _trajectory.Clear(); _lastSample = -1;
        }

        public StepRecord Step(DeliveryStep s)
        {
            if (!_steps.TryGetValue(s, out var r)) { r = new StepRecord { step = s.ToString() }; _steps[s] = r; }
            return r;
        }

        public void StepStarted(DeliveryStep s, float t) { var r = Step(s); r.attempts++; if (r.firstStartTime < 0) r.firstStartTime = t; }
        public void StepCompleted(DeliveryStep s, float t, float score)
        {
            var r = Step(s); r.completed = true; r.completedTime = t; r.durationSeconds = t - Mathf.Max(0, r.firstStartTime); r.score = score;
        }
        public void CorrectPlacement(DeliveryStep s, float sinceStepStart)
        {
            var r = Step(s); if (r.timeToCorrectPlacement < 0) r.timeToCorrectPlacement = sinceStepStart;
        }
        public void AddFault(FaultEvent f) { Result.faults.Add(f); Step(f.step).faultCount++; }

        public void Sample(TrajectorySample s)
        {
            if (s.t - _lastSample < SampleInterval * 0.95f) return;
            _lastSample = s.t; _trajectory.Add(s);
        }

        public SessionResult Finish(float totalSeconds, ScoringRubric rubric)
        {
            Result.finishedUtc = DateTime.UtcNow.ToString("o");
            Result.totalSeconds = totalSeconds;
            Result.steps.Clear();
            int attempts = 0, re = 0;
            foreach (DeliveryStep s in Enum.GetValues(typeof(DeliveryStep)))
            {
                if (!_steps.TryGetValue(s, out var r)) continue;
                Result.steps.Add(r); attempts += r.attempts; re += Mathf.Max(0, r.attempts - 1);
            }
            Result.totalAttempts = attempts; Result.rePracticeAttempts = re;
            Result.overallScore = rubric != null ? rubric.OverallScore(Result) : 0f;
            Result.competency = (rubric != null ? rubric.Competency(Result) : CompetencyStatus.NotAttempted).ToString();
            Result.summaryText = BuildSummary(Result);
            Result.trajectoryFile = WriteTrajectoryCsv();
            var json = JsonUtility.ToJson(Result, true);
            try { Service?.Submit(Result, json); } catch (Exception e) { ND.Error("Evaluation service failed: " + e.Message); }
            return Result;
        }

        string WriteTrajectoryCsv()
        {
            if (_trajectory.Count == 0) return "";
            try
            {
                var dir = Path.Combine(SessionConfig.Folder, "Sessions"); Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{FileEvaluationService.Sanitize(Result.studentCode)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}_trajectory.csv");
                var sb = new StringBuilder(_trajectory.Count * 160);
                sb.AppendLine("t,progress,step,lx,ly,lz,lqx,lqy,lqz,lqw,rx,ry,rz,rqx,rqy,rqz,rqw,lzone,rzone,lforce,rforce,trx,try,trz,lgrip,rgrip");
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                foreach (var s in _trajectory)
                {
                    sb.Append(s.t.ToString("F4", ci)).Append(',').Append(s.progress.ToString("F4", ci)).Append(',').Append(s.step).Append(',');
                    V(sb, s.leftPalm, ci); Q(sb, s.leftRot, ci); V(sb, s.rightPalm, ci); Q(sb, s.rightRot, ci);
                    sb.Append(s.leftZone).Append(',').Append(s.rightZone).Append(',');
                    sb.Append(s.leftForce.ToString("F2", ci)).Append(',').Append(s.rightForce.ToString("F2", ci)).Append(',');
                    V(sb, s.traction, ci);
                    sb.Append(s.leftGrip.ToString("F2", ci)).Append(',').Append(s.rightGrip.ToString("F2", ci)).AppendLine();
                }
                File.WriteAllText(path, sb.ToString());
                return Path.GetFileName(path);
            }
            catch (Exception e) { ND.Warn("Trajectory export failed: " + e.Message); return ""; }
        }
        static void V(StringBuilder sb, Vector3 v, IFormatProvider ci) { sb.Append(v.x.ToString("F4", ci)).Append(',').Append(v.y.ToString("F4", ci)).Append(',').Append(v.z.ToString("F4", ci)).Append(','); }
        static void Q(StringBuilder sb, Quaternion q, IFormatProvider ci) { sb.Append(q.x.ToString("F4", ci)).Append(',').Append(q.y.ToString("F4", ci)).Append(',').Append(q.z.ToString("F4", ci)).Append(',').Append(q.w.ToString("F4", ci)).Append(','); }

        static string BuildSummary(SessionResult r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Student {r.studentCode} - {r.difficulty} - censorship {r.censorship}");
            sb.AppendLine($"Overall score {r.overallScore:F0}/100 - competency: {r.competency}");
            sb.AppendLine($"Faults: {r.faults.Count}   re-practice attempts: {r.rePracticeAttempts}   time: {r.totalSeconds / 60f:F1} min");
            foreach (var f in r.faults) sb.AppendLine($" - [{f.step}] {f.name} @ {f.startTime:F1}s -> {f.complication}");
            return sb.ToString();
        }
    }
}
