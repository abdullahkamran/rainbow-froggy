using UnityEngine;
using UnityEngine.UI;

namespace RainbowFroggy.View
{
    // Gameplay HUD: three label regions visible only during active play.
    //
    //   Top-left  — Golden Flies counter  ("Flies: N")
    //   Top-right — High-score display    ("Best: N")
    //   Centre    — Score + Fever label   ("142 ×3")
    //
    // The parent CanvasGroup starts at alpha 0 in the idle state and fades in
    // when gameplay begins (GameBootstrap drives the fade).
    public sealed class HudView : MonoBehaviour
    {
        private Text _fliesLabel;
        private Text _scoreLabel;
        private Text _highScoreLabel;

        // Called by GameBootstrap after constructing the three labels.
        public void Init(Text fliesLabel, Text scoreLabel, Text highScoreLabel)
        {
            _fliesLabel     = fliesLabel;
            _scoreLabel     = scoreLabel;
            _highScoreLabel = highScoreLabel;
        }

        // Update every frame while in the Playing state.
        public void SetData(int score, int combo, int highScore, int flies)
        {
            if (_fliesLabel     != null) _fliesLabel.text     = "Flies: " + flies;
            if (_scoreLabel     != null) _scoreLabel.text     = score + " \xd7" + combo;
            if (_highScoreLabel != null) _highScoreLabel.text = "Best: " + highScore;
        }
    }
}
