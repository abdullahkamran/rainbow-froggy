using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    // Manages pad-attached power-up pickups.
    // Each pickup sits on one lily pad; its X/Y are mirrored from the host pad
    // every tick rather than scrolled independently.  Collection is triggered by
    // a jump-landing (TryCollectAt), not by tapping the pickup sprite directly.
    public sealed class PowerUpField
    {
        // Seconds between scheduled pickup spawn attempts.
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

        // Advance the field by dt seconds.
        // Ages each pickup, mirrors its position from the host pad, and removes
        // pickups whose host has scrolled off or whose TTL has expired.
        // Fires the spawn timer when it elapses.
        // A dt of 0 is a no-op (used by the cross-tap invariant hack in RainbowFroggyGame).
        public void Tick(float dt, IReadOnlyList<PadData> pads, PadColor frogColor)
        {
            if (dt == 0f) return;

            // Age, mirror, and prune.
            for (int i = _pickups.Count - 1; i >= 0; i--)
            {
                var pu = _pickups[i];
                pu.Age += dt;

                // Find the host pad.
                PadData host = null;
                foreach (var pad in pads)
                    if (pad.Id == pu.PadId) { host = pad; break; }

                // Remove if the host scrolled off or the pickup has expired.
                if (host == null || pu.Age >= PowerUpPlacement.LifetimeSeconds)
                {
                    _pickups.RemoveAt(i);
                    continue;
                }

                // Mirror position so the pickup moves with its pad.
                pu.X = host.X;
                pu.Y = host.Y;
            }

            _spawnTimer -= dt;
            if (_spawnTimer <= 0f)
            {
                _spawnTimer = SpawnInterval;
                SpawnPickup(pads, frogColor);
            }
        }

        // Remove a pickup by id (called when the player collects it via landing).
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

        // Check whether the given pad carries a pickup.
        // Returns true and sets 'pickup' if one is found; false otherwise.
        // Does NOT remove the pickup — caller must call RemovePickup(id) explicitly.
        public bool TryCollectAt(int padId, out PowerUpData pickup)
        {
            foreach (var pu in _pickups)
            {
                if (pu.PadId == padId)
                {
                    pickup = pu;
                    return true;
                }
            }
            pickup = null;
            return false;
        }

        // Spawn one pickup on an eligible lily pad, add it to the list, and return it.
        // Returns null if the active-pickup cap is reached or no unoccupied Normal pad
        // is available.  Public so tests can force a spawn deterministically.
        //
        // Placement: picks reward pads (pad.Color == frogColor) with probability
        // RewardPadFraction; picks trick pads (non-matching colour) otherwise.
        // Falls back to the other bucket when the preferred one is empty.
        public PowerUpData SpawnPickup(IReadOnlyList<PadData> pads, PadColor frogColor)
        {
            if (_pickups.Count >= PowerUpPlacement.MaxActivePickups) return null;

            // Type is rolled first so a CycleRng(0) still produces TimeFreeze
            // regardless of the pad-selection rolls that follow.
            PowerUpType type = PickType();

            // Separate unoccupied Normal pads into reward and trick buckets.
            var rewardPads = new List<PadData>();
            var trickPads  = new List<PadData>();

            foreach (var pad in pads)
            {
                // Only Normal pads host pickups.
                if (pad.Type != PadType.Normal) continue;

                // Skip pads that already carry a pickup.
                bool occupied = false;
                foreach (var pu in _pickups)
                    if (pu.PadId == pad.Id) { occupied = true; break; }
                if (occupied) continue;

                if (pad.Color == frogColor)
                    rewardPads.Add(pad);
                else
                    trickPads.Add(pad);
            }

            // Choose bucket: reward with probability RewardPadFraction, else trick.
            // Fall back to the other bucket if the preferred one is empty.
            bool preferReward  = _rng.Next(0, 100) < (int)(PowerUpPlacement.RewardPadFraction * 100f);
            List<PadData> preferred = preferReward ? rewardPads : trickPads;
            List<PadData> fallback  = preferReward ? trickPads  : rewardPads;

            List<PadData> bucket = preferred.Count > 0 ? preferred : fallback;
            if (bucket.Count == 0) return null;

            PadData host   = bucket[_rng.Next(0, bucket.Count)];
            var     pickup = new PowerUpData(_nextId++, type, host.X, host.Y);
            pickup.PadId   = host.Id;
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
