using NUnit.Framework;
using RainbowFroggy.Core;

namespace RainbowFroggy.Tests.EditMode
{
    // AC9: at every checked frame during a run there is at least one lily pad
    // on screen whose colour matches the frog's current colour.
    [TestFixture]
    public class PadFieldInvariantTests
    {
        // A deterministic RNG that cycles through a fixed sequence.
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

        [Test]
        public void MatchingPadAlwaysPresent_5000Frames_SeededRun()
        {
            // Seeded deterministic game.
            var rng   = new SeededRng(42);
            var field = new PadField(rng);

            // Pick an initial frog colour; doesn't matter which.
            var frogColor = PadColor.Ruby;
            field.Initialize(frogColor);

            const float dt         = 1f / 60f; // 60 fps
            const int   frameCount = 5000;

            for (int frame = 0; frame < frameCount; frame++)
            {
                // Simulate an occasional tap (every ~30 frames) to drive the
                // guaranteed-path algorithm through RemovePad paths too.
                if (frame % 30 == 0 && field.CountMatching(frogColor) > 0)
                {
                    // Find a matching pad and remove it (simulating a jump).
                    int targetId = -1;
                    foreach (var pad in field.Pads)
                    {
                        if (pad.Color == frogColor)
                        {
                            targetId = pad.Id;
                            break;
                        }
                    }
                    if (targetId != -1)
                        field.RemovePad(targetId, frogColor);
                }

                field.Tick(dt, frogColor);

                Assert.GreaterOrEqual(
                    field.CountMatching(frogColor), 1,
                    "Frame " + frame + ": no matching pad found for frogColor=" + frogColor);
            }
        }

        [Test]
        public void MatchingPadAlwaysPresent_AlternatingColors()
        {
            // Simulate frog flipping between Ruby and Cyan every 20 frames.
            var rng   = new SeededRng(123);
            var field = new PadField(rng);

            var frogColor = PadColor.Ruby;
            field.Initialize(frogColor);

            const float dt         = 1f / 60f;
            const int   frameCount = 5000;

            for (int frame = 0; frame < frameCount; frame++)
            {
                // Alternate frog colour every 20 frames.
                if (frame % 20 == 0)
                    frogColor = frogColor == PadColor.Ruby ? PadColor.Cyan : PadColor.Ruby;

                field.Tick(dt, frogColor);

                Assert.GreaterOrEqual(
                    field.CountMatching(frogColor), 1,
                    "Frame " + frame + ": no matching pad for " + frogColor);
            }
        }

        [Test]
        public void RemovePad_InvariantHolds_AfterEachRemoval()
        {
            var rng   = new SeededRng(7);
            var field = new PadField(rng);
            var color = PadColor.Cyan;
            field.Initialize(color);

            // Remove 20 matching pads in a row; invariant must hold each time.
            for (int i = 0; i < 20; i++)
            {
                int id = -1;
                foreach (var pad in field.Pads)
                    if (pad.Color == color) { id = pad.Id; break; }

                if (id == -1) break; // shouldn't happen if invariant holds

                field.RemovePad(id, color);

                Assert.GreaterOrEqual(field.CountMatching(color), 1,
                    "After removal " + i + ": no matching pad for " + color);
            }
        }
    }
}
