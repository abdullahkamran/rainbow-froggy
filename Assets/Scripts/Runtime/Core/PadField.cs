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

        private readonly List<PadData> _pads = new List<PadData>();
        private readonly IRng          _rng;
        private int   _nextId;
        private float _spawnTimer;

        public IReadOnlyList<PadData> Pads     => _pads;

        // Exposed for tests and diagnostics.
        public float ScrollSpeed => _scrollSpeed;

        public PadField(IRng rng)
        {
            _rng        = rng;
            _spawnTimer = SpawnInterval;
        }

        // Apply a phase transition: update scroll speed, active palette, and
        // special-pad flags.  Takes effect for all pads spawned after this call.
        public void SetPhase(int phase)
        {
            _activePalette = PhaseColors.ForPhase(phase);
            _rottenEnabled = phase >= 3;
            _driftEnabled  = phase >= 4;

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
                PadColor c = CountMatching(frogColor) == 0 ? frogColor : RandomColor();
                SpawnPad(c);
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
