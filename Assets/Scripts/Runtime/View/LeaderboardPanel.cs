using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RainbowFroggy.View
{
    // Populates the Leaderboard bottom sheet with a top-10 run history.
    // Rows are built once in Init(); Refresh() is called on every open via
    // BottomSheet.OnOpened so the list always reflects the latest saved data.
    // The top-ranked row (all-time best score) is highlighted in gold with bold
    // text; all other rows reset to the default style on each refresh so the
    // highlight never accumulates on a stale row.
    public sealed class LeaderboardPanel : MonoBehaviour
    {
        private struct RowRefs
        {
            public GameObject Go;
            public Image      BgImg;
            public Text       RankTxt;
            public Text       ScoreTxt;
            public Text       DateTxt;
        }

        private const int MaxRows = LeaderboardStore.MaxEntries;

        private readonly RowRefs[] _rows = new RowRefs[MaxRows];
        private Text _emptyLabel;

        // Called by GameBootstrap once the Leaderboard sheet exists.
        // Subscribes to sheet.OnOpened so Refresh() is called every time the
        // player opens the panel — explicit callback rather than OnEnable, so
        // the refresh path is independent of SetActive timing.
        public void Init(Transform sheetRoot, BottomSheet sheet)
        {
            BuildRows(sheetRoot);
            BuildEmptyLabel(sheetRoot);
            sheet.OnOpened += Refresh;
            Refresh();
        }

        // ------------------------------------------------------------------ //

        private void BuildRows(Transform parent)
        {
            // Content area: below title (y < 0.73) and above close button (y > 0.23).
            // Ten equal rows of height 0.05 fill this band exactly.
            const float yTop = 0.73f;
            const float rowH = 0.05f;

            for (int i = 0; i < MaxRows; i++)
            {
                float yMax = yTop - i * rowH;
                float yMin = yMax - rowH;

                var rowGO  = new GameObject("Row_" + i);
                rowGO.transform.SetParent(parent, false);
                var rowRT  = rowGO.AddComponent<RectTransform>();
                rowRT.anchorMin = new Vector2(0.03f, yMin);
                rowRT.anchorMax = new Vector2(0.97f, yMax);
                rowRT.offsetMin = new Vector2(2f,  1f);
                rowRT.offsetMax = new Vector2(-2f, -1f);

                var bgImg   = rowGO.AddComponent<Image>();
                bgImg.color = new Color(0.10f, 0.14f, 0.26f, 0.80f);

                // Rank — left column.
                var rankTxt  = MakeRowLabel(rowGO.transform, "Rank",
                    new Vector2(0f, 0f), new Vector2(0.18f, 1f),
                    fontSize: 16, align: TextAnchor.MiddleCenter);

                // Score — centre column.
                var scoreTxt = MakeRowLabel(rowGO.transform, "Score",
                    new Vector2(0.18f, 0f), new Vector2(0.62f, 1f),
                    fontSize: 16, align: TextAnchor.MiddleCenter);

                // Date — right column.
                var dateTxt  = MakeRowLabel(rowGO.transform, "Date",
                    new Vector2(0.62f, 0f), new Vector2(1f, 1f),
                    fontSize: 14, align: TextAnchor.MiddleCenter);
                dateTxt.color = new Color(0.75f, 0.75f, 0.75f);

                _rows[i] = new RowRefs
                {
                    Go       = rowGO,
                    BgImg    = bgImg,
                    RankTxt  = rankTxt,
                    ScoreTxt = scoreTxt,
                    DateTxt  = dateTxt,
                };
            }
        }

        private void BuildEmptyLabel(Transform parent)
        {
            var go  = new GameObject("EmptyLabel");
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.35f);
            rt.anchorMax = new Vector2(0.9f, 0.65f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _emptyLabel           = go.AddComponent<Text>();
            _emptyLabel.font      = FontLibrary.Body;
            _emptyLabel.fontSize  = 22;
            _emptyLabel.alignment = TextAnchor.MiddleCenter;
            _emptyLabel.color     = new Color(0.6f, 0.6f, 0.6f);
            _emptyLabel.text      = "No runs yet";
        }

        private void Refresh()
        {
            List<LeaderboardStore.Entry> entries = LeaderboardStore.Load();
            bool isEmpty = entries.Count == 0;

            _emptyLabel.gameObject.SetActive(isEmpty);

            for (int i = 0; i < MaxRows; i++)
            {
                if (i < entries.Count)
                {
                    _rows[i].Go.SetActive(true);

                    var  entry  = entries[i];
                    // Row 0 always holds the all-time best score after the
                    // store sorts by score-desc.  On a tie, exactly one row
                    // (position 0, the most recent of the tied scores) is
                    // highlighted.
                    bool isBest = (i == 0);

                    _rows[i].RankTxt.text  = "#" + (i + 1);
                    _rows[i].ScoreTxt.text = entry.Score.ToString();
                    _rows[i].DateTxt.text  = entry.DateUtc.ToString("yyyy-MM-dd");

                    if (isBest)
                    {
                        _rows[i].BgImg.color       = new Color(0.55f, 0.42f, 0.00f, 0.70f);
                        _rows[i].ScoreTxt.fontStyle = FontStyle.Bold;
                        _rows[i].RankTxt.color      = new Color(1f, 0.88f, 0.30f);
                        _rows[i].ScoreTxt.color     = new Color(1f, 0.88f, 0.30f);
                    }
                    else
                    {
                        _rows[i].BgImg.color       = new Color(0.10f, 0.14f, 0.26f, 0.80f);
                        _rows[i].ScoreTxt.fontStyle = FontStyle.Normal;
                        _rows[i].RankTxt.color      = Color.white;
                        _rows[i].ScoreTxt.color     = Color.white;
                    }
                }
                else
                {
                    _rows[i].Go.SetActive(false);
                }
            }
        }

        private static Text MakeRowLabel(Transform parent, string name,
                                          Vector2 anchorMin, Vector2 anchorMax,
                                          int fontSize, TextAnchor align)
        {
            var go  = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(2f, 0f);
            rt.offsetMax = new Vector2(-2f, 0f);
            var txt       = go.AddComponent<Text>();
            txt.font      = FontLibrary.Body;
            txt.fontSize  = fontSize;
            txt.alignment = align;
            txt.color     = Color.white;
            return txt;
        }
    }
}
