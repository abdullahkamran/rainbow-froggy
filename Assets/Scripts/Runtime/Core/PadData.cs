namespace RainbowFroggy.Core
{
    // Immutable identity; mutable position so PadField can scroll without
    // allocating new objects every tick.
    public sealed class PadData
    {
        public readonly int      Id;
        public readonly PadColor Color;

        // Normalised vertical position: 0 = top of play area, 1 = bottom edge.
        public float Y;

        // Horizontal lane centre, normalised to [0, 1].
        public readonly float X;

        public PadData(int id, PadColor color, float x, float y)
        {
            Id    = id;
            Color = color;
            X     = x;
            Y     = y;
        }
    }
}
