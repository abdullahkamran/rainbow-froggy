using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using RainbowFroggy.Core;
using RainbowFroggy.View;
using Object = UnityEngine.Object;

namespace RainbowFroggy.Tests.PlayMode
{
    // PlayMode tests for the rotten-pad visual treatment (colour tint).
    // No GameBootstrap is needed: PadView.Bind() works on a bare GameObject.
    [TestFixture]
    public class PadVisualTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _created)
                if (go != null) Object.Destroy(go);
            _created.Clear();
        }

        // Creates a fresh PadView GameObject and calls Bind() on it.
        private PadView MakePad(PadColor color, PadType type)
        {
            var go = new GameObject("TestPad_" + color + "_" + type);
            _created.Add(go);
            go.AddComponent<SpriteRenderer>();
            var view = go.AddComponent<PadView>();
            view.Bind(new PadData(0, color, 0.5f, 0.5f, type));
            return view;
        }

        private static float ColorSum(Color c) => c.r + c.g + c.b;

        // ------------------------------------------------------------------ //
        // AC3: a rotten pad's SpriteRenderer.color must be strictly darker
        //      (lower r + g + b sum) than a normal pad of the same PadColor.
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator Rotten_SpriteColor_IsDarkerThanNormal_ForSamePadColor()
        {
            PadColor[] colors = { PadColor.Ruby, PadColor.Cyan, PadColor.Mango,
                                  PadColor.Purple, PadColor.Pink };

            foreach (var color in colors)
            {
                var normal = MakePad(color, PadType.Normal);
                var rotten = MakePad(color, PadType.Rotten);

                Color normalColor = normal.GetComponent<SpriteRenderer>().color;
                Color rottenColor = rotten.GetComponent<SpriteRenderer>().color;

                Assert.That(ColorSum(rottenColor), Is.LessThan(ColorSum(normalColor)),
                    "Rotten pad must be darker than normal for PadColor." + color);
            }

            yield return null;
        }

        // ------------------------------------------------------------------ //
        // AC: Bind() must not mutate PadData.Color or PadData.Type.
        //     (PadData fields are readonly, so this is a compile-time guarantee;
        //     the assertion below documents the contract for readers.)
        // ------------------------------------------------------------------ //

        [UnityTest]
        public IEnumerator Rotten_Bind_DoesNotMutateModelColor()
        {
            var data = new PadData(1, PadColor.Ruby, 0.5f, 0.5f, PadType.Rotten);
            var go   = new GameObject("TestRottenModel");
            _created.Add(go);
            go.AddComponent<SpriteRenderer>();
            var view = go.AddComponent<PadView>();
            view.Bind(data);

            // Model fields are readonly — confirmed unchanged after Bind().
            Assert.AreEqual(PadColor.Ruby,  data.Color,
                "PadData.Color must not be mutated by Bind()");
            Assert.AreEqual(PadType.Rotten, data.Type,
                "PadData.Type must not be mutated by Bind()");

            // A normal pad's SR colour must exactly equal the raw palette entry.
            var normalGo = new GameObject("TestNormalModel");
            _created.Add(normalGo);
            normalGo.AddComponent<SpriteRenderer>();
            var normalView = normalGo.AddComponent<PadView>();
            normalView.Bind(new PadData(2, PadColor.Ruby, 0.5f, 0.5f, PadType.Normal));
            Assert.AreEqual(ColorPalette.For(PadColor.Ruby),
                            normalView.GetComponent<SpriteRenderer>().color,
                "Normal pad SR.color must equal ColorPalette.For(PadColor.Ruby)");

            yield return null;
        }
    }
}
