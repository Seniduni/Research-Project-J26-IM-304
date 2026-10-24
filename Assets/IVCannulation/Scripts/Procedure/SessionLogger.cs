using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// Appends one row per session (sessions.csv) and one row per insertion attempt (attempts.csv) to
    /// Application.persistentDataPath/IVCannulationLogs. Useful as evaluation data for the research report.
    /// On Windows this is %USERPROFILE%/AppData/LocalLow/&lt;Company&gt;/&lt;Product&gt;/,
    /// on Quest it is /sdcard/Android/data/&lt;package&gt;/files/.
    /// </summary>
    public static class SessionLogger
    {
        public static string Folder { get { return Path.Combine(Application.persistentDataPath, "IVCannulationLogs"); } }

        static string F(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
        static string Q(string s)
        {
            if (s == null) return "";
            s = s.Replace("\"", "'");
            return "\"" + s + "\"";
        }

        public static string Write(SessionMetrics m, float score, string grade, List<string> critical)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string sessions = Path.Combine(Folder, "sessions.csv");
                bool header = !File.Exists(sessions);
                var sb = new StringBuilder();
                if (header)
                {
                    sb.AppendLine("timestamp,participant,session_id,mode,difficulty,duration_s,score,grade,ended_early,end_reason," +
                                  "attempts,success_attempt,entry_angle_deg,bevel_roll_deg,lowered_after_flash,site_vessel,site_rating," +
                                  "hand_hygiene,tourniquet_before_insert,tourniquet_max_s,tourniquet_on_at_needle_removal," +
                                  "clean_coverage,scrub_s,inserted_before_dry,recontaminated,inserted_outside_clean," +
                                  "transfixions,artery_hits,catheter_in_vein,needle_disposed,unsafe_sharps,blood_drawn,flush_ok," +
                                  "dressing,removed,pressure,order_violations,critical_errors");
                }
                var success = m.SuccessfulAttempt;
                var best = success ?? m.CurrentAttempt;
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append(',')
                  .Append(Q(m.participantId)).Append(',')
                  .Append(Q(m.sessionId)).Append(',')
                  .Append(m.mode).Append(',')
                  .Append(m.difficulty).Append(',')
                  .Append(F(m.Duration)).Append(',')
                  .Append(F(score)).Append(',')
                  .Append(Q(grade)).Append(',')
                  .Append(m.endedEarly).Append(',')
                  .Append(Q(m.endReason)).Append(',')
                  .Append(m.attempts.Count).Append(',')
                  .Append(success != null ? success.number.ToString() : "").Append(',')
                  .Append(best != null ? F(best.entryAngle) : "").Append(',')
                  .Append(best != null ? F(best.bevelRoll) : "").Append(',')
                  .Append(best != null && best.lowered).Append(',')
                  .Append(Q(m.siteVessel)).Append(',')
                  .Append(m.siteSelected ? m.siteRating.ToString() : "").Append(',')
                  .Append(m.handHygiene).Append(',')
                  .Append(m.tourniquetOnAtFirstPierce).Append(',')
                  .Append(F(m.tourniquetMaxSeconds)).Append(',')
                  .Append(m.tourniquetOnAtNeedleRemoval).Append(',')
                  .Append(F(m.cleaningCoverage)).Append(',')
                  .Append(F(m.scrubSeconds)).Append(',')
                  .Append(m.insertedBeforeDry).Append(',')
                  .Append(m.recontaminated).Append(',')
                  .Append(m.insertedOutsideClean).Append(',')
                  .Append(m.Transfixions).Append(',')
                  .Append(m.ArteryHits).Append(',')
                  .Append(m.catheterInVein).Append(',')
                  .Append(m.needleDisposed).Append(',')
                  .Append(m.unsafeSharps).Append(',')
                  .Append(m.bloodDrawn).Append(',')
                  .Append(m.flushed && m.flushOk).Append(',')
                  .Append(m.dressingApplied).Append(',')
                  .Append(m.cannulaRemoved).Append(',')
                  .Append(m.pressureApplied).Append(',')
                  .Append(m.orderViolations).Append(',')
                  .Append(Q(string.Join("; ", critical.ToArray())))
                  .AppendLine();
                File.AppendAllText(sessions, sb.ToString());

                string attempts = Path.Combine(Folder, "attempts.csv");
                var sa = new StringBuilder();
                if (!File.Exists(attempts))
                    sa.AppendLine("participant,session_id,difficulty,attempt,time_s,entry_angle_deg,bevel_roll_deg,flashback,angle_at_flashback,lowered,threaded,catheter_in_vein,transfixed,artery_hit,vessel,site_rating,outcome");
                foreach (var a in m.attempts)
                {
                    sa.Append(Q(m.participantId)).Append(',').Append(Q(m.sessionId)).Append(',').Append(m.difficulty).Append(',')
                      .Append(a.number).Append(',').Append(F(a.time)).Append(',').Append(F(a.entryAngle)).Append(',')
                      .Append(F(a.bevelRoll)).Append(',').Append(a.flashback).Append(',').Append(F(a.angleAtFlashback)).Append(',')
                      .Append(a.lowered).Append(',').Append(a.threaded).Append(',').Append(a.catheterInVein).Append(',')
                      .Append(a.transfixed).Append(',').Append(a.arteryHit).Append(',').Append(Q(a.siteVessel)).Append(',')
                      .Append(a.siteRating).Append(',').Append(Q(a.outcome)).AppendLine();
                }
                File.AppendAllText(attempts, sa.ToString());
                return sessions;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[IVSim] Could not write session log: " + e.Message);
                return null;
            }
        }
    }
}
