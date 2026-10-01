using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    // Tests for the Golden Fly collection mechanic.
    //
    // GoldenFlyField, GoldenFlyData, and GoldenFlyView were removed; the
    // collection side-effect (CollectFly → +10 score, +1 FliesThisRun) lives in
    // RainbowFroggyGame and is the surviving testable surface.
    [TestFixture]
    public class GoldenFlyTests
    {
        // CollectFly() adds 10 to the session score (AC2).
        [Test]
        public void CollectFly_AddsTenToScore()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));
            int scoreBefore = game.Score;

            game.CollectFly();

            Assert.AreEqual(scoreBefore + 10, game.Score,
                "CollectFly() must add exactly 10 to Score");
        }

        // CollectFly() increments FliesThisRun by 1 (AC2).
        [Test]
        public void CollectFly_IncrementsFliesThisRun()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));
            Assert.AreEqual(0, game.FliesThisRun, "FliesThisRun starts at 0");

            game.CollectFly();

            Assert.AreEqual(1, game.FliesThisRun,
                "CollectFly() must increment FliesThisRun by 1");
        }

        // Multiple CollectFly() calls accumulate correctly.
        [Test]
        public void CollectFly_AccumulatesAcrossMultipleCalls()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));

            game.CollectFly();
            game.CollectFly();
            game.CollectFly();

            Assert.AreEqual(3,  game.FliesThisRun, "Three collects → FliesThisRun == 3");
            Assert.AreEqual(30, game.Score,        "Three collects → Score == 30");
        }

        // CollectFly() is a no-op when the game is not in the Playing state (AC2).
        [Test]
        public void CollectFly_IsNoOpOutsidePlayingState()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));

            // Force a misstep game-over by tapping a wrong-coloured pad.
            int wrongId = -1;
            foreach (var pad in game.Field.Pads)
            {
                if (pad.Color != game.FrogColor && pad.Type == PadType.Normal)
                {
                    wrongId = pad.Id;
                    break;
                }
            }

            if (wrongId == -1)
            {
                Assert.Inconclusive("Seed 1 produced no wrong-coloured Normal pad");
                return;
            }

            game.TapPad(wrongId);
            Assert.AreEqual(GameScreen.MisstepGameOver, game.Screen);

            int flysBefore  = game.FliesThisRun;
            int scoreBefore = game.Score;

            game.CollectFly();

            Assert.AreEqual(flysBefore,  game.FliesThisRun,
                "CollectFly() must not change FliesThisRun when game is over");
            Assert.AreEqual(scoreBefore, game.Score,
                "CollectFly() must not change Score when game is over");
        }

        // FliesThisRun resets to 0 on ResetRun().
        [Test]
        public void ResetRun_ResetsFliesThisRunToZero()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));

            game.CollectFly();
            game.CollectFly();
            Assert.AreEqual(2, game.FliesThisRun);

            game.ResetRun();

            Assert.AreEqual(0, game.FliesThisRun,
                "ResetRun() must reset FliesThisRun to 0");
        }
    }
}
