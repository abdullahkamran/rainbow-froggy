namespace RainbowFroggy.Core
{
    // Data record for a single Golden Fly collectible on the river.
    // Mirrors the structure of PowerUpData.
    public sealed class GoldenFlyData
    {
        public readonly int Id;
        public float X;  // normalised [0, 1]
        public float Y;  // normalised [0, 1]; increases as the fly scrolls toward the bottom

        public GoldenFlyData(int id, float x, float y)
        {
            Id = id;
            X  = x;
            Y  = y;
        }
    }
}
