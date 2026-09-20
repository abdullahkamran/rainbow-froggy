using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    [TestFixture]
    public class ScoringTests
    {
        private static int FindMatchingPadId(RainbowFroggyGame game, PadColor color)
        {
            foreach (var pad in game.Field.Pads)
                if (pad.Color == color && pad.Type == PadType.Normal) return pad.Id;
            return -1;
        }

        // Score accumulates with combo multiplier: quick jumps advance 1→2→3.
        [Test]
        public void Score_AccumulatesWithComboMultiplier()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));

            // First jump: gap from NegativeInfinity → combo stays 1.
            int id1 = FindMatchingPadId(game, game.FrogColor);
            Assert.AreNotEqual(-1, id1);
            game.TapPad(id1);
            Assert.AreEqual(1, game.ComboMultiplier);
            int scoreAfterFirst = game.Score;
            Assert.Greater(scoreAfterFirst, 0);

            // Second immediate jump (no ticks) → combo advances 1→2.
            int id2 = FindMatchingPadId(game, game.FrogColor);
            Assert.AreNotEqual(-1, id2);
            game.TapPad(id2);
            Assert.AreEqual(2, game.ComboMultiplier);
            Assert.Greater(game.Score, scoreAfterFirst);
        }

        // Combo resets to 1 after more than 1.5 s without a jump.
        [Test]
        public void ComboMultiplier_ResetsAfterGapOver1Point5Seconds()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));

            // Build combo to 2.
            int id1 = FindMatchingPadId(game, game.FrogColor);
            game.TapPad(id1);
            int id2 = FindMatchingPadId(game, game.FrogColor);
            game.TapPad(id2);
            Assert.AreEqual(2, game.ComboMultiplier);

            // Advance 1.6 s (16 × 0.1 s) — stays well within waterfall safety.
            for (int i = 0; i < 16; i++)
            {
                if (game.Screen != GameScreen.Playing) break;
                game.Tick(0.1f);
            }

            if (game.Screen == GameScreen.Playing)
                Assert.AreEqual(1, game.ComboMultiplier,
                    "ComboMultiplier must reset to 1 after >1.5 s without a jump");
        }

        // Distance Bonus of +3×combo applies when the tapped pad's Y < 0.2.
        [Test]
        public void DistanceBonus_AppliedWhenPadYLessThan0Point2()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));

            // Pad 0 is always initialised at Y=0.1 with frogColor.
            int bonusPadId = -1;
            foreach (var pad in game.Field.Pads)
            {
                if (pad.Color == game.FrogColor && pad.Type == PadType.Normal && pad.Y < 0.2f)
                {
                    bonusPadId = pad.Id;
                    break;
                }
            }

            if (bonusPadId == -1)
            {
                Assert.Inconclusive("No matching Normal pad with Y < 0.2 for this seed");
                return;
            }

            game.TapPad(bonusPadId);
            // First jump: combo stays 1; distanceBonus = 3×1 = 3 → score = 4.
            Assert.AreEqual(4, game.Score,
                "Score should be 4 (base 1 + distance bonus 3) for a pad at Y < 0.2");
        }

        // Distance Bonus does NOT apply when the tapped pad's Y >= 0.2.
        [Test]
        public void DistanceBonus_NotAppliedWhenPadYAtLeast0Point2()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));

            // Find a matching Normal pad at Y >= 0.2 (pads 1–4 start at 0.26 …).
            int normalPadId = -1;
            foreach (var pad in game.Field.Pads)
            {
                if (pad.Color == game.FrogColor && pad.Type == PadType.Normal && pad.Y >= 0.2f)
                {
                    normalPadId = pad.Id;
                    break;
                }
            }

            if (normalPadId == -1)
            {
                Assert.Inconclusive("No matching Normal pad with Y >= 0.2 for this seed");
                return;
            }

            game.TapPad(normalPadId);
            // First jump: combo stays 1; no distance bonus → score = 1.
            Assert.AreEqual(1, game.Score,
                "Score should be 1 (base only) for a pad at Y >= 0.2");
        }
    }
}
