using System;

namespace RainbowFroggy.View
{
    // Abstraction over the rewarded-ad SDK so GameOverScreen has no direct SDK dependency.
    // When com.google.ads.mobile is absent AdService no-ops; this interface keeps
    // GameOverScreen compilable regardless of whether the package is installed.
    public interface IAdService
    {
        // True while an ad load or show is in progress.
        bool IsBusy { get; }

        // Show a rewarded ad.
        // onRewarded fires (on the Unity main thread) when the player earns the reward.
        // onAdClosed fires when the ad UI closes — whether rewarded or dismissed.
        // If no ad is loaded onRewarded is not called; onAdClosed fires immediately.
        void ShowRewardedAd(Action onRewarded, Action onAdClosed = null);
    }
}
