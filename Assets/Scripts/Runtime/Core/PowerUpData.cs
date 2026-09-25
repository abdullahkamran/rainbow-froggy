namespace RainbowFroggy.Core
{
    // A power-up pickup resting on top of a lily pad.
    // Normalised coordinates follow the same convention as PadData:
    //   X ∈ [0, 1]  — horizontal lane centre (0 = left, 1 = right)
    //   Y ∈ [0, 1]  — 0 = top of play area, 1 = bottom edge
    // X/Y are mirrored from the host pad every tick; they are NOT scrolled
    // independently.
    public sealed class PowerUpData
    {
        public readonly int         Id;
        public readonly PowerUpType Type;
        public float X;
        public float Y;

        // Id of the lily pad this pickup is attached to.
        public int   PadId;

        // Seconds this pickup has been alive on its pad.
        // Removed by PowerUpField when Age >= PowerUpPlacement.LifetimeSeconds.
        public float Age;

        public PowerUpData(int id, PowerUpType type, float x, float y)
        {
            Id   = id;
            Type = type;
            X    = x;
            Y    = y;
        }
    }
}
