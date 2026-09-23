using UnityEngine;

namespace RainbowFroggy.View
{
    // Provides a single non-legacy font for all UI Text components.
    // Replaces the deprecated Unity built-in legacy shim with a proper OS font
    // so no reference to the legacy runtime font exists anywhere in the project.
    internal static class FontLibrary
    {
        private static Font _body;

        // A bold, readable sans-serif sourced from the host OS.
        // Tries a list of preferred faces in order; falls back to Unity's
        // internal default if none is available (null-safe).
        public static Font Body
        {
            get
            {
                if (_body != null) return _body;
                _body = Font.CreateDynamicFontFromOSFont(
                    new[] { "Helvetica Neue", "Helvetica", "Arial", "sans-serif" }, 32);
                return _body;
            }
        }
    }
}
