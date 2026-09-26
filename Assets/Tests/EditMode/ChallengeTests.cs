using NUnit.Framework;
using UnityEngine;
using RainbowFroggy.Core;
using RainbowFroggy.View;

namespace RainbowFroggy.Tests.EditMode
{
    // ChallengeService stores its per-day completion flag in PlayerPrefs
    // (key "ChallengeDoneDay").  Each test stashes the current value in
    // [SetUp] and restores it in [TearDown] so the developer's editor state
    // is never corrupted, and tests are order-independent.
    [TestFixture]
    public class ChallengeTests
    {
        private const string CompletionKey = "ChallengeDoneDay";
        private bool _hadStoredValue;
        private int  _storedValue;

        [SetUp]
        public void SetUp()
        {
            _hadStoredValue = PlayerPrefs.HasKey(CompletionKey);
            _storedValue    = PlayerPrefs.GetInt(CompletionKey, -1);
            PlayerPrefs.DeleteKey(CompletionKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadStoredValue)
                PlayerPrefs.SetInt(CompletionKey, _storedValue);
            else
                PlayerPrefs.DeleteKey(CompletionKey);
        }

        // Two calls to DailyChallenge.Active on the same UTC day return equal targets.
        [Test]
        public void Active_IsDeterministicForSameDay()
        {
            var first  = DailyChallenge.Active;
            var second = DailyChallenge.Active;

            Assert.AreEqual(first.Target, second.Target,
                "Two calls to Active on the same UTC day must return equal Target values");
        }

        // Each RecordFlyCollected() call increments Progress by 1.
        [Test]
        public void RecordFlyCollected_IncrementsProgress()
        {
            var svc = new ChallengeService();

            svc.RecordFlyCollected();
            Assert.AreEqual(1, svc.Progress);

            svc.RecordFlyCollected();
            Assert.AreEqual(2, svc.Progress);

            svc.RecordFlyCollected();
            Assert.AreEqual(3, svc.Progress);
        }

        // After recording exactly Target flies, TryGrantBonus() returns BonusFlies (> 0).
        [Test]
        public void TryGrantBonus_ReturnsBonusWhenTargetMet()
        {
            var svc = new ChallengeService();

            for (int i = 0; i < svc.Challenge.Target; i++)
                svc.RecordFlyCollected();

            int bonus = svc.TryGrantBonus();

            Assert.AreEqual(svc.Challenge.BonusFlies, bonus,
                "TryGrantBonus must return Challenge.BonusFlies when the target is met");
            Assert.Greater(bonus, 0, "BonusFlies must be a positive integer");
        }

        // A second TryGrantBonus() call within the same UTC day returns 0.
        [Test]
        public void TryGrantBonus_IsIdempotent()
        {
            var svc = new ChallengeService();

            for (int i = 0; i < svc.Challenge.Target; i++)
                svc.RecordFlyCollected();

            svc.TryGrantBonus();
            int second = svc.TryGrantBonus();

            Assert.AreEqual(0, second,
                "A second TryGrantBonus() call in the same UTC day must return 0");
        }

        // Reset() clears Progress but preserves the per-day completion flag.
        [Test]
        public void Reset_ClearsProgress()
        {
            var svc = new ChallengeService();

            for (int i = 0; i < svc.Challenge.Target; i++)
                svc.RecordFlyCollected();
            svc.TryGrantBonus();   // marks the day complete in PlayerPrefs

            svc.Reset();

            Assert.AreEqual(0, svc.Progress,
                "Progress must be 0 after Reset()");

            // The completion flag must be preserved — assert it directly, because
            // TryGrantBonus() would return 0 here even if the flag were cleared
            // (Progress < Target guards first).
            Assert.IsTrue(svc.IsCompletedToday(),
                "Per-day completion flag must still be set after Reset()");

            Assert.AreEqual(0, svc.TryGrantBonus(),
                "TryGrantBonus() must return 0 after Reset() because the day is already complete");
        }
    }
}
