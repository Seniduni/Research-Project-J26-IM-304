// EditMode tests: Window > General > Test Runner > EditMode > NeonatalDelivery.Tests
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NeonatalDelivery.Tests
{
    public class FaultRuleTests
    {
        static FaultRuleSet Shipped()
        {
            var p = Path.Combine(Application.streamingAssetsPath, "NeonatalDelivery", FaultRuleSet.FileName);
            Assert.IsTrue(File.Exists(p), "missing " + p);
            return FaultRuleSet.Parse(File.ReadAllText(p));
        }

        [Test]
        public void ShippedRulesParseAndAreComplete()
        {
            var set = Shipped();
            Assert.IsNotNull(set);
            Assert.Greater(set.rules.Count, 15, "expected the full taxonomy");
            var codes = new HashSet<string>();
            foreach (var r in set.rules)
            {
                Assert.IsTrue(codes.Add(r.code), "duplicate code " + r.code);
                Assert.Greater(r.stepSet.Count, 0, r.code + ": no valid DeliveryStep");
                if (r.zones != null) Assert.AreEqual(r.zones.Length, r.zoneSet.Count, r.code + ": unknown zone name");
                Assert.IsFalse(string.IsNullOrEmpty(r.detector), r.code + ": no detector");
                Assert.IsFalse(string.IsNullOrEmpty(r.remediation), r.code + ": no remediation");
                Assert.IsFalse(string.IsNullOrEmpty(r.complication), r.code + ": no complication");
                Assert.IsFalse(string.IsNullOrEmpty(r.source), r.code + ": no source reference");
                Assert.IsTrue(r.severity > 0f && r.severity <= 1f, r.code + ": severity out of range");
            }
        }

        [Test]
        public void UnsupportedCraniumFiresAfterMinDuration()
        {
            var set = FaultRuleSet.BuiltIn(); set.Prepare();
            var fired = new List<FaultEvent>();
            var fc = new FaultClassifier(set) { OnFault = fired.Add };
            var frame = new EvaluationFrame();   // no hands in contact
            float dt = 1f / 72f, t = 0f;
            for (int i = 0; i < 72 * 3; i++)
            {
                t += dt;
                fc.Evaluate(new FaultClassifier.Context { step = DeliveryStep.CrowningHeadControl, time = t, dt = dt, progress = 0.45f, progressRate = 0.02f, eval = frame });
                if (fired.Count > 0) break;
            }
            Assert.AreEqual(1, fired.Count);
            Assert.AreEqual("F01_UNSUPPORTED_CRANIUM", fired[0].code);
            Assert.AreEqual(1.5f, t, 0.05f, "fault should fire once the 1.5 s minimum duration has elapsed");
        }

        [Test]
        public void HandOnOcciputPreventsFault()
        {
            var set = FaultRuleSet.BuiltIn(); set.Prepare();
            int n = 0; var fc = new FaultClassifier(set) { OnFault = f => n++ };
            var frame = new EvaluationFrame(); frame.right.contacts = 1u << (int)LandmarkZoneId.Occiput;
            for (int i = 0; i < 300; i++)
                fc.Evaluate(new FaultClassifier.Context { step = DeliveryStep.CrowningHeadControl, time = i / 72f, dt = 1f / 72f, progress = 0.45f, progressRate = 0.02f, eval = frame });
            Assert.AreEqual(0, n);
        }
    }

    public class RigTests
    {
        [Test]
        public void RigidAlignRecoversTransform()
        {
            var q = Quaternion.Euler(-90f, 37f, 5f); var t = new Vector3(1.2f, -0.3f, 4f);
            var a = new List<Vector3> { new Vector3(0, 0.05f, 0.97f), new Vector3(0, 0.22f, 1.09f), new Vector3(0, 0.49f, 1.37f), new Vector3(0.16f, 0.41f, 1.3f), new Vector3(-0.16f, 0.41f, 1.3f) };
            var b = new List<Vector3>(); foreach (var p in a) b.Add(q * p + t);
            Assert.IsTrue(RigUtil.RigidAlign(a, b, out var rq, out var rt));
            Assert.Less(Quaternion.Angle(q, rq), 0.05f);
            Assert.Less((rt - t).magnitude, 1e-4f);
        }
    }

    public class RubricTests
    {
        [Test]
        public void FaultsAndRetriesReduceStepScore()
        {
            var r = new ScoringRubric();
            r.Attempt(DeliveryStep.AnteriorShoulder);
            Assert.AreEqual(100f, r.StepScore(DeliveryStep.AnteriorShoulder), 1e-3f);
            r.Fault(new FaultEvent { step = DeliveryStep.AnteriorShoulder, severity = 0.8f });
            r.Attempt(DeliveryStep.AnteriorShoulder);
            Assert.AreEqual(100f - 0.8f * 30f - 5f, r.StepScore(DeliveryStep.AnteriorShoulder), 1e-3f);
        }

        [Test]
        public void SevereFaultBlocksCompetent()
        {
            var r = new ScoringRubric();
            var res = new SessionResult();
            res.steps.Add(new StepRecord { completed = true, score = 95f });
            res.faults.Add(new FaultEvent { severity = 0.9f });
            Assert.AreEqual(CompetencyStatus.Developing, r.Competency(res));
            res.faults.Clear();
            Assert.AreEqual(CompetencyStatus.Competent, r.Competency(res));
        }
    }
}
