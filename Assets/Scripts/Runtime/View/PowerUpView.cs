using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Represents one power-up pickup floating on the river.
    // Stores the pickup id and type so GameBootstrap can dispatch collection
    // when the player taps it.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PowerUpView : MonoBehaviour
    {
        public int         PickupId   { get; private set; }
        public PowerUpType PickupType { get; private set; }

        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        public void Bind(PowerUpData data)
        {
            PickupId   = data.Id;
            PickupType = data.Type;
            _sr.color  = ColorForType(data.Type);
            SyncPosition(data);
        }

        public void SyncPosition(PowerUpData data)
        {
            // Same world-space convention as PadView: x in [-3.5, 3.5], y in [5, -5].
            // The +0.4 y offset places the pickup visually above the centre of its pad.
            float wx = Mathf.Lerp(-3.5f, 3.5f, data.X);
            float wy = Mathf.LerpUnclamped(5f, -5f, data.Y);
            transform.localPosition = new Vector3(wx, wy + 0.4f, 0f);
        }

        private static Color ColorForType(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.TimeFreeze: return new Color(0.6f, 0.9f, 1.0f); // icy blue
                case PowerUpType.LotusBloom: return new Color(1.0f, 0.5f, 0.8f); // lotus pink
                default: return Color.white;
            }
        }
    }
}
