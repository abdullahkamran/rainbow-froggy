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
    // Difficulty phases (driven by JumpCount):
    //   Phase 1:  0–50    ×1.0 speed, Ruby+Cyan
    //   Phase 2: 51–150   ×1.5 speed, +Mango, smaller pads
    //   Phase 3: 151–299  ×2.5 speed, +Purple, 20 % Rotten pads
    //   Phase 4: 300+     ×4.0 speed, +Pink, drifting pads
    public sealed class RainbowFroggyGame
    {
        public GameScreen Screen          { get; private set; } = GameScreen.Playing;
        public int        Score           { get; private set; }
        public int        ComboMultiplier { get; private set; } = 1;
        // Settable so GameBootstrap can seed it from PlayerPrefs at startup.
        public int        HighScore       { get; set; }
        public PadColor   FrogColor       { get; private set; }
        public int        Phase           { get; private set; } = 1;

        // Total number of successful jumps made in this session.
        public int        JumpCount       { get; private set; }

        // The id of the pad the frog is currently riding (-1 = none).
        public int FrogPadId { get; private set; } = -1;

        // Exposed so tests can query pad state directly.
        public PadField Field => _field;

        private readonly PadField _field;
        private readonly IRng     _rng;
        private float _gameTime;
        private float _lastJumpTime = float.NegativeInfinity;

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

            _gameTime += dt;
            if (_gameTime - _lastJumpTime > 1.5f)
                ComboMultiplier = 1;

            // Phase transition check — runs before field tick so that the
            // new scroll speed takes effect within this same frame.
            int newPhase = PhaseForJumpCount(JumpCount);
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
                    if (Score > HighScore) HighScore = Score;
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
                    if (Score > HighScore) HighScore = Score;
                    return TapResult.Misstep;
                }

                // Fever Multiplier: quick consecutive jumps cycle 1→2→3→5 (cap ×5).
                bool withinWindow = (_gameTime - _lastJumpTime) <= 1.5f;
                if (withinWindow)
                {
                    if      (ComboMultiplier == 1) ComboMultiplier = 2;
                    else if (ComboMultiplier == 2) ComboMultiplier = 3;
                    else if (ComboMultiplier == 3) ComboMultiplier = 5;
                    // already 5: stay at 5
                }
                else
                {
                    ComboMultiplier = 1;
                }

                // Distance Bonus: +3×combo when target pad is near the top (Y < 0.2).
                int distanceBonus = target.Y < 0.2f ? 3 * ComboMultiplier : 0;
                Score += ComboMultiplier + distanceBonus;
                _lastJumpTime = _gameTime;
                FrogPadId = target.Id;

                // Shift to a new random color different from the current one.
                PadColor[] palette = PhaseColors.ForPhase(Phase);
                PadColor newColor;
                do { newColor = palette[_rng.Next(0, palette.Length)]; }
                while (newColor == FrogColor);
                FrogColor = newColor;

                // Enforce invariant immediately: a pad of the new colour must
                // exist before the next real tick so the player always has a valid
                // target.
                _field.Tick(0f, FrogColor);

                JumpCount++;
                return TapResult.Jump;
            }
            else
            {
                Screen = GameScreen.MisstepGameOver;
                if (Score > HighScore) HighScore = Score;
                return TapResult.Misstep;
            }
        }

        // ------------------------------------------------------------------ //

        private static int PhaseForJumpCount(int jumpCount)
        {
            if (jumpCount >= 300) return 4;
            if (jumpCount >= 151) return 3;
            if (jumpCount >= 51)  return 2;
            return 1;
        }

    }
}
