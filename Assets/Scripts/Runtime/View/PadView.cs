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

        private SpriteRenderer _sr;
        private float          _wigglePhase;

        private void Awake()
        {
            _sr          = GetComponent<SpriteRenderer>();
            _wigglePhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            transform.localRotation = Quaternion.AngleAxis(
                Mathf.Sin(Time.time * WiggleFreq + _wigglePhase) * WiggleAmplitude,
                Vector3.forward);
        }

        public void Bind(PadData data)
        {
            PadId     = data.Id;
            PadType   = data.Type;
            _sr.color = ColorPalette.For(data.Color);
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
