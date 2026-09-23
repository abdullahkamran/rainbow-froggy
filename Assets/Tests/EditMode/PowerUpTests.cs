using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    // Unit tests for Rainbow Pad and Prism Mode power-ups.
    //
    // AC2 — RainbowPad_AcceptsAnyFrogColor
    // AC3 — RainbowPad_ChangesFrogColor
    // AC5 — Prism_Collect_SetsActive_AndAllowsMismatchedJump
    // AC6 — Prism_ComboStep_IsDoubleBase
    // AC8 — Prism_ExpiresAfter8Seconds
    // AC9 — Prism_AfterExpiry_MismatchedJumpIsRejected
    [TestFixture]
    public class PowerUpTests
    {
        // ------------------------------------------------------------------ //
        // Helpers
        // ------------------------------------------------------------------ //

        private static int FindMatchingNormalPad(RainbowFroggyGame game)
        {
            foreach (var pad in game.Field.Pads)
                if (pad.Color == game.FrogColor && pad.Type == PadType.Normal)
                    return pad.Id;
            return -1;
        }

        // Tick the game for approximately `seconds`, jumping every ~1 s to
        // prevent waterfall.  Returns false if the game left Playing state early.
        private static bool TickSeconds(RainbowFroggyGame game, float seconds)
        {
            const float dt = 1f / 60f;
            int ticks   = (int)(seconds / dt);
            int jumpEvery = 60; // once per simulated second
            for (int i = 0; i < ticks; i++)
            {
                if (game.Screen != GameScreen.Playing) return false;
                if (i % jumpEvery == 0)
                {
                    int mid = FindMatchingNormalPad(game);
                    if (mid != -1) game.TapPad(mid);
                }
                game.Tick(dt);
            }
            return game.Screen == GameScreen.Playing;
        }

        // ------------------------------------------------------------------ //
        // AC2 — any frog colour can land on a Rainbow Pad
        // ------------------------------------------------------------------ //

        [Test]
        public void RainbowPad_AcceptsAnyFrogColor()
        {
            // Drive several seeds to exercise different starting frog colours.
            int[] seeds = { 1, 2, 3, 7, 42 };
            foreach (int seed in seeds)
            {
                var game = new RainbowFroggyGame(new SeededRng(seed));
                game.Field.AddRainbowPadForTest(x: 0.5f, y: 0.3f);

                int rainbowId = -1;
                foreach (var pad in game.Field.Pads)
                    if (pad.Type == PadType.Rainbow) { rainbowId = pad.Id; break; }
                Assert.AreNotEqual(-1, rainbowId, "Rainbow pad not found (seed=" + seed + ")");

                var result = game.TapPad(rainbowId);
                Assert.AreEqual(TapResult.Jump, result,
                    "seed=" + seed + ": Rainbow pad must accept any frog colour");
            }
        }

        // ------------------------------------------------------------------ //
        // AC3 — landing on a Rainbow Pad changes the frog's colour
        // ------------------------------------------------------------------ //

        [Test]
        public void RainbowPad_ChangesFrogColor()
        {
            // Use several seeds to cover different starting colours.
            int[] seeds = { 1, 5, 17, 99 };
            foreach (int seed in seeds)
            {
                var game = new RainbowFroggyGame(new SeededRng(seed));
                game.Field.AddRainbowPadForTest(x: 0.5f, y: 0.3f);

                int rainbowId = -1;
                foreach (var pad in game.Field.Pads)
                    if (pad.Type == PadType.Rainbow) { rainbowId = pad.Id; break; }
                Assert.AreNotEqual(-1, rainbowId);

                PadColor before = game.FrogColor;
                game.TapPad(rainbowId);

                Assert.AreNotEqual(before, game.FrogColor,
                    "seed=" + seed + ": Rainbow pad must change frog colour to a new value");
                Assert.AreNotEqual(PadColor.Rainbow, game.FrogColor,
                    "seed=" + seed + ": New frog colour must be a palette colour, not Rainbow");
            }
        }

        // ------------------------------------------------------------------ //
        // AC5 — collecting Prism Mode sets IsPrismActive and allows
        //        mismatched-colour jumps
        // ------------------------------------------------------------------ //

        [Test]
        public void Prism_Collect_SetsActive_AndAllowsMismatchedJump()
        {
            var game    = new RainbowFroggyGame(new SeededRng(1));
            int prismId = game.PowerUps.AddPickup(PickupType.Prism);
            game.CollectPickup(prismId);
            Assert.IsTrue(game.IsPrismActive,
                "IsPrismActive must be true immediately after CollectPickup");

            // Find a Normal pad whose colour does NOT match the frog.
            int mismatchId = -1;
            foreach (var pad in game.Field.Pads)
                if (pad.Color != game.FrogColor && pad.Type == PadType.Normal)
                { mismatchId = pad.Id; break; }

            Assert.AreNotEqual(-1, mismatchId, "No non-matching Normal pad in initial field");

            var result = game.TapPad(mismatchId);
            Assert.AreEqual(TapResult.Jump, result,
                "Prism Mode must allow jumping to a colour-mismatched pad");
        }

        // ------------------------------------------------------------------ //
        // AC6 — combo increment during Prism is exactly 2× the base increment
        // ------------------------------------------------------------------ //

        [Test]
        public void Prism_ComboStep_IsDoubleBase()
        {
            // Game A — normal play: measure Δ from combo=1 on second in-window jump.
            var gameA   = new RainbowFroggyGame(new SeededRng(1));
            int midA    = FindMatchingNormalPad(gameA);
            Assert.AreNotEqual(-1, midA);
            gameA.TapPad(midA);                         // first jump: not in window → combo=1
            int beforeA = gameA.ComboMultiplier;        // 1
            midA = FindMatchingNormalPad(gameA);
            Assert.AreNotEqual(-1, midA);
            gameA.TapPad(midA);                         // second jump: in window → 1 step → 2
            int deltaBase = gameA.ComboMultiplier - beforeA; // should be 1

            // Game B — same seed + prism active: measure Δ on same relative jump.
            var gameB   = new RainbowFroggyGame(new SeededRng(1));
            int prismId = gameB.PowerUps.AddPickup(PickupType.Prism);
            gameB.CollectPickup(prismId);
            int midB    = FindMatchingNormalPad(gameB);
            Assert.AreNotEqual(-1, midB);
            gameB.TapPad(midB);                         // first jump: not in window → combo=1
            int beforeB = gameB.ComboMultiplier;        // 1
            midB = FindMatchingNormalPad(gameB);
            Assert.AreNotEqual(-1, midB);
            gameB.TapPad(midB);                         // second jump: in window + prism → 2 steps → 3
            int deltaPrism = gameB.ComboMultiplier - beforeB; // should be 2

            Assert.AreEqual(2 * deltaBase, deltaPrism,
                "Prism combo increment must be exactly 2× the base combo increment");
        }

        // ------------------------------------------------------------------ //
        // AC8 — Prism Mode expires after exactly 8 simulated seconds
        // ------------------------------------------------------------------ //

        [Test]
        public void Prism_ExpiresAfter8Seconds()
        {
            var game    = new RainbowFroggyGame(new SeededRng(42));
            int prismId = game.PowerUps.AddPickup(PickupType.Prism);
            game.CollectPickup(prismId);
            Assert.IsTrue(game.IsPrismActive);

            bool stillPlaying = TickSeconds(game, 8.1f);
            Assert.IsTrue(stillPlaying, "Game should still be Playing after 8 s of ticks");
            Assert.IsFalse(game.IsPrismActive,
                "IsPrismActive must be false after 8+ simulated seconds");
        }

        // ------------------------------------------------------------------ //
        // AC9 — after Prism Mode expires normal colour-match rules resume
        // ------------------------------------------------------------------ //

        [Test]
        public void Prism_AfterExpiry_MismatchedJumpIsRejected()
        {
            var game    = new RainbowFroggyGame(new SeededRng(42));
            int prismId = game.PowerUps.AddPickup(PickupType.Prism);
            game.CollectPickup(prismId);

            // Let the prism expire.
            bool stillPlaying = TickSeconds(game, 8.1f);
            Assert.IsTrue(stillPlaying);
            Assert.IsFalse(game.IsPrismActive,
                "Prism must have expired before testing rejection");

            // Find a Normal pad whose colour does not match the frog.
            int mismatchId = -1;
            foreach (var pad in game.Field.Pads)
                if (pad.Color != game.FrogColor && pad.Type == PadType.Normal)
                { mismatchId = pad.Id; break; }

            if (mismatchId == -1)
            {
                Assert.Inconclusive("No non-matching Normal pad visible after prism expiry");
                return;
            }

            var result = game.TapPad(mismatchId);
            Assert.AreEqual(TapResult.Misstep, result,
                "After Prism expires, a colour-mismatched jump must be rejected");
            Assert.AreEqual(GameScreen.MisstepGameOver, game.Screen,
                "Screen must be MisstepGameOver after post-prism mismatched tap");
        }
    }
}
