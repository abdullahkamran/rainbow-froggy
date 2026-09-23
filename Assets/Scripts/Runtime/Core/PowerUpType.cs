// Power-up types and their relative spawn weights.
// Weights are public constants so tests can assert relative probabilities
// without relying on specific RNG seeds.
namespace RainbowFroggy.Core
{
    public enum PowerUpType
    {
        TimeFreeze = 0,
        LotusBloom = 1,
        Prism      = 2,   // any-colour jumps, 2× combo step for 8 s
    }

    // Relative spawn weights used by PowerUpField.SpawnPickup.
    // A higher value means the type appears more often.
    // LotusBloomWeight is intentionally rare (strictly less than the others).
    public static class PowerUpWeights
    {
        public const int TimeFreezeWeight  = 10;
        public const int LotusBloomWeight  =  1;  // strictly < TimeFreezeWeight and StandardPadWeight
        public const int PrismWeight       =  5;
        public const int StandardPadWeight = 10;  // reference weight for normal pads
    }
}
