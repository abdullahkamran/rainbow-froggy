using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    // Manages the scrolling lily-pad field and the guaranteed-path invariant.
    //
    // Guaranteed-path rule (PRD §2.1 Phase 1):
    //   At every tick there must be at least one visible pad whose colour
    //   matches the frog's current colour.  The rule is enforced by:
    //     1. Before removing a pad that would leave zero matching pads,
    //        immediately spawn a replacement at the top.
    //     2. Whenever the periodic spawn fires with zero matching pads
    //        on screen, force the new pad's colour to the frog's colour.
    public sealed class PadField
    {
        public const float SpawnInterval      = 1.8f;
        public const int   InitialPadCount   = 5;

        // Every this-many scheduled-spawn timer fires, a Rainbow Pad is added
        // alongside the regular pad.  Counter-gated (no RNG call) so existing
        // seeded tests see an identical random stream.
        public const int RainbowPadInterval = 8;

        // Base scroll speed for Phase 1; scaled by SetPhase.
        private float _scrollSpeed = 0.12f;

        // Multiplier applied on top of the base scroll speed (1.0 = normal).
        // Power-ups write this to slow or speed the river without changing the
        // phase base speed — kept separate so SetPhase and freeze compose cleanly.
        private float _scrollSpeedMultiplier = 1.0f;

        // Exposed for power-up state and tests; writable by RainbowFroggyGame.
        public float ScrollSpeedMultiplier
        {
            get => _scrollSpeedMultiplier;
            set => _scrollSpeedMultiplier = value;
        }

        // Current active colour palette (grows with each phase).
        private PadColor[] _activePalette = PhaseColors.Phase1;

        // Phase 3+: 20 % of spawned pads are Rotten traps.
        private bool _rottenEnabled;

        // Phase 4+: spawned pads drift horizontally and bounce at edges.
        private bool _driftEnabled;

        // Drift speed assigned to Phase-4 pads (normalised units/second).
        private const float DriftSpeed = 0.15f;

        private int   _phase;
        private bool  _lateArrivalPending;
        private float _lateArrivalTimer;

        private readonly List<PadData> _pads = new List<PadData>();
        private readonly IRng          _rng;
        private int   _nextId;
        private float _spawnTimer;

        // Counts scheduled-spawn timer fires since the last Rainbow Pad was spawned.
        private int _spawnsSinceRainbow;

        public IReadOnlyList<PadData> Pads     => _pads;

        // Exposed for tests and diagnostics.
        public float ScrollSpeed => _scrollSpeed;

        // Interval at which a rotten-frog-colour trap is guaranteed to appear when
        // none is currently visible (Phase 3+).  Kept below the waterfall window
        // so the test can always find a rotten pad before the run ends.
        private const float RottenSpawnInterval = 1.5f;

        // Countdown timer for the rotten-pad guarantee (Phase 3+).
        private float _rottenSpawnTimer;

        public PadField(IRng rng)
        {
            _rng        = rng;
            _spawnTimer = SpawnInterval;
        }

        // Reset all mutable state back to phase-1 defaults so the field can be
        // reused across restarts without allocating a new instance.
        // Call Initialize(frogColor) afterwards to re-seed the pad list.
        public void Reset()
        {
            _pads.Clear();
            _nextId                = 0;
            _spawnTimer            = SpawnInterval;
            _lateArrivalPending    = false;
            _lateArrivalTimer      = 0f;
            _phase                 = 0;
            _scrollSpeed           = 0.12f;
            _scrollSpeedMultiplier = 1.0f;
            _activePalette         = PhaseColors.Phase1;
            _rottenEnabled         = false;
            _driftEnabled          = false;
            _rottenSpawnTimer      = 0f;
            _spawnsSinceRainbow    = 0;
        }

        // Apply a phase transition: update scroll speed, active palette, and
        // special-pad flags.  Takes effect for all pads spawned after this call.
        public void SetPhase(int phase)
        {
            _phase         = phase;
            _activePalette = PhaseColors.ForPhase(phase);
            _rottenEnabled = phase >= 3;
            _driftEnabled  = phase >= 4;

            // Arm the rotten-spawn timer the moment Phase 3 is entered so a trap
            // appears within the first RottenSpawnInterval seconds.
            if (_rottenEnabled) _rottenSpawnTimer = RottenSpawnInterval;

            switch (phase)
            {
                case 1:  _scrollSpeed = 0.12f; break; // ×1.0
                case 2:  _scrollSpeed = 0.18f; break; // ×1.5
                case 3:  _scrollSpeed = 0.30f; break; // ×2.5
                default: _scrollSpeed = 0.48f; break; // ×4.0  (phase 4+)
            }
        }

        // Pre-seed the field so the invariant holds from frame 0.
        // At least one pad of frogColor is guaranteed.
        public void Initialize(PadColor frogColor)
        {
            _pads.Clear();
            _nextId = 0;

            for (int i = 0; i < InitialPadCount; i++)
            {
                // i==0 is the frog's starting pad; place it at Y=0 (top) so it
                // takes the full 8.33 s (at Phase-1 speed 0.12/s) to scroll off.
                // Remaining pads are spaced evenly from 0.26 → 0.74.
                float    y = i == 0 ? 0f : 0.1f + i * (0.8f / InitialPadCount);
                PadColor c = i == 0 ? frogColor : RandomColor();
                float    x = RandomX();
                _pads.Add(new PadData(_nextId++, c, x, y));
            }
        }

        // Advance the field by dt seconds.
        // frogColor is the frog's colour AFTER any tap that may have just fired.
        // Returns pads that scrolled off the bottom this tick; only the frog's
        // own occupied pad triggers waterfall (caller decides).
        public List<PadData> Tick(float dt, PadColor frogColor)
        {
            // 0. Eagerly enforce invariant at the start of each tick so that any
            //    colour change between ticks is covered immediately.
            if (CountMatching(frogColor) == 0)
            {
                SpawnPad(frogColor);
                SpawnDecoys(frogColor, _pads[_pads.Count - 1].X);
            }

            // 1. Scroll all pads down; apply horizontal drift for Phase-4 pads.
            foreach (var pad in _pads)
            {
                pad.Y += _scrollSpeed * _scrollSpeedMultiplier * dt;

                if (pad.VelocityX != 0f)
                {
                    pad.X += pad.VelocityX * dt;
                    if (pad.X <= 0.05f)
                    {
                        pad.X        = 0.05f;
                        pad.VelocityX = -pad.VelocityX;
                    }
                    else if (pad.X >= 0.95f)
                    {
                        pad.X        = 0.95f;
                        pad.VelocityX = -pad.VelocityX;
                    }
                }
            }

            // 2. Collect off-screen pads.
            var removed = new List<PadData>();
            foreach (var pad in _pads)
                if (pad.Y >= 1.0f)
                    removed.Add(pad);

            // 3. Guaranteed-path check: ensure at least one matching pad will
            //    survive after ALL off-screen pads are removed this tick.
            if (CountMatchingExcluding(frogColor, removed) == 0)
                SpawnPad(frogColor);

            foreach (var pad in removed)
                _pads.Remove(pad);

            // 4. Scheduled spawn (phase 2+: 25 % chance of a 1–2 s late arrival).
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = SpawnInterval;
                if (_phase >= 2 && _rng.Next(0, 4) == 0)
                {
                    _lateArrivalPending = true;
                    _lateArrivalTimer   = _rng.Next(10, 21) * 0.1f;
                }
                else
                {
                    PadColor c = CountMatching(frogColor) == 0 ? frogColor : RandomColor();
                    SpawnPad(c);
                }

                // Occasionally add a Rainbow Pad alongside the regular scheduled pad.
                // Counter-gated (no RNG draw) to keep the seeded test stream intact.
                _spawnsSinceRainbow++;
                if (_spawnsSinceRainbow >= RainbowPadInterval)
                {
                    _spawnsSinceRainbow = 0;
                    // Centre lane — no RNG needed.
                    _pads.Add(new PadData(_nextId++, PadColor.Rainbow, 0.5f, 0f,
                                          PadType.Rainbow));
                }
            }

            // 5. Resolve any pending late arrival.
            if (_lateArrivalPending)
            {
                _lateArrivalTimer -= dt;
                if (_lateArrivalTimer <= 0f)
                {
                    _lateArrivalPending = false;
                    SpawnPad(frogColor);
                    SpawnDecoys(frogColor, _pads[_pads.Count - 1].X);
                }
            }

            // 6. Phase 3+: guarantee a rotten trap of the frog's colour is visible
            //    within RottenSpawnInterval seconds.  This fires independently of the
            //    regular spawn timer so it survives late-arrival delays.
            if (_rottenEnabled)
            {
                _rottenSpawnTimer -= dt;
                if (_rottenSpawnTimer <= 0f)
                {
                    _rottenSpawnTimer = RottenSpawnInterval;
                    if (CountRottenMatching(frogColor) == 0)
                        SpawnRottenPad(frogColor);
                }
            }

            return removed;
        }

        // Remove a pad by id (called when the frog successfully jumps onto it).
        // Enforces the guaranteed-path invariant after removal.
        public void RemovePad(int id, PadColor frogColor)
        {
            PadData target = null;
            foreach (var pad in _pads)
                if (pad.Id == id) { target = pad; break; }

            if (target == null) return;

            int matchingAfter = CountMatching(frogColor, excluding: target);
            if (matchingAfter == 0)
                SpawnPad(frogColor);

            _pads.Remove(target);
        }

        public int CountMatching(PadColor color)
        {
            int n = 0;
            foreach (var pad in _pads)
                if (pad.Color == color && pad.Type != PadType.Lotus) n++;
            return n;
        }

        // Test seam: inject a Rainbow Pad at an arbitrary position without
        // waiting for the periodic counter.  Never call from production code.
        public void AddRainbowPadForTest(float x = 0.5f, float y = 0.3f)
        {
            _pads.Add(new PadData(_nextId++, PadColor.Rainbow, x, y, PadType.Rainbow));
        }

        // Spawn a Lotus pad at the normalised centre of the play area (x=0.5, y=0.5).
        // Lotus pads are wildcards: PadData.CanLand returns true for every PadColor.
        // They are intentionally excluded from CountMatching so they do not satisfy
        // the guaranteed-path invariant (a one-shot wild cannot be the sole safe target).
        // Returns the new pad's id.
        public int SpawnLotusPad()
        {
            var pad = new PadData(_nextId++, PadColor.Ruby, 0.5f, 0.5f, PadType.Lotus);
            _pads.Add(pad);
            return pad.Id;
        }

        // ------------------------------------------------------------------ //

        private int CountMatching(PadColor color, PadData excluding)
        {
            int n = 0;
            foreach (var pad in _pads)
                if (pad.Color == color && pad.Type != PadType.Lotus && pad != excluding) n++;
            return n;
        }

        private int CountMatchingExcluding(PadColor color, List<PadData> excluding)
        {
            int n = 0;
            foreach (var pad in _pads)
                if (pad.Color == color && pad.Type != PadType.Lotus && !excluding.Contains(pad)) n++;
            return n;
        }

        private void SpawnPad(PadColor color)
        {
            PadType type = _rottenEnabled && _rng.Next(0, 5) == 0
                ? PadType.Rotten
                : PadType.Normal;

            float vx = 0f;
            float da = 0f;
            if (_driftEnabled)
            {
                da = DriftSpeed;
                vx = _rng.Next(0, 2) == 0 ? da : -da;
            }

            _pads.Add(new PadData(_nextId++, color, RandomX(), 0f, type, vx, da));
        }

        // Phase 2+: after an invariant-enforcement spawn or late-arrival spawn,
        // probabilistically place 1–2 distractor pads in adjacent lanes.
        // 40% chance of 1 decoy, 20% chance of 2 decoys, 40% none.
        private void SpawnDecoys(PadColor frogColor, float guaranteedX)
        {
            if (_phase < 2) return;

            int roll = _rng.Next(0, 5);
            int decoyCount = (roll == 0 || roll == 1) ? 1 : roll == 2 ? 2 : 0;

            if (decoyCount == 0) return;

            float[] allLanes = new float[] { 0.2f, 0.5f, 0.8f };
            var availableLanes = new List<float>();
            foreach (var lane in allLanes)
            {
                if (System.Math.Abs(lane - guaranteedX) > 0.01f)
                    availableLanes.Add(lane);
            }

            int spawned = 0;
            foreach (var lane in availableLanes)
            {
                if (spawned >= decoyCount) break;

                PadColor decoyColor = frogColor;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    decoyColor = RandomColor();
                    if (decoyColor != frogColor) break;
                }
                if (decoyColor == frogColor) continue;

                _pads.Add(new PadData(_nextId++, decoyColor, lane, 0f));
                spawned++;
            }
        }

        // Count rotten pads whose colour matches the frog (i.e. visible traps).
        private int CountRottenMatching(PadColor color)
        {
            int n = 0;
            foreach (var pad in _pads)
                if (pad.Type == PadType.Rotten && pad.Color == color) n++;
            return n;
        }

        // Spawn a rotten pad using the frog's colour so it looks like a valid
        // landing target.  Called by the rotten-spawn timer in Tick.
        private void SpawnRottenPad(PadColor frogColor)
        {
            float vx = 0f;
            float da = 0f;
            if (_driftEnabled)
            {
                da = DriftSpeed;
                vx = _rng.Next(0, 2) == 0 ? da : -da;
            }
            _pads.Add(new PadData(_nextId++, frogColor, RandomX(), 0f,
                                  PadType.Rotten, vx, da));
        }

        private PadColor RandomColor()
        {
            return _activePalette[_rng.Next(0, _activePalette.Length)];
        }

        private float RandomX()
        {
            // Three lanes: 0.2, 0.5, 0.8
            int lane = _rng.Next(0, 3);
            return 0.2f + lane * 0.3f;
        }
    }
}
