using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Shared game-over panel.  The header text changes based on failure type.
    // NOTE: gameObject starts inactive; Init() must be called before Show().
    public sealed class GameOverScreen : MonoBehaviour
    {
        private Text   _headerLabel;
        private Button _restartButton;

        // Called by GameBootstrap after constructing the UI children.
        public void Init(Text header, Button restart)
        {
            _headerLabel   = header;
            _restartButton = restart;
            _restartButton.onClick.AddListener(OnRestart);
            gameObject.SetActive(false);
        }

        public void Show(GameScreen reason)
        {
            _headerLabel.text = reason == GameScreen.MisstepGameOver
                ? "Wrong Pad!"
                : "Swept Away!";
            gameObject.SetActive(true);
        }

        private static void OnRestart() => SceneManager.LoadScene(0);
    }
}
