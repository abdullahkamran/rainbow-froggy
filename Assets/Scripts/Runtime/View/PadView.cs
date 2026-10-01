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

        // Lily pad base is always solid green; the flower carries the pad's effective colour.
        private static readonly Color LilyPadGreen = new Color(0.18f, 0.60f, 0.18f);

        private SpriteRenderer _sr;
        private SpriteRenderer _flowerSr;
        private float          _wigglePhase;

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
        }

        public void Bind(PadData data)
        {
            PadId   = data.Id;
            PadType = data.Type;
            EnsureRefs();
            _sr.color       = LilyPadGreen;
            _flowerSr.color = ColorPalette.For(data.Color);
            SyncPosition(data);
        }

        public void SyncPosition(PadData data)
        {
            // Play-area world space: x in [-3.5, 3.5], y in [5, -5] (top to bottom).
            float wx = Mathf.Lerp(-3.5f, 3.5f, data.X);
            float wy = Mathf.LerpUnclamped(5f, -5f, data.Y);
            transform.localPosition = new Vector3(wx, wy, 0f);
        }
    }
}
