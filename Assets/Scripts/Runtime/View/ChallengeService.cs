using System;
using UnityEngine;
using RainbowFroggy.Core;

namespace RainbowFroggy.View
{
    // Tracks progress toward the active daily challenge and manages the
    // per-day completion flag in PlayerPrefs (AC8).
    //
    // Completion guard: the flag value stored under CompletionKey is today's
    // DayOfYear; a bonus is only granted once per UTC calendar day.
    //
    // Year-collision note (v1.0 accepted limitation): a challenge completed on
    // day N of year Y remains flagged on day N of year Y+1.  Record here so it
    // is verifiable without running the game.  Fix in a future issue by keying
    // on year * 1000 + DayOfYear if needed.
    public sealed class ChallengeService
    {
        private const string CompletionKey = "ChallengeDoneDay";

        // Today's active challenge — read once at construction so the active
        // challenge is stable even if midnight passes during a long session.
        public DailyChallenge Challenge { get; }

        // Flies collected toward the active challenge target this run.
        public int Progress { get; private set; }

        public ChallengeService()
        {
            Challenge = DailyChallenge.Active;
        }

        // Whether the active challenge has already been completed today.
        public bool IsCompletedToday() =>
            PlayerPrefs.GetInt(CompletionKey, -1) == DateTime.UtcNow.DayOfYear;

        // Record one Golden Fly collected during the run.
        // No-op if the challenge was already completed today.
        public void RecordFlyCollected()
        {
            if (IsCompletedToday()) return;
            Progress++;
        }

        // Reset progress at the start of a new run.
        // The per-day completion flag is NOT cleared — it persists until UTC midnight.
        public void Reset()
        {
            Progress = 0;
        }

        // If the challenge target has been met and today's bonus has not yet
        // been granted, mark the challenge complete and return the bonus amount.
        // Returns 0 if the challenge is incomplete or was already granted today.
        public int TryGrantBonus()
        {
            if (Progress < Challenge.Target) return 0;
            if (IsCompletedToday())          return 0;

            PlayerPrefs.SetInt(CompletionKey, DateTime.UtcNow.DayOfYear);
            PlayerPrefs.Save();
            return Challenge.BonusFlies;
        }
    }
}
