using UnityEngine;

namespace RainbowFroggy.View
{
    // Configuration component for Golden Fly spawning.
    //
    // Attach to the Bootstrap GameObject alongside GameBootstrap.  GameBootstrap
    // reads SpawnIntervalSeconds in Start() and forwards it to GoldenFlyField so
    // the rate is tunable from the Inspector without code changes (AC1).
    //
    // The [SerializeField] attribute makes _spawnIntervalSeconds visible and
    // editable in the Unity Inspector.
    public sealed class GoldenFlySpawner : MonoBehaviour
    {
        [SerializeField] float _spawnIntervalSeconds = 5.0f;

        public float SpawnIntervalSeconds => _spawnIntervalSeconds;
    }
}
