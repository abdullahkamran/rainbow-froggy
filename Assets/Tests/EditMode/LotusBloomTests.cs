using System;
using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    // Tests for Lotus Bloom power-up: spawn position, wildcard landing, one-shot removal.
    // All tests run in EditMode (pure C#, no MonoBehaviours).
    [TestFixture]
    public class LotusBloomTests
    {
        // ------------------------------------------------------------------ //
        // AC5 (part 1): collecting Lotus Bloom spawns a lotus pad at screen centre
        // ------------------------------------------------------------------ //

        [Test]
        public void CollectLotusBloom_SpawnsLotusPadAtNormalisedCentre()
        {
            var game = new RainbowFroggyGame(new SeededRng(10));
            game.StartRun();

            game.CollectPowerUp(PowerUpType.LotusBloom);

            // Find the lotus pad in the field.
            PadData lotus = null;
            foreach (var pad in game.Field.Pads)
            {
                if (pad.Type == PadType.Lotus) { lotus = pad; break; }
            }

            Assert.IsNotNull(lotus, "A Lotus pad must be present in the field after collection");
            Assert.AreEqual(0.5f, lotus.X, 0.01f,
                "Lotus pad X must be at normalised centre (0.5)");
            Assert.AreEqual(0.5f, lotus.Y, 0.01f,
                "Lotus pad Y must be at normalised centre (0.5)");
        }

        // ------------------------------------------------------------------ //
        // AC5 (combined): simulating collection and CanLand for every colour
        // ------------------------------------------------------------------ //

        [Test]
        public void LotusBloom_CanLand_ReturnsTrueForAllFrogColors()
        {
            // Simulate collecting the Lotus Bloom pickup via the game model so
            // that any bug in the CollectPowerUp path (wrong PadType, wrong
            // spawn position) is caught — not just a property of PadData itself.
            var game = new RainbowFroggyGame(new SeededRng(20));
            game.StartRun();
            game.CollectPowerUp(PowerUpType.LotusBloom);

            // Find the lotus pad that CollectPowerUp spawned.
            PadData lotus = null;
            foreach (var pad in game.Field.Pads)
            {
                if (pad.Type == PadType.Lotus) { lotus = pad; break; }
            }

            Assert.IsNotNull(lotus,
                "CollectPowerUp(LotusBloom) must spawn a Lotus pad in the field");

            // AC5: CanLand must return true for every PadColor on the collected Lotus pad.
            foreach (PadColor color in Enum.GetValues(typeof(PadColor)))
            {
                Assert.IsTrue(lotus.CanLand(color),
                    "CanLand must return true for every PadColor on a Lotus pad, failed for: " + color);
            }
        }

        // ------------------------------------------------------------------ //
        // AC6: Lotus pad is removed after the first frog landing (one-shot)
        // ------------------------------------------------------------------ //

        [Test]
        public void LotusBloom_OneShotBehavior_RemovedAfterFirstLanding()
        {
            var game = new RainbowFroggyGame(new SeededRng(11));
            game.StartRun();

            game.CollectPowerUp(PowerUpType.LotusBloom);

            // Find the lotus pad.
            int lotusId = -1;
            foreach (var pad in game.Field.Pads)
            {
                if (pad.Type == PadType.Lotus) { lotusId = pad.Id; break; }
            }
            Assert.AreNotEqual(-1, lotusId, "Lotus pad must exist before first landing");

            // First landing: any frog color can land on a Lotus pad.
            var result = game.TapPad(lotusId);
            Assert.AreEqual(TapResult.Jump, result,
                "TapPad on a Lotus pad must return Jump regardless of frog color");

            // Lotus pad must no longer be in the field.
            bool found = false;
            foreach (var pad in game.Field.Pads)
                if (pad.Id == lotusId) { found = true; break; }
            Assert.IsFalse(found, "Lotus pad must be removed from the field after first landing");

            // Second landing attempt on the same id must return None.
            var secondResult = game.TapPad(lotusId);
            Assert.AreEqual(TapResult.None, secondResult,
                "TapPad on a removed Lotus pad id must return None");
        }
    }
}
