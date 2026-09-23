using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    public enum PickupType { Prism = 0 }

    // Data record for a floating power-up pickup on the river.
    public sealed class PickupData
    {
        public readonly int        Id;
        public readonly PickupType Type;
        public float X;
        public float Y;

        public PickupData(int id, PickupType type, float x, float y)
        {
            Id   = id;
            Type = type;
            X    = x;
            Y    = y;
        }
    }

    // Manages floating power-up pickups that scroll down the river layer
    // at the same speed as lily pads.  A Prism pickup is spawned every
    // PrismSpawnInterval seconds in the centre lane — no RNG used so the
    // main game's seeded random stream is not disturbed.
    public sealed class PowerUpField
    {
        public const float PrismSpawnInterval = 15f;

        private readonly List<PickupData> _pickups = new List<PickupData>();
        private int   _nextId;
        private float _spawnTimer;

        public IReadOnlyList<PickupData> Pickups => _pickups;

        public PowerUpField()
        {
            _spawnTimer = PrismSpawnInterval;
        }

        // Reset to initial state; call before each new run.
        public void Reset()
        {
            _pickups.Clear();
            _nextId     = 0;
            _spawnTimer = PrismSpawnInterval;
        }

        // Advance pickups by dt seconds.  fieldScrollSpeed should match
        // PadField.ScrollSpeed so pickups move at the same rate as pads.
        public void Tick(float dt, float fieldScrollSpeed)
        {
            // Scroll all pickups toward the bottom of the play area.
            foreach (var p in _pickups)
                p.Y += fieldScrollSpeed * dt;

            // Remove off-screen pickups.
            for (int i = _pickups.Count - 1; i >= 0; i--)
                if (_pickups[i].Y >= 1.0f)
                    _pickups.RemoveAt(i);

            // Spawn a Prism pickup in the centre lane once the timer expires.
            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = PrismSpawnInterval;
                _pickups.Add(new PickupData(_nextId++, PickupType.Prism, 0.5f, 0f));
            }
        }

        // Remove a pickup by id (called when the player collects it).
        public void Remove(int id)
        {
            for (int i = _pickups.Count - 1; i >= 0; i--)
                if (_pickups[i].Id == id)
                { _pickups.RemoveAt(i); break; }
        }

        // Test seam: inject a pickup directly without waiting for the timer.
        // Returns the assigned id so the caller can pass it to CollectPickup.
        public int AddPickup(PickupType type, float x = 0.5f, float y = 0.5f)
        {
            int id = _nextId++;
            _pickups.Add(new PickupData(id, type, x, y));
            return id;
        }
    }
}
