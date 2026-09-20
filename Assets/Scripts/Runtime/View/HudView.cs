using UnityEngine;
using UnityEngine.UI;

namespace RainbowFroggy.View
{
    // Displays score, combo multiplier, and high score.
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private Text _label;
        [SerializeField] private Text _highScoreLabel;

        // Called by GameBootstrap after constructing the labels.
        public void Init(Text label, Text highScoreLabel)
        {
            _label          = label;
            _highScoreLabel = highScoreLabel;
        }

        public void SetScore(int score, int combo, int highScore)
        {
            if (_label != null)
                _label.text = "Score: " + score + "   x" + combo;
            if (_highScoreLabel != null)
                _highScoreLabel.text = "Best: " + highScore;
        }
    }
}
