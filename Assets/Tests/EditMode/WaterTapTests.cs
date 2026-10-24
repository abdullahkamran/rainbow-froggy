using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    [TestFixture]
    public class WaterTapTests
    {
        [Test]
        public void TapWater_WhilePlaying_IsMisstepGameOver()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));

            var result = game.TapWater();

            Assert.AreEqual(TapResult.Misstep, result, "Tapping open water must return Misstep");
            Assert.AreEqual(GameScreen.MisstepGameOver, game.Screen,
                "Screen must be MisstepGameOver after the frog jumps into the water");
        }

        [Test]
        public void TapWater_AfterGameOver_IsIgnored()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));
            game.TapWater();

            Assert.AreEqual(TapResult.None, game.TapWater());
        }

        [Test]
        public void TapWater_NewHighScore_IsRecorded()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));
            int matchId = -1;
            foreach (var pad in game.Field.Pads)
                if (pad.Color == game.FrogColor && pad.Type == PadType.Normal) { matchId = pad.Id; break; }
            Assert.AreNotEqual(-1, matchId, "Seed must provide a matching pad");
            game.TapPad(matchId);
            int score = game.Score;

            game.TapWater();

            Assert.IsTrue(game.IsNewHighScore);
            Assert.AreEqual(score, game.HighScore);
        }
    }
}
