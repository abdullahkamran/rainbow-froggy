using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RainbowFroggy.View;
using Object = UnityEngine.Object;

namespace RainbowFroggy.Tests.PlayMode
{
    // PlayMode tests for FrogView visual-sizing requirements.
    // Run in PlayMode so the full Unity runtime (shaders, Texture2D) is available.
    [TestFixture]
    public class FrogViewTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.Destroy(_go);
            _go = null;
        }

        // Criterion 7 (visual sizing): FrogView must initialise its localScale to
        // (1.0, 1.0, 1) — the value specified by the visual-sizing requirement
        // (raised from the previous 0.85 to give the frog a more prominent footprint).
        // Pinning this here means any future regression to the old value is caught
        // immediately by the Test Runner.
        [UnityTest]
        public IEnumerator FrogView_Awake_SetsLocalScaleTo1_0()
        {
            _go = new GameObject("TestFrog");
            _go.AddComponent<FrogView>();
            yield return null; // let Awake complete and the frame settle

            Assert.AreEqual(
                new Vector3(1.0f, 1.0f, 1f),
                _go.transform.localScale,
                "FrogView must initialise localScale to (1.0, 1.0, 1) per the visual-sizing spec");
        }

        // Verify that Reset() restores localScale to the same initialised value after
        // a sink animation would have zeroed it out.
        [UnityTest]
        public IEnumerator FrogView_Reset_RestoresLocalScaleTo1_0()
        {
            _go = new GameObject("TestFrog");
            var frogView = _go.AddComponent<FrogView>();
            yield return null; // let Awake complete

            // Simulate what a completed sink animation does: zero the scale.
            _go.transform.localScale = Vector3.zero;

            frogView.Reset();

            Assert.AreEqual(
                new Vector3(1.0f, 1.0f, 1f),
                _go.transform.localScale,
                "FrogView.Reset() must restore localScale to (1.0, 1.0, 1)");
        }
    }
}
