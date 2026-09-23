using System;
using UnityEngine.UI;

namespace RainbowFroggy.View
{
    // Idle-state overlay: game title, tap-to-play prompt, and a full-screen
    // transparent button (the "tap zone") that starts the run.
    //
    // GameBootstrap subscribes to OnTapZonePressed and calls StartRun() on
    // the model, then fades this chrome out via the parent CanvasGroup.
    public sealed class MenuChrome : UnityEngine.MonoBehaviour
    {
        // Raised when the player taps the centre tap zone while in Idle.
        public Action OnTapZonePressed;

        // Called by GameBootstrap after all child elements exist.
        public void Init(Button tapZoneButton)
        {
            tapZoneButton.onClick.AddListener(() => OnTapZonePressed?.Invoke());
        }
    }
}
