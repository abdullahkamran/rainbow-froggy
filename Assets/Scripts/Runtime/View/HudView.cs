using UnityEngine;
using UnityEngine.UI;

namespace RainbowFroggy.View
{
    // Gameplay HUD: label regions visible only during active play.
    //
    //   Top-left  — Golden Flies counter  ("Flies: N")
    //   Top-right — High-score display    ("Best: N")
    //   Centre    — Score + Fever label   ("142 ×3")
    //   Top-centre — Prism countdown     ("PRISM 7.4s")  hidden when inactive
    //
    // The parent CanvasGroup starts at alpha 0 in the idle state and fades in
    // when gameplay begins (GameBootstrap drives the fade).
    public sealed class HudView : MonoBehaviour
    {
        private Text _fliesLabel;
        private Text _scoreLabel;
        private Text _highScoreLabel;
        private Text _prismLabel;

        // Called by GameBootstrap after constructing the labels.
        public void Init(Text fliesLabel, Text scoreLabel, Text highScoreLabel,
                         Text prismLabel)
        {
            _fliesLabel     = fliesLabel;
            _scoreLabel     = scoreLabel;
            _highScoreLabel = highScoreLabel;
            _prismLabel     = prismLabel;

            // Hidden by default; shown only while Prism Mode is active.
            if (_prismLabel != null) _prismLabel.gameObject.SetActive(false);
        }

        // Update every frame while in the Playing state.
        public void SetData(int score, int combo, int highScore, int flies)
        {
            if (_fliesLabel     != null) _fliesLabel.text     = "Flies: " + flies;
            if (_scoreLabel     != null) _scoreLabel.text     = score + " \xd7" + combo;
            if (_highScoreLabel != null) _highScoreLabel.text = "Best: " + highScore;
        }

        // Show or hide the Prism Mode countdown banner.
        // remaining is in seconds; the label formats it to one decimal place.
        public void SetPrism(bool active, float remaining)
        {
            if (_prismLabel == null) return;
            _prismLabel.gameObject.SetActive(active);
            if (active)
                _prismLabel.text = "PRISM " + remaining.ToString("0.0") + "s";
        }
    }
}
