using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    public enum GameScreen
    {
        Playing,
        MisstepGameOver,    // tapped a pad whose colour didn't match the frog, or a Rotten pad
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
    //
    // Difficulty phases (driven by Score):
    //   Phase 1:  0–50    ×1.0 speed, Ruby+Cyan
    //   Phase 2: 51–150   ×1.5 speed, +Mango, smaller pads
    //   Phase 3: 151–299  ×2.5 speed, +Purple, 20 % Rotten pads
    //   Phase 4: 300+     ×4.0 speed, +Pink, drifting pads
    public sealed class RainbowFroggyGame
    {
        public GameScreen Screen    { get; private set; } = GameScreen.Playing;
        public int        JumpCount { get; private set; }
        public PadColor   FrogColor { get; private set; }
        public int        Phase     { get; private set; } = 1;

        // Score proxy — kept as an alias so future issues can decouple it.
        public int Score => JumpCount;

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

            // Phase transition check — runs before field tick so that the
            // new scroll speed takes effect within this same frame.
            int newPhase = PhaseForScore(Score);
            if (newPhase != Phase)
            {
                Phase = newPhase;
                _field.SetPhase(newPhase);
            }

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
                // Rotten pads look right but are traps.
                if (target.Type == PadType.Rotten)
                {
                    Screen = GameScreen.MisstepGameOver;
                    return TapResult.Misstep;
                }

                JumpCount++;
                FrogPadId = target.Id;

                // Shift to a new random color different from the current one so
                // the guaranteed-path invariant must be re-checked for that color.
                // Written generically so it holds when Phase 2+ adds more colors.
                PadColor newColor;
                do { newColor = Phase1Colors.Active[_rng.Next(0, Phase1Colors.Active.Length)]; }
                while (newColor == FrogColor);
                FrogColor = newColor;

                // Enforce invariant immediately: a pad of the new color must exist
                // before the next real tick so the player always has a valid target.
                _field.Tick(0f, FrogColor);

                return TapResult.Jump;
            }
            else
            {
                Screen = GameScreen.MisstepGameOver;
                return TapResult.Misstep;
            }
        }

        // ------------------------------------------------------------------ //

        private static int PhaseForScore(int score)
        {
            if (score >= 300) return 4;
            if (score >= 151) return 3;
            if (score >= 51)  return 2;
            return 1;
        }
    }
}
