using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Simple coloured quad that represents the frog.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class FrogView : MonoBehaviour
    {
        private SpriteRenderer _sr;

        private void Awake() => _sr = GetComponent<SpriteRenderer>();

        public void SetColor(PadColor c) => _sr.color = ColorPalette.For(c);
    }
}
