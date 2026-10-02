using NUnit.Framework;
using UnityEngine;
using RainbowFroggy.Core;
using RainbowFroggy.View;
using Object = UnityEngine.Object;

namespace RainbowFroggy.Tests.EditMode
{
    // AC2: PadView.SyncPosition maps normalised X lane centres to the expected world X.
    // AC3: PadView.SyncPosition maps normalised Y 0/1 to the scroll-extent world Y values.
    [TestFixture]
    public class LayoutCalibrationTests
    {
        private GameObject _go;
        private PadView    _padView;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestPad");
            _go.AddComponent<SpriteRenderer>();
            _padView = _go.AddComponent<PadView>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        // AC2 — lane centres at norm 0.2, 0.5, 0.8 should map to world X −3.0, 0.0, +3.0.
        [Test]
        public void PadView_NormalisedX_MapsToLaneCentres()
        {
            _padView.SyncPosition(new PadData(0, PadColor.Ruby, 0.2f, 0.5f));
            Assert.AreEqual(-3.0f, _padView.transform.localPosition.x, 0.05f,
                "X_norm=0.2 should produce world X ≈ -3.0");

            _padView.SyncPosition(new PadData(0, PadColor.Ruby, 0.5f, 0.5f));
            Assert.AreEqual(0.0f, _padView.transform.localPosition.x, 0.05f,
                "X_norm=0.5 should produce world X ≈ 0.0");

            _padView.SyncPosition(new PadData(0, PadColor.Ruby, 0.8f, 0.5f));
            Assert.AreEqual(3.0f, _padView.transform.localPosition.x, 0.05f,
                "X_norm=0.8 should produce world X ≈ +3.0");
        }

        // AC3 — scroll extents: norm 0.0 → +8.5 (top), norm 1.0 → −8.5 (bottom).
        [Test]
        public void PadView_NormalisedY_MapsToScrollExtents()
        {
            _padView.SyncPosition(new PadData(0, PadColor.Ruby, 0.5f, 0.0f));
            Assert.AreEqual(8.5f, _padView.transform.localPosition.y, 0.1f,
                "Y_norm=0.0 should produce world Y ≈ +8.5");

            _padView.SyncPosition(new PadData(0, PadColor.Ruby, 0.5f, 1.0f));
            Assert.AreEqual(-8.5f, _padView.transform.localPosition.y, 0.1f,
                "Y_norm=1.0 should produce world Y ≈ -8.5");
        }
    }
}
