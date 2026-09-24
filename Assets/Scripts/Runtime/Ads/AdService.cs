using System;
using UnityEngine;
#if GOOGLE_MOBILE_ADS && !UNITY_WEBGL
using GoogleMobileAds.Api;
#endif

namespace RainbowFroggy.View
{
    // Initialises AdMob and keeps one rewarded ad preloaded.
    //
    // The test App ID (ca-app-pub-3940256099942544~3347511713) is declared in
    // Assets/Plugins/Android/AndroidManifest.xml as required by the SDK; it is
    // also stored here as AppId for documentation and traceability.
    //
    // All GMA API calls are compiled only when com.google.ads.mobile is present
    // (GOOGLE_MOBILE_ADS define set via versionDefines in RainbowFroggy.Runtime.asmdef).
    // When the package is absent the class compiles and all ad operations silently no-op,
    // so the project builds on machines without the registry.
    public sealed class AdService : MonoBehaviour, IAdService
    {
        // Test App ID — mirrors the value in AndroidManifest.xml.
        private const string AppId  = "ca-app-pub-3940256099942544~3347511713";
        // Test rewarded ad unit ID.
        private const string UnitId = "ca-app-pub-3940256099942544/5224354917";

        public bool IsBusy { get; private set; }

#if GOOGLE_MOBILE_ADS && !UNITY_WEBGL
        private RewardedAd _ad;
        // Per-show callback stored so the close/fail event handlers can invoke it.
        private Action     _pendingOnAdClosed;
#endif

        private void Awake()
        {
#if GOOGLE_MOBILE_ADS && !UNITY_WEBGL
            // AppId is picked up by the SDK from AndroidManifest.xml at build time.
            // Initialize triggers SDK setup and kicks off the first ad pre-load.
            MobileAds.Initialize(_ => LoadAd());
#else
            Debug.Log("[AdService] com.google.ads.mobile not installed — ads disabled.");
#endif
        }

#if GOOGLE_MOBILE_ADS && !UNITY_WEBGL
        private void LoadAd()
        {
            _ad?.Destroy();
            _ad = null;
            RewardedAd.Load(UnitId, new AdRequest(), (ad, error) =>
            {
                if (error != null)
                {
                    Debug.LogWarning("[AdService] Load failed: " + error.GetMessage());
                    return;
                }
                _ad = ad;
                _ad.OnAdFullScreenContentClosed += OnAdClosed;
                _ad.OnAdFullScreenContentFailed += OnAdFailed;
            });
        }

        private void OnAdClosed()
        {
            IsBusy = false;
            var cb = _pendingOnAdClosed;
            _pendingOnAdClosed = null;
            LoadAd();
            cb?.Invoke();
        }

        private void OnAdFailed(AdError error)
        {
            Debug.LogWarning("[AdService] Show failed: " + error.GetMessage());
            IsBusy = false;
            var cb = _pendingOnAdClosed;
            _pendingOnAdClosed = null;
            LoadAd();
            cb?.Invoke();
        }
#endif

        public void ShowRewardedAd(Action onRewarded, Action onAdClosed = null)
        {
            if (IsBusy) return;
            IsBusy = true;
#if GOOGLE_MOBILE_ADS && !UNITY_WEBGL
            if (_ad == null)
            {
                Debug.LogWarning("[AdService] No ad loaded; cannot show.");
                IsBusy = false;
                onAdClosed?.Invoke();
                return;
            }
            _pendingOnAdClosed = onAdClosed;
            _ad.Show(reward => onRewarded?.Invoke());
#else
            Debug.Log("[AdService] com.google.ads.mobile not installed; no ad shown.");
            IsBusy = false;
            onAdClosed?.Invoke();
#endif
        }
    }
}
