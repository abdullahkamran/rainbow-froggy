using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Procedural sprite generators for game objects that have no art assets yet.
    // All methods return a new Sprite each call; callers should cache or reuse
    // the result rather than calling per-frame.
    internal static class SpriteFactory
    {
        // Distinct icon sprite for each power-up type.
        public static Sprite PowerUp(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.TimeFreeze: return MakeTimeFreeze();
                case PowerUpType.LotusBloom: return MakeLotusBloom();
                default:                    return MakePrism();
            }
        }

        // Golden fly: yellow circle body with two wing arcs on each side.
        public static Sprite GoldenFly()
        {
            const int S = 48;
            var pixels = new Color32[S * S];
            float cx = S / 2f - 0.5f;
            float cy = S / 2f - 0.5f;

            for (int i = 0; i < pixels.Length; i++)
            {
                float x = i % S;
                float y = i / S;
                float dx = x - cx;
                float dy = y - cy;

                // Body: small circle in the centre.
                bool body = dx * dx + dy * dy <= 64f; // r=8

                // Wings: two ellipses, one on each side.
                float lwx = x - (cx - 13f);
                float lwy = y - (cy + 2f);
                bool leftWing  = (lwx * lwx) / 64f + (lwy * lwy) / 36f <= 1f; // 8×6 ellipse

                float rwx = x - (cx + 13f);
                bool rightWing = (rwx * rwx) / 64f + (lwy * lwy) / 36f <= 1f;

                if (body)
                    pixels[i] = new Color32(255, 210, 20, 255);  // golden body
                else if (leftWing || rightWing)
                    pixels[i] = new Color32(255, 240, 120, 180); // translucent wing
                else
                    pixels[i] = new Color32(0, 0, 0, 0);
            }

            return MakeSprite(S, S, pixels);
        }

        // TimeFreeze: icy-blue circle with a 6-ray asterisk.
        private static Sprite MakeTimeFreeze()
        {
            const int S = 48;
            var pixels = new Color32[S * S];
            float cx = S / 2f - 0.5f;
            float cy = S / 2f - 0.5f;

            for (int i = 0; i < pixels.Length; i++)
            {
                float x  = i % S;
                float y  = i / S;
                float dx = x - cx;
                float dy = y - cy;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);

                if (d > 22f) { pixels[i] = new Color32(0, 0, 0, 0); continue; }

                // 6 rays: 0°, 60°, 120°, 180°, 240°, 300°
                bool ray = false;
                for (int r = 0; r < 6; r++)
                {
                    float angle = r * Mathf.PI / 3f;
                    float nx    = Mathf.Cos(angle);
                    float ny    = Mathf.Sin(angle);
                    float proj  = dx * nx + dy * ny;       // length along ray
                    float perp  = Mathf.Abs(dx * ny - dy * nx); // distance from ray
                    if (perp <= 1.8f && proj >= 0f && proj <= 19f) { ray = true; break; }
                }

                if (ray)
                    pixels[i] = new Color32(220, 245, 255, 255); // bright ice
                else
                    pixels[i] = new Color32(100, 185, 225, 210); // icy blue fill
            }

            return MakeSprite(S, S, pixels);
        }

        // LotusBloom: pink circle with 6 white petal dots and a yellow centre.
        private static Sprite MakeLotusBloom()
        {
            const int S = 48;
            var pixels = new Color32[S * S];
            float cx = S / 2f - 0.5f;
            float cy = S / 2f - 0.5f;

            var petalOffsets = new Vector2[]
            {
                new Vector2( 0f,  11f), new Vector2( 9.5f,  5.5f), new Vector2( 9.5f, -5.5f),
                new Vector2( 0f, -11f), new Vector2(-9.5f, -5.5f), new Vector2(-9.5f,  5.5f),
            };

            for (int i = 0; i < pixels.Length; i++)
            {
                float x  = i % S;
                float y  = i / S;
                float dx = x - cx;
                float dy = y - cy;
                float d  = Mathf.Sqrt(dx * dx + dy * dy);

                if (d > 22f) { pixels[i] = new Color32(0, 0, 0, 0); continue; }

                // Centre dot.
                if (d <= 5f)         { pixels[i] = new Color32(255, 215, 60, 255); continue; }

                // Petal dots.
                bool petal = false;
                foreach (var po in petalOffsets)
                {
                    float pdx = dx - po.x;
                    float pdy = dy - po.y;
                    if (pdx * pdx + pdy * pdy <= 22f) { petal = true; break; } // r≈4.7
                }

                pixels[i] = petal
                    ? new Color32(255, 255, 255, 240)
                    : new Color32(235, 115, 175, 220);
            }

            return MakeSprite(S, S, pixels);
        }

        // Prism: circle with 6 diagonal rainbow bands.
        private static Sprite MakePrism()
        {
            const int S = 48;
            var pixels = new Color32[S * S];
            float cx = S / 2f - 0.5f;
            float cy = S / 2f - 0.5f;

            var bands = new Color32[]
            {
                new Color32(255,  70,  70, 255),
                new Color32(255, 155,  40, 255),
                new Color32(255, 235,  55, 255),
                new Color32( 70, 215,  70, 255),
                new Color32( 55, 130, 255, 255),
                new Color32(175,  70, 255, 255),
            };

            for (int i = 0; i < pixels.Length; i++)
            {
                float x  = i % S;
                float y  = i / S;
                float dx = x - cx;
                float dy = y - cy;

                if (dx * dx + dy * dy > 22f * 22f) { pixels[i] = new Color32(0, 0, 0, 0); continue; }

                // Map diagonal position (x+y) to band.
                float t    = (x + y) / (S * 2f - 2f) * bands.Length;
                int   band = Mathf.Clamp((int)t, 0, bands.Length - 1);
                pixels[i] = bands[band];
            }

            return MakeSprite(S, S, pixels);
        }

        private static Sprite MakeSprite(int w, int h, Color32[] pixels)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), w);
        }
    }
}
