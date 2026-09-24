using UnityEngine;

namespace RainbowFroggy.View
{
    // Resolves AudioClips by name from Resources/Audio/<name>.
    // Falls back to synthesized placeholder clips when no asset file is found
    // so the audio pipeline works in a project with no binary audio assets.
    // Synthesized clips use attack/decay envelopes to avoid click-popping.
    internal static class AudioLibrary
    {
        // Canonical clip names; paths are Resources/Audio/<name>.
        // Replace the matching file under Assets/Resources/Audio/ to swap in
        // a real asset without changing any code.
        public const string BgmClip       = "bgm";
        public const string JumpClip      = "jump";
        public const string LandingClip   = "landing";
        public const string WaterfallClip = "waterfall";
        public const string MisstepClip   = "misstep";
        public const string RainbowClip   = "rainbow";
        public const string PrismClip     = "prism";
        public const string FreezeClip    = "timefreeze";

        // Load a clip by name.  A real asset in Resources/Audio/ takes priority;
        // a synthesized placeholder is returned when none exists.
        public static AudioClip Load(string name)
        {
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            return clip != null ? clip : Synthesize(name);
        }

        // ------------------------------------------------------------------ //
        // Placeholder synthesis
        // ------------------------------------------------------------------ //

        // Parameters chosen to produce clearly distinct, non-intrusive sounds.
        private static AudioClip Synthesize(string name)
        {
            switch (name)
            {
                case BgmClip:
                    // Warm, low-register sine — long enough to feel like a loop.
                    return BuildTone(name, freq: 220f, seconds: 4f,
                                     kind: WaveKind.Sine, vol: 0.30f,
                                     loopFriendly: true);
                case JumpClip:
                    // Snappy high-pitched bloop.
                    return BuildTone(name, freq: 660f, seconds: 0.12f,
                                     kind: WaveKind.Sine, vol: 0.40f);
                case LandingClip:
                    // Bright glassy "ting".
                    return BuildTone(name, freq: 1046f, seconds: 0.10f,
                                     kind: WaveKind.Triangle, vol: 0.35f);
                case WaterfallClip:
                    // Low rumbling fade — whoosh approximation.
                    return BuildTone(name, freq: 90f, seconds: 0.80f,
                                     kind: WaveKind.Triangle, vol: 0.40f);
                case MisstepClip:
                    // Mid-range hollow tone — underwater gurgle approximation.
                    return BuildTone(name, freq: 180f, seconds: 0.50f,
                                     kind: WaveKind.Sine, vol: 0.40f);
                case RainbowClip:
                    // Bright ethereal chime.
                    return BuildTone(name, freq: 1760f, seconds: 0.30f,
                                     kind: WaveKind.Sine, vol: 0.35f);
                case PrismClip:
                    // Warm triangle-wave chord approximation.
                    return BuildTone(name, freq: 440f, seconds: 0.40f,
                                     kind: WaveKind.Triangle, vol: 0.40f);
                case FreezeClip:
                    // Deep bass drop.
                    return BuildTone(name, freq: 60f, seconds: 0.60f,
                                     kind: WaveKind.Sine, vol: 0.40f);
                default:
                    return BuildTone(name, freq: 440f, seconds: 0.20f,
                                     kind: WaveKind.Sine, vol: 0.35f);
            }
        }

        private enum WaveKind { Sine, Triangle }

        // Builds a mono AudioClip at 22 050 Hz with a trapezoidal amplitude
        // envelope (attack 5 %, sustain, decay 25 %) to prevent click-popping.
        // loopFriendly: cross-fades the tail into silence and the head from
        // silence over the last/first 5 % so AudioSource.loop plays cleanly.
        private static AudioClip BuildTone(string clipName,
                                           float freq, float seconds,
                                           WaveKind kind, float vol,
                                           bool loopFriendly = false)
        {
            const int sampleRate = 22050;
            int       count      = Mathf.Max(1, Mathf.RoundToInt(seconds * sampleRate));
            var       data       = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t    = (float)i / sampleRate;
                float wave = kind == WaveKind.Triangle
                    ? 2f * Mathf.Abs(2f * ((t * freq) % 1f) - 1f) - 1f
                    : Mathf.Sin(t * freq * 2f * Mathf.PI);

                float env = AttackDecay((float)i / count,
                                        attackFrac: 0.05f, decayFrac: 0.25f);
                data[i] = wave * env * vol;
            }

            if (loopFriendly && count > 40)
            {
                // Fade the tail to zero and the head from zero so the loop
                // seam is inaudible.
                int xfLen = count / 20;
                for (int i = 0; i < xfLen; i++)
                {
                    float alpha = (float)i / xfLen;
                    data[count - xfLen + i] *= 1f - alpha; // tail → silence
                    data[i]                 *= alpha;       // head from silence
                }
            }

            var clip = AudioClip.Create(clipName, count,
                                        channels: 1, frequency: sampleRate,
                                        stream: false);
            clip.SetData(data, 0);
            return clip;
        }

        // Smooth trapezoidal envelope — value in [0, 1].
        private static float AttackDecay(float norm, float attackFrac, float decayFrac)
        {
            if (norm < attackFrac)     return norm / attackFrac;
            if (norm > 1f - decayFrac) return (1f - norm) / decayFrac;
            return 1f;
        }
    }
}
