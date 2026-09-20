using System;

namespace RainbowFroggy.Core
{
    // Thin seam so tests can inject deterministic sequences.
    public interface IRng
    {
        /// Returns a value in [minInclusive, maxExclusive).
        int Next(int minInclusive, int maxExclusive);
    }

    public sealed class SeededRng : IRng
    {
        private readonly Random _rnd;

        public SeededRng(int seed) => _rnd = new Random(seed);

        public int Next(int minInclusive, int maxExclusive) =>
            _rnd.Next(minInclusive, maxExclusive);
    }
}
