using System.Collections.Generic;
using RainbowFroggy.Core;
using UnityEngine;

namespace RainbowFroggy.View
{
    // Infinitely scrolling river-bank strip.
    //
    // Awake creates N tile GameObjects (each with a SpriteRenderer) stacked
    // contiguously in world space.  Update translates every tile downward each
    // frame; when a tile's top edge passes below the bottom of the visible area
    // it is repositioned directly above the current topmost tile so the scroll
    // is gapless and endless.
    //
    // N = Mathf.Max(2, Mathf.CeilToInt(screenHeight / spriteWorldHeight) + 1)
    // guarantees at least one full tile of off-screen buffer and a minimum of
    // two tiles so wrapping has a tile to track against.
    public sealed class RiverBankScroll : MonoBehaviour
    {
        public Sprite BankSprite;
        public float  ScrollSpeed;
        public float  VerticalOffset;

        // Set to true before the GameObject is activated so the flip is applied
        // to every tile in Awake.
        public bool FlipX;

        private readonly List<Transform> _tiles = new List<Transform>();
        private float _spriteWorldHeight;
        private float _spritePivotCenterY;
        private float _screenHalfHeight;
        private RainbowFroggyGame _game;

        // Test seam: exposes tile transforms for assertion in play-mode tests.
        public IReadOnlyList<Transform> Tiles => _tiles;

        // Called by BuildBanks before the GameObject is made active so
        // ScrollSpeed is driven from the field model each frame.
        public void Bind(RainbowFroggyGame game) => _game = game;

        private void Awake()
        {
            if (BankSprite == null) return;

            // Use bounds rather than rect/pixelsPerUnit so placement is
            // pivot-agnostic; for the default centre pivot both are equivalent.
            _spriteWorldHeight  = BankSprite.bounds.size.y;
            _spritePivotCenterY = BankSprite.bounds.center.y;

            if (_spriteWorldHeight <= 0f) return;

            var cam = Camera.main;
            float screenHeight = cam != null ? cam.orthographicSize * 2f : 18f;
            _screenHalfHeight  = screenHeight * 0.5f;

            int n = Mathf.Max(2, Mathf.CeilToInt(screenHeight / _spriteWorldHeight) + 1);

            for (int i = 0; i < n; i++)
            {
                var child = new GameObject("Tile" + i);
                child.transform.SetParent(transform, false);

                var sr          = child.AddComponent<SpriteRenderer>();
                sr.sprite       = BankSprite;
                sr.flipX        = FlipX;
                sr.sortingOrder = -5;

                // Stack tiles contiguously starting at the bottom of the screen.
                // Subtract the pivot-centre offset so positioning is anchor-agnostic;
                // for the default centre pivot (0.5, 0.5) this term is zero.
                // The VerticalOffset shifts the right bank by half a tile to
                // create a visual stagger between left and right banks.
                float localY = -_screenHalfHeight
                             + _spriteWorldHeight * (0.5f + i)
                             - _spritePivotCenterY
                             + VerticalOffset;
                child.transform.localPosition = new Vector3(0f, localY, 0f);

                _tiles.Add(child.transform);
            }
        }

        private void Update()
        {
            // Keep scroll speed in sync with the field model every frame so
            // bank speed tracks lily-pad speed across all difficulty phases.
            if (_game != null) ScrollSpeed = _game.Field.ScrollSpeed * 10f;

            if (_tiles.Count == 0) return;

            float delta        = ScrollSpeed * Time.deltaTime;
            float screenBottom = -_screenHalfHeight;

            // Translate all tiles downward.
            for (int i = 0; i < _tiles.Count; i++)
            {
                var p = _tiles[i].localPosition;
                _tiles[i].localPosition = new Vector3(p.x, p.y - delta, p.z);
            }

            // Wrap any tile whose top edge has passed below the screen bottom.
            for (int i = 0; i < _tiles.Count; i++)
            {
                float tileTop = _tiles[i].localPosition.y + _spritePivotCenterY + _spriteWorldHeight * 0.5f;
                if (tileTop >= screenBottom) continue;

                // Find the current topmost tile to anchor repositioning; this
                // avoids float drift that a modulo-from-origin approach would
                // accumulate over time.
                Transform topmost = _tiles[0];
                for (int j = 1; j < _tiles.Count; j++)
                    if (_tiles[j].localPosition.y > topmost.localPosition.y)
                        topmost = _tiles[j];

                var pos = _tiles[i].localPosition;
                _tiles[i].localPosition = new Vector3(
                    pos.x,
                    topmost.localPosition.y + _spriteWorldHeight,
                    pos.z);
            }
        }
    }
}
