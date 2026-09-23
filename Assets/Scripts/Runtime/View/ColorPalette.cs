using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Converts PadColor enum values to UnityEngine.Color.
    //
    // PRD §6 palette — all hex values are the single source of truth.
    // Named string constants below are locally verifiable (AC2, AC3).
    public static class ColorPalette
    {
        // ---- Hex string constants (verifiable by reading this file) ----

        public const string RiverHex        = "#1A2543"; // AC2: Indigo Deep Water
        public const string RubyRedHex      = "#FF4552"; // AC3: Ruby Red
        public const string MangoYellowHex  = "#FFD035"; // AC3: Mango Yellow
        public const string ElectricCyanHex = "#00E5FF"; // AC3: Electric Cyan
        public const string ToxicPurpleHex  = "#B429F9"; // AC3: Toxic Purple
        public const string NeonPinkHex     = "#FF3399"; // AC3: Neon Pink
        public const string RainbowPadHex   = "#FFFFFF"; // Rainbow Pad: white/shimmer
        public const string PrismPickupHex  = "#FFE033"; // Prism pickup: bright gold

        // ---- Named Color constants (AC3) ----

        public static readonly Color RubyRed      = new Color(1f,     0.271f, 0.322f); // #FF4552
        public static readonly Color MangoYellow   = new Color(1f,     0.816f, 0.208f); // #FFD035
        public static readonly Color ElectricCyan  = new Color(0f,     0.898f, 1f);     // #00E5FF
        public static readonly Color ToxicPurple   = new Color(0.706f, 0.161f, 0.976f); // #B429F9
        public static readonly Color NeonPink      = new Color(1f,     0.2f,   0.6f);   // #FF3399

        // River / background tint — Indigo Deep Water (AC2: #1A2543).
        public static readonly Color River = new Color(0.102f, 0.145f, 0.263f); // #1A2543

        // Rainbow Pad tint — pure white so the shimmer overlay shows through.
        public static readonly Color RainbowPad   = Color.white;                // #FFFFFF
        // Prism pickup tint — bright gold, visible against the river.
        public static readonly Color PrismPickup  = new Color(1f, 0.878f, 0.2f); // #FFE033

        // Internal aliases for For() — keeps the switch readable.
        private static readonly Color _ruby   = RubyRed;
        private static readonly Color _cyan   = ElectricCyan;
        private static readonly Color _mango  = MangoYellow;
        private static readonly Color _purple = ToxicPurple;
        private static readonly Color _pink   = NeonPink;

        public static Color For(PadColor c)
        {
            switch (c)
            {
                case PadColor.Ruby:    return _ruby;
                case PadColor.Cyan:    return _cyan;
                case PadColor.Mango:   return _mango;
                case PadColor.Purple:  return _purple;
                case PadColor.Pink:    return _pink;
                case PadColor.Rainbow: return RainbowPad;
                default:               return Color.white;
            }
        }
    }
}
