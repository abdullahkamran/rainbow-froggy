namespace RainbowFroggy.Core
{
    // Skin identifiers and score-unlock thresholds — pure C#, no Unity deps.
    public static class SkinCatalog
    {
        public const string DefaultId = "default";
        public const string MintId    = "mint";
        public const string SunsetId  = "sunset";
        public const string GalaxyId  = "galaxy";

        // Minimum high score required to unlock a skin (0 = always unlocked).
        public static int UnlockScore(string skinId)
        {
            switch (skinId)
            {
                case MintId:   return 50;
                case SunsetId: return 100;
                case GalaxyId: return 200;
                default:       return 0;
            }
        }

        public static bool IsUnlocked(string skinId, int highScore)
            => highScore >= UnlockScore(skinId);
    }
}
