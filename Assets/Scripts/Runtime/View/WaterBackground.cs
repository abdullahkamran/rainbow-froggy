using UnityEngine;

namespace RainbowFroggy.View
{
    internal sealed class WaterBackground : MonoBehaviour
    {
        [SerializeField] private float _fps = 12f;

        // Set each frame by GameBootstrap to match pad scroll speed.
        public float ScrollSpeed { get; set; }

        private Sprite[]       _frames;
        private SpriteRenderer _bl, _br;   // bottom row (first visible)
        private SpriteRenderer _tl, _tr;   // top row (waiting above, for seamless loop)
        private float          _leftX, _rightX;
        private float          _tileHeight;   // world units
        private int            _frameIndex;
        private float          _frameDur;
        private float          _frameTimer;
        private float          _scrollY;      // accumulated downward scroll

        private Material       _mat;

        private void Awake()
        {
            var tex = Resources.Load<Texture2D>("WaterLeft");
            if (tex == null) return;

            int cols = 6, rows = 6;
            int fw   = tex.width  / cols;
            int fh   = tex.height / rows;

            _frames = new Sprite[rows * cols];
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                // Unity tex origin is bottom-left; row 0 of the sheet is the top row.
                float px = c * fw;
                float py = (rows - 1 - r) * fh;
                _frames[r * cols + c] = Sprite.Create(
                    tex, new Rect(px, py, fw, fh),
                    new Vector2(0.5f, 0.5f), pixelsPerUnit: 100f);
            }

            float nativeW = fw / 100f;   // sprite frame width in world units at 100 PPU
            float nativeH = fh / 100f;

            // Scale uniformly so the tile spans half the 9-unit background width AND
            // covers the full camera height.  The camera is orthographicSize = 9 (18 world
            // units tall); a width-only fit gives ~16.2 units of height, leaving a visible
            // gap at the vertical wrap edge.  The +1 margin adds one extra unit of overlap
            // to absorb floating-point accumulation across the wrap boundary.
            float camH  = Camera.main != null ? Camera.main.orthographicSize * 2f : 18f;
            float scale = Mathf.Max(4.5f / nativeW, (camH + 1f) / nativeH);

            float actualTileW = nativeW * scale;
            _tileHeight       = nativeH * scale;

            // Shift tile centres inward by a fraction of a world unit to close the
            // sub-pixel gap that can appear at the horizontal seam.
            const float seam = 0.05f;
            _leftX  = -(actualTileW * 0.5f - seam);
            _rightX =  (actualTileW * 0.5f - seam);

            _mat = new Material(Shader.Find("Sprites/Default"));

            // Bottom row starts at y=0 (camera centre); top row starts one tile above.
            _bl = MakeTile("WaterBL", scale, flipX: false, x: _leftX,  y: 0f);
            _br = MakeTile("WaterBR", scale, flipX: true,  x: _rightX, y: 0f);
            _tl = MakeTile("WaterTL", scale, flipX: false, x: _leftX,  y: _tileHeight);
            _tr = MakeTile("WaterTR", scale, flipX: true,  x: _rightX, y: _tileHeight);

            _frameDur = 1f / Mathf.Max(_fps, 1f);
            SetFrame(0);
        }

        private SpriteRenderer MakeTile(string name, float scale, bool flipX, float x, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, y, 0f);
            go.transform.localScale    = new Vector3(scale, scale, 1f);

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.material     = _mat;
            sr.flipX        = flipX;
            sr.sortingOrder = -10;
            return sr;
        }

        private void Update()
        {
            if (_frames == null) return;

            // Frame animation — subtract to avoid drift across long sessions.
            _frameTimer += Time.deltaTime;
            if (_frameTimer >= _frameDur)
            {
                _frameTimer -= _frameDur;
                SetFrame((_frameIndex + 1) % _frames.Length);
            }

            // Vertical scroll — accumulate and use Repeat for seamless wrap.
            _scrollY += ScrollSpeed * Time.deltaTime;
            float offset = Mathf.Repeat(_scrollY, _tileHeight);

            // Bottom tiles scroll off screen; top tiles follow from above.
            float yBottom = -offset;
            float yTop    = _tileHeight - offset;

            _bl.transform.localPosition = new Vector3(_leftX,  yBottom, 0f);
            _br.transform.localPosition = new Vector3(_rightX, yBottom, 0f);
            _tl.transform.localPosition = new Vector3(_leftX,  yTop,    0f);
            _tr.transform.localPosition = new Vector3(_rightX, yTop,    0f);
        }

        private void SetFrame(int idx)
        {
            _frameIndex = idx;
            _bl.sprite  = _frames[idx];
            _br.sprite  = _frames[idx];
            _tl.sprite  = _frames[idx];
            _tr.sprite  = _frames[idx];
        }

        private void OnDestroy()
        {
            if (_mat != null) Object.Destroy(_mat);
        }
    }
}
