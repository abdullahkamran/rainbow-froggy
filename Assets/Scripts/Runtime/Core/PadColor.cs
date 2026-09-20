// Phase 1 active colour set: Ruby Red and Electric Cyan.
// Additional colours are added in later phases; keep the enum contiguous so
// array-index look-ups stay cheap.
namespace RainbowFroggy.Core
{
    public enum PadColor
    {
        Ruby = 0,
        Cyan = 1,
    }

    public static class Phase1Colors
    {
        public static readonly PadColor[] Active = { PadColor.Ruby, PadColor.Cyan };
    }
}
