using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    // Tests for Late Arrivals and Decoys spawning mechanics (PRD §2.1, issue #6).
    [TestFixture]
    public class LateArrivalsDecoyTests
    {
        // Deterministic RNG that always returns a constant value, useful for
        // ensuring probability-gated code paths always or never fire.
        private sealed class ConstRng : IRng
        {
            private readonly int _val;
            public ConstRng(int val) => _val = val;
            public int Next(int min, int max) => min + (_val % (max - min));
        }

        // ------------------------------------------------------------------ //
        // Late Arrivals — Phase guard
        // ------------------------------------------------------------------ //

        [Test]
        public void LateArrivals_DoNotActivate_InPhase1()
        {
            // ConstRng(0) makes TryRollLateArrival() always return true IF the
            // feature is enabled.  In Phase 1 it must never become pending.
            var field = new PadField(new ConstRng(0));
            field.Initialize(PadColor.Ruby);
            // Phase 1 is the default — deliberately do NOT call SetPhase(2).

            const float dt = 1f / 60f;
            for (int i = 0; i < 1000; i++)
            {
                field.Tick(dt, PadColor.Ruby);
                Assert.IsFalse(field.LateArrivalPending,
                    "LateArrivalPending must never be true in Phase 1 (frame " + i + ")");
            }
        }

        [Test]
        public void LateArrivals_Activate_FromPhase2()
        {
            // ConstRng(0) makes TryRollLateArrival() always fire in Phase 2+.
            var field = new PadField(new ConstRng(0));
            field.Initialize(PadColor.Ruby);
            field.SetPhase(2);

            const float dt = 1f / 60f;
            bool activated = false;
            for (int i = 0; i < 500 && !activated; i++)
            {
                field.Tick(dt, PadColor.Ruby);
                if (field.LateArrivalPending)
                    activated = true;
            }
            Assert.IsTrue(activated,
                "LateArrivalPending must become true in Phase 2 within 500 ticks");
        }

        // ------------------------------------------------------------------ //
        // Late Arrivals — invariant after resolution
        // ------------------------------------------------------------------ //

        [Test]
        public void LateArrival_Resolves_WithGuaranteedPadPresent()
        {
            // ConstRng(0) always triggers Late Arrival, so the pattern is:
            //   timer fire 1 → pending = true, timer extended
            //   timer fire 2 → pending = false, frogColor pad spawned
            var field = new PadField(new ConstRng(0));
            field.Initialize(PadColor.Ruby);
            field.SetPhase(2);

            const float dt      = 1f / 60f;
            bool        checked = false;

            for (int i = 0; i < 5000 && !checked; i++)
            {
                bool wasPending = field.LateArrivalPending;
                field.Tick(dt, PadColor.Ruby);

                if (wasPending && !field.LateArrivalPending)
                {
                    // Late arrival just resolved — the guaranteed pad must exist.
                    Assert.GreaterOrEqual(field.CountMatching(PadColor.Ruby), 1,
                        "After a late arrival resolves, at least one Ruby pad must exist");
                    checked = true;
                }
            }
            Assert.IsTrue(checked,
                "A late arrival should have resolved within 5000 ticks");
        }

        [Test]
        public void LateArrival_InvariantHolds_ThroughoutDelay()
        {
            // Verify the guaranteed-path invariant is never broken during the
            // 1–2 s window while a late arrival is pending.
            var field = new PadField(new ConstRng(0));
            field.Initialize(PadColor.Ruby);
            field.SetPhase(2);

            const float dt = 1f / 60f;
            for (int i = 0; i < 5000; i++)
            {
                field.Tick(dt, PadColor.Ruby);
                Assert.GreaterOrEqual(field.CountMatching(PadColor.Ruby), 1,
                    "Guaranteed-path invariant violated at frame " + i +
                    " (LateArrivalPending=" + field.LateArrivalPending + ")");
            }
        }

        // ------------------------------------------------------------------ //
        // Decoys — Phase guard
        // ------------------------------------------------------------------ //

        [Test]
        public void Decoys_DoNotSpawn_InPhase1()
        {
            // With ConstRng(0) in Phase 1:
            //   - Late Arrivals are disabled, so every scheduled spawn fires immediately.
            //   - RandomColor() always returns Phase1[0] = Ruby.
            //   - No decoy path is reached; all pads must be Ruby.
            var field = new PadField(new ConstRng(0));
            field.Initialize(PadColor.Ruby);
            // Phase 1 default — do NOT call SetPhase.

            const float dt = 1f / 60f;
            for (int i = 0; i < 1000; i++)
            {
                field.Tick(dt, PadColor.Ruby);
                foreach (var pad in field.Pads)
                    Assert.AreEqual(PadColor.Ruby, pad.Color,
                        "In Phase 1 with ConstRng(0) all pads must be Ruby; found " +
                        pad.Color + " at frame " + i);
            }
        }

        // ------------------------------------------------------------------ //
        // Decoys — colour invariant
        // ------------------------------------------------------------------ //

        [Test]
        public void Decoys_HaveDifferentColor_FromGuaranteedPad()
        {
            // ConstRng(0) ensures:
            //   - TryRollLateArrival always fires → late arrival pending on first timer.
            //   - TryRollDecoy always fires → decoys always accompany the resolution.
            //   - RandomColorExcluding picks the first non-Ruby colour in the Phase 2
            //     palette {Ruby, Cyan, Mango}, which is Cyan.
            // After the late arrival resolves we expect a Cyan pad at Y ≈ 0
            // (just spawned, not yet scrolled this tick).
            var field = new PadField(new ConstRng(0));
            field.Initialize(PadColor.Ruby);
            field.SetPhase(2);

            const float dt      = 1f / 60f;
            bool        checked = false;

            for (int i = 0; i < 5000 && !checked; i++)
            {
                bool wasPending = field.LateArrivalPending;
                field.Tick(dt, PadColor.Ruby);

                if (wasPending && !field.LateArrivalPending)
                {
                    // Scan for a decoy: a non-Ruby pad at Y == 0 (spawned this tick,
                    // not scrolled — only step-4 pads remain at exactly Y=0).
                    bool foundDecoy = false;
                    foreach (var pad in field.Pads)
                    {
                        if (pad.Y < 0.001f && pad.Color != PadColor.Ruby)
                        {
                            Assert.AreNotEqual(PadColor.Ruby, pad.Color,
                                "Decoy colour must differ from the guaranteed pad colour");
                            foundDecoy = true;
                        }
                    }
                    Assert.IsTrue(foundDecoy,
                        "At least one decoy pad must appear alongside the late-arrival resolution");
                    checked = true;
                }
            }
            Assert.IsTrue(checked, "A late arrival should have resolved within 5000 ticks");
        }

        [Test]
        public void Decoys_Activate_FromPhase2_WithSeededRng()
        {
            // Use a real seeded RNG over many ticks to confirm that decoys do
            // eventually appear in Phase 2 (colour != frogColor at Y ≈ 0 after a
            // guaranteed spawn fires).  We probe across many frames rather than
            // relying on a specific seed pattern.
            var field = new PadField(new SeededRng(7));
            field.Initialize(PadColor.Ruby);
            field.SetPhase(2);

            const float dt          = 1f / 60f;
            bool        foundDecoy  = false;

            for (int i = 0; i < 20_000 && !foundDecoy; i++)
            {
                bool wasPending = field.LateArrivalPending;
                field.Tick(dt, PadColor.Ruby);

                // After a late arrival resolves or a direct guaranteed spawn fires
                // (wasPending → false, or CountMatching changed), check for decoys.
                if (wasPending && !field.LateArrivalPending)
                {
                    foreach (var pad in field.Pads)
                        if (pad.Y < 0.001f && pad.Color != PadColor.Ruby)
                            foundDecoy = true;
                }
            }

            // With seed 7 over 20 000 ticks, at least one decoy cluster must have fired.
            Assert.IsTrue(foundDecoy,
                "Decoys must appear at least once in 20 000 Phase-2 ticks");
        }
    }
}
