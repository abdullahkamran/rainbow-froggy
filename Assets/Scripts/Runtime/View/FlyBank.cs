using UnityEngine;

namespace RainbowFroggy.View
{
    // Persistent lifetime Golden Fly balance, stored in PlayerPrefs.
    //
    // Every system that reads or writes the lifetime balance — game-over commit,
    // challenge bonuses, wardrobe/shop unlock checks — must use LifetimeKey as
    // the PlayerPrefs key so there is a single source of truth.
    public static class FlyBank
    {
        // Canonical PlayerPrefs key for the lifetime Fly balance.
        public const string LifetimeKey = "LifetimeFlies";

        // Read the current lifetime balance (0 if never written).
        public static int Get() => PlayerPrefs.GetInt(LifetimeKey, 0);

        // Add amount to the lifetime balance and persist immediately.
        public static void Add(int amount)
        {
            PlayerPrefs.SetInt(LifetimeKey, Get() + amount);
            PlayerPrefs.Save();
        }
    }
}
