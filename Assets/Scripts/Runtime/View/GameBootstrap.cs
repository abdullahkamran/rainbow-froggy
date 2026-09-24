using System.Collections;
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
        private MenuChrome        _menuChrome;
        private BottomNavBar      _bottomNavBar;
        private SkinService       _skinService;

        // CanvasGroups driven by state transitions.
        private CanvasGroup _hudCG;
        private CanvasGroup _menuChromeCG;
        private CanvasGroup _bottomNavCG;

        // padId → PadView
        private readonly Dictionary<int, PadView> _padViews =
            new Dictionary<int, PadView>();

        // pickupId → PowerUpView
        private readonly Dictionary<int, PowerUpView> _pickupViews =
            new Dictionary<int, PowerUpView>();

        // flyId → GoldenFlyView
        private readonly Dictionary<int, GoldenFlyView> _flyViews =
            new Dictionary<int, GoldenFlyView>();

        // Power-up UI: frost overlay and HUD countdown.
        private GameObject _frostOverlayGO;
        private GameObject _timeFreezeCountdownGO;

        private Material         _spriteMat;
        private AudioService     _audioService;
        private int              _lastPhase = 1;
        private ChallengeService _challengeService;
        private Text             _lifetimeFliesText;  // main-menu lifetime balance display

        // Test seams: frost overlay and countdown GameObjects.
        public GameObject FrostOverlay         => _frostOverlayGO;
        public GameObject TimeFreezeCountdown  => _timeFreezeCountdownGO;

        private const float UiFadeDuration = 0.3f;

        // Test seams: expose model state and key components for assertions and teardown.
        public GameScreen  Screen         => _game != null ? _game.Screen : GameScreen.Playing;
        public int         Score          => _game != null ? _game.Score  : 0;
        public GameOverScreen GameOverScreen => _gameOverScreen;

        // Test seam: directly invoke power-up collection (bypasses tap input).
        // Immediately syncs the overlay GameObjects to reflect the new state.
        public void ForceCollectPowerUp(RainbowFroggy.Core.PowerUpType type)
        {
            _game.CollectPowerUp(type);
            if (_audioService != null) _audioService.PlayPowerUp(type);
            bool frozen = _game.IsTimeFreezeActive;
            if (_frostOverlayGO      != null) _frostOverlayGO.SetActive(frozen);
            if (_timeFreezeCountdownGO != null) _timeFreezeCountdownGO.SetActive(frozen);
        }

        // All root-level GameObjects created in Start(); tests use this for teardown.
        public IReadOnlyList<GameObject> CreatedRoots => _createdRoots;
        private readonly List<GameObject> _createdRoots = new List<GameObject>();

        // ------------------------------------------------------------------ //
        // Unity lifecycle
        // ------------------------------------------------------------------ //

        private void Start()
        {
            _spriteMat = new Material(Shader.Find("Sprites/Default"));

            _game = new RainbowFroggyGame(
                new SeededRng(UnityEngine.Random.Range(0, int.MaxValue)));
            _game.HighScore = PlayerPrefs.GetInt("HighScore", 0);
            _game.EnterIdle();

            // Forward the serialized spawn rate to the GoldenFlyField so the
            // Inspector value is respected at runtime (AC1).
            var flySpawner = GetComponent<GoldenFlySpawner>();
            if (flySpawner != null)
                _game.GoldenFlyField.SpawnInterval = flySpawner.SpawnIntervalSeconds;

            _challengeService = new ChallengeService();

            ConfigureCamera();
            BuildBackground();
            BuildFrog();
            _skinService = new SkinService();
            _frogView.SetSkin(_skinService.EquippedSprite);
            BuildHud();
            BuildFrostOverlay();
            BuildAudioRig();
            BuildMenuAndNav();
            BuildGameOverScreen();

            // Create the ad service and wire it to the game-over screen.
            var adServiceGO = new GameObject("AdService");
            var adService   = adServiceGO.AddComponent<AdService>();
            _createdRoots.Add(adServiceGO);
            _gameOverScreen.InitAds(adService, OnRevive);

            SyncAllPads();

            // Start in idle: HUD hidden, chrome + nav visible.
            SetCanvasGroupState(_hudCG,        alpha: 0f);
            SetCanvasGroupState(_menuChromeCG, alpha: 1f);
            SetCanvasGroupState(_bottomNavCG,  alpha: 1f);
        }

        private void Update()
        {
            // Only tick and handle gameplay input while in Playing state.
            if (_game.Screen != GameScreen.Playing) return;

            _game.Tick(Time.deltaTime);

            // Sync BGM tempo on phase transition.
            int currentPhase = _game.Phase;
            if (currentPhase != _lastPhase)
            {
                _lastPhase = currentPhase;
                _audioService.SetPhase(currentPhase);
            }

            // Check for waterfall BEFORE SyncAllPads — the frog's pad view is still
            // alive this frame; SyncAllPads destroys it in the same pass.
            if (_game.Screen == GameScreen.WaterfallGameOver)
            {
                _audioService.PlayWaterfall();
                PlayerPrefs.SetInt("HighScore", _game.HighScore);
                PlayerPrefs.Save();
                int challengeBonus1 = _challengeService.TryGrantBonus();
                _gameOverScreen.SetPendingFlies(_game.FliesThisRun, challengeBonus1);
                float worldSpeed = _game.Field.ScrollSpeed * 10f;
                bool  newHigh1   = _game.IsNewHighScore;
                int   hi1        = _game.HighScore;
                int   sc1        = _game.Score;
                int   fl1        = _game.FliesThisRun;
                GameScreen sr1   = _game.Screen;
                _frogView.RideDown(worldSpeed, () =>
                    _gameOverScreen.Show(sr1, sc1, fl1, hi1, newHigh1));
            }

            SyncAllPads();
            SyncPickups();
            SyncFlies();
            _hud.SetData(_game.Score, _game.ComboMultiplier, _game.HighScore,
                         _game.FliesThisRun);
            _hud.SetPrism(_game.IsPrismActive, _game.PrismRemaining);
            _hud.SetChallenge(_challengeService.Progress,
                              _challengeService.Challenge.Target);

            // Sync Time Freeze overlay and countdown.
            bool frozen = _game.IsTimeFreezeActive;
            if (_frostOverlayGO != null)
                _frostOverlayGO.SetActive(frozen);
            if (_timeFreezeCountdownGO != null)
                _timeFreezeCountdownGO.SetActive(frozen);
            if (frozen)
                _hud.SetFreeze(_game.TimeFreezeRemaining);

            HandleInput();
        }

        // ------------------------------------------------------------------ //
        // State transitions
        // ------------------------------------------------------------------ //

        // Called by MenuChrome when the tap zone is pressed.
        private void OnMenuTapZone()
        {
            if (_game.Screen != GameScreen.Idle) return;
            _game.StartRun();
            StartCoroutine(FadeCanvasGroup(_menuChromeCG, 0f, UiFadeDuration));
            StartCoroutine(FadeCanvasGroup(_bottomNavCG,  0f, UiFadeDuration));
            StartCoroutine(FadeCanvasGroup(_hudCG,        1f, UiFadeDuration));
        }

        // Called by GameOverScreen's Restart button.
        private void OnRestart()
        {
            _gameOverScreen.Hide();

            // Destroy all pad views so SyncAllPads re-creates them from the
            // freshly seeded field.
            foreach (var kv in _padViews)
                Destroy(kv.Value.gameObject);
            _padViews.Clear();

            // Destroy all pickup views; SyncPickups re-creates them as needed.
            foreach (var kv in _pickupViews)
                Destroy(kv.Value.gameObject);
            _pickupViews.Clear();

            // Destroy all fly views; SyncFlies re-creates them as needed.
            foreach (var kv in _flyViews)
                Destroy(kv.Value.gameObject);
            _flyViews.Clear();

            // Hide power-up overlays.
            if (_frostOverlayGO        != null) _frostOverlayGO.SetActive(false);
            if (_timeFreezeCountdownGO != null) _timeFreezeCountdownGO.SetActive(false);

            // Reset model state (returns to Idle, re-seeds pads and Golden Fly field).
            _game.ResetRun();
            _challengeService.Reset();

            // Refresh the lifetime balance label now that the previous run is committed.
            if (_lifetimeFliesText != null)
                _lifetimeFliesText.text = "Flies: " + FlyBank.Get();

            // Reset frog visual state (stop any lingering death animation).
            _frogView.Reset();
            _frogView.SetColor(_game.FrogColor);

            SyncAllPads();

            // Position frog on its starting pad.
            if (_game.FrogPadId != -1 &&
                _padViews.TryGetValue(_game.FrogPadId, out var fp))
            {
                _frogView.SetAnchor(fp.transform.position + new Vector3(0f, 0.3f, 0f));
            }

            // Restore idle chrome immediately (no fade on restart).
            SetCanvasGroupState(_hudCG,        alpha: 0f);
            SetCanvasGroupState(_menuChromeCG, alpha: 1f);
            SetCanvasGroupState(_bottomNavCG,  alpha: 1f);
        }

        // Called by GameOverScreen's Second Chance button after the rewarded ad completes.
        // Hides the panel and returns the player to idle without wiping the current run
        // (no ResetRun, no fly commit — flies remain pending for the next game-over).
        private void OnRevive()
        {
            // Panel already hidden by the rewarded-ad callback in GameOverScreen.
            _frogView.Reset();
            _frogView.SetColor(_game.FrogColor);

            // Re-seat the frog on its current pad if it still exists (misstep revive).
            // After waterfall the pad has already scrolled off, so the frog floats at
            // its reset position until the first tap after returning to idle.
            if (_game.FrogPadId != -1 &&
                _padViews.TryGetValue(_game.FrogPadId, out var rp))
            {
                _frogView.SetAnchor(rp.transform.position + new Vector3(0f, 0.3f, 0f));
            }

            _game.EnterIdle();

            // Restore idle chrome (same layout as post-restart).
            SetCanvasGroupState(_hudCG,        alpha: 0f);
            SetCanvasGroupState(_menuChromeCG, alpha: 1f);
            SetCanvasGroupState(_bottomNavCG,  alpha: 1f);

            // Refresh lifetime balance label (flies not committed yet; re-reads the bank).
            if (_lifetimeFliesText != null)
                _lifetimeFliesText.text = "Flies: " + FlyBank.Get();
        }

        // ------------------------------------------------------------------ //
        // Input
        // ------------------------------------------------------------------ //

        private void HandleInput()
        {
            // Block all input while a sink or ride-off animation is in progress.
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

            // Power-up pickup: collect it and remove its view.
            var pickupView = hit.GetComponent<PowerUpView>();
            if (pickupView != null)
            {
                _game.CollectPowerUp(pickupView.PickupType);
                _audioService.PlayPowerUp(pickupView.PickupType);
                _game.PowerUpField.RemovePickup(pickupView.PickupId);
                if (_pickupViews.TryGetValue(pickupView.PickupId, out var pv))
                {
                    Destroy(pv.gameObject);
                    _pickupViews.Remove(pickupView.PickupId);
                }
                return;
            }

            // Golden Fly collectible: same hit-test path as power-ups and pads (AC2).
            // Guard on _flyViews first: Unity's Destroy() is deferred to end-of-frame, so a fly
            // that was already removed from _flyViews by SyncFlies() (e.g. it scrolled off-screen
            // this Tick) still has a live collider.  Only grant score when the fly is still in our
            // view dictionary, which is the authoritative "still collectible" gate.
            var flyView = hit.GetComponent<GoldenFlyView>();
            if (flyView != null && _flyViews.TryGetValue(flyView.FlyId, out var fv))
            {
                _game.CollectFly();
                _game.GoldenFlyField.RemoveFly(flyView.FlyId);
                Destroy(fv.gameObject);
                _flyViews.Remove(flyView.FlyId);
                _challengeService.RecordFlyCollected();
                return;
            }

            var padView = hit.GetComponent<PadView>();
            if (padView == null) return;

            var result = _game.TapPad(padView.PadId);

            if (result == TapResult.Jump)
            {
                // Play jump SFX immediately on input (AC3); pitch reflects current
                // combo tier.  The double-fire guard in PlayJump handles rapid taps.
                _audioService.PlayJump(_game.ComboMultiplier);

                // Dispatch the jump tween immediately.  Color, anchor, and landing
                // SFX are all applied inside the callback so they fire on arrival
                // rather than at initiation (AC4).
                _frogView.JumpTo(padView.transform, () =>
                {
                    _frogView.SetAnchor(padView.transform.position +
                                        new Vector3(0f, 0.3f, 0f));
                    _frogView.FlashColor(_game.FrogColor);
                    _audioService.PlayLanding();                   // AC4: on arrival
                    if (padView.PadType == PadType.Rainbow)
                        _audioService.PlayRainbowPad();            // AC7: rainbow pad
                });
            }
            else if (result == TapResult.Misstep)
            {
                // Jump to the wrong pad, then sink, then show game-over.
                _audioService.PlayMisstep();
                PlayerPrefs.SetInt("HighScore", _game.HighScore);
                PlayerPrefs.Save();
                int challengeBonus2 = _challengeService.TryGrantBonus();
                _gameOverScreen.SetPendingFlies(_game.FliesThisRun, challengeBonus2);
                bool  newHigh2 = _game.IsNewHighScore;
                int   hi2      = _game.HighScore;
                int   sc2      = _game.Score;
                int   fl2      = _game.FliesThisRun;
                _frogView.JumpTo(padView.transform, () =>
                    _frogView.Sink(() =>
                        _gameOverScreen.Show(GameScreen.MisstepGameOver,
                                             sc2, fl2, hi2, newHigh2)));
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
                    bool isLotus = pad.Type == RainbowFroggy.Core.PadType.Lotus;
                    GameObject go;
                    if (isLotus)
                    {
                        go = CreateSpriteQuad("LotusPad_" + pad.Id, new Vector2(2.5f, 2.5f));
                        var col = go.AddComponent<BoxCollider2D>();
                        col.size = new Vector2(2.5f, 2.5f);
                        go.GetComponent<SpriteRenderer>().color = new Color(1f, 0.5f, 0.8f);
                    }
                    else
                    {
                        go = new GameObject("Pad_" + pad.Id);
                        var sr     = go.AddComponent<SpriteRenderer>();
                        sr.sprite  = FrogView.MakeCircleSprite(32);
                        sr.material = _spriteMat;
                        go.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
                        var col = go.AddComponent<CircleCollider2D>();
                        col.radius = 0.5f;
                    }
                    view = go.AddComponent<PadView>();
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

        private void SyncPickups()
        {
            var activeIds = new HashSet<int>();
            foreach (var pu in _game.PowerUpField.Pickups)
                activeIds.Add(pu.Id);

            var toRemove = new List<int>();
            foreach (var kv in _pickupViews)
                if (!activeIds.Contains(kv.Key))
                    toRemove.Add(kv.Key);

            foreach (var id in toRemove)
            {
                Destroy(_pickupViews[id].gameObject);
                _pickupViews.Remove(id);
            }

            foreach (var pu in _game.PowerUpField.Pickups)
            {
                if (_pickupViews.TryGetValue(pu.Id, out var view))
                {
                    view.SyncPosition(pu);
                }
                else
                {
                    var go  = CreateSpriteQuad("PowerUp_" + pu.Id, new Vector2(0.8f, 0.8f));
                    view    = go.AddComponent<PowerUpView>();
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(0.8f, 0.8f);
                    view.Bind(pu);
                    _pickupViews[pu.Id] = view;
                }
            }
        }

        private void SyncFlies()
        {
            var activeIds = new HashSet<int>();
            foreach (var fly in _game.GoldenFlyField.Flies)
                activeIds.Add(fly.Id);

            var toRemove = new List<int>();
            foreach (var kv in _flyViews)
                if (!activeIds.Contains(kv.Key))
                    toRemove.Add(kv.Key);

            foreach (var id in toRemove)
            {
                Destroy(_flyViews[id].gameObject);
                _flyViews.Remove(id);
            }

            foreach (var fly in _game.GoldenFlyField.Flies)
            {
                if (_flyViews.TryGetValue(fly.Id, out var view))
                {
                    view.SyncPosition(fly);
                }
                else
                {
                    var go  = CreateSpriteQuad("GoldenFly_" + fly.Id, new Vector2(0.6f, 0.6f));
                    go.GetComponent<SpriteRenderer>().color = new Color(1f, 0.85f, 0.2f); // golden
                    view    = go.AddComponent<GoldenFlyView>();
                    var col = go.AddComponent<BoxCollider2D>();
                    col.size = new Vector2(0.6f, 0.6f);
                    view.Bind(fly);
                    _flyViews[fly.Id] = view;
                }
            }
        }

        // ------------------------------------------------------------------ //
        // Scene helpers
        // ------------------------------------------------------------------ //

        // Create the AudioRig root and start BGM (AC1).
        private void BuildAudioRig()
        {
            var go        = new GameObject("AudioRig");
            _audioService = go.AddComponent<AudioService>();
            _audioService.Init();
            _createdRoots.Add(go);
        }

        private void ConfigureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go  = new GameObject("Main Camera");
                go.tag  = "MainCamera";
                cam     = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
                _createdRoots.Add(go);
            }
            cam.orthographic       = true;
            cam.orthographicSize   = 6f;
            cam.clearFlags         = CameraClearFlags.SolidColor;
            cam.backgroundColor    = ColorPalette.River;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void BuildBackground()
        {
            var go = CreateSpriteQuad("Background", new Vector2(9f, 13f));
            go.GetComponent<SpriteRenderer>().color        = ColorPalette.River;
            go.GetComponent<SpriteRenderer>().sortingOrder = -10;
            _createdRoots.Add(go);
        }

        private void BuildFrog()
        {
            var go    = new GameObject("Frog");
            _frogView = go.AddComponent<FrogView>();
            _frogView.SetColor(_game.FrogColor);
            _createdRoots.Add(go);
        }

        // ------------------------------------------------------------------
        // Gameplay HUD (top-left flies, top-right high-score, centre score×combo)
        // ------------------------------------------------------------------

        private void BuildHud()
        {
            var canvas = new GameObject("HUDCanvas");
            var c      = canvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.AddComponent<CanvasScaler>();
            canvas.AddComponent<GraphicRaycaster>();
            _createdRoots.Add(canvas);

            var hudGO = new GameObject("GameplayHUD");
            hudGO.transform.SetParent(canvas.transform, false);
            _hudCG = hudGO.AddComponent<CanvasGroup>();
            _hud   = hudGO.AddComponent<HudView>();

            // Top-left: Golden Flies counter.
            var fliesGO            = new GameObject("FliesLabel");
            fliesGO.transform.SetParent(hudGO.transform, false);
            var fliesRT            = fliesGO.AddComponent<RectTransform>();
            fliesRT.anchorMin      = new Vector2(0f, 1f);
            fliesRT.anchorMax      = new Vector2(0f, 1f);
            fliesRT.pivot          = new Vector2(0f, 1f);
            fliesRT.anchoredPosition = new Vector2(20f, -20f);
            fliesRT.sizeDelta      = new Vector2(220f, 50f);
            var fliesText          = fliesGO.AddComponent<Text>();
            fliesText.font         = FontLibrary.Body;
            fliesText.fontSize     = 28;
            fliesText.color        = new Color(1f, 0.85f, 0.2f); // golden
            fliesText.text         = "Flies: 0";

            // Top-right: High score.
            var bestGO             = new GameObject("HighScoreLabel");
            bestGO.transform.SetParent(hudGO.transform, false);
            var bestRT             = bestGO.AddComponent<RectTransform>();
            bestRT.anchorMin       = new Vector2(1f, 1f);
            bestRT.anchorMax       = new Vector2(1f, 1f);
            bestRT.pivot           = new Vector2(1f, 1f);
            bestRT.anchoredPosition = new Vector2(-20f, -20f);
            bestRT.sizeDelta       = new Vector2(220f, 50f);
            var bestText           = bestGO.AddComponent<Text>();
            bestText.font          = FontLibrary.Body;
            bestText.fontSize      = 28;
            bestText.color         = Color.white;
            bestText.alignment     = TextAnchor.UpperRight;
            bestText.text          = "Best: 0";

            // Centre: Score + Fever Multiplier ("142 ×3").
            var scoreGO            = new GameObject("ScoreLabel");
            scoreGO.transform.SetParent(hudGO.transform, false);
            var scoreRT            = scoreGO.AddComponent<RectTransform>();
            scoreRT.anchorMin      = new Vector2(0.5f, 1f);
            scoreRT.anchorMax      = new Vector2(0.5f, 1f);
            scoreRT.pivot          = new Vector2(0.5f, 1f);
            scoreRT.anchoredPosition = new Vector2(0f, -20f);
            scoreRT.sizeDelta      = new Vector2(280f, 50f);
            var scoreText          = scoreGO.AddComponent<Text>();
            scoreText.font         = FontLibrary.Body;
            scoreText.fontSize     = 32;
            scoreText.fontStyle    = FontStyle.Bold;
            scoreText.color        = Color.white;
            scoreText.alignment    = TextAnchor.UpperCenter;
            scoreText.text         = "0 \xd71";

            // Top-centre: Prism Mode countdown ("PRISM 7.4s") — hidden until active.
            var prismGO            = new GameObject("PrismLabel");
            prismGO.transform.SetParent(hudGO.transform, false);
            var prismRT            = prismGO.AddComponent<RectTransform>();
            prismRT.anchorMin      = new Vector2(0.5f, 1f);
            prismRT.anchorMax      = new Vector2(0.5f, 1f);
            prismRT.pivot          = new Vector2(0.5f, 1f);
            prismRT.anchoredPosition = new Vector2(0f, -76f); // below score label
            prismRT.sizeDelta      = new Vector2(280f, 44f);
            var prismText          = prismGO.AddComponent<Text>();
            prismText.font         = FontLibrary.Body;
            prismText.fontSize     = 26;
            prismText.fontStyle    = FontStyle.Bold;
            prismText.color        = new Color(1f, 0.878f, 0.2f); // gold, matches pickup
            prismText.alignment    = TextAnchor.UpperCenter;
            prismText.text         = "PRISM 8.0s";

            // Bottom-centre: Time Freeze countdown — hidden until active.
            var countdownGO            = new GameObject("TimeFreezeCountdown");
            countdownGO.transform.SetParent(hudGO.transform, false);
            var countdownRT            = countdownGO.AddComponent<RectTransform>();
            countdownRT.anchorMin      = new Vector2(0.5f, 0f);
            countdownRT.anchorMax      = new Vector2(0.5f, 0f);
            countdownRT.pivot          = new Vector2(0.5f, 0f);
            countdownRT.anchoredPosition = new Vector2(0f, 30f);
            countdownRT.sizeDelta      = new Vector2(240f, 50f);
            var countdownText          = countdownGO.AddComponent<Text>();
            countdownText.font         = FontLibrary.Body;
            countdownText.fontSize     = 30;
            countdownText.fontStyle    = FontStyle.Bold;
            countdownText.alignment    = TextAnchor.MiddleCenter;
            countdownText.color        = new Color(0.6f, 0.9f, 1.0f); // icy blue
            countdownText.text         = "Freeze: 5.0s";
            _timeFreezeCountdownGO     = countdownGO;

            // Top-left (below flies): daily challenge progress ("Challenge: N / M").
            var challengeGO              = new GameObject("ChallengeLabel");
            challengeGO.transform.SetParent(hudGO.transform, false);
            var challengeRT              = challengeGO.AddComponent<RectTransform>();
            challengeRT.anchorMin        = new Vector2(0f, 1f);
            challengeRT.anchorMax        = new Vector2(0f, 1f);
            challengeRT.pivot            = new Vector2(0f, 1f);
            challengeRT.anchoredPosition = new Vector2(20f, -76f); // 56 px below FliesLabel
            challengeRT.sizeDelta        = new Vector2(220f, 44f);
            var challengeText            = challengeGO.AddComponent<Text>();
            challengeText.font           = FontLibrary.Body;
            challengeText.fontSize       = 22;
            challengeText.color          = new Color(1f, 0.85f, 0.2f); // golden
            challengeText.text           = "Challenge: 0 / " +
                                           _challengeService.Challenge.Target;

            _hud.Init(fliesText, scoreText, bestText, prismText, countdownText, challengeText);
        }

        // ------------------------------------------------------------------
        // Frost overlay — full-screen tint visible during Time Freeze
        // ------------------------------------------------------------------

        private void BuildFrostOverlay()
        {
            // A dedicated canvas so it renders above the river but below the HUD.
            var canvas     = new GameObject("FrostOverlayCanvas");
            var c          = canvas.AddComponent<Canvas>();
            c.renderMode   = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 3;
            canvas.AddComponent<CanvasScaler>();
            _createdRoots.Add(canvas);

            var overlayGO  = new GameObject("FrostOverlay");
            overlayGO.transform.SetParent(canvas.transform, false);
            var rt         = overlayGO.AddComponent<RectTransform>();
            rt.anchorMin   = Vector2.zero;
            rt.anchorMax   = Vector2.one;
            rt.offsetMin   = Vector2.zero;
            rt.offsetMax   = Vector2.zero;
            var img        = overlayGO.AddComponent<Image>();
            img.color      = new Color(0.65f, 0.90f, 1.00f, 0.28f); // semi-transparent icy blue
            overlayGO.AddComponent<FrostOverlay>();

            _frostOverlayGO = overlayGO;
            overlayGO.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Menu canvas: idle chrome + bottom nav bar + bottom sheets
        // ------------------------------------------------------------------

        private void BuildMenuAndNav()
        {
            var evsGO = new GameObject("EventSystem");
            evsGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
            evsGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            _createdRoots.Add(evsGO);

            var canvas = new GameObject("MenuCanvas");
            var c      = canvas.AddComponent<Canvas>();
            c.renderMode   = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 5;
            canvas.AddComponent<CanvasScaler>();
            canvas.AddComponent<GraphicRaycaster>();
            _createdRoots.Add(canvas);

            // ---- Menu chrome (title + tap zone) ----
            var chromeGO = new GameObject("MenuChrome");
            chromeGO.transform.SetParent(canvas.transform, false);
            _menuChromeCG = chromeGO.AddComponent<CanvasGroup>();
            var chromeRT  = chromeGO.AddComponent<RectTransform>();
            chromeRT.anchorMin = Vector2.zero;
            chromeRT.anchorMax = Vector2.one;
            chromeRT.offsetMin = Vector2.zero;
            chromeRT.offsetMax = Vector2.zero;

            // Title text.
            var titleGO            = new GameObject("TitleText");
            titleGO.transform.SetParent(chromeGO.transform, false);
            var titleRT            = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin      = new Vector2(0.1f, 0.60f);
            titleRT.anchorMax      = new Vector2(0.9f, 0.80f);
            titleRT.offsetMin      = Vector2.zero;
            titleRT.offsetMax      = Vector2.zero;
            var titleText          = titleGO.AddComponent<Text>();
            titleText.font         = FontLibrary.Body;
            titleText.fontSize     = 52;
            titleText.fontStyle    = FontStyle.Bold;
            titleText.alignment    = TextAnchor.MiddleCenter;
            titleText.color        = Color.white;
            titleText.text         = "RAINBOW FROGGY";

            // Tap-to-play prompt.
            var promptGO           = new GameObject("TapPrompt");
            promptGO.transform.SetParent(chromeGO.transform, false);
            var promptRT           = promptGO.AddComponent<RectTransform>();
            promptRT.anchorMin     = new Vector2(0.2f, 0.42f);
            promptRT.anchorMax     = new Vector2(0.8f, 0.52f);
            promptRT.offsetMin     = Vector2.zero;
            promptRT.offsetMax     = Vector2.zero;
            var promptText         = promptGO.AddComponent<Text>();
            promptText.font        = FontLibrary.Body;
            promptText.fontSize    = 28;
            promptText.alignment   = TextAnchor.MiddleCenter;
            promptText.color       = new Color(1f, 1f, 1f, 0.7f);
            promptText.text        = "TAP TO PLAY";

            // Lifetime Fly balance — reads from FlyBank.LifetimeKey (AC6).
            var lifetimeGO             = new GameObject("LifetimeFliesLabel");
            lifetimeGO.transform.SetParent(chromeGO.transform, false);
            var lifetimeRT             = lifetimeGO.AddComponent<RectTransform>();
            lifetimeRT.anchorMin       = new Vector2(0.1f, 0.28f);
            lifetimeRT.anchorMax       = new Vector2(0.9f, 0.38f);
            lifetimeRT.offsetMin       = Vector2.zero;
            lifetimeRT.offsetMax       = Vector2.zero;
            _lifetimeFliesText         = lifetimeGO.AddComponent<Text>();
            _lifetimeFliesText.font    = FontLibrary.Body;
            _lifetimeFliesText.fontSize    = 26;
            _lifetimeFliesText.alignment   = TextAnchor.MiddleCenter;
            _lifetimeFliesText.color       = new Color(1f, 0.85f, 0.2f); // golden
            _lifetimeFliesText.text        = "Flies: " + FlyBank.Get();

            // Full-screen transparent tap-zone button (sits behind the title text
            // in the hierarchy so the title renders on top).
            var tapZoneGO           = new GameObject("TapZone");
            tapZoneGO.transform.SetParent(chromeGO.transform, false);
            tapZoneGO.transform.SetAsFirstSibling();
            var tapZoneRT           = tapZoneGO.AddComponent<RectTransform>();
            tapZoneRT.anchorMin     = Vector2.zero;
            tapZoneRT.anchorMax     = Vector2.one;
            tapZoneRT.offsetMin     = Vector2.zero;
            tapZoneRT.offsetMax     = Vector2.zero;
            var tapZoneImg          = tapZoneGO.AddComponent<Image>();
            tapZoneImg.color        = new Color(0f, 0f, 0f, 0f); // fully transparent
            var tapZoneBtn          = tapZoneGO.AddComponent<Button>();
            var tapZoneCols         = tapZoneBtn.colors;
            tapZoneCols.normalColor = new Color(0f, 0f, 0f, 0f);
            tapZoneBtn.colors       = tapZoneCols;

            _menuChrome = chromeGO.AddComponent<MenuChrome>();
            _menuChrome.Init(tapZoneBtn);
            _menuChrome.OnTapZonePressed = OnMenuTapZone;

            // ---- Bottom nav bar ----
            var navGO    = new GameObject("BottomNavBar");
            navGO.transform.SetParent(canvas.transform, false);
            _bottomNavCG = navGO.AddComponent<CanvasGroup>();
            var navRT    = navGO.AddComponent<RectTransform>();
            navRT.anchorMin      = new Vector2(0f, 0f);
            navRT.anchorMax      = new Vector2(1f, 0f);
            navRT.pivot          = new Vector2(0.5f, 0f);
            navRT.anchoredPosition = Vector2.zero;
            navRT.sizeDelta      = new Vector2(0f, 80f);
            var navBg            = navGO.AddComponent<Image>();
            navBg.color          = new Color(0.05f, 0.08f, 0.15f, 0.92f);

            // ---- Bottom sheets (built before wiring nav buttons) ----
            var sheetCanvas = new GameObject("BottomSheetCanvas");
            var sc          = sheetCanvas.AddComponent<Canvas>();
            sc.renderMode   = RenderMode.ScreenSpaceOverlay;
            sc.sortingOrder = 6;
            sheetCanvas.AddComponent<CanvasScaler>();
            sheetCanvas.AddComponent<GraphicRaycaster>();
            _createdRoots.Add(sheetCanvas);

            var wardrobeSheet    = BuildBottomSheet(sheetCanvas.transform, "Wardrobe",    "Wardrobe");
            var leaderboardSheet = BuildBottomSheet(sheetCanvas.transform, "Leaderboard", "Leaderboard");
            var settingsSheet    = BuildBottomSheet(sheetCanvas.transform, "Settings",    "Settings");

            // Populate the settings sheet with the mute toggle (AC8).
            var sp = settingsSheet.gameObject.AddComponent<SettingsPanel>();
            sp.Init(settingsSheet.transform, _audioService);

            // Populate the wardrobe sheet with the skin grid.
            var wp = wardrobeSheet.gameObject.AddComponent<WardrobePanel>();
            wp.Init(wardrobeSheet.transform, _skinService,
                    () => _game.HighScore,
                    skin => { if (_frogView != null) _frogView.SetSkin(skin?.sprite); });

            // ---- Nav buttons ----
            Button wardrobeBtn    = BuildNavButton(navGO.transform, "Wardrobe",    0);
            Button leaderboardBtn = BuildNavButton(navGO.transform, "Leaderboard", 1);
            Button settingsBtn    = BuildNavButton(navGO.transform, "Settings",    2);

            _bottomNavBar = navGO.AddComponent<BottomNavBar>();
            _bottomNavBar.Init(wardrobeBtn,    wardrobeSheet,
                               leaderboardBtn, leaderboardSheet,
                               settingsBtn,    settingsSheet);
        }

        // Create one nav button at column index (0 = left, 1 = mid, 2 = right).
        private Button BuildNavButton(Transform parent, string label, int col)
        {
            float xMin = col / 3f;
            float xMax = (col + 1) / 3f;

            var go  = new GameObject("NavBtn_" + label);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, 0f);
            rt.anchorMax = new Vector2(xMax, 1f);
            rt.offsetMin = new Vector2(4f,  4f);
            rt.offsetMax = new Vector2(-4f, -4f);

            var img   = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.18f, 0.30f, 0.85f);

            var btn   = go.AddComponent<Button>();

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = Vector2.zero;
            var txt         = lblGO.AddComponent<Text>();
            txt.font        = FontLibrary.Body;
            txt.fontSize    = 20;
            txt.alignment   = TextAnchor.MiddleCenter;
            txt.color       = Color.white;
            txt.text        = label;

            return btn;
        }

        // Create a slide-up bottom-sheet panel with a title and close button.
        private BottomSheet BuildBottomSheet(Transform canvasParent,
                                             string goName, string title)
        {
            const float sheetHeight = 360f;

            var go  = new GameObject("Sheet_" + goName);
            go.transform.SetParent(canvasParent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin      = new Vector2(0f, 0f);
            rt.anchorMax      = new Vector2(1f, 0f);
            rt.pivot          = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, -sheetHeight); // off-screen
            rt.sizeDelta      = new Vector2(0f, sheetHeight);

            var bg    = go.AddComponent<Image>();
            bg.color  = new Color(0.08f, 0.12f, 0.22f, 0.96f);

            // Title.
            var titleGO  = new GameObject("Title");
            titleGO.transform.SetParent(go.transform, false);
            var titleRT  = titleGO.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.1f, 0.75f);
            titleRT.anchorMax = new Vector2(0.9f, 0.95f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;
            var titleTxt      = titleGO.AddComponent<Text>();
            titleTxt.font      = FontLibrary.Body;
            titleTxt.fontSize  = 32;
            titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.color     = Color.white;
            titleTxt.text      = title;

            // Close button.
            var closeBtnGO = new GameObject("CloseBtn");
            closeBtnGO.transform.SetParent(go.transform, false);
            var closeBtnRT       = closeBtnGO.AddComponent<RectTransform>();
            closeBtnRT.anchorMin = new Vector2(0.35f, 0.08f);
            closeBtnRT.anchorMax = new Vector2(0.65f, 0.22f);
            closeBtnRT.offsetMin = Vector2.zero;
            closeBtnRT.offsetMax = Vector2.zero;
            var closeBtnImg      = closeBtnGO.AddComponent<Image>();
            closeBtnImg.color    = new Color(0.2f, 0.25f, 0.4f, 0.9f);
            var closeBtn         = closeBtnGO.AddComponent<Button>();

            var closeLblGO  = new GameObject("Label");
            closeLblGO.transform.SetParent(closeBtnGO.transform, false);
            var closeLblRT  = closeLblGO.AddComponent<RectTransform>();
            closeLblRT.anchorMin = Vector2.zero;
            closeLblRT.anchorMax = Vector2.one;
            closeLblRT.offsetMin = Vector2.zero;
            closeLblRT.offsetMax = Vector2.zero;
            var closeLblTxt      = closeLblGO.AddComponent<Text>();
            closeLblTxt.font      = FontLibrary.Body;
            closeLblTxt.fontSize  = 22;
            closeLblTxt.alignment = TextAnchor.MiddleCenter;
            closeLblTxt.color     = Color.white;
            closeLblTxt.text      = "Close";

            var sheet = go.AddComponent<BottomSheet>();
            sheet.Init(rt, closedY: -sheetHeight, openY: 0f);

            // Wire the close button now that sheet is initialised.
            closeBtn.onClick.AddListener(sheet.Close);

            return sheet;
        }

        // ------------------------------------------------------------------
        // Game-over screen
        // ------------------------------------------------------------------

        private void BuildGameOverScreen()
        {
            var canvas = new GameObject("GameOverCanvas");
            var c      = canvas.AddComponent<Canvas>();
            c.renderMode   = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 10;
            canvas.AddComponent<CanvasScaler>();
            canvas.AddComponent<GraphicRaycaster>();
            _createdRoots.Add(canvas);

            // Dark full-screen glassmorphic overlay.
            var panelGO  = new GameObject("GameOverPanel");
            panelGO.transform.SetParent(canvas.transform, false);
            var panelRT  = panelGO.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0.04f, 0.06f, 0.12f, 0.88f);

            _gameOverScreen = panelGO.AddComponent<GameOverScreen>();

            // Failure-type header.
            var headerText = MakeLabel(panelGO.transform, "Header",
                new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.87f),
                fontSize: 52, bold: true, align: TextAnchor.MiddleCenter);
            headerText.text = "";

            // Score.
            var scoreText = MakeLabel(panelGO.transform, "ScoreLabel",
                new Vector2(0.15f, 0.59f), new Vector2(0.85f, 0.69f),
                fontSize: 34, bold: false, align: TextAnchor.MiddleCenter);
            scoreText.text = "Score: 0";

            // Flies earned.
            var fliesText = MakeLabel(panelGO.transform, "FliesLabel",
                new Vector2(0.15f, 0.48f), new Vector2(0.85f, 0.58f),
                fontSize: 30, bold: false, align: TextAnchor.MiddleCenter);
            fliesText.color = new Color(1f, 0.85f, 0.2f); // golden
            fliesText.text  = "Flies: 0";

            // Second Chance button.
            var secondChanceBtn = MakeButton(panelGO.transform, "SecondChance",
                new Vector2(0.15f, 0.36f), new Vector2(0.85f, 0.46f),
                "Second Chance", new Color(0.1f, 0.35f, 0.6f, 0.9f));

            // Fly Multiplier button.
            var flyMultiplierBtn = MakeButton(panelGO.transform, "FlyMultiplier",
                new Vector2(0.15f, 0.24f), new Vector2(0.85f, 0.34f),
                "Fly Multiplier", new Color(0.55f, 0.25f, 0.05f, 0.9f));

            // Restart button.
            var restartBtn = MakeButton(panelGO.transform, "RestartButton",
                new Vector2(0.15f, 0.10f), new Vector2(0.85f, 0.21f),
                "Restart", new Color(0.1f, 0.42f, 0.18f, 0.9f));

            // All-time best score (always visible on the game-over panel).
            var bestText = MakeLabel(panelGO.transform, "BestScoreLabel",
                new Vector2(0.15f, 0.88f), new Vector2(0.85f, 0.97f),
                fontSize: 26, bold: false, align: TextAnchor.MiddleCenter);
            bestText.text = "Best: 0";

            // "New High Score!" flash — starts inactive; shown only when score beats the record.
            var newHighGO    = new GameObject("NewHighScoreLabel");
            newHighGO.transform.SetParent(panelGO.transform, false);
            var newHighRT    = newHighGO.AddComponent<RectTransform>();
            newHighRT.anchorMin = new Vector2(0.05f, 0.79f);
            newHighRT.anchorMax = new Vector2(0.95f, 0.87f);
            newHighRT.offsetMin = Vector2.zero;
            newHighRT.offsetMax = Vector2.zero;
            var newHighTxt   = newHighGO.AddComponent<Text>();
            newHighTxt.font      = FontLibrary.Body;
            newHighTxt.fontSize  = 28;
            newHighTxt.fontStyle = FontStyle.Bold;
            newHighTxt.alignment = TextAnchor.MiddleCenter;
            newHighTxt.color     = new Color(1f, 0.85f, 0.1f); // golden

            newHighTxt.text = "New High Score!";

            _gameOverScreen.Init(headerText, scoreText, fliesText,
                                 secondChanceBtn, flyMultiplierBtn, restartBtn,
                                 OnRestart);
            _gameOverScreen.InitHighScore(bestText, newHighGO);
        }

        // ------------------------------------------------------------------ //
        // UI builder helpers
        // ------------------------------------------------------------------ //

        // Create a Text label anchored to anchorMin/anchorMax (no offset).
        private Text MakeLabel(Transform parent, string goName,
                               Vector2 anchorMin, Vector2 anchorMax,
                               int fontSize, bool bold, TextAnchor align)
        {
            var go  = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var txt       = go.AddComponent<Text>();
            txt.font      = FontLibrary.Body;
            txt.fontSize  = fontSize;
            txt.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            txt.alignment = align;
            txt.color     = Color.white;
            return txt;
        }

        // Create a Button with a label, anchored to anchorMin/anchorMax.
        private Button MakeButton(Transform parent, string goName,
                                  Vector2 anchorMin, Vector2 anchorMax,
                                  string label, Color bgColor)
        {
            var go  = new GameObject(goName);
            go.transform.SetParent(parent, false);
            var rt  = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img   = go.AddComponent<Image>();
            img.color = bgColor;
            var btn   = go.AddComponent<Button>();

            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lblRT = lblGO.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = Vector2.zero;
            var txt       = lblGO.AddComponent<Text>();
            txt.font      = FontLibrary.Body;
            txt.fontSize  = 26;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color     = Color.white;
            txt.text      = label;

            return btn;
        }

        // ------------------------------------------------------------------ //
        // CanvasGroup helpers
        // ------------------------------------------------------------------ //

        private static void SetCanvasGroupState(CanvasGroup cg, float alpha)
        {
            cg.alpha          = alpha;
            cg.interactable   = alpha > 0f;
            cg.blocksRaycasts = alpha > 0f;
        }

        private IEnumerator FadeCanvasGroup(CanvasGroup cg, float target, float duration)
        {
            float startAlpha = cg.alpha;
            float elapsed    = 0f;
            while (elapsed < duration)
            {
                elapsed  += Time.deltaTime;
                cg.alpha  = Mathf.Lerp(startAlpha, target,
                                Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            cg.alpha          = target;
            cg.interactable   = target > 0f;
            cg.blocksRaycasts = target > 0f;
        }

        // ------------------------------------------------------------------ //
        // Sprite helpers
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
