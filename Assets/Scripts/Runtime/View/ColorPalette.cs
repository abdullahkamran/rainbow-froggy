using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Converts PadColor enum values to UnityEngine.Color.
    public static class ColorPalette
    {
        // Phase 1 colours — Ruby Red and Electric Cyan.
        private static readonly Color Ruby = new Color(1f, 0.271f, 0.322f); // #FF4552
        private static readonly Color Cyan = new Color(0f, 0.898f, 1f);     // #00E5FF

        public static Color For(PadColor c)
        {
            switch (c)
            {
                case PadColor.Ruby: return Ruby;
                case PadColor.Cyan: return Cyan;
                default:            return Color.white;
            }
        }

        // River/background tint — a calm dark teal.
        public static readonly Color River = new Color(0.047f, 0.18f, 0.22f);
    }
}
