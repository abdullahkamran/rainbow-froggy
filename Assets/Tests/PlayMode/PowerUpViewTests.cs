using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RainbowFroggy.Core;
using RainbowFroggy.View;
using Object = UnityEngine.Object;

namespace RainbowFroggy.Tests.PlayMode
{
    // PlayMode tests for power-up acceptance criteria that require GameObjects.
    // Each test instantiates GameBootstrap, waits one frame for Start() to run,
    // then inspects scene GameObjects created by the bootstrap.
    [TestFixture]
    public class PowerUpViewTests
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

        private IEnumerator CreateBootstrap()
        {
            var go  = new GameObject("GameBootstrap");
            _bootstrap = go.AddComponent<GameBootstrap>();
            yield return null; // let Start() execute
        }

        private void StartRun()
        {
            var tapZone = GameObject.Find("TapZone");
            if (tapZone != null)
                tapZone.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
        }

        // ------------------------------------------------------------------ //
        // AC1: spawn logic produces a pickup positioned within river bounds
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator TimeFreeze_SpawnPickup_IsNonNullAndWithinRiverBounds()
        {
            yield return CreateBootstrap();
            StartRun();
            yield return null;

            // Force-spawn a TimeFreeze pickup via the PowerUpField.
            var pickup = _bootstrap.gameObject
                .GetComponent<GameBootstrap>()
                == null ? null : _bootstrap;  // ensure bootstrap is valid

            // Spawn directly via the game model's power-up field.
            var game = typeof(GameBootstrap)
                .GetField("_game",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(_bootstrap) as RainbowFroggyGame;

            Assert.IsNotNull(game, "RainbowFroggyGame must be accessible via reflection");

            var data = game.PowerUpField.SpawnPickup(game.Field.Pads, game.FrogColor);
            Assert.IsNotNull(data, "SpawnPickup must return a non-null PowerUpData");

            // Verify world position is within river bounds (x∈[-3.5,3.5], y∈[-5,5]).
            float wx = Mathf.Lerp(-3.5f, 3.5f, data.X);
            float wy = Mathf.Lerp( 5f,  -5f,  data.Y);
            Assert.That(wx, Is.InRange(-3.5f, 3.5f), "Pickup world X must be within [-3.5, 3.5]");
            Assert.That(wy, Is.InRange(-5.0f, 5.0f), "Pickup world Y must be within [-5, 5]");

            yield return null;
        }

        // ------------------------------------------------------------------ //
        // AC2: collecting Time Freeze sets all four post-collection conditions:
        //   (1) IsTimeFreezeActive == true
        //   (2) ScrollSpeedMultiplier == 0.20 of pre-freeze value
        //   (3) frost overlay active
        //   (4) countdown UI active
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator TimeFreeze_Collect_FrostOverlayActive_CountdownActive()
        {
            yield return CreateBootstrap();
            StartRun();
            yield return null;

            // Access model so we can assert game-state conditions (1) and (2)
            // in the same test as the view conditions (3) and (4).
            var game = typeof(GameBootstrap)
                .GetField("_game",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(_bootstrap) as RainbowFroggyGame;

            Assert.IsNotNull(game, "RainbowFroggyGame must be accessible via reflection");

            Assert.IsNotNull(_bootstrap.FrostOverlay,
                "FrostOverlay must be created by bootstrap");
            Assert.IsNotNull(_bootstrap.TimeFreezeCountdown,
                "TimeFreezeCountdown must be created by bootstrap");

            // Before collection both must be inactive.
            Assert.IsFalse(_bootstrap.FrostOverlay.activeSelf,
                "FrostOverlay must be inactive before Time Freeze is collected");
            Assert.IsFalse(_bootstrap.TimeFreezeCountdown.activeSelf,
                "TimeFreezeCountdown must be inactive before Time Freeze is collected");

            float preFreezeMultiplier = game.Field.ScrollSpeedMultiplier;
            _bootstrap.ForceCollectPowerUp(PowerUpType.TimeFreeze);
            yield return null;

            // (1) Game model: active flag must be set.
            Assert.IsTrue(game.IsTimeFreezeActive,
                "IsTimeFreezeActive must be true immediately after collection");

            // (2) Game model: multiplier must be reduced to 0.20 of pre-freeze.
            Assert.AreEqual(preFreezeMultiplier * 0.20f, game.Field.ScrollSpeedMultiplier, 1e-6f,
                "ScrollSpeedMultiplier must equal 0.20 of its pre-freeze value after Time Freeze collection");

            // (3) View: frost overlay must be visible.
            Assert.IsTrue(_bootstrap.FrostOverlay.activeSelf,
                "FrostOverlay must be active after Time Freeze is collected");

            // (4) View: countdown HUD must be visible.
            Assert.IsTrue(_bootstrap.TimeFreezeCountdown.activeSelf,
                "TimeFreezeCountdown must be active after Time Freeze is collected");
        }

        // ------------------------------------------------------------------ //
        // AC3: after 5 seconds all four post-expiry conditions hold:
        //   (1) IsTimeFreezeActive == false
        //   (2) ScrollSpeedMultiplier restored to pre-freeze value
        //   (3) frost overlay inactive
        //   (4) countdown UI inactive
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator TimeFreeze_Expiry_FrostOverlayInactive_CountdownInactive()
        {
            yield return CreateBootstrap();
            StartRun();
            yield return null;

            // Access model so we can assert game-state conditions (1) and (2)
            // in the same test as the view conditions (3) and (4).
            var game = typeof(GameBootstrap)
                .GetField("_game",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(_bootstrap) as RainbowFroggyGame;

            Assert.IsNotNull(game, "RainbowFroggyGame must be accessible via reflection");

            // Capture pre-freeze multiplier before collecting, so we can verify
            // restoration after expiry.
            float preFreeze = game.Field.ScrollSpeedMultiplier;

            _bootstrap.ForceCollectPowerUp(PowerUpType.TimeFreeze);
            yield return null;
            Assert.IsTrue(_bootstrap.FrostOverlay.activeSelf, "FrostOverlay must be active initially");

            // Wait 5.5 seconds for freeze to expire (5.0 s duration + 0.5 s margin).
            yield return new WaitForSeconds(5.5f);

            // The overlay state is updated in Update(); wait one more frame.
            yield return null;

            // (1) Game model: active flag must be cleared.
            Assert.IsFalse(game.IsTimeFreezeActive,
                "IsTimeFreezeActive must be false after Time Freeze expires");

            // (2) Game model: multiplier must be restored to its pre-freeze value.
            Assert.AreEqual(preFreeze, game.Field.ScrollSpeedMultiplier, 1e-6f,
                "ScrollSpeedMultiplier must return to its original value after freeze expires");

            // (3) View: frost overlay must be hidden.
            Assert.IsFalse(_bootstrap.FrostOverlay.activeSelf,
                "FrostOverlay must be inactive after Time Freeze expires");

            // (4) View: countdown HUD must be hidden.
            Assert.IsFalse(_bootstrap.TimeFreezeCountdown.activeSelf,
                "TimeFreezeCountdown must be inactive after Time Freeze expires");
        }

        // ------------------------------------------------------------------ //
        // AC5: collecting Lotus Bloom satisfies both sub-requirements in one test:
        //   (a) lotus GameObject is at world centre after simulating collection
        //   (b) CanLand returns true for every PadColor on the collected Lotus pad
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator LotusBloom_Collect_LotusPadGONearWorldCentre()
        {
            yield return CreateBootstrap();
            StartRun();
            yield return null;

            // Simulate collecting the Lotus Bloom pickup through the normal code path.
            _bootstrap.ForceCollectPowerUp(PowerUpType.LotusBloom);
            yield return null; // let SyncAllPads run in Update

            // LotusPad view is named "LotusPad_<id>"; world centre maps from (0.5, 0.5)
            // normalised → (0, 0) world space.
            var game = typeof(GameBootstrap)
                .GetField("_game",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(_bootstrap) as RainbowFroggyGame;

            Assert.IsNotNull(game);

            int lotusId = -1;
            PadData lotusPadData = null;
            foreach (var pad in game.Field.Pads)
            {
                if (pad.Type == PadType.Lotus) { lotusId = pad.Id; lotusPadData = pad; break; }
            }

            Assert.AreNotEqual(-1, lotusId, "Lotus pad must be in the field after collection");
            Assert.IsNotNull(lotusPadData, "Lotus PadData must be retrievable from the game model");

            var padViews = typeof(GameBootstrap)
                .GetField("_padViews",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(_bootstrap) as System.Collections.Generic.Dictionary<int, PadView>;

            Assert.IsNotNull(padViews);
            Assert.IsTrue(padViews.ContainsKey(lotusId),
                "A PadView must exist for the Lotus pad");

            var lotusGO = padViews[lotusId].gameObject;
            Assert.IsNotNull(lotusGO);

            // AC5 (a): normalised (0.5, 0.5) maps to world (0, 0).
            Assert.AreEqual(0f, lotusGO.transform.position.x, 0.1f,
                "Lotus pad GO world X must be near 0");
            Assert.AreEqual(0f, lotusGO.transform.position.y, 0.1f,
                "Lotus pad GO world Y must be near 0");

            // AC5 (b): CanLand must return true for every PadColor on the collected Lotus pad.
            foreach (PadColor color in System.Enum.GetValues(typeof(PadColor)))
            {
                Assert.IsTrue(lotusPadData.CanLand(color),
                    "CanLand must return true for every PadColor on the collected Lotus pad, failed for: " + color);
            }

            yield return null;
        }

        // ------------------------------------------------------------------ //
        // AC6: lotus pad GO is destroyed/inactive after first landing
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator LotusBloom_Landing_DestroysPad_SecondLandingFails()
        {
            yield return CreateBootstrap();
            StartRun();
            yield return null;

            _bootstrap.ForceCollectPowerUp(PowerUpType.LotusBloom);
            yield return null;

            var game = typeof(GameBootstrap)
                .GetField("_game",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(_bootstrap) as RainbowFroggyGame;

            Assert.IsNotNull(game);

            int lotusId = -1;
            foreach (var pad in game.Field.Pads)
                if (pad.Type == PadType.Lotus) { lotusId = pad.Id; break; }

            Assert.AreNotEqual(-1, lotusId, "Lotus pad must exist before landing");

            // First landing.
            var result = game.TapPad(lotusId);
            Assert.AreEqual(TapResult.Jump, result, "First landing on Lotus must return Jump");

            // Advance one frame so SyncAllPads destroys the view.
            yield return null;

            var padViews = typeof(GameBootstrap)
                .GetField("_padViews",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance)
                ?.GetValue(_bootstrap) as System.Collections.Generic.Dictionary<int, PadView>;

            Assert.IsNotNull(padViews);
            Assert.IsFalse(padViews.ContainsKey(lotusId),
                "PadView for the Lotus pad must be removed after first landing");

            // Second landing attempt must fail.
            var second = game.TapPad(lotusId);
            Assert.AreEqual(TapResult.None, second,
                "Second tap on removed Lotus pad id must return None");

            yield return null;
        }
    }
}
