// Phase colour set grows with each phase; keep enum contiguous so
// array-index look-ups stay cheap.
namespace RainbowFroggy.Core
{
    public enum PadColor
    {
        Ruby    = 0,
        Cyan    = 1,
        Mango   = 2,
        Purple  = 3,
        Pink    = 4,
        Rainbow = 5,   // sentinel for the white/shimmer pad; never in PhaseColors arrays
    }

    public enum PadType
    {
        Normal  = 0,
        Rotten  = 1,
        Rainbow = 2,   // white/shimmer pad; any frog colour can land on it
    }

    public static class PhaseColors
    {
        public static readonly PadColor[] Phase1 = { PadColor.Ruby, PadColor.Cyan };
        public static readonly PadColor[] Phase2 = { PadColor.Ruby, PadColor.Cyan, PadColor.Mango };
        public static readonly PadColor[] Phase3 = { PadColor.Ruby, PadColor.Cyan, PadColor.Mango, PadColor.Purple };
        public static readonly PadColor[] Phase4 = { PadColor.Ruby, PadColor.Cyan, PadColor.Mango, PadColor.Purple, PadColor.Pink };

        public static PadColor[] ForPhase(int phase)
        {
            switch (phase)
            {
                case 1:  return Phase1;
                case 2:  return Phase2;
                case 3:  return Phase3;
                case 4:  return Phase4;
                default: return phase < 1 ? Phase1 : Phase4;
            }
        }
    }

    // Backward-compat alias used by Phase 1 code paths.
    public static class Phase1Colors
    {
        public static PadColor[] Active => PhaseColors.Phase1;
    }
}
