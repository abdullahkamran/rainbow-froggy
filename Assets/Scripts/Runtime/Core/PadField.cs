using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    // Manages the scrolling lily-pad field and the guaranteed-path invariant.
    //
    // Guaranteed-path rule (PRD §2.1 Phase 1):
    //   At every tick there must be at least one visible pad whose colour
    //   matches the frog's current colour (or an equivalent oracle-valid pad).
    //   The rule is enforced by:
    //     1. Before removing a pad that would leave zero oracle-valid pads,
    //        immediately spawn a replacement at the top.
    //     2. After each burst spawn, PadPathOracle.HasGuaranteedPath is checked;
    //        if it returns false, one additional frog-colour pad is force-spawned.
    public sealed class PadField
    {
        // How many pads are pre-seeded when a run begins.  At least three of
        // these will match the frog's colour so the player always has a target.
        public const int InitialPadCount = 8;

        // Per-phase spawn configuration: interval range and simultaneous-lane cap.
        public readonly struct SpawnConfig
        {
            public readonly float MinInterval;
            public readonly float MaxInterval;
            public readonly int   MaxSimultaneousLanes;

            public SpawnConfig(float min, float max, int maxLanes)
            {
                MinInterval          = min;
                MaxInterval          = max;
                MaxSimultaneousLanes = maxLanes;
            }
        }

        // Phase-dependent frog-colour bias: probability (0–100) that a burst-
        // spawned pad matches the frog's colour.
        private const int FrogBiasPhase1 = 60;
        private const int FrogBiasPhase2 = 45;
        private const int FrogBiasPhase3 = 30;
        private const int FrogBiasPhase4 = 20;

        // Phase-dependent flaky-pad spawn chances (applied per burst event).
        private const int FlakySpawnChancePhase2    = 15; // ~15 % in Phase 2
        private const int FlakySpawnChancePhase3Plus = 25; // ~25 % in Phase 3+

        // Base scroll speed for Phase 1; scaled by SetPhase.
        private float _scrollSpeed = 0.18f;

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

        private int _phase;

        private SpawnConfig _spawnConfig;

        private readonly List<PadData> _pads = new List<PadData>();
        private readonly IRng          _rng;
        private int   _nextId;
        private float _spawnTimer;

        public IReadOnlyList<PadData> Pads => _pads;

        // Exposed for tests and diagnostics.
        public float ScrollSpeed => _scrollSpeed;

        // Interval at which a rotten-frog-colour trap is guaranteed to appear when
        // none is currently visible (Phase 3+).  Kept below the waterfall window
        // so the test can always find a rotten pad before the run ends.
        private const float RottenSpawnInterval = 1.5f;

        // Off-screen margin in normalised Y units.  Pads spawn above the top
        // viewport edge (Y = -OffscreenMargin) and are removed only after they
        // have scrolled fully past the bottom edge (Y >= 1 + OffscreenMargin).
        private const float OffscreenMargin = 0.25f;

        // Minimum normalised Y distance between an existing pad and the spawn
        // point (Y = −OffscreenMargin) required before a new pad may be placed
        // in the same lane.  Burst spawns and guaranteed-path spawns both
        // respect this constraint; the oracle-driven override (AC4) may bypass it.
        public const float MinLaneClearance = 0.12f;

        // Countdown timer for the rotten-pad guarantee (Phase 3+).
        private float _rottenSpawnTimer;

        // Default spawn config matches Phase 1 settings.
        private static readonly SpawnConfig DefaultSpawnConfig = new SpawnConfig(0.8f, 1.8f, 3);

        public PadField(IRng rng)
        {
            _rng         = rng;
            _spawnConfig = DefaultSpawnConfig;
            _spawnTimer  = DefaultSpawnConfig.MaxInterval;
        }

        // Reset all mutable state back to phase-1 defaults so the field can be
        // reused across restarts without allocating a new instance.
        // Call Initialize(frogColor) afterwards to re-seed the pad list.
        public void Reset()
        {
            _pads.Clear();
            _nextId                = 0;
            _spawnConfig           = DefaultSpawnConfig;
            _spawnTimer            = DefaultSpawnConfig.MaxInterval;
            _phase                 = 0;
            _scrollSpeed           = 0.18f;
            _scrollSpeedMultiplier = 1.0f;
            _activePalette         = PhaseColors.Phase1;
            _rottenEnabled         = false;
            _driftEnabled          = false;
            _rottenSpawnTimer      = 0f;
        }

        // Apply a phase transition: update scroll speed, active palette, spawn config,
        // and special-pad flags.  Takes effect for all pads spawned after this call.
        public void SetPhase(int phase)
        {
            _phase         = phase;
            _spawnConfig   = SpawnConfigForPhase(phase);
            _activePalette = PhaseColors.ForPhase(phase);
            _rottenEnabled = phase >= 3;
            _driftEnabled  = phase >= 4;

            // Arm the rotten-spawn timer the moment Phase 3 is entered so a trap
            // appears within the first RottenSpawnInterval seconds.
            if (_rottenEnabled) _rottenSpawnTimer = RottenSpawnInterval;

            switch (phase)
            {
                case 1:  _scrollSpeed = 0.18f; break; // ×1.0
                case 2:  _scrollSpeed = 0.36f; break; // ×1.5
                case 3:  _scrollSpeed = 0.60f; break; // ×2.5
                default: _scrollSpeed = 0.96f; break; // ×4.0  (phase 4+)
            }
        }

        // Pre-seed the field so the invariant holds from frame 0.
        // Pads 0–2 are always the frog's colour; the rest are random.
        // At least 3 matching pads are guaranteed so the player always has
        // both a starting pad and a visible jump target.
        public void Initialize(PadColor frogColor)
        {
            _pads.Clear();
            _nextId = 0;

            // AC7: a separate, non-seeded System.Random supplies the initial Y
            // offsets so they differ on every run ('random' criterion).  Using a
            // private local here — rather than _rng — leaves the _rng draw
            // sequence (colour/X) completely untouched, preserving seeded-test
            // reproducibility (AC8).
            var initOffsetRng = new System.Random();

            for (int i = 0; i < InitialPadCount; i++)
            {
                // Pad 0 sits at the very top (Y=0) so it gives the frog maximum
                // scroll-time before falling off.  The remaining pads fill Y=0.2–0.9
                // in equal steps, giving the player visible targets immediately.
                float    baseY   = i == 0 ? 0f : 0.1f + i * (0.8f / InitialPadCount);
                PadColor c       = i < 3 ? frogColor : RandomColor();
                float    x       = RandomX();
                // AC7: small random Y offset in (−0.04, +0.04) so pads are not
                // all at exactly their evenly-spaced baseline Y values.
                float    yOffset = (float)(initOffsetRng.NextDouble() * 0.08 - 0.04);
                _pads.Add(new PadData(_nextId++, c, x, baseY + yOffset));
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
            if (!PadPathOracle.HasGuaranteedPath(_pads, frogColor, -1))
                SpawnPad(frogColor);

            // 1. Scroll all pads down; apply horizontal drift for Phase-4 pads.
            foreach (var pad in _pads)
            {
                pad.Y += _scrollSpeed * _scrollSpeedMultiplier * dt;

                if (pad.VelocityX != 0f)
                {
                    pad.X += pad.VelocityX * dt;
                    if (pad.X <= 0.05f)
                    {
                        pad.X         = 0.05f;
                        pad.VelocityX = -pad.VelocityX;
                    }
                    else if (pad.X >= 0.95f)
                    {
                        pad.X         = 0.95f;
                        pad.VelocityX = -pad.VelocityX;
                    }
                }
            }

            // 2. Collect off-screen pads.
            var removed = new List<PadData>();
            foreach (var pad in _pads)
                if (pad.Y >= 1.0f + OffscreenMargin)
                    removed.Add(pad);

            // 3. Guaranteed-path check: ensure at least one oracle-valid pad will
            //    survive after ALL off-screen pads are removed this tick.
            bool pathAfterRemoval = false;
            foreach (var p in _pads)
            {
                if (!removed.Contains(p) && PadPathOracle.IsValidForFrog(p, frogColor, -1))
                {
                    pathAfterRemoval = true;
                    break;
                }
            }
            // Pass `removed` so the internal oracle check does not count pads
            // that are about to be deleted as a valid path (AC4 correctness).
            if (!pathAfterRemoval) SpawnPad(frogColor, removed);

            foreach (var pad in removed)
                _pads.Remove(pad);

            // 4. Burst spawn: roll a random interval and lane count, then spawn one
            //    pad per chosen lane.  Intervals are computed in tenths of a second
            //    so integer RNG can cover the full [min, max] range precisely.
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                int minT = (int)(_spawnConfig.MinInterval * 10f + 0.5f);
                int maxT = (int)(_spawnConfig.MaxInterval * 10f + 0.5f);
                _spawnTimer = _rng.Next(minT, maxT + 1) * 0.1f;

                int laneCount = _rng.Next(1, _spawnConfig.MaxSimultaneousLanes + 1);

                // Fisher-Yates partial shuffle: select laneCount distinct lane indices
                // from the three available lanes (0 → x=0.2, 1 → x=0.5, 2 → x=0.8).
                int[] laneIdx = { 0, 1, 2 };
                for (int i = 0; i < laneCount; i++)
                {
                    int j   = i + _rng.Next(0, 3 - i);
                    int tmp = laneIdx[i]; laneIdx[i] = laneIdx[j]; laneIdx[j] = tmp;
                }

                for (int i = 0; i < laneCount; i++)
                {
                    float x = 0.2f + laneIdx[i] * 0.3f;
                    // AC2: skip this lane if an existing pad is within MinLaneClearance
                    // of the spawn point; neither a colour draw nor a Y-offset draw
                    // is consumed for a skipped lane (AC6).
                    if (!LaneIsClear(x)) continue;
                    // Colour is drawn first to preserve the pre-AC7/AC8 RNG sequence
                    // for seeded tests (AC8).  The Y offset is drawn after so neither
                    // call shifts the other's RNG position relative to the original.
                    // AC5/6: each pad gets its own independent draw — never shared.
                    PadColor c       = PickSpawnColor(frogColor);
                    float    yOffset = RandomSpawnYOffset();
                    SpawnPad(c, x, yOffset: yOffset);
                }

                // Optional flaky pad spawned alongside the burst (Phase 2+).
                // AC5: the flaky pad is part of the burst event and receives its
                // own independent Y offset, drawn separately from the burst pads.
                if (_phase >= 2)
                {
                    int flakyChance = _phase >= 3 ? FlakySpawnChancePhase3Plus : FlakySpawnChancePhase2;
                    if (_rng.Next(0, 100) < flakyChance)
                    {
                        _pads.Add(new PadData(_nextId++, frogColor, RandomX(),
                                              -OffscreenMargin + RandomSpawnYOffset(), PadType.Flaky));
                    }
                }

                // Post-burst path guarantee: if no oracle-valid pad is currently
                // visible, force-spawn one of the frog's colour.
                if (!PadPathOracle.HasGuaranteedPath(_pads, frogColor, -1))
                    SpawnPad(frogColor);
            }

            // 5. Phase 3+: guarantee a rotten trap of the frog's colour is visible
            //    within RottenSpawnInterval seconds.
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

            // Guaranteed-path check: ensure a valid path survives the removal.
            bool hasPath = false;
            foreach (var p in _pads)
            {
                if (p != target && PadPathOracle.IsValidForFrog(p, frogColor, -1))
                {
                    hasPath = true;
                    break;
                }
            }
            // Pass target as ignored so the internal oracle check does not count
            // the pad being removed as a valid path (AC4 correctness).
            if (!hasPath) SpawnPad(frogColor, new List<PadData> { target });

            _pads.Remove(target);
        }

        // Returns the count of pads that the oracle considers valid destinations
        // for a frog of the given colour (frogPadId = -1: no active-flaky exclusion).
        // Used by tests and by the guaranteed-path assertion in RainbowFroggyGame.
        public int CountMatching(PadColor color)
        {
            int n = 0;
            foreach (var pad in _pads)
                if (PadPathOracle.IsValidForFrog(pad, color, -1)) n++;
            return n;
        }

        // Test seam: inject a Rainbow Pad at an arbitrary position.
        // Never call from production code.
        public void AddRainbowPadForTest(float x = 0.5f, float y = 0.3f)
        {
            _pads.Add(new PadData(_nextId++, PadColor.Rainbow, x, y, PadType.Rainbow));
        }

        // Spawn a Lotus pad at the normalised centre of the play area (x=0.5, y=0.5).
        // Lotus pads are wildcards: PadData.CanLand returns true for every PadColor.
        // They are intentionally excluded from the oracle so they do not satisfy
        // the guaranteed-path invariant (a one-shot wild cannot be the sole safe target).
        // Returns the new pad's id.
        public int SpawnLotusPad()
        {
            var pad = new PadData(_nextId++, PadColor.Ruby, 0.5f, 0.5f, PadType.Lotus);
            _pads.Add(pad);
            return pad.Id;
        }

        // ------------------------------------------------------------------ //

        // Returns the per-phase spawn configuration.
        private static SpawnConfig SpawnConfigForPhase(int phase)
        {
            switch (phase)
            {
                case 1:  return new SpawnConfig(0.8f, 1.8f, 3); // 1–3 lanes
                case 2:  return new SpawnConfig(1.2f, 2.5f, 2); // 1–2 lanes
                case 3:  return new SpawnConfig(0.8f, 3.5f, 3); // 1–3 lanes
                default: return new SpawnConfig(0.5f, 4.0f, 3); // phase 4+
            }
        }

        // Returns the frog's colour with phase-dependent probability, or a
        // random palette colour otherwise.  Keeps the field biased toward
        // playable pads in early phases without completely removing challenge.
        private PadColor PickSpawnColor(PadColor frogColor)
        {
            int bias;
            switch (_phase)
            {
                case 2:  bias = FrogBiasPhase2; break;
                case 3:  bias = FrogBiasPhase3; break;
                case 4:  bias = FrogBiasPhase4; break;
                default: bias = FrogBiasPhase1; break; // phase 0 or 1
            }
            return _rng.Next(0, 100) < bias ? frogColor : RandomColor();
        }

        // ---- Lane-clearance helpers (AC1–4) --------------------------------

        // Half the pitch between lane centres; pads assigned to a lane may have
        // drifted up to this far from their origin while still belonging to it.
        private const float LaneTolerance = 0.15f;

        // Distance between the spawn point (Y = −OffscreenMargin) and the nearest
        // existing pad whose lane X is within LaneTolerance of x.
        // Returns float.MaxValue when no pad occupies that lane.
        private float NearestPadDistanceInLane(float x)
        {
            float spawnY  = -OffscreenMargin;
            float minDist = float.MaxValue;
            foreach (var pad in _pads)
            {
                float dx = pad.X - x;
                if (dx < 0f) dx = -dx;
                if (dx < LaneTolerance)
                {
                    float dy = pad.Y - spawnY;
                    if (dy < 0f) dy = -dy;
                    if (dy < minDist) minDist = dy;
                }
            }
            return minDist;
        }

        // Returns true when no existing pad is within MinLaneClearance of the
        // spawn point in this lane.
        private bool LaneIsClear(float x) => NearestPadDistanceInLane(x) >= MinLaneClearance;

        // Returns the lane X (0.2, 0.5, or 0.8) whose nearest existing pad is
        // furthest from the spawn point.  Used as the override target when all
        // lanes are blocked by the clearance rule but the oracle demands a spawn.
        private float BestClearanceLaneX()
        {
            float d0 = NearestPadDistanceInLane(0.2f);
            float d1 = NearestPadDistanceInLane(0.5f);
            float d2 = NearestPadDistanceInLane(0.8f);
            if (d0 >= d1 && d0 >= d2) return 0.2f;
            if (d1 >= d2)              return 0.5f;
            return 0.8f;
        }

        // Returns a random Y offset in [−0.08, 0] for burst-spawned pads (AC5/6).
        // Callers must invoke this once per pad — never share a single call across
        // multiple pads in the same burst event.
        private float RandomSpawnYOffset() => -(_rng.Next(0, 8001) * 0.00001f);

        // ---- Spawn helpers --------------------------------------------------

        // Guaranteed-path force-spawn: picks a lane that satisfies MinLaneClearance,
        // places a Normal pad there, then checks the oracle.  If all lanes are blocked
        // no pad is spawned (AC3).  After the attempt, if the oracle still reports no
        // valid path — because skipping left none (AC4) — a pad is force-spawned in
        // the lane with the most clearance, overriding the block.
        //
        // ignored: pads excluded from the oracle check so that about-to-be-removed
        // pads (Tick step 3, RemovePad) are not counted as a surviving valid path.
        private void SpawnPad(PadColor color, List<PadData> ignored = null)
        {
            // Collect clear lanes (AC2).
            int     clearCount = 0;
            float[] clearLanes = new float[3];
            if (LaneIsClear(0.2f)) clearLanes[clearCount++] = 0.2f;
            if (LaneIsClear(0.5f)) clearLanes[clearCount++] = 0.5f;
            if (LaneIsClear(0.8f)) clearLanes[clearCount++] = 0.8f;

            if (clearCount > 0)
            {
                // Pick one clear lane at random and spawn (AC2).
                float chosenX = clearLanes[_rng.Next(0, clearCount)];
                SpawnPad(color, chosenX, forceNormal: true);
            }
            // else: every lane is blocked — no spawn this event (AC3).

            // AC4: if no guaranteed path exists after the (possibly skipped) spawn,
            // force-spawn in the lane with the most clearance, overriding the block.
            bool hasPath = false;
            foreach (var p in _pads)
            {
                if (ignored != null && ignored.Contains(p)) continue;
                if (PadPathOracle.IsValidForFrog(p, color, -1)) { hasPath = true; break; }
            }
            if (!hasPath)
                SpawnPad(color, BestClearanceLaneX(), forceNormal: true);
        }

        // Core spawn helper: places one pad at the given normalised x.
        // Rotten type is applied probabilistically in Phase 3+ UNLESS forceNormal
        // is true (set by guaranteed-path enforcement paths).  Drift is applied in
        // Phase 4+ regardless of forceNormal.
        // yOffset is added to −OffscreenMargin at construction (AC5); the value is
        // never subsequently modified — pads scroll at the same speed so relative
        // stagger within a burst is preserved throughout the pad's lifetime.
        private void SpawnPad(PadColor color, float x, bool forceNormal = false, float yOffset = 0f)
        {
            PadType type = (!forceNormal && _rottenEnabled && _rng.Next(0, 5) == 0)
                ? PadType.Rotten
                : PadType.Normal;

            float vx = 0f;
            float da = 0f;
            if (_driftEnabled)
            {
                da = DriftSpeed;
                vx = _rng.Next(0, 2) == 0 ? da : -da;
            }

            _pads.Add(new PadData(_nextId++, color, x, -OffscreenMargin + yOffset, type, vx, da));
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
            _pads.Add(new PadData(_nextId++, frogColor, RandomX(), -OffscreenMargin,
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
