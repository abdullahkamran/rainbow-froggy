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

**Frog colour after landing** — `FrogColor` is set to `pad.Color` after a
successful `TapPad`.  Since only matching pads can be jumped to, this is
idempotent in Phase 1 (frog stays the same colour).  The property is explicitly
set so future phases that change the landing mechanic will be tested correctly.

## Phase 1 constants

| Constant       | Value   |
|----------------|---------|
| ScrollSpeed    | 0.12 (normalised units/sec) |
| SpawnInterval  | 1.8 s   |
| InitialPadCount | 5      |
| Active colours | 2 (Ruby, Cyan) |

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
