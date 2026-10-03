using System.Collections.Generic;
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
    // N = Mathf.CeilToInt(screenHeight / spriteWorldHeight) + 1 guarantees at
    // least one full tile of off-screen buffer regardless of sprite size.
    public sealed class RiverBankScroll : MonoBehaviour
    {
        public Sprite BankSprite;
        public float  ScrollSpeed;
        public float  VerticalOffset;

        private readonly List<Transform> _tiles = new List<Transform>();
        private float _spriteWorldHeight;
        private float _screenHalfHeight;
        private bool  _flipX;

        // Test seam: exposes tile transforms for assertion in play-mode tests.
        public IReadOnlyList<Transform> Tiles => _tiles;

        // Must be set before this component's Awake fires (i.e. before the
        // GameObject is made active) so the flip is applied to every tile.
        public void SetFlipX(bool flip) => _flipX = flip;

        private void Awake()
        {
            if (BankSprite == null) return;

            _spriteWorldHeight = BankSprite.rect.height / BankSprite.pixelsPerUnit;

            var cam = Camera.main;
            float screenHeight = cam != null ? cam.orthographicSize * 2f : 18f;
            _screenHalfHeight  = screenHeight * 0.5f;

            int n = Mathf.CeilToInt(screenHeight / _spriteWorldHeight) + 1;

            for (int i = 0; i < n; i++)
            {
                var child = new GameObject("Tile" + i);
                child.transform.SetParent(transform, false);

                var sr          = child.AddComponent<SpriteRenderer>();
                sr.sprite       = BankSprite;
                sr.flipX        = _flipX;
                sr.sortingOrder = -5;

                // Stack tiles contiguously starting at the bottom of the screen.
                // The VerticalOffset shifts the right bank by half a tile to
                // create a visual stagger between left and right banks.
                float localY = -_screenHalfHeight
                             + _spriteWorldHeight * (0.5f + i)
                             + VerticalOffset;
                child.transform.localPosition = new Vector3(0f, localY, 0f);

                _tiles.Add(child.transform);
            }
        }

        private void Update()
        {
            if (_tiles.Count == 0) return;

            float delta       = ScrollSpeed * Time.deltaTime;
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
                float tileTop = _tiles[i].localPosition.y + _spriteWorldHeight * 0.5f;
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
