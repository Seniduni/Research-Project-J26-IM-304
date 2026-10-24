using System.Collections.Generic;
using UnityEngine;

namespace IVSim
{
    /// <summary>
    /// Turns session metrics into an OSCE-style checklist score (100 points) with feedback lines.
    /// Edit the weights here if your nursing faculty uses a different checklist.
    /// </summary>
    public static class ReportBuilder
    {
        const string OK = "<color=#3DBE6B>[OK]</color> ";
        const string PART = "<color=#E3B341>[~~]</color> ";
        const string NO = "<color=#E5534B>[X]</color> ";

        static void Line(List<string> lines, ref float total, string label, float earned, float max, string detail)
        {
            string tag = earned >= max - 0.01f ? OK : earned > 0.01f ? PART : NO;
            lines.Add(tag + label + "  <color=#9AA4AE>" + earned.ToString("0.#") + "/" + max.ToString("0") + "</color>" +
                      (string.IsNullOrEmpty(detail) ? "" : "\n      <size=17><color=#B8C2CC>" + detail + "</color></size>"));
            total += earned;
        }

        public static void Build(SessionMetrics m, ClinicalSettings c, List<string> lines, List<string> critical, out float score, out string grade)
        {
            lines.Clear();
            critical.Clear();
            float total = 0f;

            // --- Preparation (20)
            Line(lines, ref total, "Hand hygiene & gloves", m.handHygiene ? (m.handHygieneBeforeContact ? 5f : 2.5f) : 0f, 5f,
                 m.handHygiene ? (m.handHygieneBeforeContact ? "" : "Gloves were put on after touching the patient.") : "Always perform hand hygiene and wear gloves.");

            float tq = (m.tourniquetOnAtFirstPierce ? 3f : 0f) + (m.tourniquetMaxSeconds <= c.tourniquetLimitSeconds ? 2f : 0f);
            Line(lines, ref total, "Tourniquet use", tq, 5f,
                 (m.tourniquetOnAtFirstPierce ? "" : "Apply a tourniquet before insertion to distend the vein. ") +
                 (m.tourniquetMaxSeconds > c.tourniquetLimitSeconds ? "Left on " + Mathf.RoundToInt(m.tourniquetMaxSeconds) + " s (limit " + Mathf.RoundToInt(c.tourniquetLimitSeconds) + " s)." : ""));

            float site;
            string siteLabel, siteDetail;
            AttemptRecord flashAttempt = null;
            foreach (var at in m.attempts) if (at.flashback) { flashAttempt = at; break; }
            if (m.siteSelected)
            {
                site = m.siteRating == SiteRating.Ideal ? 10f : m.siteRating == SiteRating.Acceptable ? 6f : 1f;
                siteLabel = "Vein selection (" + m.siteVessel + ", " + IVSession.RatingText(m.siteRating) + ")";
                siteDetail = m.siteRating == SiteRating.Ideal ? "" : m.siteNote;
            }
            else if (flashAttempt != null)
            {
                site = flashAttempt.siteRating == SiteRating.Ideal ? 5f : flashAttempt.siteRating == SiteRating.Acceptable ? 3f : 0f;
                siteLabel = "Vein selection (" + flashAttempt.siteVessel + ", not palpated)";
                siteDetail = "Always palpate and assess the vein before inserting.";
            }
            else
            {
                site = 0f;
                siteLabel = "Vein selection";
                siteDetail = "Palpate and choose a straight, bouncy forearm vein away from joints.";
            }
            Line(lines, ref total, siteLabel, site, 10f, siteDetail);

            // --- Asepsis (20)
            float cov = Mathf.Clamp01(m.cleaningCoverage / Mathf.Max(0.01f, c.requiredCoverage));
            float tim = Mathf.Clamp01(m.scrubSeconds / Mathf.Max(0.1f, c.requiredScrubSeconds));
            float cleanPts = m.insertedBeforeCleaning && !m.cleaningComplete ? 0f : (m.cleaningComplete ? 10f : 5f * cov + 5f * tim);
            Line(lines, ref total, "Skin antisepsis (coverage " + Mathf.RoundToInt(m.cleaningCoverage * 100f) + "%, " + m.scrubSeconds.ToString("0") + " s)", cleanPts, 10f,
                 m.cleaningComplete ? "" : "Scrub the whole area around the site for the full time.");
            Line(lines, ref total, "Allowed antiseptic to dry", m.cleaningComplete && !m.insertedBeforeDry ? 5f : 0f, 5f,
                 m.insertedBeforeDry ? "Inserted while still wet - antiseptic is only effective once dry." : m.cleaningComplete ? "" : "Site was not cleaned.");
            float asep = 5f;
            string asepNote = "";
            if (m.recontaminated) { asep -= 2.5f; asepNote += "Re-touched the cleaned site. "; }
            if (m.insertedOutsideClean) { asep -= 2.5f; asepNote += "Inserted outside the cleaned area. "; }
            if (!m.cleaningComplete) { asep = 0f; }
            Line(lines, ref total, "Aseptic non-touch technique", Mathf.Max(0f, asep), 5f, asepNote);

            // --- Insertion (40)
            AttemptRecord best = m.SuccessfulAttempt ?? m.CurrentAttempt;
            if (best != null)
            {
                float ang = best.entryAngle;
                float angPts = ang >= c.idealAngleMin && ang <= c.idealAngleMax ? 15f :
                               ang >= c.acceptableAngleMin && ang <= c.acceptableAngleMax ? 9f : 3f;
                Line(lines, ref total, "Insertion angle " + ang.ToString("0") + "° (ideal " + c.idealAngleMin.ToString("0") + "-" + c.idealAngleMax.ToString("0") + "°)", angPts, 15f,
                     angPts >= 15f ? "" : ang < c.idealAngleMin ? "Too shallow: the needle can glide above the vein." : "Too steep: risk of piercing the back wall of the vein.");
                Line(lines, ref total, "Bevel up (rotation " + Mathf.Abs(best.bevelRoll).ToString("0") + "°)", Mathf.Abs(best.bevelRoll) <= c.bevelTolerance ? 5f : 0f, 5f,
                     Mathf.Abs(best.bevelRoll) <= c.bevelTolerance ? "" : "Keep the bevel facing up for a smooth, less painful entry.");
            }
            else
            {
                Line(lines, ref total, "Insertion angle", 0f, 15f, "No insertion attempted.");
                Line(lines, ref total, "Bevel up", 0f, 5f, "");
            }

            int attempts = m.attempts.Count;
            var success = m.SuccessfulAttempt;
            float access = success == null ? 0f : success.number == 1 ? 10f : success.number == 2 ? 6f : 3f;
            Line(lines, ref total, "Venous access (" + attempts + " attempt" + (attempts == 1 ? "" : "s") + ")", access, 10f,
                 success == null ? "Catheter was not successfully placed in the vein." : success.number > 1 ? "Aim for first-attempt success to reduce patient discomfort." : "");

            bool lowered = success != null ? success.lowered : (best != null && best.lowered);
            Line(lines, ref total, "Lowered angle & advanced after flashback", lowered ? 5f : 0f, 5f,
                 lowered ? "" : "After flashback, lower the angle and advance 1-2 mm so the catheter tip enters the vein.");
            Line(lines, ref total, "Catheter advanced off the needle", m.catheterPlaced && m.catheterInVein ? 5f : m.catheterPlaced ? 2f : 0f, 5f,
                 m.catheterPlaced && !m.catheterInVein ? "Catheter was advanced but not inside the vein." : "");

            // --- Completion (20)
            Line(lines, ref total, "Tourniquet released before needle removal", m.catheterPlaced && !m.tourniquetOnAtNeedleRemoval ? 5f : 0f, 5f,
                 m.tourniquetOnAtNeedleRemoval ? "Removing the needle with the tourniquet on causes blood spillage." : "");
            Line(lines, ref total, "Safe sharps disposal", m.needleDisposed ? (m.unsafeSharps ? 2f : 5f) : 0f, 5f,
                 m.unsafeSharps ? "The used needle was put down instead of going straight into the sharps bin." : m.needleDisposed ? "" : "Needle not disposed.");
            float post = (m.bloodDrawn ? 2.5f : 0f) + (m.flushed ? (m.flushOk ? 2.5f : 1f) : 0f);
            Line(lines, ref total, "Blood sample & saline flush", post, 5f, m.flushed && !m.flushOk ? "Swelling on flushing = infiltration. Stop, remove and re-site." : "");
            float fin = (m.dressingApplied ? 2f : 0f) + (m.cannulaRemoved ? 1.5f : 0f) + (m.pressureApplied ? 1.5f : 0f);
            Line(lines, ref total, "Dressing, removal & pressure", fin, 5f, "");

            // --- Deductions
            float deduct = 0f;
            if (m.Transfixions > 0)
            {
                deduct += 5f * m.Transfixions;
                lines.Add(NO + "Back wall of vein punctured x" + m.Transfixions + "  <color=#E5534B>-" + (5 * m.Transfixions) + "</color>");
            }
            if (m.ArteryHits > 0)
            {
                deduct += 10f * m.ArteryHits;
                lines.Add(NO + "Arterial puncture x" + m.ArteryHits + "  <color=#E5534B>-" + (10 * m.ArteryHits) + "</color>");
                critical.Add("Arterial puncture");
            }
            if (m.reusedCannula)
            {
                deduct += 5f;
                lines.Add(NO + "Re-used a cannula after a failed attempt  <color=#E5534B>-5</color>");
                critical.Add("Re-used a contaminated cannula");
            }
            if (m.orderViolations > 0)
            {
                deduct += 2f * m.orderViolations;
                lines.Add(PART + "Steps out of order x" + m.orderViolations + "  <color=#E5534B>-" + (2 * m.orderViolations) + "</color>");
            }
            if (!m.handHygiene) critical.Add("No hand hygiene / gloves");
            if (m.unsafeSharps) critical.Add("Unsafe sharps handling");
            if (m.insertedBeforeCleaning) critical.Add("Inserted through uncleaned skin");

            score = Mathf.Clamp(total - deduct, 0f, 100f);
            if (critical.Count > 0) grade = "Not yet competent (critical error)";
            else if (score >= 85f) grade = "Competent";
            else if (score >= 70f) grade = "Satisfactory - keep practising";
            else grade = "Needs supervised practice";
        }
    }
}
