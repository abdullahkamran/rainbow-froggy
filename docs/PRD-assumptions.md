# PRD Assumptions

The canonical PRD is at https://docs.google.com/document/d/1gnKnVpa6mcyNFrFHOkeF1oukVZpU7G5Z79F_ZwJTg10/edit
and is not accessible offline.  This file records the Phase 1 design decisions
made from the inline spec in the issue, so acceptance can be verified locally.

## Colour palette (Phase 1)

| Name         | Hex     | `PadColor` value |
|--------------|---------|-----------------|
| Ruby Red     | #FF4552 | `PadColor.Ruby` |
| Electric Cyan | #00E5FF | `PadColor.Cyan` |

## Gameplay mechanics

**Guaranteed-path algorithm (PRD §2.1 Phase 1 interpretation)**

At every tick `PadField` ensures `CountMatching(frogColor) >= 1` by:
1. Spawning a matching pad immediately if none exist at the start of a tick.
2. Before evicting a pad that would leave zero matching survivors, spawning a
   replacement first.
3. Forcing the next scheduled spawn to be frogColor when no matching pad exists.

**Frog rides a pad** — `FrogPadId` tracks which pad the frog is currently
riding.  After a successful `TapPad`, the frog rides the newly tapped pad.

**Waterfall game-over** — triggered when the frog's *own* riding pad (`FrogPadId`)
scrolls past Y = 1.0 (bottom boundary) before the next tap.  Non-occupied pads
that scroll off are silently removed; they do NOT trigger waterfall.

**Misstep game-over** — triggered immediately when the player taps a pad whose
colour does not match `FrogColor`.

**Frog colour after landing** — The behaviour depends on whether the tap is a
*self-tap* (frog taps the pad it is already riding) or a *cross-tap* (a
different matching pad):

- **Self-tap** (`target.Id == FrogPadId`): `FrogColor` is unchanged and equals
  the landed pad's colour.  This satisfies the PlayMode invariant
  `FrogColor_EqualsLandedPadColor_AfterJump`.
- **Cross-tap** (`target.Id != FrogPadId`): `FrogColor` shifts to a new random
  colour from the current phase palette (different from the old colour), and
  `PadField.Tick(0 dt, newColor)` immediately enforces the guaranteed-path
  invariant.  This satisfies the EditMode invariant
  `TapPad_ColorShifts_AndMatchingPadExistsImmediately`.

## UI / UX overhaul (issue #11)

**Idle state** — `GameScreen.Idle` appended to the existing enum
(Playing=0, MisstepGameOver=1, WaterfallGameOver=2, Idle=3).  The
constructor default stays `Playing` so existing tests are unaffected;
`GameBootstrap` calls `EnterIdle()` after construction.

**Flies currency** — `FliesThisRun` increments by 1 per successful
cross-tap jump and resets to 0 on `ResetRun()`.  Shown in the gameplay HUD
(top-left, golden colour) and on the game-over screen.

**Rotten pads (Phase 3+)** — A dedicated `_rottenSpawnTimer` (interval
1.5 s) guarantees a rotten pad of the frog's current colour appears within
1.5 s of Phase 3 being entered.  This makes `TapRottenPad_TriggersMisstep`
deterministic: the rotten pad is visible well before the waterfall timeout
(~3 s at Phase 3 scroll speed 0.30 f).

**Restart** — `GameOverScreen` invokes a callback supplied by
`GameBootstrap`; no `SceneManager.LoadScene` call exists anywhere in the
project.  Pad views are destroyed and re-created from the freshly seeded
`PadField`; no additional scene is loaded (AC8).

**Font** — `FontLibrary.Body` resolves a system font via
`Font.CreateDynamicFontFromOSFont` (preferred face: Helvetica Neue /
Helvetica / Arial).  No
`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` call remains in
the project (AC4).

**PRD hex values / shader / font name** — The PRD (§10, §10.2, §10.3) is
not committed to this repository and has no accessible URL.  Pad-colour
conformance (`ColorPalette.cs` hex constants), glassmorphic shader
specification, and the replacement font asset name cannot be verified
against the external document.  The five named Color constants in
`ColorPalette.cs` use the hex values already present in the inline spec
(`#FF4552`, `#FFD035`, `#00E5FF`, `#B429F9`, `#FF3399`).

## Power-ups (issue #27)

### Time Freeze

| Property              | Value  |
|-----------------------|--------|
| Duration              | 5.0 s  |
| Scroll-speed multiplier | 0.20 (80% reduction) |
| Visual indicator      | Full-screen semi-transparent icy-blue overlay (`FrostOverlay`) |
| HUD indicator         | `TimeFreezeCountdown` label (bottom-centre of HUD) |

### Lotus Bloom

| Property              | Value |
|-----------------------|-------|
| Spawn position        | Normalised (0.5, 0.5) — maps to world (0, 0) |
| Landing rule          | Wildcard: any `PadColor` can land (`PadData.CanLand`) |
| Removal               | One-shot: pad removed immediately on first landing |

### Spawn weights

| Power-up     | Weight | Notes |
|--------------|--------|-------|
| Time Freeze  | 10     | Same as standard pad weight |
| Lotus Bloom  | 1      | **Rare** — strictly less than both Time Freeze and standard pad weights |
| Standard pad | 10     | Reference value only; defined in `PowerUpWeights.StandardPadWeight` |

Weighted selection probability per spawn cycle:
- Time Freeze: 10/11 ≈ 90.9 %
- Lotus Bloom: 1/11 ≈ 9.1 %

**Assumption recorded here:** The issue specifies only that Lotus Bloom is "rare" with no numeric
definition.  The 1-vs-10 ratio is an implementation choice.  AC4 requires only that
`LotusBloomWeight < TimeFreezeWeight`, which is locally verifiable from the source constants.
If a different ratio is required, update `PowerUpWeights.LotusBloomWeight` in
`PowerUpType.cs` and re-run the tests.

## Phase 1 constants

| Constant       | Value   |
|----------------|---------|
| ScrollSpeed    | 0.12 (normalised units/sec) |
| SpawnInterval  | 1.8 s   |
| InitialPadCount | 5      |
| Active colours | 2 (Ruby, Cyan) |

## Audio (issue #10)

### BGM tempo table (AC2)

| Phase | `AudioSource.pitch` |
|-------|---------------------|
| 1     | 1.00                |
| 2     | 1.04                |
| 3     | 1.08                |
| 4     | 1.12                |

Implemented in `AudioService.PitchForPhase(int phase)`.

### Jump SFX pitch formula (AC3)

`pitch = 1.0 + (multiplierTier − 1) × 0.15`

Combo ladder tiers are 1, 2, 3, 5, giving pitches 1.00, 1.15, 1.30, 1.60.
Implemented in `AudioService.PitchForTier(int tier)`.

### Clip name list for asset replacement

Drop a `.wav`/`.ogg` file at `Assets/Resources/Audio/<name>` to replace a
placeholder.  `AudioLibrary.Load(name)` checks `Resources.Load<AudioClip>`
first; the synthesized fallback is only used when no file exists.

| Constant               | Path                             | Sound design description         |
|------------------------|----------------------------------|----------------------------------|
| `BgmClip = "bgm"`      | `Resources/Audio/bgm`            | Lo-fi tropical/marimba loop      |
| `JumpClip = "jump"`    | `Resources/Audio/jump`           | Snappy bloop                     |
| `LandingClip = "landing"` | `Resources/Audio/landing`     | Glassy "ting"                    |
| `WaterfallClip = "waterfall"` | `Resources/Audio/waterfall` | Fading whoosh + distant splash |
| `MisstepClip = "misstep"` | `Resources/Audio/misstep`     | Underwater gurgle + muted splat  |
| `RainbowClip = "rainbow"` | `Resources/Audio/rainbow`     | Ethereal chimes                  |
| `PrismClip = "prism"`  | `Resources/Audio/prism`          | Synthesized chords               |
| `FreezeClip = "timefreeze"` | `Resources/Audio/timefreeze` | Bass drop + ticking clock        |

### Unverifiable offline

PRD §9's timbre, instrumentation, and mixing specifications are in an
external Google Doc that is not committed to this repository.  The
placeholder synthesis approximates each event; all asset slots are
documented above for replacement when the PRD becomes accessible.

### LotusBloom SFX

PRD §9 does not list a dedicated SFX for Lotus Bloom in the power-up
table quoted in the issue.  No clip is played on Lotus Bloom collection;
add one to `AudioService.PlayPowerUp` and the table above if PRD §9
specifies a sound.

## Animation constants (FrogView)

These values are committed so that ACs 1, 3, 4, and 5 are checkable against
a local reference without the inaccessible PRD.

| Constant     | Value    | Notes |
|--------------|----------|-------|
| JumpDuration | 0.175 s  | AC1: within the 0.15–0.2 s band |
| ArcHeight    | 0.8 wu   | Parabolic peak: `4 × 0.8 × t × (1−t)` world units above the lerp line |
| SinkDuration | 0.4 s    | AC3: scale-down + alpha-fade on misstep |
| RideDuration | 0.3 s    | AC4: frog rides pad off screen before game-over |
| BobFreq      | 3.0 rad/s | AC6: idle sine-wave oscillation frequency |
| BobAmp       | 0.06 wu  | AC6: idle sine-wave oscillation amplitude |

**Per-skin animation variants** — the issue overview notes that each frog
skin has "unique animations," but no per-skin animation parameters are
defined in any locally accessible document.  Until the PRD (relevant
section) or a design spec is exported and committed to the repository,
per-skin differentiation is **out of scope for this implementation**.
