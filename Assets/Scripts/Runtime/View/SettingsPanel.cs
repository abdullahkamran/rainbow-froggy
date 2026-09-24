using UnityEngine;
using UnityEngine.UI;

namespace RainbowFroggy.View
{
    // Populates the Settings bottom sheet with a mute/unmute toggle (AC8).
    // AudioListener.pause silences all Unity audio globally.
    // The preference is persisted across sessions via PlayerPrefs.
    public sealed class SettingsPanel : MonoBehaviour
    {
        private const string MuteKey = "AudioMuted";

        private AudioService _audio;
        private Text         _muteLabel;

        // Called by GameBootstrap once the Settings sheet exists.
        public void Init(Transform sheetRoot, AudioService audio)
        {
            _audio = audio;

            // Restore mute state from the previous session.
            bool wasMuted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            _audio.SetMuted(wasMuted);

            BuildMuteButton(sheetRoot, wasMuted);
        }

        private void BuildMuteButton(Transform parent, bool initialMuted)
        {
            var go  = new GameObject("MuteToggle");
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 0.50f);
            rt.anchorMax = new Vector2(0.85f, 0.65f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img   = go.AddComponent<Image>();
            img.color = new Color(0.20f, 0.25f, 0.40f, 0.90f);
            var btn   = go.AddComponent<Button>();

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = Vector2.zero;
            _muteLabel            = lblGO.AddComponent<Text>();
            _muteLabel.font       = FontLibrary.Body;
            _muteLabel.fontSize   = 26;
            _muteLabel.alignment  = TextAnchor.MiddleCenter;
            _muteLabel.color      = Color.white;

            RefreshLabel(initialMuted);
            btn.onClick.AddListener(OnMuteToggle);
        }

        private void OnMuteToggle()
        {
            bool nowMuted = !AudioListener.pause;
            _audio.SetMuted(nowMuted);
            PlayerPrefs.SetInt(MuteKey, nowMuted ? 1 : 0);
            PlayerPrefs.Save();
            RefreshLabel(nowMuted);
        }

        private void RefreshLabel(bool muted)
        {
            if (_muteLabel != null)
                _muteLabel.text = muted ? "Unmute" : "Mute";
        }
    }
}
