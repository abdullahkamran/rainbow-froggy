using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Represents one lily pad on screen.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PadView : MonoBehaviour
    {
        public int PadId { get; private set; }

        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        public void Bind(PadData data)
        {
            PadId     = data.Id;
            _sr.color = ColorPalette.For(data.Color);
            SyncPosition(data);
        }

        public void SyncPosition(PadData data)
        {
            // Play-area world space: x in [-3.5, 3.5], y in [5, -5] (top to bottom).
            float wx = Mathf.Lerp(-3.5f, 3.5f, data.X);
            float wy = Mathf.Lerp(5f, -5f, data.Y);
            transform.localPosition = new Vector3(wx, wy, 0f);
        }
    }
}
