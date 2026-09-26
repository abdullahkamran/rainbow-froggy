using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    [TestFixture]
    public class GoldenFlyTests
    {
        // After Tick(dt, speed), a fly's Y coordinate increases by speed × dt.
        [Test]
        public void Tick_ScrollsFlyDownward()
        {
            var field = new GoldenFlyField(new SeededRng(1));
            field.SpawnFly();

            var   fly   = field.Flies[0];   // GoldenFlyData is a class; this is a ref
            float startY = fly.Y;
            const float dt    = 0.1f;
            const float speed = 0.3f;

            field.Tick(dt, speed);

            Assert.AreEqual(startY + speed * dt, fly.Y, 1e-4f,
                "Fly Y must increase by speed × dt after one tick");
        }

        // A fly whose Y reaches 1.0 is removed from Flies during the same tick.
        [Test]
        public void Tick_RemovesFlyAtBottom()
        {
            var field = new GoldenFlyField(new SeededRng(1));
            field.SpawnFly();
            int flyId = field.Flies[0].Id;

            // Position the fly just below the boundary so a small tick crosses it.
            field.Flies[0].Y = 0.99f;   // GoldenFlyData.Y is a public mutable field
            field.Tick(0.1f, 0.5f);     // Y → 0.99 + 0.05 = 1.04 ≥ 1.0

            // Search by id so an incidental spawn cannot mask the removal.
            bool stillPresent = false;
            foreach (var fly in field.Flies)
                if (fly.Id == flyId) { stillPresent = true; break; }

            Assert.IsFalse(stillPresent,
                "Fly must be absent from Flies once its Y coordinate reaches 1.0");
        }

        // A single Tick call whose dt equals SpawnInterval produces exactly one fly.
        [Test]
        public void Tick_SpawnsFlyAfterInterval()
        {
            var field = new GoldenFlyField(new SeededRng(1));

            // One call with dt == SpawnInterval crosses the >= threshold without
            // fractional-accumulation drift that can miss it by one ULP.
            field.Tick(field.SpawnInterval, 0f);

            Assert.AreEqual(1, field.Flies.Count,
                "Exactly one fly must be spawned after advancing one full SpawnInterval");
        }

        // CollectFly() adds 10 to Score and 1 to FliesThisRun.
        [Test]
        public void CollectFly_AddsScoreAndCounter()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));
            int scoreBefore = game.Score;
            int fliesBefore = game.FliesThisRun;

            game.CollectFly();

            Assert.AreEqual(scoreBefore + 10, game.Score,
                "Score must increase by 10 after CollectFly()");
            Assert.AreEqual(fliesBefore + 1, game.FliesThisRun,
                "FliesThisRun must increase by 1 after CollectFly()");
        }

        // After Reset(), Flies is empty and the spawn timer is zeroed so no fly
        // appears during the first SpawnInterval seconds of subsequent ticks.
        [Test]
        public void Reset_ClearsFlyList()
        {
            var field = new GoldenFlyField(new SeededRng(1));

            // Advance close to the spawn boundary so the timer is non-zero when
            // Reset() is called.  0.25 is exact in binary; 19 × 0.25 = 4.75 s
            // is short of the default 5 s interval, so no fly spawns yet.
            for (int i = 0; i < 19; i++)
                field.Tick(0.25f, 0f);

            field.SpawnFly();
            Assert.AreEqual(1, field.Flies.Count, "Precondition: one fly before Reset");

            field.Reset();
            Assert.AreEqual(0, field.Flies.Count,
                "Flies must be empty immediately after Reset()");

            // Tick another 4.75 s — if the timer was truly zeroed by Reset, the
            // accumulated time (4.75 s) stays below SpawnInterval and no fly spawns.
            for (int i = 0; i < 19; i++)
                field.Tick(0.25f, 0f);

            Assert.AreEqual(0, field.Flies.Count,
                "No fly must spawn during the first SpawnInterval seconds after Reset()");
        }
    }
}
