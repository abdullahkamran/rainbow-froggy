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
    //
    // Late Arrivals (PRD §2.1, Phase 2+):
    //   The guaranteed-path spawn is occasionally delayed by 1–2 seconds,
    //   creating tension as the player's current pad drifts toward the bottom.
    //   The invariant is preserved by the eager steps 0 and 3 below.
    //
    // Decoys (PRD §2.1, Phase 2+):
    //   When a guaranteed-path pad spawns via the periodic timer, 1–2 nearby
    //   pads of different colours appear in adjacent lane positions at the same
    //   Y, visually distracting the player.
    public sealed class PadField
    {
        public const float SpawnInterval   = 1.8f;
        public const int   InitialPadCount = 5;

        // Base scroll speed for Phase 1; scaled by SetPhase.
        private float _scrollSpeed = 0.12f;

        // Current active colour palette (grows with each phase).
        private PadColor[] _activePalette = PhaseColors.Phase1;

        // Phase 3+: 20 % of spawned pads are Rotten traps.
        private bool _rottenEnabled;

        // Phase 4+: spawned pads drift horizontally and bounce at edges.
        private bool _driftEnabled;

        // Drift speed assigned to Phase-4 pads (normalised units/second).
        private const float DriftSpeed = 0.15f;

        // Late Arrivals (Phase 2+): flag a pending delayed guaranteed-path spawn.
        private bool _lateArrivalEnabled;
        private bool _lateArrivalPending;

        // Decoys (Phase 2+): spawn incorrect-colour pads alongside guaranteed spawns.
        private bool _decoyEnabled;

        // Tracks current phase so Late Arrival frequency can scale with phase.
        private int _currentPhase = 1;

        private readonly List<PadData> _pads = new List<PadData>();
        private readonly IRng          _rng;
        private int   _nextId;
        private float _spawnTimer;

        public IReadOnlyList<PadData> Pads     => _pads;

        // Exposed for tests and diagnostics.
        public float ScrollSpeed        => _scrollSpeed;

        // Exposed so tests can observe when a Late Arrival is in flight.
        public bool LateArrivalPending  => _lateArrivalPending;

        public PadField(IRng rng)
        {
            _rng        = rng;
            _spawnTimer = SpawnInterval;
        }

        // Apply a phase transition: update scroll speed, active palette, and
        // special-pad flags.  Takes effect for all pads spawned after this call.
        public void SetPhase(int phase)
        {
            _activePalette      = PhaseColors.ForPhase(phase);
            _rottenEnabled      = phase >= 3;
            _driftEnabled       = phase >= 4;
            _lateArrivalEnabled = phase >= 2;
            _decoyEnabled       = phase >= 2;
            _currentPhase       = phase;

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
                float    y = 0.1f + i * (0.8f / InitialPadCount);
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
                SpawnPad(frogColor);

            // 1. Scroll all pads down; apply horizontal drift for Phase-4 pads.
            foreach (var pad in _pads)
            {
                pad.Y += _scrollSpeed * dt;

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

            // 4. Scheduled spawn.
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = SpawnInterval;

                if (_lateArrivalPending)
                {
                    // Late Arrival resolves: the delayed guaranteed-path pad
                    // finally arrives.  The invariant has been maintained by
                    // steps 0 and 3 throughout the delay window.
                    _lateArrivalPending = false;
                    SpawnPad(frogColor);
                    if (_decoyEnabled && TryRollDecoy())
                        SpawnDecoys(frogColor);
                }
                else
                {
                    PadColor c = CountMatching(frogColor) == 0 ? frogColor : RandomColor();

                    // Late Arrivals (Phase 2+): occasionally extend the timer by
                    // 1–2 s and flag a pending forced-colour spawn, creating
                    // tension while the player waits for the correct pad.
                    if (_lateArrivalEnabled && TryRollLateArrival())
                    {
                        _lateArrivalPending  = true;
                        _spawnTimer         += _rng.Next(10, 21) * 0.1f; // 1.0–2.0 s
                    }
                    else
                    {
                        SpawnPad(c);
                        // Decoys (Phase 2+): cluster incorrect-colour pads around
                        // guaranteed-path spawns to visually distract the player.
                        if (_decoyEnabled && c == frogColor && TryRollDecoy())
                            SpawnDecoys(frogColor);
                    }
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
                if (pad.Color == color) n++;
            return n;
        }

        // ------------------------------------------------------------------ //

        private int CountMatching(PadColor color, PadData excluding)
        {
            int n = 0;
            foreach (var pad in _pads)
                if (pad.Color == color && pad != excluding) n++;
            return n;
        }

        private int CountMatchingExcluding(PadColor color, List<PadData> excluding)
        {
            int n = 0;
            foreach (var pad in _pads)
                if (pad.Color == color && !excluding.Contains(pad)) n++;
            return n;
        }

        // Spawn a pad at a random lane position at the top of the play area.
        private void SpawnPad(PadColor color)
        {
            SpawnPad(color, RandomX(), 0f);
        }

        // Spawn a pad at an explicit position, applying Rotten/Drift flags.
        private void SpawnPad(PadColor color, float x, float y)
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

            _pads.Add(new PadData(_nextId++, color, x, y, type, vx, da));
        }

        // Spawn 1–2 decoy pads adjacent to the most-recently-added guaranteed pad.
        // Decoy colours always differ from the guaranteed pad's colour.
        private void SpawnDecoys(PadColor guaranteedColor)
        {
            // The guaranteed pad is the most-recently-added pad.
            float gx = _pads[_pads.Count - 1].X;
            float gy = _pads[_pads.Count - 1].Y;

            // Build the list of the other two lane X values.
            float[] allLanes = { 0.2f, 0.5f, 0.8f };
            var available = new List<float>();
            foreach (float lx in allLanes)
                if (System.Math.Abs(lx - gx) > 0.01f)
                    available.Add(lx);

            // Spawn 1 or 2 decoys, each in a distinct non-guaranteed lane.
            int count = _rng.Next(1, 3); // 1 or 2
            if (count > available.Count) count = available.Count;

            for (int i = 0; i < count; i++)
            {
                int   idx        = _rng.Next(0, available.Count);
                float decoyX     = available[idx];
                available.RemoveAt(idx);

                PadColor decoyColor = RandomColorExcluding(guaranteedColor);
                SpawnPad(decoyColor, decoyX, gy);
            }
        }

        // Returns true when this scheduled spawn should be delayed (Late Arrival).
        // Frequency scales with phase: Phase 2 ≈20 %, Phase 3 ≈33 %, Phase 4+ ≈50 %.
        private bool TryRollLateArrival()
        {
            int divisor = _currentPhase == 2 ? 5 : _currentPhase == 3 ? 3 : 2;
            return _rng.Next(0, divisor) == 0;
        }

        // Returns true when decoys should accompany this guaranteed-path spawn (~50 %).
        private bool TryRollDecoy()
        {
            return _rng.Next(0, 2) == 0;
        }

        private PadColor RandomColor()
        {
            return _activePalette[_rng.Next(0, _activePalette.Length)];
        }

        // Pick a random colour from the active palette that is not `excluded`.
        // Safe for palettes of size 1 (returns the only colour as a fallback).
        private PadColor RandomColorExcluding(PadColor excluded)
        {
            if (_activePalette.Length == 1) return _activePalette[0];

            // Count valid alternatives, then pick one by index to avoid looping.
            int count = 0;
            foreach (var c in _activePalette)
                if (c != excluded) count++;

            int pick = _rng.Next(0, count);
            int idx  = 0;
            foreach (var c in _activePalette)
            {
                if (c == excluded) continue;
                if (idx == pick) return c;
                idx++;
            }
            return _activePalette[0]; // unreachable; satisfies compiler
        }

        private float RandomX()
        {
            // Three lanes: 0.2, 0.5, 0.8
            int lane = _rng.Next(0, 3);
            return 0.2f + lane * 0.3f;
        }
    }
}
