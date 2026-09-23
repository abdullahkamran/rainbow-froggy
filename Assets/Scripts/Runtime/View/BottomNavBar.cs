using UnityEngine;
using UnityEngine.UI;

namespace RainbowFroggy.View
{
    // Three-button bottom navigation bar (Wardrobe / Leaderboard / Settings).
    // Visible in the idle/menu state; hidden (alpha 0, no raycasts) during
    // active gameplay.  GameBootstrap drives the CanvasGroup alpha.
    public sealed class BottomNavBar : MonoBehaviour
    {
        // Called by GameBootstrap after all buttons and sheets are wired.
        public void Init(Button wardrobe,    BottomSheet wardrobeSheet,
                         Button leaderboard, BottomSheet leaderboardSheet,
                         Button settings,   BottomSheet settingsSheet)
        {
            wardrobe   .onClick.AddListener(() => wardrobeSheet.Open());
            leaderboard.onClick.AddListener(() => leaderboardSheet.Open());
            settings   .onClick.AddListener(() => settingsSheet.Open());
        }
    }
}
