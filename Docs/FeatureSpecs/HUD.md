# Feature Spec — HUD

**Last audited:** 2026-05-19

## Responsibilities

Display player-relevant runtime state (health, future: abilities, resources) without coupling to gameplay MonoBehaviours. Show and hide menus in response to game state changes.

---

## Current State

No HUD exists. `SampleScene` uses only the default Unity camera. This spec describes the intended design.

---

## Design Goals

- HUD subscribes to events and reads from ScriptableObjects — it never polls gameplay component fields
- Adding a new HUD element does not require structural Canvas changes
- Pause, game-over, and menu overlays share the same Canvas and respect the same boundary rules
- Vertical slice scope: health display only; all other slots reserved as empty GameObjects

---

## Canvas Architecture

```
UIRoot (Canvas, Screen Space — Overlay or Camera mode with UICamera)
├── HUD                         — always visible during gameplay
│   ├── HealthDisplay           — heart / bar representation of current health
│   └── [reserved: AbilitySlots]  — empty GameObject; populate in Milestone 3
│   └── [reserved: ResourceBar]   — empty GameObject; populate when resource system exists
└── Menus                       — shown/hidden by game state
    ├── PauseMenu               — activated by GameManager.Pause()
    └── [reserved: GameOverScreen]  — empty GameObject; populate in Milestone 5
    └── [reserved: MainMenu]        — lives in its own scene; reserved slot here for in-game return
```

**UICamera** — a separate orthographic camera (Clear Flags: Depth Only, Culling Mask: UI only) renders this Canvas on top of the gameplay camera. Set `Canvas.renderMode` to `Screen Space - Camera` and assign `UICamera`.

---

## Health Display

`HealthDisplay` (MonoBehaviour on the `HealthDisplay` GameObject):

- Subscribes to `HeroHealthComponent.OnDamaged` and `HeroHealthComponent.OnDeath` at scene init via `GameManager.SceneInit`.
- On `OnDamaged`: update the visual representation to reflect `currentHealth / maxHealth`.
- On `OnDeath`: hide or grey out the display.
- Does not hold a frame-to-frame reference to `HeroHealthComponent`. Subscribes once at `SceneInit`, unsubscribes on scene unload.

Visual representation is not specified here — hearts, a bar, a number — choose based on art direction. The subscription pattern is fixed.

---

## Pause Menu

`PauseMenu` (MonoBehaviour):

- Enabled / disabled by `GameManager.Pause()` and `GameManager.Unpause()`.
- Does not set `Time.timeScale` or add control locks directly — those are `GameManager`'s concern.
- Exposes a `Show()` and `Hide()` method; `GameManager` calls these.
- Buttons: Resume (calls `GameManager.Unpause()`), Quit to Desktop (`Application.Quit()`).
- Future: Save and Quit, Settings.

---

## UIFlowController

`UIFlowController` (MonoBehaviour on `UIRoot`) translates UI button intent into system calls:

- `OnNewGamePressed()` → `SaveManager.CreateFreshSave()` → `GameManager.BeginSceneTransition("Level_01", "default")`
- `OnQuitPressed()` → `SaveManager.Save()` → `Application.Quit()`
- No UI MonoBehaviour other than `UIFlowController` calls `SaveManager` or `GameManager` methods directly.

---

## Dependencies

- `HeroHealthComponent` — event source for health display
- `GameManager` — pause state, scene init event
- `SaveManager` — via UIFlowController only

---

## Rules

- No HUD MonoBehaviour calls `GetComponent` on a hero or enemy object per-frame.
- No HUD MonoBehaviour sets `Time.timeScale` directly.
- No HUD MonoBehaviour calls `SaveManager.Save()` or `SceneManager.LoadSceneAsync` directly.
- Structural Canvas changes (adding a new panel, a new layer) must not be required to add a new HUD element — only filling a reserved slot or adding a child to an existing group.
