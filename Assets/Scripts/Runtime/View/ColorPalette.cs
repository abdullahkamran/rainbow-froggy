using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Converts PadColor enum values to UnityEngine.Color.
    //
    // PRD §6 palette — all hex values are authoritative:
    //   Background / river  Indigo Deep Water  #1A2543
    //   Color 1             Ruby Red           #FF4552
    //   Color 2             Mango Yellow       #FFD035
    //   Color 3             Electric Cyan      #00E5FF
    //   Color 4             Toxic Purple       #B429F9
    //   Color 5             Neon Pink          #FF3399
    public static class ColorPalette
    {
        private static readonly Color Ruby   = new Color(1f,     0.271f, 0.322f);    // #FF4552
        private static readonly Color Cyan   = new Color(0f,     0.898f, 1f);        // #00E5FF
        private static readonly Color Mango  = new Color(1f,     0.816f, 0.208f);   // #FFD035
        private static readonly Color Purple = new Color(0.706f, 0.161f, 0.976f);   // #B429F9
        private static readonly Color Pink   = new Color(1f,     0.2f,   0.6f);     // #FF3399

        public static Color For(PadColor c)
        {
            switch (c)
            {
                case PadColor.Ruby:   return Ruby;
                case PadColor.Cyan:   return Cyan;
                case PadColor.Mango:  return Mango;
                case PadColor.Purple: return Purple;
                case PadColor.Pink:   return Pink;
                default:              return Color.white;
            }
        }

        // River/background tint — Indigo Deep Water (#1A2543).
        public static readonly Color River = new Color(0.102f, 0.145f, 0.263f);
    }
}
