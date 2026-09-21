using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Wires the game model to the scene at runtime.
    // A single instance lives on a GameObject in Assets/Scenes/Game.unity.
    // Do NOT use DontDestroyOnLoad or RuntimeInitializeOnLoadMethod — those
    // patterns duplicate the object on restart and leak into test scenes.
    public sealed class GameBootstrap : MonoBehaviour
    {
        private RainbowFroggyGame _game;
        private FrogView          _frogView;
        private HudView           _hud;
        private GameOverScreen    _gameOverScreen;

        // padId → PadView
        private readonly Dictionary<int, PadView> _padViews =
            new Dictionary<int, PadView>();

        private Material _spriteMat;

        // ------------------------------------------------------------------ //
        // Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Start()
        {
            _spriteMat = new Material(Shader.Find("Sprites/Default"));

            _game = new RainbowFroggyGame(
                new SeededRng(UnityEngine.Random.Range(0, int.MaxValue)));
            _game.HighScore = PlayerPrefs.GetInt("HighScore", 0);

            ConfigureCamera();
            BuildBackground();
            BuildFrog();
            BuildHud();
            BuildGameOverScreen();
            SyncAllPads();
        }

        private void Update()
        {
            if (_game.Screen != GameScreen.Playing) return;

            _game.Tick(Time.deltaTime);

            // Check for waterfall BEFORE SyncAllPads — the frog's pad view is still
            // alive this frame; SyncAllPads destroys it in the same pass (AC4).
            if (_game.Screen == GameScreen.WaterfallGameOver)
            {
                PlayerPrefs.SetInt("HighScore", _game.HighScore);
                float worldSpeed = _game.Field.ScrollSpeed * 10f;
                _frogView.RideDown(worldSpeed, () =>
                {
                    _gameOverScreen.Show(GameScreen.WaterfallGameOver);
                });
            }

            SyncAllPads();
            _hud.SetScore(_game.Score, _game.ComboMultiplier, _game.HighScore);

            HandleInput();
        }

        // ------------------------------------------------------------------ //
        // Input
        // ------------------------------------------------------------------ //

        private void HandleInput()
        {
            // Block all input while a sink or ride-off animation is in progress (AC5).
            if (_frogView.IsInputBlocking) return;

            bool    tapped    = false;
            Vector2 screenPos = Vector2.zero;

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            if (Input.GetMouseButtonDown(0))
            {
                tapped    = true;
                screenPos = Input.mousePosition;
            }
#else
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                tapped    = true;
                screenPos = Input.GetTouch(0).position;
            }
#endif

            if (!tapped) return;

            var worldPos = Camera.main.ScreenToWorldPoint(screenPos);
            var hit      = Physics2D.OverlapPoint(worldPos);
            if (hit == null) return;

            var padView = hit.GetComponent<PadView>();
            if (padView == null) return;

            var result = _game.TapPad(padView.PadId);

            if (result == TapResult.Jump)
            {
                // Dispatch the jump tween immediately.  Color and anchor are updated
                // in the landing callback so they are invisible until the frog arrives (AC2).
                _frogView.JumpTo(padView.transform, () =>
                {
                    _frogView.SetAnchor(padView.transform.position +
                                        new Vector3(0f, 0.3f, 0f));
                    _frogView.SetColor(_game.FrogColor);
                });
            }
            else if (result == TapResult.Misstep)
            {
                // Jump to the wrong pad, then sink, then show game-over (AC3).
                PlayerPrefs.SetInt("HighScore", _game.HighScore);
                _frogView.JumpTo(padView.transform, () =>
                    _frogView.Sink(() =>
                    {
                        _gameOverScreen.Show(GameScreen.MisstepGameOver);
                    }));
            }
        }

        // ------------------------------------------------------------------ //
        // Pad view sync
        // ------------------------------------------------------------------ //

        private void SyncAllPads()
        {
            var activeIds = new HashSet<int>();
            foreach (var pad in _game.Field.Pads)
                activeIds.Add(pad.Id);

            var toRemove = new List<int>();
            foreach (var kv in _padViews)
                if (!activeIds.Contains(kv.Key))
                    toRemove.Add(kv.Key);

            foreach (var id in toRemove)
            {
                Destroy(_padViews[id].gameObject);
                _padViews.Remove(id);
            }

            foreach (var pad in _game.Field.Pads)
            {
                if (_padViews.TryGetValue(pad.Id, out var view))
                {
                    view.SyncPosition(pad);
                }
                else
                {
                    var go  = CreateSpriteQuad("Pad_" + pad.Id, new Vector2(1.5f, 0.4f));
                    view    = go.AddComponent<PadView>();
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(1.5f, 0.4f);
                    view.Bind(pad);
                    _padViews[pad.Id] = view;
                }
            }

            // Update the frog's anchor so it rides its current pad.
            // Skip while an animation is running — the coroutine owns the
            // transform during that time and must not be stomped.
            if (_frogView != null && _game.FrogPadId != -1 &&
                !_frogView.IsAnimating &&
                _padViews.TryGetValue(_game.FrogPadId, out var frogPad))
            {
                _frogView.SetAnchor(frogPad.transform.position +
                                    new Vector3(0f, 0.3f, 0f));
            }
        }

        // ------------------------------------------------------------------ //
        // Scene helpers
        // ------------------------------------------------------------------ //

        private void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go  = new GameObject("Main Camera");
                go.tag  = "MainCamera";
                cam     = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic     = true;
            cam.orthographicSize = 6f;
            cam.clearFlags       = CameraClearFlags.SolidColor;
            cam.backgroundColor  = ColorPalette.River;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void BuildBackground()
        {
            var go = CreateSpriteQuad("Background", new Vector2(9f, 13f));
            go.GetComponent<SpriteRenderer>().color        = ColorPalette.River;
            go.GetComponent<SpriteRenderer>().sortingOrder = -10;
        }

        private void BuildFrog()
        {
            var go    = CreateSpriteQuad("Frog", new Vector2(0.6f, 0.6f));
            _frogView = go.AddComponent<FrogView>();
            _frogView.SetColor(_game.FrogColor);
        }

        private void BuildHud()
        {
            var canvas = new GameObject("HUDCanvas");
            var c      = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<CanvasScaler>();
            canvas.AddComponent<GraphicRaycaster>();

            var hudGO = new GameObject("HUD");
            hudGO.transform.SetParent(canvas.transform, false);
            _hud = hudGO.AddComponent<HudView>();

            var scoreGO = new GameObject("ScoreLabel");
            scoreGO.transform.SetParent(hudGO.transform, false);
            var scoreRT = scoreGO.AddComponent<RectTransform>();
            scoreRT.anchorMin        = new Vector2(0f, 1f);
            scoreRT.anchorMax        = new Vector2(0f, 1f);
            scoreRT.pivot            = new Vector2(0f, 1f);
            scoreRT.anchoredPosition = new Vector2(20f, -20f);
            scoreRT.sizeDelta        = new Vector2(260f, 50f);

            var scoreLabel      = scoreGO.AddComponent<Text>();
            scoreLabel.font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            scoreLabel.fontSize = 28;
            scoreLabel.color    = Color.white;
            scoreLabel.text     = "Score: 0   x1";

            var bestGO = new GameObject("HighScoreLabel");
            bestGO.transform.SetParent(hudGO.transform, false);
            var bestRT = bestGO.AddComponent<RectTransform>();
            bestRT.anchorMin        = new Vector2(1f, 1f);
            bestRT.anchorMax        = new Vector2(1f, 1f);
            bestRT.pivot            = new Vector2(1f, 1f);
            bestRT.anchoredPosition = new Vector2(-20f, -20f);
            bestRT.sizeDelta        = new Vector2(200f, 50f);

            var bestLabel           = bestGO.AddComponent<Text>();
            bestLabel.font          = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bestLabel.fontSize      = 28;
            bestLabel.color         = Color.white;
            bestLabel.alignment     = TextAnchor.UpperRight;
            bestLabel.text          = "Best: 0";

            _hud.Init(scoreLabel, bestLabel);
        }

        private void BuildGameOverScreen()
        {
            var canvas = new GameObject("GameOverCanvas");
            var c      = canvas.AddComponent<Canvas>();
            c.renderMode   = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10;
            canvas.AddComponent<CanvasScaler>();
            canvas.AddComponent<GraphicRaycaster>();

            var panelGO = new GameObject("GameOverPanel");
            panelGO.transform.SetParent(canvas.transform, false);
            var panelRT = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            var img           = panelGO.AddComponent<Image>();
            img.color         = new Color(0f, 0f, 0f, 0.75f);

            _gameOverScreen = panelGO.AddComponent<GameOverScreen>();

            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(panelGO.transform, false);
            var headerRT        = headerGO.AddComponent<RectTransform>();
            headerRT.anchorMin  = new Vector2(0.1f, 0.55f);
            headerRT.anchorMax  = new Vector2(0.9f, 0.75f);
            headerRT.offsetMin  = Vector2.zero;
            headerRT.offsetMax  = Vector2.zero;
            var headerText      = headerGO.AddComponent<Text>();
            headerText.font     = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerText.fontSize = 48;
            headerText.alignment = TextAnchor.MiddleCenter;
            headerText.color    = Color.white;
            headerText.text     = "";

            var btnGO = new GameObject("RestartButton");
            btnGO.transform.SetParent(panelGO.transform, false);
            var btnRT     = btnGO.AddComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.3f, 0.35f);
            btnRT.anchorMax = new Vector2(0.7f, 0.50f);
            btnRT.offsetMin = Vector2.zero;
            btnRT.offsetMax = Vector2.zero;
            var btnImg    = btnGO.AddComponent<Image>();
            btnImg.color  = new Color(0.2f, 0.6f, 0.2f);
            var btn       = btnGO.AddComponent<Button>();

            var btnLabelGO       = new GameObject("Label");
            btnLabelGO.transform.SetParent(btnGO.transform, false);
            var btnLabelRT       = btnLabelGO.AddComponent<RectTransform>();
            btnLabelRT.anchorMin = Vector2.zero;
            btnLabelRT.anchorMax = Vector2.one;
            btnLabelRT.offsetMin = Vector2.zero;
            btnLabelRT.offsetMax = Vector2.zero;
            var btnText          = btnLabelGO.AddComponent<Text>();
            btnText.font         = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            btnText.fontSize     = 32;
            btnText.alignment    = TextAnchor.MiddleCenter;
            btnText.color        = Color.white;
            btnText.text         = "Restart";

            // Wire up the screen AFTER all children exist.
            _gameOverScreen.Init(headerText, btn);
        }

        // ------------------------------------------------------------------ //

        private GameObject CreateSpriteQuad(string name, Vector2 size)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite   = WhiteSprite;
            sr.material = _spriteMat;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            return go;
        }

        private static Sprite _whiteSprite;
        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite != null) return _whiteSprite;
                var tex = new Texture2D(2, 2);
                tex.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                tex.Apply();
                _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2),
                    new Vector2(0.5f, 0.5f), pixelsPerUnit: 1f);
                return _whiteSprite;
            }
        }
    }
}
