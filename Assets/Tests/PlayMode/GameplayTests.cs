using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.PlayMode
{
    // Play-mode tests for acceptance criteria 5-9.
    // All tests drive the pure-C# model (no MonoBehaviour); they run as
    // Play-mode tests so the test runner can report them under PlayMode.
    [TestFixture]
    public class GameplayTests
    {
        // ------------------------------------------------------------------ //
        // Helpers
        // ------------------------------------------------------------------ //

        // Returns the id of the first pad in the field that matches color.
        private static int FindMatchingPadId(RainbowFroggyGame game, PadColor color)
        {
            foreach (var pad in game.Field.Pads)
                if (pad.Color == color) return pad.Id;
            return -1;
        }

        // Returns the id of the first pad in the field that does NOT match color.
        private static int FindNonMatchingPadId(RainbowFroggyGame game, PadColor color)
        {
            foreach (var pad in game.Field.Pads)
                if (pad.Color != color) return pad.Id;
            return -1;
        }

        // ------------------------------------------------------------------ //
        // AC5: after a frog-landing event the frog's reported colour equals
        //      the colour of the pad it landed on.
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator FrogColor_EqualsLandedPadColor_AfterJump()
        {
            var game = new RainbowFroggyGame(new SeededRng(1));

            int matchId = FindMatchingPadId(game, game.FrogColor);
            Assert.AreNotEqual(-1, matchId, "Should have a matching pad");

            PadColor padColor = PadColor.Ruby;
            foreach (var pad in game.Field.Pads)
                if (pad.Id == matchId) { padColor = pad.Color; break; }

            var result = game.TapPad(matchId);
            Assert.AreEqual(TapResult.Jump, result);

            // AC5: frog's colour == the landed pad's colour.
            Assert.AreEqual(padColor, game.FrogColor,
                "FrogColor must equal the colour of the pad just landed on");

            yield return null;
        }

        // ------------------------------------------------------------------ //
        // AC6: tapping a matching pad increments the jump counter by exactly 1.
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator TapMatchingPad_IncrementsJumpCountByOne()
        {
            var game = new RainbowFroggyGame(new SeededRng(2));

            int before  = game.JumpCount;
            int matchId = FindMatchingPadId(game, game.FrogColor);
            Assert.AreNotEqual(-1, matchId);

            var result = game.TapPad(matchId);
            Assert.AreEqual(TapResult.Jump, result);
            Assert.AreEqual(before + 1, game.JumpCount,
                "JumpCount must increase by exactly 1 on a successful tap");

            yield return null;
        }

        // ------------------------------------------------------------------ //
        // AC7: tapping a non-matching pad triggers the misstep game-over state.
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator TapNonMatchingPad_TriggersMisstepGameOver()
        {
            var game = new RainbowFroggyGame(new SeededRng(3));

            // Make sure a non-matching pad exists; tick a bit if necessary.
            int nonMatchId = -1;
            for (int i = 0; i < 200 && nonMatchId == -1; i++)
            {
                nonMatchId = FindNonMatchingPadId(game, game.FrogColor);
                if (nonMatchId == -1)
                    game.Tick(1f / 60f);
            }
            Assert.AreNotEqual(-1, nonMatchId, "Should find a non-matching pad");

            var result = game.TapPad(nonMatchId);
            Assert.AreEqual(TapResult.Misstep, result);
            Assert.AreEqual(GameScreen.MisstepGameOver, game.Screen,
                "Screen must be MisstepGameOver after tapping a wrong-colour pad");

            yield return null;
        }

        // ------------------------------------------------------------------ //
        // AC8: a lily pad crossing the bottom-edge boundary before the frog
        //      jumps triggers the waterfall game-over state.
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator FrogPadScrollsOff_TriggersWaterfallGameOver()
        {
            var game = new RainbowFroggyGame(new SeededRng(4));

            // Advance with large dt steps so the frog's pad scrolls off.
            // No taps are issued.
            int safeguard = 0;
            while (game.Screen == GameScreen.Playing && safeguard < 10000)
            {
                game.Tick(0.1f);
                safeguard++;
            }

            Assert.AreEqual(GameScreen.WaterfallGameOver, game.Screen,
                "Screen must be WaterfallGameOver when frog's pad reaches the bottom");

            yield return null;
        }

        // ------------------------------------------------------------------ //
        // AC9: spawn/guaranteed-path invariant — ≥1 matching pad on screen
        //      at all times during a run (PlayMode variant with interleaved taps).
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator GuaranteedPath_MatchingPadAlwaysPresent()
        {
            var game = new RainbowFroggyGame(new SeededRng(5));

            const float dt         = 1f / 60f;
            const int   frameCount = 5000;

            for (int frame = 0; frame < frameCount; frame++)
            {
                // Tap every 45 frames to exercise RemovePad invariant path.
                if (frame % 45 == 0)
                {
                    int matchId = FindMatchingPadId(game, game.FrogColor);
                    if (matchId != -1)
                        game.TapPad(matchId);
                }

                if (game.Screen != GameScreen.Playing) break;

                game.Tick(dt);

                Assert.GreaterOrEqual(
                    game.Field.CountMatching(game.FrogColor), 1,
                    "Frame " + frame + ": no matching pad visible for frogColor=" + game.FrogColor);
            }

            yield return null;
        }
    }
}
