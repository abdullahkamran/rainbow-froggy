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
    //   • Second Chance button  (ad-revive — no-op stub)
    //   • Fly Multiplier button (ad-double-flies — no-op stub)
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

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnRestartClicked() => _onRestart?.Invoke();
    }
}
