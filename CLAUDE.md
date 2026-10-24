# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Rainbow Froggy is a Unity 6 (`6000.6.0f1`) hyper-casual mobile game: a frog jumps between drifting lily pads by tapping pads that match its current color. Game design lives in [docs/PRD.md](docs/PRD.md). Design decisions made where the PRD was silent are recorded in [docs/PRD-assumptions.md](docs/PRD-assumptions.md); read it before changing gameplay rules, and add to it when you make a new rule decision.

Whenever a game-design decision is made or changed (rules, balance, sizing, difficulty, visuals), update [docs/PRD.md](docs/PRD.md) or the relevant doc in `docs/` in the same change so the docs never drift from the game.

## Commands

All commands use the `unity` CLI from the repo root. The `com.unity.pipeline` package lets the CLI drive a running Editor.

```sh
unity test --mode EditMode --output TestResults-EditMode.xml
unity test --mode PlayMode --output TestResults-PlayMode.xml
unity test --mode EditMode --filter ScoringTests     # single fixture or test by name
unity build --target WebGL --execute-method Build.PerformWebGLBuild -o Builds/WebGL
```

- `./scripts/play-webgl.ps1` builds WebGL and serves it on the LAN at port 8080. Pass `-NoBuild` to serve the last build. It uses [scripts/serve.js](scripts/serve.js).
- `node docs/pixel-check.js Builds/WebGL` is a headless smoke test that checks the canvas renders more than one color. It needs `puppeteer-core`, and its Chrome path is hard-coded for macOS.
- The only scene is `Assets/Scenes/Game.unity`. [Assets/Editor/Build.cs](Assets/Editor/Build.cs) builds only that scene and turns WebGL compression off so a plain static server can host the build.

## Architecture

There are two layers, each with its own namespace, in a single assembly (`RainbowFroggy.Runtime`):

- **`RainbowFroggy.Core`** ([Assets/Scripts/Runtime/Core/](Assets/Scripts/Runtime/Core/)) is the pure C# game model with no `UnityEngine` dependency. `RainbowFroggyGame` is the top-level model: it holds screen state, score and combo, frog color, difficulty phase, and power-ups. It owns the `PadField`, `PowerUpField` and `GoldenFlyField`, which spawn, scroll and evict entities in normalized coordinates (Y=1 is the bottom edge). Randomness goes through `IRng`; use `SeededRng` in tests so they are deterministic.
- **`RainbowFroggy.View`** ([Assets/Scripts/Runtime/View/](Assets/Scripts/Runtime/View/)) holds the MonoBehaviours. `GameBootstrap` is the single scene entry point. In `Start()` it builds everything procedurally: the camera, background, banks, frog, HUD canvas, menus, and audio. Each frame it ticks the model and syncs view objects using id-keyed dictionaries (padId → `PadView`, and likewise for power-ups and flies). There are almost no prefabs. Sprites come from `SpriteFactory` (procedural) or `Resources.Load` (for example `bank.png`, `WaterLeft.png`, and `Resources/Skins`).

Key invariants:

- **Guaranteed path:** every decision about whether a pad is a valid jump target goes through `PadPathOracle` (see [docs/pad-path-oracle.md](docs/pad-path-oracle.md)). After any removal or spawn, `PadField` must make sure a valid destination exists for the frog's color, and it force-spawns one if none does. Never add a pad type or eviction path that bypasses the oracle.
- Difficulty phases are driven by `JumpCount`. They control the color palette size, scroll speed, pad size, Rotten pads and drifting. The phase table is in the header comment of `RainbowFroggyGame`.
- `GameBootstrap` must not use `DontDestroyOnLoad` or `RuntimeInitializeOnLoadMethod`, because those duplicate the object on restart and leak into test scenes. It exposes public "test seams" (`Screen`, `Score`, `CreatedRoots`, `ForceCollectPowerUp`, and others) that PlayMode tests use for assertions and teardown.

Ads: `AdService`/`IAdService` wrap Google Mobile Ads (`com.google.ads.mobile` via OpenUPM). The SDK code is compiled under the `GOOGLE_MOBILE_ADS` version define in the runtime asmdef.

## Tests

- **EditMode** ([Assets/Tests/EditMode/](Assets/Tests/EditMode/)) tests the Core model directly: scoring, difficulty, spawn weights, pad-field invariants, and layout calibration.
- **PlayMode** ([Assets/Tests/PlayMode/](Assets/Tests/PlayMode/)) covers gameplay acceptance criteria, mostly by driving the pure model, plus view and UI-flow tests that instantiate `GameBootstrap`.
- Test names and comments often cite acceptance criteria (for example "AC5"). Keep those references when you edit tests.

## Workflow

Commit directly to `main`. Don't use branches or PRs.

After finishing each feature, the WebGL build must be re-run (`./scripts/play-webgl.ps1`) so the change can be tried. Ask the user in one prompt whether to build and run WebGL and whether to commit and push to `main`.
