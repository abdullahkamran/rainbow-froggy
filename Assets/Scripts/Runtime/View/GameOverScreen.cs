using System;
using UnityEngine;
using UnityEngine.UI;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Game-over panel shown after either failure type.
    //
    // Required elements (AC7):
    //   • Failure-type header  ("Wrong Pad!" | "Swept Away!")
    //   • Score value for the current run
    //   • Flies earned for the current run
    //   • Second Chance button  (ad-revive)
    //   • Fly Multiplier button (ad-double-flies)
    //   • Restart button        (in-place reset, no scene load)
    //
    // NOTE: gameObject starts inactive; Init() must be called before Show().
    // Restart is handled by a callback injected at Init time so this class
    // has no dependency on SceneManager.
    public sealed class GameOverScreen : MonoBehaviour
    {
        private Text       _headerLabel;
        private Text       _scoreLabel;
        private Text       _fliesLabel;
        private Button     _secondChanceButton;
        private Button     _flyMultiplierButton;
        private Button     _restartButton;
        private Action     _onRestart;

        // Extended elements wired by InitHighScore (optional; safe to call after Init).
        private Text       _bestScoreLabel;
        private GameObject _newHighScoreGO;

        // Challenge-complete banner wired by InitChallengeBanner (optional; safe to skip in tests).
        private Text   _challengeLabel;
        private string _challengeName;
        private int    _challengeBonus;

        // Ad service and revive callback wired by InitAds (optional; safe to skip in tests).
        private IAdService _ads;
        private Action     _onRevive;

        // Fly count set by GameBootstrap before Show(); committed to FlyBank on restart
        // or doubled-then-committed on Fly Multiplier reward.
        private int  _pendingFlies;
        private bool _fliesCommitted;

        // Called by GameBootstrap after all child elements exist.
        public void Init(Text   header,       Text   scoreLabel,    Text   fliesLabel,
                         Button secondChance, Button flyMultiplier, Button restart,
                         Action onRestart)
        {
            _headerLabel         = header;
            _scoreLabel          = scoreLabel;
            _fliesLabel          = fliesLabel;
            _secondChanceButton  = secondChance;
            _flyMultiplierButton = flyMultiplier;
            _restartButton       = restart;
            _onRestart           = onRestart;

            _restartButton.onClick.AddListener(OnRestartClicked);
            gameObject.SetActive(false);
        }

        // Wire the optional high-score elements created by GameBootstrap.
        public void InitHighScore(Text bestScoreLabel, GameObject newHighScoreGO)
        {
            _bestScoreLabel  = bestScoreLabel;
            _newHighScoreGO  = newHighScoreGO;
            if (_newHighScoreGO != null) _newHighScoreGO.SetActive(false);
        }

        // Wire the optional challenge-complete banner created by GameBootstrap.
        public void InitChallengeBanner(Text label)
        {
            _challengeLabel = label;
            if (_challengeLabel != null) _challengeLabel.gameObject.SetActive(false);
        }

        // Store the challenge name and bonus for the next Show() call.  Called by
        // GameBootstrap immediately after SetPendingFlies().
        public void SetChallengeBanner(string name, int bonus)
        {
            _challengeName  = name;
            _challengeBonus = bonus;
        }

        // Wire the ad service and revive callback.  Called by GameBootstrap after Init.
        // Second Chance and Fly Multiplier onClick listeners are added here.
        public void InitAds(IAdService ads, Action onRevive)
        {
            _ads      = ads;
            _onRevive = onRevive;
            _secondChanceButton.onClick.AddListener(OnSecondChanceClicked);
            _flyMultiplierButton.onClick.AddListener(OnFlyMultiplierClicked);
        }

        // Set the fly count to commit on restart.  Called by GameBootstrap in place of
        // the direct FlyBank.Add that used to run before Show(); bonus is the
        // daily-challenge amount already computed.
        public void SetPendingFlies(int flies, int bonus)
        {
            _pendingFlies   = flies + bonus;
            _fliesCommitted = false;
        }

        // Original 3-arg Show — preserved so existing tests are unaffected.
        public void Show(GameScreen reason, int score, int flies)
        {
            _headerLabel.text = reason == GameScreen.MisstepGameOver
                ? "Wrong Pad!"
                : "Swept Away!";
            _scoreLabel.text = "Score: " + score;
            _fliesLabel.text = "Flies: " + flies;

            if (_bestScoreLabel  != null) _bestScoreLabel.gameObject.SetActive(false);
            if (_newHighScoreGO  != null) _newHighScoreGO.SetActive(false);

            ApplyChallengeBanner();
            SetAdButtonsInteractable(true);
            gameObject.SetActive(true);
        }

        // Extended Show used by GameBootstrap to display best score and new-high indicator.
        public void Show(GameScreen reason, int score, int flies, int highScore, bool isNewHigh)
        {
            _headerLabel.text = reason == GameScreen.MisstepGameOver
                ? "Wrong Pad!"
                : "Swept Away!";
            _scoreLabel.text = "Score: " + score;
            _fliesLabel.text = "Flies: " + flies;

            if (_bestScoreLabel != null)
            {
                _bestScoreLabel.text = "Best: " + highScore;
                _bestScoreLabel.gameObject.SetActive(true);
            }

            if (_newHighScoreGO != null)
                _newHighScoreGO.SetActive(isNewHigh);

            ApplyChallengeBanner();
            SetAdButtonsInteractable(true);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // Show or hide the challenge-complete banner depending on whether a bonus was earned.
        private void ApplyChallengeBanner()
        {
            if (_challengeLabel == null) return;
            if (_challengeBonus > 0)
            {
                _challengeLabel.color = new Color(1f, 0.85f, 0.2f); // golden
                _challengeLabel.text  = string.Format("✓ {0}  +{1} flies",
                                                      _challengeName, _challengeBonus);
                _challengeLabel.gameObject.SetActive(true);
            }
            else
            {
                _challengeLabel.gameObject.SetActive(false);
            }
        }

        // Enable or disable both ad-gated buttons together.
        // Called before showing an ad (disable) and on dismissed/failed ad (re-enable).
        private void SetAdButtonsInteractable(bool value)
        {
            if (_secondChanceButton  != null) _secondChanceButton.interactable  = value;
            if (_flyMultiplierButton != null) _flyMultiplierButton.interactable = value;
        }

        // Commit pending flies to FlyBank exactly once per game-over.
        private void CommitFlies(int amount)
        {
            if (_fliesCommitted) return;
            _fliesCommitted = true;
            if (amount > 0) FlyBank.Add(amount);
        }

        private void OnRestartClicked()
        {
            CommitFlies(_pendingFlies);
            _onRestart?.Invoke();
        }

        // Tapping Second Chance shows a rewarded ad; on reward the panel is hidden
        // and the revive callback returns the player to idle with the run intact.
        private void OnSecondChanceClicked()
        {
            if (_ads == null || _ads.IsBusy) return;
            SetAdButtonsInteractable(false);
            bool rewarded = false;
            _ads.ShowRewardedAd(
                onRewarded: () =>
                {
                    rewarded = true;
                    Hide();
                    _onRevive?.Invoke();
                },
                onAdClosed: () =>
                {
                    // Re-enable buttons only if the player dismissed without reward.
                    if (!rewarded) SetAdButtonsInteractable(true);
                });
        }

        // Tapping Fly Multiplier shows a rewarded ad; on reward the pending fly count
        // is doubled, the label is updated, and the doubled amount is committed to FlyBank.
        private void OnFlyMultiplierClicked()
        {
            if (_ads == null || _ads.IsBusy) return;
            SetAdButtonsInteractable(false);
            bool rewarded = false;
            _ads.ShowRewardedAd(
                onRewarded: () =>
                {
                    rewarded = true;
                    int doubled = _pendingFlies * 2;
                    _fliesLabel.text = "Flies: " + doubled;
                    CommitFlies(doubled);
                    // Buttons remain disabled — multiplier is one-time per game-over.
                },
                onAdClosed: () =>
                {
                    if (!rewarded)
                    {
                        // Dismissed without reward: re-enable both buttons.
                        SetAdButtonsInteractable(true);
                    }
                    else
                    {
                        // Rewarded: Fly Multiplier is one-time per game-over, stays disabled.
                        // Second Chance was never used; re-enable it so the player can still revive.
                        if (_secondChanceButton != null) _secondChanceButton.interactable = true;
                    }
                });
        }
    }
}
