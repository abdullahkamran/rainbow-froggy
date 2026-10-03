using UnityEngine;

namespace RainbowFroggy.View
{
    internal sealed class WaterBackground : MonoBehaviour
    {
        [SerializeField] private float _fps = 12f;

        private Sprite[]        _frames;
        private SpriteRenderer  _left;
        private SpriteRenderer  _right;
        private int             _frameIndex;
        private float           _timer;

        private Material        _mat;

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
                float x = c * fw;
                float y = (rows - 1 - r) * fh;
                _frames[r * cols + c] = Sprite.Create(
                    tex, new Rect(x, y, fw, fh),
                    new Vector2(0.5f, 0.5f), pixelsPerUnit: 100f);
            }

            // Scale: two tiles = 9 world units wide.
            float tileWorldW    = 4.5f;
            float spriteNativeW = fw / 100f;   // world units at 100 PPU
            float scaleX        = tileWorldW / spriteNativeW;

            // Height: ensure the tile covers the full 20-unit tall camera view.
            float spriteNativeH = fh / 100f;
            float scaleY        = Mathf.Max(scaleX, 20f / spriteNativeH);

            _mat   = new Material(Shader.Find("Sprites/Default"));
            _left  = MakeTile("WaterLeft",  scaleX, scaleY, flipX: false, x: -tileWorldW * 0.5f);
            _right = MakeTile("WaterRight", scaleX, scaleY, flipX: true,  x:  tileWorldW * 0.5f);

            SetFrame(0);
        }

        private SpriteRenderer MakeTile(string name, float scaleX, float scaleY, bool flipX, float x)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(x, 0f, 0f);
            go.transform.localScale    = new Vector3(scaleX, scaleY, 1f);

            var sr          = go.AddComponent<SpriteRenderer>();
            sr.material     = _mat;
            sr.flipX        = flipX;
            sr.sortingOrder = -10;
            return sr;
        }

        private void Update()
        {
            if (_frames == null) return;

            _timer += Time.deltaTime;
            float frameDur = 1f / Mathf.Max(_fps, 1f);
            if (_timer >= frameDur)
            {
                _timer -= frameDur;
                SetFrame((_frameIndex + 1) % _frames.Length);
            }
        }

        private void SetFrame(int idx)
        {
            _frameIndex   = idx;
            _left.sprite  = _frames[idx];
            _right.sprite = _frames[idx];
        }

        private void OnDestroy()
        {
            if (_mat != null) Object.Destroy(_mat);
        }
    }
}
