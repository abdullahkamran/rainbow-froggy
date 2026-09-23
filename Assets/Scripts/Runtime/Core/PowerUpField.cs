using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    // Manages floating power-up pickups that scroll down the river alongside
    // the lily-pad field.  Pickups are spawned on a timer using weighted-random
    // selection.
    public sealed class PowerUpField
    {
        // Seconds between scheduled pickup spawns.
        public const float SpawnInterval = 6.0f;

        private readonly List<PowerUpData> _pickups = new List<PowerUpData>();
        private readonly IRng              _rng;
        private int   _nextId;
        private float _spawnTimer;

        public IReadOnlyList<PowerUpData> Pickups => _pickups;

        public PowerUpField(IRng rng)
        {
            _rng        = rng;
            _spawnTimer = SpawnInterval;
        }

        // Reset all state so the field can be reused across restarts.
        public void Reset()
        {
            _pickups.Clear();
            _nextId     = 0;
            _spawnTimer = SpawnInterval;
        }

        // Advance the field by dt seconds at the given scroll speed.
        // Pickups that scroll past the bottom (Y >= 1) are removed automatically.
        // A dt of 0 is a no-op (used by the cross-tap invariant hack in RainbowFroggyGame).
        public void Tick(float dt, float scrollSpeed)
        {
            if (dt == 0f) return;

            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                _pickups[i].Y += scrollSpeed * dt;
                if (_pickups[i].Y >= 1.0f)
                    _pickups.RemoveAt(i);
            }

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = SpawnInterval;
                SpawnPickup();
            }
        }

        // Remove a pickup by id (called when the player collects it).
        public void RemovePickup(int id)
        {
            for (int i = 0; i < _pickups.Count; i++)
            {
                if (_pickups[i].Id == id)
                {
                    _pickups.RemoveAt(i);
                    return;
                }
            }
        }

        // Spawn one pickup using weighted-random type selection, add it to the
        // list, and return it.  Public so tests can force a spawn deterministically.
        public PowerUpData SpawnPickup()
        {
            PowerUpType type    = PickType();
            float       x       = 0.2f + _rng.Next(0, 3) * 0.3f; // lanes: 0.2, 0.5, 0.8
            var         pickup  = new PowerUpData(_nextId++, type, x, 0.05f);
            _pickups.Add(pickup);
            return pickup;
        }

        // ------------------------------------------------------------------ //

        // Weighted-random type selection across all three power-up types.
        private PowerUpType PickType()
        {
            int total = PowerUpWeights.TimeFreezeWeight
                      + PowerUpWeights.LotusBloomWeight
                      + PowerUpWeights.PrismWeight;
            int roll = _rng.Next(0, total);
            if (roll < PowerUpWeights.TimeFreezeWeight)
                return PowerUpType.TimeFreeze;
            roll -= PowerUpWeights.TimeFreezeWeight;
            if (roll < PowerUpWeights.PrismWeight)
                return PowerUpType.Prism;
            return PowerUpType.LotusBloom;
        }
    }
}
