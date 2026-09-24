using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Visual representation of a Golden Fly collectible on the river.
    // Mirrors the structure of PowerUpView.
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GoldenFlyView : MonoBehaviour
    {
        public int FlyId { get; private set; }

        public void Bind(GoldenFlyData data)
        {
            FlyId = data.Id;
            SyncPosition(data);
        }

        // Update world position from normalised field coordinates.
        // Matches the mapping used by PowerUpView:
        //   X [0,1] → world [-3.5, 3.5]
        //   Y [0,1] → world [  5,  -5 ]
        public void SyncPosition(GoldenFlyData data)
        {
            float wx = Mathf.Lerp(-3.5f, 3.5f, data.X);
            float wy = Mathf.Lerp(5f, -5f, data.Y);
            transform.position = new Vector3(wx, wy, 0f);
        }
    }
}
