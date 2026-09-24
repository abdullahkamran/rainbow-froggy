using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    // Manages the set of Golden Fly collectibles currently active on the river.
    // Mirrors the structure of PowerUpField.
    public sealed class GoldenFlyField
    {
        // Spawn interval in seconds.  Overridden at runtime by GameBootstrap
        // after reading the GoldenFlySpawner serialized field (AC1).
        public float SpawnInterval = 5.0f;

        private readonly List<GoldenFlyData> _flies = new List<GoldenFlyData>();
        private readonly IRng                _rng;
        private float _spawnTimer;
        private int   _nextId = 1000;  // distinct range from PadField / PowerUpField ids

        public IReadOnlyList<GoldenFlyData> Flies => _flies;

        public GoldenFlyField(IRng rng)
        {
            _rng = rng;
        }

        // Advance the field by dt seconds at the given scroll speed.
        public void Tick(float dt, float scrollSpeed)
        {
            if (dt <= 0f) return;

            // Scroll all active flies downward; discard any that reach the bottom.
            for (int i = _flies.Count - 1; i >= 0; i--)
            {
                _flies[i].Y += scrollSpeed * dt;
                if (_flies[i].Y >= 1.0f)
                    _flies.RemoveAt(i);
            }

            _spawnTimer += dt;
            if (_spawnTimer >= SpawnInterval)
            {
                _spawnTimer = 0f;
                SpawnFly();
            }
        }

        // Remove a fly by id (called after the player collects it).
        public void RemoveFly(int id)
        {
            for (int i = 0; i < _flies.Count; i++)
            {
                if (_flies[i].Id == id)
                {
                    _flies.RemoveAt(i);
                    return;
                }
            }
        }

        // Spawn a new fly in one of three lanes.
        // Public so tests can force an immediate spawn.
        public void SpawnFly()
        {
            float[] lanes = { 0.2f, 0.5f, 0.8f };
            float x = lanes[_rng.Next(0, lanes.Length)];
            _flies.Add(new GoldenFlyData(_nextId++, x, 0.05f));
        }

        // Clear all flies and reset the spawn timer.
        // Called from RainbowFroggyGame.ResetRun().
        public void Reset()
        {
            _flies.Clear();
            _spawnTimer = 0f;
        }
    }
}
