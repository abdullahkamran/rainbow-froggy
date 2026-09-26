# Product Requirements Document: Rainbow Froggy

**Platform:** Mobile (iOS & Android)
**Genre:** Hyper-casual / Endless Runner (Vertical)
**Controls:** Tap-to-Target
**Target Audience:** Casual gamers looking for quick, high-reflex challenge loops.

---

## 1. Core Gameplay Mechanics

The primary loop revolves around color-matching and fast reflexes. The player controls a frog traversing down a vertically flowing river by jumping across lily pads.

- **Color Shifting:** Upon landing on a lily pad, the frog instantly changes to a new random color.
- **Target Selection:** The player must tap a lily pad that matches the frog's new color to successfully leap to it. Tapping the wrong color results in a failure state.
- **Environment:** The river flows from top to bottom. Lily pads spawn continuously from the top and drift downwards.

---

## 2. Logic & Systems

### 2.1 Spawning Algorithm (The Guaranteed Path)

To ensure the game relies on skill rather than luck, the procedural generation utilizes a Guaranteed Path Algorithm.

- **Forward Calculation:** The system pre-determines the frog's next color upon landing and forces at least one corresponding pad to spawn at the top of the screen.
- **Psychological Difficulty:** To increase difficulty without breaking fairness, the game introduces:
  - **Late Arrivals** — the correct pad takes longer to scroll into view, forcing the player to wait perilously close to the bottom edge.
  - **Decoys** — clusters of incorrect colors to distract the player.

### 2.2 Failure States (Game Over Conditions)

1. **The Waterfall (Screen Out):** The player's current lily pad drifts off the bottom edge of the screen before a jump is executed. The frog is swept away.
2. **The Misstep (Wrong Color):** The player taps an incompatible color. The frog jumps to it but immediately sinks into the water upon landing.

---

## 3. Scoring System

Points are calculated based on survival, speed, and risk assessment.

| Source | Points |
|---|---|
| Base Jump | +1 per successful jump |
| Fever Multiplier | Consecutive jumps within 1.5s build a combo meter (x2, x3, x5) |
| Distance Bonus | Jumping to a newly spawned pad near the top | +3 |
| Golden Fly collectible | +10 points (and currency) |

---

## 4. Difficulty Progression & Phases

| Phase | Score Threshold | Active Colors | Water Speed | Special Obstacles / Modifiers |
|---|---|---|---|---|
| 1 (Easy) | 0 – 50 | 2 (Red, Blue) | Slow | Large, plentiful pads |
| 2 (Medium) | 51 – 150 | 3 | Moderate | Pad sizes reduced slightly |
| 3 (Hard) | 151 – 300 | 4 | Fast | "Rotten Pads" introduced (sink instantly upon touch) |
| 4 (Extreme) | 300+ | 5 | Very Fast | Drifting pads (horizontal movement + vertical descent) |

---

## 5. Items, Power-Ups & Wildcards

These items occasionally float down the river to aid the player.

- **The Rainbow Pad (Environmental):** A glowing white pad. Acts as a universal bridge; any frog color can jump to it. Resets frog color upon landing.
- **Prism Mode (Pickup):** Frog glows with a rainbow aura for 8 seconds, allowing jumps to any colored pad, rapidly building the combo meter.
- **Time Freeze (Pickup):** Reduces river current speed by 80% for 5 seconds, allowing for strategic planning.
- **Lotus Bloom (Pickup):** Spawns a massive, multi-colored central lotus that acts as an unmissable safety net/checkpoint.

---

## 6. Art Direction & Color Palette

High-contrast neon colors on a dark background to ensure visibility during fast gameplay.

| Element | Color | Hex |
|---|---|---|
| Background | Indigo Deep Water | `#1A2543` |
| Base Pads | Emerald Green | `#204E38` |
| Ruby Red | Target Identifier | `#FF4552` |
| Mango Yellow | Target Identifier | `#FFD035` |
| Electric Cyan | Target Identifier | `#00E5FF` |
| Toxic Purple | Target Identifier | `#B429F9` |
| Neon Pink | Target Identifier | `#FF3399` |

---

## 7. Meta-Progression & Economy

Players are incentivized to keep playing via an in-game cosmetic shop.

- **Currency:** Golden Flies. Collected during runs, earned via high scores, and rewarded for completing Daily Challenges (e.g., "Catch 50 Flies").
- **Cosmetic Skins:** Unlockable frog avatars that alter visual aesthetics and jump animations but do not affect hitboxes.

| Skin | Description |
|---|---|
| The Dart | Default sleek poison dart frog |
| The Drip Frog | Wears streetwear, sneakers, and a chain |
| The Tape Baller | Sports jersey, sweatband, ready for the pitch |
| The Architect | Neon-wireframe digital hologram aesthetic |
| The Astronaut | Zero-gravity float animation upon game over |
| The Ninja | Leaves a smoke trail on successful jumps |

---

## 8. Monetization Strategy

The game utilizes a hybrid ad-revenue and in-app purchase model focused on maintaining player retention without aggressive ad interruptions.

### 8.1 Rewarded Video Ads (Opt-In)

- **The "Second Chance" (Revive):** Offer a 3–5 second window upon failure to watch an ad and continue the run (limited to once per run).
- **End-of-Run Multiplier:** Option to watch an ad on the Game Over screen to multiply the Golden Flies earned in that run (x2 or x3).
- **Cosmetic Trials:** Option to test-drive premium skins for 3 runs by watching an ad.

### 8.2 Interstitial & Banner Ads

- **Interstitial Ads:** Shown only after every 3rd or 4th Game Over, never during active gameplay. Implemented in Update 1.1 to build an initial loyal player base.
- **Banner Ads:** Anchored at the bottom of the Main Menu, Shop UI, and Game Over screens. Strictly avoided during active gameplay to prevent accidental clicks.

### 8.3 In-App Purchases (IAPs)

- **"Remove Ads" ($1.99 or $2.99):** Permanently removes Interstitial and Banner ads while keeping Rewarded Ads intact.
- **Golden Fly Bundles:** Microtransactions allowing players to bypass the grind and instantly purchase premium cosmetics.

---

## 9. Audio & Sound Effects (SFX)

Audio provides vital feedback for the high-speed tapping gameplay loop.

### 9.1 Core Loop Audio

- **Background Music (BGM):** Lo-fi beat with tropical/marimba instrumentation. Tempo subtly scales up as the game enters harder phases.
- **The Jump:** A snappy, satisfying 'bloop'. Pitch scales upwards concurrently with the Fever Multiplier combo.
- **Color Shift:** A subtle glassy 'ting' playing exactly upon landing, providing auditory confirmation of the color change.

### 9.2 Game Over & Power-Ups

- **The Waterfall (Screen Out):** A fading 'whoosh' followed by a distant splash.
- **The Misstep (Wrong Color):** A comical, hollow underwater gurgle paired with a muted 'splat'.
- **Power-Ups:** Ethereal chimes for the Rainbow Pad, synthesized chords for Prism Mode, and a bass drop with a ticking clock for Time Freeze.

---

## 10. UI/UX & User Flows

The user interface avoids traditional hyper-casual "bubbly" tropes in favor of a sleek, modern architectural approach.

### 10.1 Camera & Perspective

- **Orthogonal 2.5D View:** The game utilizes an orthogonal camera setup on a vertical screen. This creates a sense of depth for the river and lily pads, making distance judgment and water flow tracking easier without altering the straightforward 2D hitboxes.

### 10.2 Menu Architecture & Aesthetic

- **Techwear/Streetwear UI:** Clean, glassmorphic panels (frosted glass) floating over the gameplay background. Typography relies on bold, oversized, high-end sans-serif fonts.
- **Menu Layout:**
  - Top Left: Golden Flies currency counter.
  - Top Right: High Score display.
  - Center: A massive, invisible or minimally outlined "Tap to Start" action area.
  - Bottom: A pinned navigation bar handling bottom-sheet popups for the Wardrobe, Leaderboards, and Settings.

### 10.3 Seamless State Transitions

- **Zero-Layout Shift:** The Main Menu is not a separate scene; it is simply the "idle" state of the gameplay engine. The camera is positioned slightly upriver with the frog idling on the starting pad.
- **Menu to Gameplay:** When the user taps the screen to start, there are no loading screens. The UI elements (navigation bar, score counters) smoothly fade out via alpha transitions. Simultaneously, the river's scrolling physics engine immediately activates, hydrating the active gameplay state in a fluid, continuous motion.
