# PadPathOracle

`Assets/Scripts/Runtime/Core/PadPathOracle.cs` is the single source of truth for deciding which lily pads count as safe jump destinations for the frog.  All guaranteed-path enforcement in `PadField` routes through `PadPathOracle.HasGuaranteedPath`.

---

## IsValidForFrog decision table

| Pad type | Condition | Result |
|---|---|---|
| `Normal` | `pad.Color == frogColor` | **true** |
| `Normal` | colour mismatch | false |
| `Rainbow` | *(any)* | **true** |
| `Flaky` | `FlakeyCountdownActive == false` | **true** |
| `Flaky` | `pad.Id == frogPadId` | false |
| `Lotus` | *(any)* | false |
| `Rotten` | *(any)* | false |
| anything else | *(any)* | false |

---

## Rationale for exclusions

**Lotus (`false`):** A Lotus pad is a one-shot wildcard that vanishes on the first landing.  If it were counted as the sole path guarantee the frog could be stranded the instant the player uses it.  Lotus pads are therefore excluded so the guarantee is always backed by a durable pad.

**Active Flaky (`false` when `FlakeyCountdownActive`):** Once the countdown is ticking the pad is already "occupied" by the frog's live session; re-counting it as a jump destination would let it satisfy the guarantee while the frog is racing against expiry on it.  Before the frog lands (`FlakeyCountdownActive == false`) the pad is perfectly safe and counts normally.

**Flaky when `pad.Id == frogPadId` (`false`):** The frog is standing on this pad right now; it cannot be its own next destination.

---

## HasGuaranteedPath

```
HasGuaranteedPath(IReadOnlyList<PadData> pads, PadColor frogColor, int frogPadId)
  → true  if any pad satisfies IsValidForFrog(pad, frogColor, frogPadId)
  → false otherwise
```

Called after every removal and after every burst spawn event in `PadField`.  If it returns `false` a Normal pad of the frog's colour is force-spawned at the top of the field.

---

> **Update this file and `IsValidForFrog` whenever a new `PadType` is added.**
