namespace RainbowFroggy.Core
{
    // Immutable identity; mutable position so PadField can scroll without
    // allocating new objects every tick.
    public sealed class PadData
    {
        public readonly int      Id;
        public readonly PadColor Color;
        public readonly PadType  Type;

        // Normalised vertical position: 0 = top of play area, 1 = bottom edge.
        // Negative values are above the viewport; values > 1 are below it.
        public float Y;

        // Horizontal lane centre, normalised to [0, 1].  Mutable so drift can
        // update it each tick without allocating a new PadData.
        public float X;

        // Horizontal drift velocity in normalised units/second (0 = no drift).
        public float VelocityX;

        // Maximum drift speed assigned at spawn; kept for reference.
        public float DriftAmplitude;

        // Flaky-pad countdown state.  All three fields are mutated by RainbowFroggyGame
        // on landing and each tick; they carry no meaning for other pad types.
        public bool  FlakeyCountdownActive;
        public float FlakeyTimeRemaining;
        public bool  IsBlinking;

        public PadData(int id, PadColor color, float x, float y,
                       PadType type = PadType.Normal,
                       float velocityX = 0f, float driftAmplitude = 0f)
        {
            Id             = id;
            Color          = color;
            Type           = type;
            X              = x;
            Y              = y;
            VelocityX      = velocityX;
            DriftAmplitude = driftAmplitude;
        }

        // Returns true if a frog of the given colour may land on this pad.
        // Lotus pads are wildcards and accept every frog colour.
        // Flaky pads also accept every frog colour unconditionally.
        public bool CanLand(PadColor frogColor) =>
            Type == PadType.Lotus || Type == PadType.Flaky || Color == frogColor;
    }
}
