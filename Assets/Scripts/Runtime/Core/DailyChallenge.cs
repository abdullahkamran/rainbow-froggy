using System;

namespace RainbowFroggy.Core
{
    // A single daily challenge: collect a target number of Golden Flies in one
    // run to earn a lifetime-balance bonus.
    //
    // v1.0: three hardcoded entries cycling by UTC day-of-year.  No remote config.
    // One challenge is active per calendar day; the selection key is
    // DateTime.UtcNow.DayOfYear modulo the set size (AC7).
    public sealed class DailyChallenge
    {
        public string Name       { get; }
        public int    Target     { get; }    // flies to collect in a single run
        public int    BonusFlies { get; }    // bonus added to the lifetime balance on completion

        private DailyChallenge(string name, int target, int bonusFlies)
        {
            Name       = name;
            Target     = target;
            BonusFlies = bonusFlies;
        }

        // v1.0 challenge set — extend here for variety in a future issue.
        private static readonly DailyChallenge[] _all =
        {
            new DailyChallenge("Fly Catcher",   5,  10),
            new DailyChallenge("Golden Streak", 10, 25),
            new DailyChallenge("Fly Hoarder",   20, 50),
        };

        // Today's active challenge, keyed by UTC day-of-year.
        // The index wraps cyclically through the hardcoded set.
        public static DailyChallenge Active =>
            _all[DateTime.UtcNow.DayOfYear % _all.Length];
    }
}
