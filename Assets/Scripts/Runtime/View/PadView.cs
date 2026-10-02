using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Represents one lily pad on screen.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PadView : MonoBehaviour
    {
        public int     PadId   { get; private set; }
        public PadType PadType { get; private set; }

        // Wiggle constants: ±8° amplitude, ~4 s period (2π/4 ≈ 1.5708 rad/s, within the 3–5 s band).
        private const float WiggleAmplitude = 8f;
        private const float WiggleFreq      = 1.5708f; // 2π / 4 s

        // Lily pad base colour for Normal pads.
        private static readonly Color LilyPadGreen = new Color(0.18f, 0.60f, 0.18f);

        // Warm/golden tint applied to Flaky pads so they are visually distinct
        // from the standard green base without requiring new art assets.
        private static readonly Color FlakyGold = new Color(0.95f, 0.75f, 0.25f);

        private SpriteRenderer _sr;
        private SpriteRenderer _flowerSr;
        private float          _wigglePhase;

        // Cached reference to the bound PadData; updated on every Bind call so
        // SyncFlaky can read IsBlinking each frame without a separate argument.
        private PadData _data;

        private void Awake()
        {
            _sr          = GetComponent<SpriteRenderer>();
            _wigglePhase = Random.Range(0f, Mathf.PI * 2f);
            EnsureRefs();
        }

        // Idempotent setup: creates the flower child once and caches both SpriteRenderers.
        // Also called from Bind() so the order of Awake vs. Bind can never produce a null-ref.
        private void EnsureRefs()
        {
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            _sr.sprite = SpriteFactory.LilyPad();

            if (_flowerSr == null)
            {
                var flowerGO                  = new GameObject("Flower");
                flowerGO.transform.SetParent(transform, false);
                flowerGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                flowerGO.transform.localScale    = new Vector3(0.5f, 0.5f, 1f);
                _flowerSr                     = flowerGO.AddComponent<SpriteRenderer>();
                _flowerSr.sprite              = SpriteFactory.Flower();
                _flowerSr.sharedMaterial      = _sr.sharedMaterial;
                _flowerSr.sortingLayerID      = _sr.sortingLayerID;
                _flowerSr.sortingOrder        = _sr.sortingOrder;
            }
        }

        private void Update()
        {
            transform.localRotation = Quaternion.AngleAxis(
                Mathf.Sin(Time.time * WiggleFreq + _wigglePhase) * WiggleAmplitude,
                Vector3.forward);

            SyncFlaky();
        }

        public void Bind(PadData data)
        {
            _data   = data;
            PadId   = data.Id;
            PadType = data.Type;
            EnsureRefs();

            if (data.Type == PadType.Flaky)
            {
                // Flaky pads use a warm/golden base and hide the flower so the
                // player can distinguish them from normal pads at a glance.
                _sr.color         = FlakyGold;
                _flowerSr.enabled = false;
            }
            else
            {
                _sr.color         = LilyPadGreen;
                _flowerSr.enabled = true;
                _flowerSr.color   = ColorPalette.For(data.Color);
            }

            SyncPosition(data);
        }

        public void SyncPosition(PadData data)
        {
            // Play-area world space: x in [-5, 5], y in [8.5, -8.5] (top to bottom).
            float wx = Mathf.Lerp(-5f, 5f, data.X);
            float wy = Mathf.LerpUnclamped(8.5f, -8.5f, data.Y);
            transform.localPosition = new Vector3(wx, wy, 0f);
        }

        // Drive alpha oscillation when the flaky countdown is near expiry.
        // Oscillates between 0.4 and 1.0 at ~4 Hz to warn the player.
        private void SyncFlaky()
        {
            if (_data == null || _data.Type != PadType.Flaky || !_data.IsBlinking) return;
            Color c = _sr.color;
            c.a     = Mathf.Lerp(0.4f, 1.0f, (Mathf.Sin(Time.time * 8f * Mathf.PI) + 1f) * 0.5f);
            _sr.color = c;
        }
    }
}
