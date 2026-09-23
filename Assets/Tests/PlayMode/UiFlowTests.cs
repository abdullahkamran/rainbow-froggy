using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using RainbowFroggy.Core;
using RainbowFroggy.View;
using Object = UnityEngine.Object;

namespace RainbowFroggy.Tests.PlayMode
{
    // PlayMode tests for UI / UX acceptance criteria (AC5–AC8).
    // Each test instantiates GameBootstrap, waits one frame for Start() to run,
    // then inspects named GameObjects created by the bootstrap.
    [TestFixture]
    public class UiFlowTests
    {
        private GameBootstrap _bootstrap;

        [TearDown]
        public void Cleanup()
        {
            if (_bootstrap == null) return;
            foreach (var root in _bootstrap.CreatedRoots)
                if (root != null) Object.Destroy(root);
            Object.Destroy(_bootstrap.gameObject);
            _bootstrap = null;
        }

        // Helper: instantiate GameBootstrap and wait one frame for Start().
        private IEnumerator CreateBootstrap()
        {
            var go = new GameObject("GameBootstrap");
            _bootstrap = go.AddComponent<GameBootstrap>();
            yield return null; // let Start() execute
        }

        // ------------------------------------------------------------------ //
        // AC5: bottom nav bar visible in idle, hidden once gameplay begins
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator BottomNav_VisibleInIdle_HiddenInPlay()
        {
            yield return CreateBootstrap();

            // In idle: nav bar CanvasGroup should be at full alpha.
            var navBarGO = GameObject.Find("BottomNavBar");
            Assert.IsNotNull(navBarGO, "BottomNavBar should exist after Start");
            var cg = navBarGO.GetComponent<CanvasGroup>();
            Assert.IsNotNull(cg, "BottomNavBar must have a CanvasGroup");
            Assert.AreEqual(1f, cg.alpha, 0.01f,
                "BottomNavBar should be fully visible in idle");
            Assert.IsTrue(cg.interactable,   "BottomNavBar should be interactable in idle");
            Assert.IsTrue(cg.blocksRaycasts, "BottomNavBar should block raycasts in idle");

            // Tap the centre zone to start a run.
            var tapZoneGO = GameObject.Find("TapZone");
            Assert.IsNotNull(tapZoneGO, "TapZone should exist after Start");
            tapZoneGO.GetComponent<Button>().onClick.Invoke();

            // Wait for the fade coroutine to complete (UiFadeDuration = 0.3 s + margin).
            yield return new WaitForSeconds(0.5f);

            Assert.Less(cg.alpha, 0.05f,
                "BottomNavBar should be faded out after gameplay starts");
            Assert.IsFalse(cg.interactable,   "BottomNavBar should not be interactable during play");
            Assert.IsFalse(cg.blocksRaycasts, "BottomNavBar should not block raycasts during play");
        }

        // ------------------------------------------------------------------ //
        // AC6: tapping in the centre zone starts gameplay without scene load
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator CenterZoneTap_StartsRun_NoSceneLoad()
        {
            yield return CreateBootstrap();

            // Should start in idle.
            Assert.AreEqual(GameScreen.Idle, _bootstrap.Screen,
                "GameBootstrap should start in Idle");

            int    scenesAtStart = SceneManager.sceneCount;
            bool   sceneLoaded  = false;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler =
                (s, m) => { sceneLoaded = true; };
            SceneManager.sceneLoaded += handler;

            // Invoke the tap zone.
            var tapZoneGO = GameObject.Find("TapZone");
            Assert.IsNotNull(tapZoneGO);
            tapZoneGO.GetComponent<Button>().onClick.Invoke();

            yield return null;

            SceneManager.sceneLoaded -= handler;

            Assert.AreEqual(GameScreen.Playing, _bootstrap.Screen,
                "Screen should be Playing after centre-zone tap");
            Assert.IsFalse(sceneLoaded,
                "No additional scene should have been loaded");
            Assert.AreEqual(scenesAtStart, SceneManager.sceneCount,
                "Scene count must not change");
        }

        // ------------------------------------------------------------------ //
        // AC7: game-over panel contains all required elements
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator GameOverPanel_ContainsRequiredElements()
        {
            yield return CreateBootstrap();

            // Use the bootstrap's test seam — the panel starts inactive so
            // GameObject.Find would skip it.
            var screen  = _bootstrap.GameOverScreen;
            Assert.IsNotNull(screen, "GameOverScreen must be created by bootstrap");
            var panelGO = screen.gameObject;

            // Check all required child elements regardless of active state.
            bool foundHeader       = false;
            bool foundScore        = false;
            bool foundFlies        = false;
            bool foundSecondChance = false;
            bool foundFlyMult      = false;
            bool foundRestart      = false;

            foreach (Transform child in panelGO.transform)
            {
                switch (child.name)
                {
                    case "Header":        foundHeader       = true; break;
                    case "ScoreLabel":    foundScore        = true; break;
                    case "FliesLabel":    foundFlies        = true; break;
                    case "SecondChance":  foundSecondChance = true; break;
                    case "FlyMultiplier": foundFlyMult      = true; break;
                    case "RestartButton": foundRestart      = true; break;
                }
            }

            Assert.IsTrue(foundHeader,       "Header required in GameOverPanel");
            Assert.IsTrue(foundScore,        "ScoreLabel required in GameOverPanel");
            Assert.IsTrue(foundFlies,        "FliesLabel required in GameOverPanel");
            Assert.IsTrue(foundSecondChance, "SecondChance button required in GameOverPanel");
            Assert.IsTrue(foundFlyMult,      "FlyMultiplier button required in GameOverPanel");
            Assert.IsTrue(foundRestart,      "RestartButton required in GameOverPanel");

            // Show with misstep and confirm the header text.
            screen.Show(GameScreen.MisstepGameOver, 42, 3);

            var headerTxt = panelGO.transform.Find("Header")?.GetComponent<Text>();
            Assert.IsNotNull(headerTxt, "Header must have a Text component");
            Assert.AreEqual("Wrong Pad!", headerTxt.text,
                "Header text must be 'Wrong Pad!' for MisstepGameOver");

            // Show with waterfall and confirm the header text.
            screen.Show(GameScreen.WaterfallGameOver, 0, 0);
            Assert.AreEqual("Swept Away!", headerTxt.text,
                "Header text must be 'Swept Away!' for WaterfallGameOver");

            yield return null;
        }

        // ------------------------------------------------------------------ //
        // AC8: Restart resets game state in-place without loading another scene
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator Restart_ResetsInPlace()
        {
            yield return CreateBootstrap();

            bool   sceneLoaded = false;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> handler =
                (s, m) => { sceneLoaded = true; };
            SceneManager.sceneLoaded += handler;

            // Start a run so the game is in Playing state.
            var tapZoneGO = GameObject.Find("TapZone");
            tapZoneGO.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameScreen.Playing, _bootstrap.Screen);

            // Simulate a game-over by showing the panel directly.
            // The panel starts inactive so we use the bootstrap test seam.
            var gos     = _bootstrap.GameOverScreen;
            var panelGO = gos.gameObject;
            gos.Show(GameScreen.MisstepGameOver, 5, 2);
            yield return null;

            // Click Restart — found via the (now active) panel's Transform.
            var restartT = panelGO.transform.Find("RestartButton");
            Assert.IsNotNull(restartT, "RestartButton must exist inside GameOverPanel");
            restartT.GetComponent<Button>().onClick.Invoke();
            yield return null;

            SceneManager.sceneLoaded -= handler;

            // Game should be in Idle with reset score.
            Assert.AreEqual(GameScreen.Idle, _bootstrap.Screen,
                "After restart, Screen should return to Idle");
            Assert.AreEqual(0, _bootstrap.Score,
                "After restart, Score should be 0");
            Assert.IsFalse(panelGO.activeSelf,
                "GameOverPanel should be hidden after restart");
            Assert.IsFalse(sceneLoaded,
                "Restart must not trigger a scene load");
        }
    }
}
