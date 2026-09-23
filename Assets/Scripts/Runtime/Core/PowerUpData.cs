namespace RainbowFroggy.Core
{
    // A floating power-up pickup drifting down the river.
    // Normalised coordinates follow the same convention as PadData:
    //   X ∈ [0, 1]  — horizontal lane centre (0 = left, 1 = right)
    //   Y ∈ [0, 1]  — 0 = top of play area, 1 = bottom edge
    public sealed class PowerUpData
    {
        public readonly int         Id;
        public readonly PowerUpType Type;
        public float X;
        public float Y;

        public PowerUpData(int id, PowerUpType type, float x, float y)
        {
            Id   = id;
            Type = type;
            X    = x;
            Y    = y;
        }
    }
}
