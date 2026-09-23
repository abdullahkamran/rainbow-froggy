using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Represents one floating power-up pickup on screen.
    // Requires a SpriteRenderer (set by GameBootstrap.CreateSpriteQuad) and
    // a BoxCollider2D so HandleInput can detect taps via Physics2D.OverlapPoint.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PickupView : MonoBehaviour
    {
        public int PickupId { get; private set; }

        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        public void Bind(PickupData data)
        {
            PickupId = data.Id;
            _sr.color = data.Type == PickupType.Prism
                ? ColorPalette.PrismPickup
                : Color.white;
            SyncPosition(data);
        }

        public void SyncPosition(PickupData data)
        {
            // Mirror PadView's coordinate mapping:
            // normalised X [0,1] → world X [-3.5, 3.5]
            // normalised Y [0,1] → world Y [5, -5]  (0 = top, 1 = bottom)
            float wx = Mathf.Lerp(-3.5f, 3.5f, data.X);
            float wy = Mathf.Lerp(5f, -5f, data.Y);
            transform.localPosition = new Vector3(wx, wy, 0f);
        }
    }
}
