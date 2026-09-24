using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Manages all in-game audio: BGM looping with per-phase tempo scaling
    // and one-shot SFX for jumps, landings, game-over events, and power-ups.
    // One instance lives on the AudioRig root GameObject created by GameBootstrap.
    public sealed class AudioService : MonoBehaviour
    {
        private AudioSource _bgm;   // looping background music
        private AudioSource _sfx;   // one-shot sound effects

        // Double-fire guard for jump SFX (AC9): a second PlayJump call within
        // the same frame or within a 50 ms real-time window is silently dropped.
        private int   _jumpLastFrame     = -1;
        private float _jumpCooldownUntil = 0f;
        private const float JumpCooldownSeconds = 0.05f;

        // ------------------------------------------------------------------ //
        // Pitch helpers (static so tests can call them without a scene)
        // ------------------------------------------------------------------ //

        // BGM tempo table (AC2).
        //   Phase 1: 1.00   Phase 2: 1.04   Phase 3: 1.08   Phase 4: 1.12
        public static float PitchForPhase(int phase)
        {
            switch (phase)
            {
                case 2:  return 1.04f;
                case 3:  return 1.08f;
                case 4:  return 1.12f;
                default: return 1.00f;
            }
        }

        // Jump SFX pitch formula (AC3): pitch = 1.0 + (multiplierTier − 1) × 0.15
        public static float PitchForTier(int tier)
        {
            return 1f + (tier - 1) * 0.15f;
        }

        // ------------------------------------------------------------------ //
        // Initialisation (called by GameBootstrap right after AddComponent)
        // ------------------------------------------------------------------ //

        public void Init()
        {
            // BGM: loops seamlessly from the moment gameplay begins (AC1).
            _bgm             = gameObject.AddComponent<AudioSource>();
            _bgm.clip        = AudioLibrary.Load(AudioLibrary.BgmClip);
            _bgm.loop        = true;
            _bgm.playOnAwake = false; // Play() is called explicitly below
            _bgm.volume      = 0.55f;
            _bgm.Play();

            // SFX: single source; PlayOneShot allows overlapping short clips.
            _sfx             = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.volume      = 1.00f;
        }

        // ------------------------------------------------------------------ //
        // BGM controls
        // ------------------------------------------------------------------ //

        // Adjust BGM tempo to reflect the current gameplay phase (AC2).
        public void SetPhase(int phase)
        {
            if (_bgm != null)
                _bgm.pitch = PitchForPhase(phase);
        }

        // ------------------------------------------------------------------ //
        // SFX one-shots
        // ------------------------------------------------------------------ //

        // Plays the jump bloop; pitch scales with the combo multiplier tier (AC3).
        // Calls within the same frame or within 50 ms are ignored (AC9).
        public void PlayJump(int multiplierTier)
        {
            int frameNow = Time.frameCount;
            if (frameNow == _jumpLastFrame)                     return;
            if (Time.realtimeSinceStartup < _jumpCooldownUntil) return;

            _jumpLastFrame       = frameNow;
            _jumpCooldownUntil   = Time.realtimeSinceStartup + JumpCooldownSeconds;

            // AC3: set pitch before the play call.
            _sfx.pitch = PitchForTier(multiplierTier);
            _sfx.PlayOneShot(AudioLibrary.Load(AudioLibrary.JumpClip));
        }

        // Plays the landing "ting" — call inside the landing callback, not on
        // jump initiation, so it fires at the moment of arrival (AC4).
        public void PlayLanding()
        {
            _sfx.pitch = 1f;
            _sfx.PlayOneShot(AudioLibrary.Load(AudioLibrary.LandingClip));
        }

        // Plays the waterfall / swept-off-screen SFX (AC5).
        public void PlayWaterfall()
        {
            _sfx.pitch = 1f;
            _sfx.PlayOneShot(AudioLibrary.Load(AudioLibrary.WaterfallClip));
        }

        // Plays the wrong-colour misstep SFX — distinct clip from waterfall (AC6).
        public void PlayMisstep()
        {
            _sfx.pitch = 1f;
            _sfx.PlayOneShot(AudioLibrary.Load(AudioLibrary.MisstepClip));
        }

        // Plays the Rainbow Pad activation chimes (AC7).
        public void PlayRainbowPad()
        {
            _sfx.pitch = 1f;
            _sfx.PlayOneShot(AudioLibrary.Load(AudioLibrary.RainbowClip));
        }

        // Plays the appropriate power-up activation SFX (AC7).
        // Each PowerUpType triggers a distinct clip.
        public void PlayPowerUp(PowerUpType type)
        {
            _sfx.pitch = 1f;
            switch (type)
            {
                case PowerUpType.Prism:
                    _sfx.PlayOneShot(AudioLibrary.Load(AudioLibrary.PrismClip));
                    break;
                case PowerUpType.TimeFreeze:
                    _sfx.PlayOneShot(AudioLibrary.Load(AudioLibrary.FreezeClip));
                    break;
                // LotusBloom: no dedicated SFX in PRD §9; no sound played.
            }
        }

        // ------------------------------------------------------------------ //
        // Mute (AC8)
        // ------------------------------------------------------------------ //

        // Silence or restore all Unity audio via AudioListener.pause.
        // Persisting the preference is the caller's responsibility.
        public void SetMuted(bool muted)
        {
            AudioListener.pause = muted;
        }
    }
}
