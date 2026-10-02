using System.Collections.Generic;

namespace RainbowFroggy.Core
{
    // Static oracle that decides which pads count as safe landing targets for
    // the frog.  See docs/pad-path-oracle.md for the decision table and rationale.
    //
    // Update this file and IsValidForFrog whenever a new PadType is added.
    public static class PadPathOracle
    {
        // Returns true if the given pad is a valid jump destination for the frog.
        //
        // Decision table:
        //   Normal, color == frogColor              → true
        //   PadType.Rainbow                         → true
        //   PadType.Flaky, CountdownActive == false → true
        //   PadType.Flaky, pad.Id == frogPadId      → false  (frog is already on it)
        //   PadType.Lotus                           → false  (one-shot wildcard excluded)
        //   PadType.Rotten                          → false
        //   anything else                           → false
        public static bool IsValidForFrog(PadData pad, PadColor frogColor, int frogPadId)
        {
            switch (pad.Type)
            {
                case PadType.Normal:
                    return pad.Color == frogColor;
                case PadType.Rainbow:
                    return true;
                case PadType.Flaky:
                    if (pad.Id == frogPadId) return false;   // frog already on it
                    return !pad.FlakeyCountdownActive;
                default:
                    // Covers Lotus, Rotten, and any future types not yet handled.
                    return false;
            }
        }

        // Returns true if at least one pad in the list satisfies IsValidForFrog.
        public static bool HasGuaranteedPath(IReadOnlyList<PadData> pads, PadColor frogColor, int frogPadId)
        {
            foreach (var pad in pads)
                if (IsValidForFrog(pad, frogColor, frogPadId)) return true;
            return false;
        }
    }
}
