using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace RainbowFroggy.View
{
    // Persists the top-10 run history (score + date) across sessions via PlayerPrefs.
    //
    // Serialisation format: one delimited string stored under Key.
    // Each entry is "score|ticks"; entries are separated by ";".
    // Example: "420|638700000000000000;380|638699000000000000"
    //
    // Load() skips any token it cannot fully parse — never throws on bad data.
    // Record() inserts, re-sorts score-desc / date-desc, trims to MaxEntries,
    // and calls PlayerPrefs.Save() before returning.
    public static class LeaderboardStore
    {
        // Canonical PlayerPrefs key for the leaderboard list.
        public const string Key        = "Leaderboard";
        public const int    MaxEntries = 10;

        public readonly struct Entry
        {
            public readonly int      Score;
            public readonly DateTime DateUtc;

            public Entry(int score, DateTime dateUtc)
            {
                Score   = score;
                DateUtc = dateUtc;
            }
        }

        // Return up to MaxEntries, sorted score-desc / date-desc.
        // Returns an empty list when the key is absent or the value is blank.
        public static List<Entry> Load()
        {
            var    entries = new List<Entry>();
            string raw     = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(raw)) return entries;

            foreach (var token in raw.Split(';'))
            {
                if (string.IsNullOrEmpty(token)) continue;
                var parts = token.Split('|');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], out int score)) continue;
                if (!long.TryParse(parts[1], out long ticks)) continue;
                if (ticks < 0 || ticks > DateTime.MaxValue.Ticks) continue;
                entries.Add(new Entry(score, new DateTime(ticks, DateTimeKind.Utc)));
            }
            return entries;
        }

        // Record a run score using the current UTC time.
        public static void Record(int score) => Record(score, DateTime.UtcNow);

        // Record a run score with an explicit UTC timestamp.
        // Inserts, re-sorts, trims to MaxEntries, and persists immediately.
        public static void Record(int score, DateTime utc)
        {
            var entries = Load();
            entries.Add(new Entry(score, utc));

            // Sort score-desc; tiebreak by date-desc (most recent first).
            entries.Sort((a, b) => a.Score != b.Score
                ? b.Score.CompareTo(a.Score)
                : b.DateUtc.Ticks.CompareTo(a.DateUtc.Ticks));

            if (entries.Count > MaxEntries)
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);

            var sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(entries[i].Score);
                sb.Append('|');
                sb.Append(entries[i].DateUtc.Ticks);
            }
            PlayerPrefs.SetString(Key, sb.ToString());
            PlayerPrefs.Save();
        }
    }
}
