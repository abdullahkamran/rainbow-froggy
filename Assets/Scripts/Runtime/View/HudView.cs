using UnityEngine;
using UnityEngine.UI;

namespace RainbowFroggy.View
{
    // Displays the jump counter in the top-left corner.
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private Text _label;

        // Called by GameBootstrap after constructing the label.
        public void Init(Text label) => _label = label;

        public void SetJumpCount(int n)
        {
            if (_label != null)
                _label.text = "Jumps: " + n;
        }
    }
}
