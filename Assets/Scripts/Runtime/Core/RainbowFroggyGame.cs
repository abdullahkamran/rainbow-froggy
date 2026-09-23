using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    public enum GameScreen
    {
        Playing,
        MisstepGameOver,    // tapped a pad whose colour didn't match the frog, or a Rotten pad
        WaterfallGameOver,  // the frog's own pad reached the bottom before a jump
        Idle,               // main-menu / idle state — frog sits on starting pad, no scrolling
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
    //
    // Power-ups:
    //   Rainbow Pad — white/shimmer pad; any frog colour can land on it;
    //                 landing assigns a new random colour to the frog.
    //   Prism Mode  — 8 s duration; any-colour jumps, combo advances 2 rungs
    //                 per jump instead of 1; countdown shown in HUD.
    public sealed class RainbowFroggyGame
    {
        public GameScreen Screen          { get; private set; } = GameScreen.Playing;
        public int        Score           { get; private set; }
        public int        ComboMultiplier { get; private set; } = 1;
        // Settable so GameBootstrap can seed it from PlayerPrefs at startup.
        public int        HighScore       { get; set; }
        public PadColor   FrogColor       { get; private set; }
        public int        Phase           { get; private set; } = 1;

        // Golden flies earned in the current run; incremented by 1 per successful jump.
        public int        FliesThisRun    { get; private set; }

        // Total number of successful jumps made in this session.
        public int        JumpCount       { get; private set; }

        // The id of the pad the frog is currently riding (-1 = none).
        public int FrogPadId { get; private set; } = -1;

        // ---- Power-up state ----

        // Duration of one Prism Mode activation in seconds.
        public const float PrismDuration = 8f;

        // True while Prism Mode is active (any-colour jumps, 2× combo step).
        public bool  IsPrismActive  { get; private set; }

        // Seconds remaining until Prism Mode expires (0 when inactive).
        public float PrismRemaining { get; private set; }

        // Floating pickups managed by the power-up field.
        public PowerUpField PowerUps => _powerUps;

        // Exposed so tests can query pad state directly.
        public PadField Field => _field;

        private readonly PadField    _field;
        private readonly PowerUpField _powerUps;
        private readonly IRng        _rng;
        private float _gameTime;
        private float _lastJumpTime = float.NegativeInfinity;

        public RainbowFroggyGame(IRng rng)
        {
            _rng      = rng;
            _powerUps = new PowerUpField();
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
            // Prism countdown decrements before the Playing guard so that it
            // ticks every frame the caller drives (deterministic expiry).
            if (IsPrismActive)
            {
                PrismRemaining -= dt;
                if (PrismRemaining <= 0f)
                {
                    PrismRemaining = 0f;
                    IsPrismActive  = false;
                }
            }

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
            _powerUps.Tick(dt, _field.ScrollSpeed);

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

        // The player collected a power-up pickup.
        public void CollectPickup(int id)
        {
            if (Screen != GameScreen.Playing) return;

            PickupData target = null;
            foreach (var p in _powerUps.Pickups)
                if (p.Id == id) { target = p; break; }

            if (target == null) return;

            if (target.Type == PickupType.Prism)
            {
                IsPrismActive  = true;
                PrismRemaining = PrismDuration;
            }

            _powerUps.Remove(id);
        }

        // The player tapped a pad.
        public TapResult TapPad(int padId)
        {
            if (Screen != GameScreen.Playing) return TapResult.None;

            PadData target = null;
            foreach (var pad in _field.Pads)
                if (pad.Id == padId) { target = pad; break; }

            if (target == null) return TapResult.None;

            // Landing eligibility:
            //   Rainbow Pad — always land-able (any frog colour).
            //   Prism Mode  — any-colour Normal pads are land-able.
            //   Otherwise   — colour must match.
            bool canLand = target.Type == PadType.Rainbow
                        || IsPrismActive
                        || target.Color == FrogColor;

            if (!canLand)
            {
                Screen = GameScreen.MisstepGameOver;
                if (Score > HighScore) HighScore = Score;
                return TapResult.Misstep;
            }

            // Rotten pads are traps regardless of prism or rainbow eligibility.
            if (target.Type == PadType.Rotten)
            {
                Screen = GameScreen.MisstepGameOver;
                if (Score > HighScore) HighScore = Score;
                return TapResult.Misstep;
            }

            // Fever Multiplier: quick consecutive jumps advance the ladder
            // 1→2→3→5 (capped at 5).  During Prism Mode the ladder advances
            // by 2 rungs per jump instead of the usual 1.
            bool withinWindow = (_gameTime - _lastJumpTime) <= 1.5f;
            if (withinWindow)
            {
                int steps = IsPrismActive ? 2 : 1;
                for (int s = 0; s < steps; s++)
                {
                    if      (ComboMultiplier == 1) ComboMultiplier = 2;
                    else if (ComboMultiplier == 2) ComboMultiplier = 3;
                    else if (ComboMultiplier == 3) ComboMultiplier = 5;
                    // already 5: stay at 5
                }
            }
            else
            {
                ComboMultiplier = 1;
            }

            // Distance Bonus: +3×combo when target pad is near the top (Y < 0.2).
            int distanceBonus = target.Y < 0.2f ? 3 * ComboMultiplier : 0;
            Score += ComboMultiplier + distanceBonus;
            _lastJumpTime = _gameTime;

            // Capture whether this is a self-tap before updating FrogPadId.
            bool selfTap = (target.Id == FrogPadId);
            FrogPadId = target.Id;

            if (target.Type == PadType.Rainbow)
            {
                // Rainbow Pad: always assign a new random palette colour ≠ current,
                // then enforce the invariant for the new colour.
                PadColor[] palette = PhaseColors.ForPhase(Phase);
                PadColor newColor;
                do { newColor = palette[_rng.Next(0, palette.Length)]; }
                while (newColor == FrogColor);
                FrogColor = newColor;
                _field.Tick(0f, FrogColor);
            }
            else if (!selfTap)
            {
                // Cross-tap on a Normal pad: shift to a new random colour and
                // enforce the invariant so the player always has a valid target.
                PadColor[] palette = PhaseColors.ForPhase(Phase);
                PadColor newColor;
                do { newColor = palette[_rng.Next(0, palette.Length)]; }
                while (newColor == FrogColor);
                FrogColor = newColor;

                _field.Tick(0f, FrogColor);
            }
            // Self-tap on a Normal pad: FrogColor stays equal to the landed
            // pad's colour — required by PlayMode AC5.

            FliesThisRun++;
            JumpCount++;
            return TapResult.Jump;
        }

        // ------------------------------------------------------------------ //
        // Idle / restart helpers
        // ------------------------------------------------------------------ //

        // Enter the idle/menu state.  Called by GameBootstrap at startup and
        // after a restart.  Tick() is a no-op while Idle.
        public void EnterIdle()
        {
            Screen = GameScreen.Idle;
        }

        // Transition from Idle → Playing.  No-op if not currently Idle.
        public void StartRun()
        {
            if (Screen != GameScreen.Idle) return;
            Screen = GameScreen.Playing;
        }

        // Reset all run state (score, phase, flies, pad field) and re-enter Idle.
        // The HighScore carry-over is preserved.
        public void ResetRun()
        {
            Score           = 0;
            ComboMultiplier = 1;
            JumpCount       = 0;
            Phase           = 1;
            FliesThisRun    = 0;
            _gameTime       = 0f;
            _lastJumpTime   = float.NegativeInfinity;
            IsPrismActive   = false;
            PrismRemaining  = 0f;

            FrogColor = Phase1Colors.Active[_rng.Next(0, Phase1Colors.Active.Length)];
            _field.Reset();
            _field.Initialize(FrogColor);
            _powerUps.Reset();

            // Frog starts on the first matching pad.
            FrogPadId = -1;
            foreach (var pad in _field.Pads)
            {
                if (pad.Color == FrogColor) { FrogPadId = pad.Id; break; }
            }

            Screen = GameScreen.Idle;
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
