using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    // Tests for Time Freeze power-up state transitions.
    // All tests run in EditMode (pure C#, no MonoBehaviours).
    [TestFixture]
    public class TimeFreezeTests
    {
        // ------------------------------------------------------------------ //
        // AC1: spawn logic returns a non-null pickup within normalised bounds
        // ------------------------------------------------------------------ //

        [Test]
        public void SpawnPickup_TimeFreezeType_IsNonNullAndWithinBounds()
        {
            // Force all rolls to TimeFreeze by using a CycleRng that rolls 0 every time.
            var rng    = new CycleRng(new int[] { 0 });
            var field  = new PowerUpField(rng);
            var pickup = field.SpawnPickup();

            Assert.IsNotNull(pickup, "SpawnPickup must return a non-null PowerUpData");
            Assert.AreEqual(PowerUpType.TimeFreeze, pickup.Type,
                "Pickup type must be TimeFreeze when roll < TimeFreezeWeight");

            // Normalised river bounds: X ∈ [0, 1], Y ∈ [0, 1].
            Assert.That(pickup.X, Is.InRange(0.0f, 1.0f),
                "Pickup X must be within normalised horizontal bounds [0, 1]");
            Assert.That(pickup.Y, Is.InRange(0.0f, 1.0f),
                "Pickup Y must be within normalised vertical bounds [0, 1]");
        }

        // ------------------------------------------------------------------ //
        // AC2: collecting Time Freeze sets active flag and reduces multiplier
        // ------------------------------------------------------------------ //

        [Test]
        public void CollectTimeFreeze_SetsActiveAndReducesMultiplier()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));
            game.StartRun();

            float preFreezeMultiplier = game.Field.ScrollSpeedMultiplier;
            Assert.AreEqual(1.0f, preFreezeMultiplier, 1e-6f,
                "ScrollSpeedMultiplier must be 1.0 before any power-up");

            game.CollectPowerUp(PowerUpType.TimeFreeze);

            Assert.IsTrue(game.IsTimeFreezeActive,
                "IsTimeFreezeActive must be true immediately after collection");

            float expected = preFreezeMultiplier * 0.20f;
            Assert.AreEqual(expected, game.Field.ScrollSpeedMultiplier, 1e-6f,
                "ScrollSpeedMultiplier must equal 0.20 of its pre-freeze value");
        }

        // ------------------------------------------------------------------ //
        // AC3: after 5 seconds the freeze expires and state is restored
        // ------------------------------------------------------------------ //

        [Test]
        public void TimeFreeze_ExpiresAfter5Seconds()
        {
            var game = new RainbowFroggyGame(new SeededRng(2));
            game.StartRun();

            float preFreeze = game.Field.ScrollSpeedMultiplier;
            game.CollectPowerUp(PowerUpType.TimeFreeze);

            // Advance in small steps to cover exactly 5 seconds.
            const float step = 1f / 60f;
            float elapsed    = 0f;
            while (elapsed < 5.0f)
            {
                game.Tick(step);
                elapsed += step;
            }

            Assert.IsFalse(game.IsTimeFreezeActive,
                "IsTimeFreezeActive must be false after 5 seconds have elapsed");
            Assert.AreEqual(preFreeze, game.Field.ScrollSpeedMultiplier, 1e-6f,
                "ScrollSpeedMultiplier must return to its original value after freeze expires");
        }

        // ------------------------------------------------------------------ //
        // Correctness: a dt == 0 tick must not decrement the freeze timer
        // ------------------------------------------------------------------ //

        [Test]
        public void TimeFreeze_ZeroDtTick_DoesNotDecrementTimer()
        {
            var game = new RainbowFroggyGame(new SeededRng(3));
            game.StartRun();

            game.CollectPowerUp(PowerUpType.TimeFreeze);
            float remainingBefore = game.TimeFreezeRemaining;

            // Simulate the cross-tap invariant-hack dt==0 tick.
            game.Tick(0f);

            Assert.AreEqual(remainingBefore, game.TimeFreezeRemaining, 1e-6f,
                "TimeFreezeRemaining must not change when Tick is called with dt == 0");
            Assert.IsTrue(game.IsTimeFreezeActive,
                "IsTimeFreezeActive must still be true after a dt==0 tick");
        }

        // ------------------------------------------------------------------ //
        // Inner helpers
        // ------------------------------------------------------------------ //

        // Deterministic RNG cycling through a fixed sequence (shared pattern).
        private sealed class CycleRng : IRng
        {
            private readonly int[] _seq;
            private int _i;
            public CycleRng(int[] seq) => _seq = seq;
            public int Next(int min, int max)
            {
                int v = _seq[_i % _seq.Length];
                _i++;
                return min + (v % (max - min));
            }
        }
    }
}
