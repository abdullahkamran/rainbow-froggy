using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    public enum GameScreen
    {
        Playing,
        MisstepGameOver,    // tapped a pad whose colour didn't match the frog
        WaterfallGameOver,  // the frog's own pad reached the bottom before a jump
    }

    public enum TapResult
    {
        None,
        Jump,
        Misstep,
    }

    // Top-level game model.  No Unity dependencies — testable from plain C#.
    //
    // Frog mechanics:
    //   The frog rides one pad at a time (FrogPadId).  On a successful tap the
    //   frog jumps to the tapped pad; the old pad is NOT removed — it scrolls
    //   off on its own.  If the frog's current pad reaches Y >= 1 before the
    //   next jump the game transitions to WaterfallGameOver.
    public sealed class RainbowFroggyGame
    {
        public GameScreen Screen    { get; private set; } = GameScreen.Playing;
        public int        JumpCount { get; private set; }
        public PadColor   FrogColor { get; private set; }

        // The id of the pad the frog is currently riding (-1 = none).
        public int FrogPadId { get; private set; } = -1;

        // Exposed so tests can query pad state directly.
        public PadField Field => _field;

        private readonly PadField _field;
        private readonly IRng     _rng;

        public RainbowFroggyGame(IRng rng)
        {
            _rng      = rng;
            FrogColor = Phase1Colors.Active[rng.Next(0, Phase1Colors.Active.Length)];
            _field    = new PadField(rng);
            _field.Initialize(FrogColor);

            // Frog starts on the first matching pad.
            foreach (var pad in _field.Pads)
            {
                if (pad.Color == FrogColor)
                {
                    FrogPadId = pad.Id;
                    break;
                }
            }
        }

        // Advance the simulation by dt seconds.
        public void Tick(float dt)
        {
            if (Screen != GameScreen.Playing) return;

            List<PadData> offScreen = _field.Tick(dt, FrogColor);

            // Waterfall: only if the frog's own riding pad scrolled off.
            foreach (var gone in offScreen)
            {
                if (gone.Id == FrogPadId)
                {
                    Screen = GameScreen.WaterfallGameOver;
                    return;
                }
            }
        }

        // The player tapped a pad.
        public TapResult TapPad(int padId)
        {
            if (Screen != GameScreen.Playing) return TapResult.None;

            PadData target = null;
            foreach (var pad in _field.Pads)
                if (pad.Id == padId) { target = pad; break; }

            if (target == null) return TapResult.None;

            if (target.Color == FrogColor)
            {
                JumpCount++;

                // Frog lands on the new pad; colour = that pad's colour.
                FrogColor = target.Color;
                FrogPadId = target.Id;

                return TapResult.Jump;
            }
            else
            {
                Screen = GameScreen.MisstepGameOver;
                return TapResult.Misstep;
            }
        }
    }
}
