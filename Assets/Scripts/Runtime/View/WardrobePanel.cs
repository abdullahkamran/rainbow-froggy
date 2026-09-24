using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Populates the Wardrobe bottom sheet with a 2-column × 2-row skin grid.
    // Locked skins are greyed-out with a "Score N" sub-label and non-interactable.
    // Unlocked skins show "Equipped" or "Tap to equip" and fire onEquip on click.
    //
    // OnEnable is called by Unity each time the BottomSheet's GameObject is made
    // active (i.e. every time the player opens the Wardrobe).  RefreshCells()
    // re-reads the live high score via _getHighScore so that skins unlocked during
    // the current session are immediately reflected (AC7).
    public sealed class WardrobePanel : MonoBehaviour
    {
        // Per-cell references kept so we can update them without rebuilding.
        private struct CellRefs
        {
            public FrogSkin Skin;
            public Button   Btn;
            public Image    BgImg;
            public Text     NameTxt;
            public Text     SubTxt;
        }

        private SkinService       _skinService;
        private Func<int>         _getHighScore;
        private Action<FrogSkin>  _onEquip;
        private readonly List<CellRefs> _cells = new List<CellRefs>();

        // Called by GameBootstrap after the Wardrobe sheet is created.
        // contentParent  — the BottomSheet's transform (cells are parented here)
        // skinService    — service that owns the skin list and equipped state
        // getHighScore   — live accessor so the panel reflects the current best
        // onEquip        — callback when the player selects a new skin
        public void Init(Transform        contentParent,
                         SkinService      skinService,
                         Func<int>        getHighScore,
                         Action<FrogSkin> onEquip)
        {
            _skinService  = skinService;
            _getHighScore = getHighScore;
            _onEquip      = onEquip;
            BuildGrid(contentParent);
        }

        // Unity calls this every time the GameObject becomes active — i.e. every
        // time the Wardrobe sheet slides open.  Re-read the live high score so
        // within-session unlocks are reflected immediately (AC7).
        private void OnEnable() => RefreshCells();

        // ------------------------------------------------------------------ //

        private void BuildGrid(Transform parent)
        {
            var skins = _skinService.Skins;
            if (skins == null || skins.Length == 0) return;

            // Snapshot high score for the initial visual state only.
            // RefreshCells() will re-read it live on every subsequent open.
            int highScore = _getHighScore();

            for (int i = 0; i < skins.Length; i++)
            {
                var  skin = skins[i];
                int  col  = i % 2;
                int  row  = i / 2;

                // Content area: below title (y<0.75) and above close button (y>0.22).
                // Two rows, each ~0.22 tall, with 0.03 gap.
                float yMax = 0.73f - row * 0.25f;
                float yMin = yMax - 0.22f;
                float xMin = 0.05f + col * 0.50f;
                float xMax = xMin + 0.43f;

                var cellGO = new GameObject("SkinCell_" + skin.id);
                cellGO.transform.SetParent(parent, false);
                var cellRT       = cellGO.AddComponent<RectTransform>();
                cellRT.anchorMin = new Vector2(xMin, yMin);
                cellRT.anchorMax = new Vector2(xMax, yMax);
                cellRT.offsetMin = Vector2.zero;
                cellRT.offsetMax = Vector2.zero;

                bool unlocked = SkinCatalog.IsUnlocked(skin.id, highScore);

                var bgImg   = cellGO.AddComponent<Image>();
                bgImg.color = unlocked
                    ? new Color(0.15f, 0.22f, 0.40f, 0.9f)
                    : new Color(0.10f, 0.12f, 0.18f, 0.9f);

                var btn          = cellGO.AddComponent<Button>();
                btn.interactable = unlocked;

                // Skin name label (upper half of cell).
                var nameGO  = new GameObject("NameLabel");
                nameGO.transform.SetParent(cellGO.transform, false);
                var nameRT  = nameGO.AddComponent<RectTransform>();
                nameRT.anchorMin = new Vector2(0f, 0.52f);
                nameRT.anchorMax = Vector2.one;
                nameRT.offsetMin = new Vector2(4f, 0f);
                nameRT.offsetMax = new Vector2(-4f, 0f);
                var nameTxt = nameGO.AddComponent<Text>();
                nameTxt.font      = FontLibrary.Body;
                nameTxt.fontSize  = 18;
                nameTxt.alignment = TextAnchor.MiddleCenter;
                nameTxt.color     = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f);
                nameTxt.text      = skin.displayName;

                // Status / lock sub-label (lower half of cell).
                var subGO  = new GameObject("SubLabel");
                subGO.transform.SetParent(cellGO.transform, false);
                var subRT  = subGO.AddComponent<RectTransform>();
                subRT.anchorMin = Vector2.zero;
                subRT.anchorMax = new Vector2(1f, 0.50f);
                subRT.offsetMin = new Vector2(4f, 0f);
                subRT.offsetMax = new Vector2(-4f, 0f);
                var subTxt = subGO.AddComponent<Text>();
                subTxt.font      = FontLibrary.Body;
                subTxt.fontSize  = 16;
                subTxt.alignment = TextAnchor.MiddleCenter;

                if (unlocked)
                {
                    bool isEquipped  = _skinService.Equipped == skin;
                    subTxt.color     = isEquipped
                        ? new Color(0.3f, 1f, 0.4f)
                        : new Color(0.7f, 0.7f, 0.7f);
                    subTxt.text = isEquipped ? "Equipped" : "Tap to equip";
                }
                else
                {
                    subTxt.color = new Color(0.8f, 0.5f, 0.2f);
                    subTxt.text  = "Score " + skin.unlockScore;
                }

                // Always wire onClick — interactability gates whether it fires.
                // Wiring unconditionally means newly-unlocked skins (whose
                // interactable flag is raised by RefreshCells on open) already
                // have their listener ready without a rebuild.
                var capSkin = skin;
                btn.onClick.AddListener(() =>
                {
                    _skinService.Equip(capSkin);
                    _onEquip?.Invoke(capSkin);
                    // Refresh all cells so the previously-equipped skin reverts
                    // to "Tap to equip" and the new one shows "Equipped".
                    RefreshCells();
                });

                _cells.Add(new CellRefs
                {
                    Skin    = skin,
                    Btn     = btn,
                    BgImg   = bgImg,
                    NameTxt = nameTxt,
                    SubTxt  = subTxt,
                });
            }
        }

        // Re-evaluate every cell's lock / equipped state using the live high
        // score.  Called on each open (OnEnable) and after every equip action.
        private void RefreshCells()
        {
            if (_skinService == null || _getHighScore == null) return;

            int highScore = _getHighScore();

            foreach (var cell in _cells)
            {
                bool unlocked = SkinCatalog.IsUnlocked(cell.Skin.id, highScore);

                cell.BgImg.color = unlocked
                    ? new Color(0.15f, 0.22f, 0.40f, 0.9f)
                    : new Color(0.10f, 0.12f, 0.18f, 0.9f);

                cell.Btn.interactable = unlocked;
                cell.NameTxt.color    = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.5f);

                if (unlocked)
                {
                    bool isEquipped   = _skinService.Equipped == cell.Skin;
                    cell.SubTxt.color = isEquipped
                        ? new Color(0.3f, 1f, 0.4f)
                        : new Color(0.7f, 0.7f, 0.7f);
                    cell.SubTxt.text  = isEquipped ? "Equipped" : "Tap to equip";
                }
                else
                {
                    cell.SubTxt.color = new Color(0.8f, 0.5f, 0.2f);
                    cell.SubTxt.text  = "Score " + cell.Skin.unlockScore;
                }
            }
        }
    }
}
