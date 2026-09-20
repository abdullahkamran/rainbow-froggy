using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    [TestFixture]
    public class DifficultyProgressionTests
    {
        // Helper: tap matching Normal pads until score reaches `target` or game ends.
        private static void AdvanceScore(RainbowFroggyGame game, int target)
        {
            int guard = 0;
            while (game.JumpCount < target && game.Screen == GameScreen.Playing && guard < 200_000)
            {
                guard++;
                int matchId = -1;
                foreach (var pad in game.Field.Pads)
                {
                    if (pad.Color == game.FrogColor && pad.Type == PadType.Normal)
                    { matchId = pad.Id; break; }
                }

                if (matchId != -1)
                    game.TapPad(matchId);
                else
                    game.Tick(1f / 60f);
            }
        }

        // ------------------------------------------------------------------ //
        // Phase 1 → 2 transition at score = 51
        // ------------------------------------------------------------------ //

        [Test]
        public void Game_StaysPhase1_AtScore50()
        {
            var game = new RainbowFroggyGame(new SeededRng(10));
            AdvanceScore(game, 50);
            game.Tick(0f);
            Assert.AreEqual(1, game.Phase, "Phase must remain 1 while score <= 50");
        }

        [Test]
        public void Game_AdvancesToPhase2_WhenScoreReaches51()
        {
            var game = new RainbowFroggyGame(new SeededRng(10));
            AdvanceScore(game, 51);
            game.Tick(0f);
            Assert.AreEqual(2, game.Phase, "Phase must become 2 when score reaches 51");
        }

        [Test]
        public void Phase2_ScrollSpeed_IsScaled1_5x()
        {
            var field = new PadField(new SeededRng(1));
            field.Initialize(PadColor.Ruby);
            field.SetPhase(2);
            Assert.AreEqual(0.18f, field.ScrollSpeed, 1e-5f,
                "Phase 2 scroll speed must be 0.12 × 1.5 = 0.18");
        }

        [Test]
        public void Phase2_SpawnsMangoColor()
        {
            var field = new PadField(new SeededRng(42));
            field.Initialize(PadColor.Ruby);
            field.SetPhase(2);

            bool found = false;
            for (int i = 0; i < 5000 && !found; i++)
            {
                field.Tick(1f / 60f, PadColor.Ruby);
                foreach (var pad in field.Pads)
                    if (pad.Color == PadColor.Mango) { found = true; break; }
            }
            Assert.IsTrue(found, "Phase 2 palette must include Mango");
        }

        // ------------------------------------------------------------------ //
        // Rotten pad triggers misstep (Phase 3)
        // ------------------------------------------------------------------ //

        [Test]
        public void TapRottenPad_TriggersMisstep()
        {
            var game = new RainbowFroggyGame(new SeededRng(42));

            // Force Phase 3 directly on the field so Rotten pads start spawning.
            game.Field.SetPhase(3);

            // Tick until a Rotten pad of the frog's colour appears.
            int rottenId = -1;
            for (int i = 0; i < 10_000 && rottenId == -1; i++)
            {
                game.Tick(1f / 60f);
                if (game.Screen != GameScreen.Playing) break;

                foreach (var pad in game.Field.Pads)
                {
                    if (pad.Type == PadType.Rotten && pad.Color == game.FrogColor)
                    { rottenId = pad.Id; break; }
                }
            }

            Assert.AreNotEqual(-1, rottenId,
                "A Rotten pad matching frog colour should appear within 10 000 ticks");

            var result = game.TapPad(rottenId);

            Assert.AreEqual(TapResult.Misstep, result,
                "Tapping a Rotten pad must return Misstep");
            Assert.AreEqual(GameScreen.MisstepGameOver, game.Screen,
                "Screen must be MisstepGameOver after tapping a Rotten pad");
        }

        // ------------------------------------------------------------------ //
        // Phase 4: drifting pads
        // ------------------------------------------------------------------ //

        [Test]
        public void Phase4_SpawnsPadsWithNonZeroVelocityX()
        {
            var field = new PadField(new SeededRng(42));
            field.Initialize(PadColor.Ruby);
            field.SetPhase(4);

            bool found = false;
            for (int i = 0; i < 5000 && !found; i++)
            {
                field.Tick(1f / 60f, PadColor.Ruby);
                foreach (var pad in field.Pads)
                    if (pad.VelocityX != 0f) { found = true; break; }
            }
            Assert.IsTrue(found, "Phase 4 must produce drifting pads (VelocityX != 0)");
        }

        [Test]
        public void Phase4_DriftPads_StayWithinBounds()
        {
            var field = new PadField(new SeededRng(7));
            field.Initialize(PadColor.Ruby);
            field.SetPhase(4);

            for (int i = 0; i < 5000; i++)
            {
                field.Tick(1f / 60f, PadColor.Ruby);
                foreach (var pad in field.Pads)
                {
                    Assert.GreaterOrEqual(pad.X, 0.05f - 1e-4f,
                        $"Pad {pad.Id} X={pad.X} drifted below lower bound 0.05");
                    Assert.LessOrEqual(pad.X, 0.95f + 1e-4f,
                        $"Pad {pad.Id} X={pad.X} drifted above upper bound 0.95");
                }
            }
        }
    }
}
