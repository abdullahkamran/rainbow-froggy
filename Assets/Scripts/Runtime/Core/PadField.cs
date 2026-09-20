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
        // Phase 1 constants
        public const float ScrollSpeed     = 0.12f; // normalised units per second
        public const float SpawnInterval   = 1.8f;  // seconds between scheduled spawns
        public const int   InitialPadCount = 5;     // pads pre-seeded at start

        private readonly List<PadData> _pads = new List<PadData>();
        private readonly IRng          _rng;
        private int   _nextId;
        private float _spawnTimer;

        public IReadOnlyList<PadData> Pads => _pads;

        public PadField(IRng rng)
        {
            _rng        = rng;
            _spawnTimer = SpawnInterval;
        }

        // Pre-seed the field so the invariant holds from frame 0.
        // At least one pad of frogColor is guaranteed.
        public void Initialize(PadColor frogColor)
        {
            _pads.Clear();
            _nextId = 0;

            // Spread pads evenly across the visible area, starting below top.
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

            // 1. Scroll all pads down.
            foreach (var pad in _pads)
                pad.Y += ScrollSpeed * dt;

            // 2. Collect off-screen pads.
            var removed = new List<PadData>();
            foreach (var pad in _pads)
                if (pad.Y >= 1.0f)
                    removed.Add(pad);

            // 3. Guaranteed-path check: ensure at least one matching pad will
            //    survive after ALL off-screen pads are removed this tick.
            //    We must account for every pad in `removed` simultaneously —
            //    checking them one-by-one would see the others still in _pads
            //    and incorrectly conclude a survivor exists when there isn't one.
            if (CountMatchingExcluding(frogColor, removed) == 0)
                SpawnPad(frogColor); // spawns at top before the old ones leave

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

            // Check invariant before removing.
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

        // Counts matching pads while excluding every pad in the given list.
        // Used by Tick to correctly handle multiple simultaneous removals.
        private int CountMatchingExcluding(PadColor color, List<PadData> excluding)
        {
            int n = 0;
            foreach (var pad in _pads)
                if (pad.Color == color && !excluding.Contains(pad)) n++;
            return n;
        }

        private void SpawnPad(PadColor color)
        {
            _pads.Add(new PadData(_nextId++, color, RandomX(), 0f));
        }

        private PadColor RandomColor()
        {
            var colors = Phase1Colors.Active;
            return colors[_rng.Next(0, colors.Length)];
        }

        private float RandomX()
        {
            // Three lanes: 0.2, 0.5, 0.8
            int lane = _rng.Next(0, 3);
            return 0.2f + lane * 0.3f;
        }
    }
}
