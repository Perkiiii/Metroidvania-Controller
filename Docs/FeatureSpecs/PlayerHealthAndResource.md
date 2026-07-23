# Feature Spec — Player Health, Bonus Health, Resource, and Bind

**Last audited:** 2026-07-22

## Goals

- A single, persistent, save-round-tripping owner for current/maximum/bonus health and current/maximum resource parts.
- A scene-side facade (`HeroHealthComponent`) that owns i-frames, invulnerability, and contextual damage/hazard/death events without mirroring the persistent values.
- Accepted-hit resource generation decided by the attacker reading a result-bearing combat contract, never by a receiver calling back into hero-side code.
- A grounded, hold-to-heal Bind action with atomic spend-then-heal completion and a fully explicit, tested cancellation matrix.
- A persistent, event-driven HUD that never polls and never plays gameplay feedback for neutral state application.
- Explicit, implemented lifecycle policy for every save/load, New Game, death, hazard, checkpoint, and transition seam — including protection against ever resuming an active hero at zero health.

## Non-goals (this milestone and prior ones)

- Passive resource regeneration, partial-resource decay, parry resource generation.
- Resource-capacity upgrade gameplay, maximum-health upgrade pickup gameplay, checkpoint healing.
- Aerial Bind, staged Bind healing, resource reservation/refunds, final Bind animation/audio/VFX.
- Final HUD artwork or animation polish.
- Any new combat abilities, boss systems, enemy health bars, or other excluded feature work (see `Docs/ImplementationPlan.md`).

## Ownership

| Value / responsibility | Owner | Notes |
|---|---|---|
| Current/maximum/bonus health | `PlayerHealthState` (persistent `ScriptableObject`, `ISaveTarget`) | Sole authority. Never duplicated in `HeroHealthComponent`. |
| Current/maximum resource parts | `PlayerResourceState` (persistent `ScriptableObject`, `ISaveTarget`) | Sole authority. |
| i-frames, invulnerability, damage/hazard/death events | `HeroHealthComponent` (scene facade on Hero) | Counted invulnerability-source set (`HashSet<object>`), not a single bool. Delegates all value mutation to `PlayerHealthState`. |
| Bind lifecycle | `HeroBindAction` (plain C# class, owned by `HeroActionController`) | Reads/writes `PlayerHealthState`/`PlayerResourceState` directly; no MonoBehaviour. |
| Resource-award decision | `HeroAttackAction` | Reads `HeroAttackResult` after the receiver resolves the hit; the receiver never calls back into hero code. |
| Game-flow sequencing (death/respawn/hazard/transition placement) | `GameManager` | Delegates value mutation to `HeroController.ResetAfterRespawn()` / `ResetAfterHazardRecovery()`; never mutates `PlayerHealthState`/`PlayerResourceState` fields directly, except the one narrow zero-health-continue normalization seam described below. |
| Serialization / save orchestration | `SaveManager` | Gather/apply loop over `ISaveTarget`s; contains no gameplay lifecycle rules. |
| Presentation | `HealthDisplay` / `ResourceDisplay` under `PersistentHudRoot` | Subscribe directly to the state assets; never mutate gameplay state. |

## Persistent health state

`PlayerHealthState` (`Assets/_Project/Scripts/Hero/Core/PlayerHealthState.cs`) holds `currentHealth`, `maximumHealth`, `bonusHealth`, plus an authored `initialMaximumHealth` fresh-save default. API:

- `ApplyDamage(int amount) -> int` — bonus-first absorption (see Damage flow), returns damage actually applied, fires `Damage` reason only if damage was actually applied.
- `Heal(int amount) -> int` — normal health only, clamped to maximum, never restores bonus health.
- `FullRestore(bool preserveBonus = true)` — sets current = maximum; bonus is cleared only if `preserveBonus` is false.
- `ForceDeplete() -> bool` — zeroes current and bonus health unconditionally (used by instant-death/forced hazards); no-op (`false`) if already depleted, which is the guard that prevents a duplicate death event.
- `GrantBonusHealth(int amount)` / `ClearBonusHealth()`.
- `SetMaximumHealth(int value, bool restoreToFull = false)`.
- `IsDepleted => currentHealth <= 0`; `CanHeal(int amount)`.
- `NormalizeDepletedContinue() -> bool` — the zero-health-save-protection seam (see Lifecycle policy table below).
- `GatherSaveData(SaveData)` / `ApplySaveData(SaveData)` (`ISaveTarget`).

`Changed` fires a `PlayerHealthChangeInfo{CurrentHealth, MaximumHealth, BonusHealth, Reason}`. `PlayerHealthChangeReason` values: `Damage, Heal, FullRestore, BonusGranted, BonusCleared, MaximumChanged, ForcedDepletion, StateApplied, Reset`. `StateApplied` (used by `ApplySaveData` and `NormalizeDepletedContinue`) and `Reset` are the two reasons the HUD treats as neutral (no gameplay feedback); every other reason is a real gameplay change.

## Bonus health

Bonus health absorbs damage before normal health (`ApplyDamage`'s `absorbedByBonus = Min(bonusHealth, remainingDamage)` runs first). Healing (`Heal`) never restores it — bonus health is granted only through the explicit `GrantBonusHealth` API. It is cleared explicitly on death (`FullRestore(preserveBonus: false)` inside `RestoreAfterDeath`) and on forced depletion (`ForceDeplete` always zeroes it), never as an incidental side effect of an unrelated mutator.

## Damage flow

`HeroHealthComponent.TakeDamage(int amount, object iFrameSource, Vector2 knockback)`:
1. Early-out if `IsInvincible` (i-frame source set non-empty) or `amount <= 0`.
2. `ApplyDamage(amount)` internally early-outs (`WasIgnored = true`) if `healthState.IsDepleted` — this is the guard against a second lethal hit re-firing death.
3. Otherwise delegates to `PlayerHealthState.ApplyDamage`, wraps before/after health into a `DamageResult`, grants i-frames if not ignored, fires `OnDeath` if fatal else `OnDamaged`.

`TakeHazardDamage(int amount, object source)` follows the same `ApplyDamage` path but bypasses the `IsInvincible` check (hazards ignore i-frames) and fires `OnHazardDamaged` instead of `OnDamaged` when non-fatal.

`TriggerHazardDeath()` (instant-death hazards) bypasses health/invincibility entirely via `PlayerHealthState.ForceDeplete()`, relying on its own already-dead guard.

i-frames/invulnerability are a counted set (`HashSet<object> iFrameSources` + per-source coroutines), not a single bool, so multiple independent grantors (a normal hit, a respawn grace period, a hazard-recovery grace period) compose correctly.

## Hazard flow

- **Recoverable (`RecoverLocal`)**: `HeroBox.HandleHazard` calls `TakeHazardDamage`; if non-fatal, `GameManager.BeginHazardRecoverySequence` grants temporary i-frames, repositions locally via `HazardRespawnMarker`, and calls `HeroController.ResetAfterHazardRecovery()` — which resets only transient hero/motor/blackboard state and never touches `PlayerHealthState`/`PlayerResourceState`. Reduced health and current resource are both preserved.
- **Lethal recoverable hazard** (damage reduces health to zero): `TakeHazardDamage` itself detects the fatal result and fires `OnDeath` directly; `HeroBox` never calls `BeginHazardRecoverySequence` in this case (`result.IsFatal` short-circuits). This funnels into the exact same single death path as normal death — no duplicate clear/restore logic.
- **Forced/instant-death hazard**: `HeroBox.HandleHazard` calls `HeroHealthComponent.TriggerHazardDeath()` for `HazardRecoveryMode.InstantDeath`, which fires the same `OnDeath` event as every other death cause.

## Death and respawn

Single authoritative chain, reached identically by normal combat death, lethal recoverable hazards, and forced-death hazards:

```
OnDeath (fired exactly once, guarded by IsDepleted / ForceDeplete's already-dead check)
  -> HeroController.HandleDeath()          cancels Bind, locks control, kills velocity, plays death anim
  -> OnDeathAnimationComplete / DeathFallbackRoutine -> TriggerRespawn() (idempotent, respawnTriggered guard)
  -> GameManager.BeginRespawnSequence() -> ApplyNormalDeathRespawn(marker, scene)
       teleport -> grant post-respawn i-frames -> HeroController.ResetAfterRespawn() -> SetCurrentScene
  -> HeroController.ResetAfterRespawn():
       health.RestoreAfterDeath()      -> PlayerHealthState.FullRestore(preserveBonus: false)
       resourceState?.Clear()          -> PlayerResourceState.Clear()   (added in Milestone 7)
       ResetTransientHeroState()       -> cancels attack/bind, resets motor/blackboard, re-enables collider
       controlLocks.Clear(); respawnTriggered = false
```

`HeroController.ResetAfterRespawn()` is the single call site for health restoration, bonus-health clearing, and resource clearing — it is never duplicated in `HeroHealthComponent` or `GameManager`. Health and resource are restored/cleared exactly once because this method itself is reached exactly once per death (guarded by `respawnTriggered`), and both `PlayerHealthState.FullRestore`/`PlayerResourceState.Clear` are individually idempotent (safe to call twice).

## Persistent resource

`PlayerResourceState` (`Assets/_Project/Scripts/Hero/Core/PlayerResourceState.cs`) holds `currentParts`, `maximumParts`, and an authored `initialMaximumParts` fresh-save default. API:

- `Gain(int parts) -> int` — clamped to maximum, returns actual amount gained.
- `CanAfford(int parts) -> bool` / `TrySpend(int parts) -> bool`.
- `SetMaximumParts(int value)` — clamps current into the new range; does not auto-refill current.
- `Clear()` — zeroes current, preserves maximum; no-op (no notify) if already zero. Added in Milestone 7 for the death lifecycle policy.
- `GatherSaveData(SaveData)` / `ApplySaveData(SaveData)` (`ISaveTarget`).

`Changed` fires a `PlayerResourceChangeInfo{CurrentParts, MaximumParts, Reason}`. `PlayerResourceChangeReason` values: `Gain, Spend, MaximumChanged, StateApplied, Reset, Cleared`. `StateApplied` and `Reset` are neutral (no HUD feedback); `Cleared` (death) is treated as a real gameplay change like `Gain`/`Spend`, since forfeiting resource on death is a real, player-visible event, not a neutral state application.

Resource has no orb/pip sub-unit model — it is a plain integer-parts value, presented as one continuous fill bar (see HUD below).

## Accepted-hit resource generation

`IHeroAttackReceiver.ReceiveHeroAttack` returns a `HeroAttackResult{Outcome, DamageApplied, ResourceEligible}` with `Outcome` in `{Ignored, Blocked, Invulnerable, Damaged, Killed}`. `ResourceEligible` defaults to true for accepted `Damaged`/`Killed` outcomes, via an optional `resourceEligible` parameter on the `Damaged`/`Killed` factories (World Persistence Phase 3.1) — a receiver can opt out explicitly by passing `false`. `HeroAttackAction` — the attacker — reads this result and decides whether to award resource; the receiver (e.g. `EnemyHealthComponent`) never calls back into hero-side singleton state. Per-swing/per-target deduplication (already-hit collider and already-hit responder sets) prevents double-award from one swing.

**Environmental receivers are non-resource-eligible by default.** `PersistentBreakable.ReceiveHeroAttack` passes `resourceEligible: false` — destroying scenery does not fuel the hero's resource meter. This is a deliberate policy decision (Phase 3.1), not an oversight: `EnemyHealthComponent` never changed (it still defaults to eligible), and a future breakable that should award resource can opt in explicitly with `resourceEligible: true`.

### Generation policies

Each `HeroAttackModule` carries a `HeroResourceGenerationMode` + `resourceGainParts`:
- `None` — never awards.
- `PerSuccessfulTarget` — awards once for each distinct accepted, resource-eligible receiver in the swing.
- `FirstSuccessfulHitPerAttack` — awards once for the first eligible result in the attack, resetting per new attack.

Parries/clashes never generate resource (out of this milestone's scope; not implemented).

## Bind action

`HeroBindAction` (`Assets/_Project/Scripts/Hero/Actions/HeroBindAction.cs`), a plain C# class owned by `HeroActionController`. First-pass design, kept intentionally simple per project decision:

- **Grounded-only** — `CanStart()`/`CanContinue()` both require `blackboard.grounded`.
- **Lump-sum healing** — no staged/incremental healing.
- **Full cost spent only on successful completion** — no partial spend, no reservation, no refund path except the defensive one described below.
- **No aerial Bind, no regeneration, no decay.**

**Start conditions** (`CanStart`): alive, not hurt/dead, not mid-attack/dash/wall-slide/wall-jump/recoiling, controls not locked, grounded, missing health present (`CanHeal`), resource affordable (`CanAfford`), animation playable, input pressed-and-held this frame.

**Completion** (`TryComplete`, atomic): revalidate `CanContinue()` (health/resource/state still valid) → `TrySpend` → `Heal`. If `Heal` unexpectedly returns 0 after a successful spend, the spend is refunded (`Gain`) and the attempt cancels — a defensive invariant-preserving path, not a normal-path partial refund.

**Cancellation** — every trigger below routes through `HeroActionController.CancelBind()` → `HeroBindAction.Cancel()`, which is idempotent (safe to call twice), always clears `blackboard.binding`, releases movement suppression (`motor.SetNormalMovementSuppressed(false)`), stops/fades the Bind animation, and spends/heals nothing:

| Trigger | Wiring |
|---|---|
| Input release | `CanContinue()` checks `input.BindHeld`/`BindReleasedThisFrame` every tick |
| Accepted damage | `HeroController.HandleDamaged` calls `CancelBind()` explicitly |
| Recoverable hazard | `HeroController.HandleHazardDamaged` calls `CancelBind()` explicitly |
| Lethal/forced/normal death | `HeroController.HandleDeath` calls `CancelBind()`; also transitively via `AddControlLock` |
| Scene transition | `AddControlLock` (added by `SceneTransitionManager`/`HeroSceneEntry`) unconditionally cancels Bind as a side effect; `BeginSceneEntryPlacement`/`BeginSceneEntryMotion` also cancel explicitly |
| Control lock (any) | `AddControlLock` cancels Bind; `CanContinue()` also checks `blackboard.controlLocked`/`inputBlocked` |
| Loss of ground | `FixedTick()` cancels immediately if `!blackboard.grounded`; `CanContinue()` also checks it |
| Hero reset (respawn/hazard recovery) | `ResetTransientHeroState()` (called by both `ResetAfterRespawn` and `ResetAfterHazardRecovery`) calls `CancelBind()` |
| Invalid animation/config dependency | `CanContinue()` checks `canPlayAnimation()`/config nullability |

Animation-complete is a completion *signal*, not a bypass: `CompleteFromAnimation()` only calls `TryComplete()` if the configured hold duration has already elapsed — the duration timer is the authoritative fail-safe even if the animation event never fires.

## HUD ownership

Exactly one persistent HUD exists, under `_GameCameras` (`DontDestroyOnLoad`, self-destructing duplicate instances in `Awake`). `HealthDisplay` and `ResourceDisplay` subscribe directly to `PlayerHealthState.Changed`/`PlayerResourceState.Changed` — never through `HeroHealthComponent`, `HeroController`, or `GameManager.SceneInit`. Subscription happens once per `OnEnable` (guarded by a `subscribed` bool, idempotent under repeated `Configure()` calls) and is released in `OnDisable`/`OnDestroy`. Neither view polls in `Update`. The `HUDCamera` is a URP Overlay camera (`ClearFlags = Nothing`) stacked onto the gameplay `MainCamera` and does not clear the gameplay view.

### Health display

Renders dynamic normal-health slots (`NormalSlotCount = MaximumHealth`) and a separate bonus-health slot container, via `HealthSlotView`. `StateApplied`/`Reset` refresh with no gameplay feedback; every other reason updates `GameplayFeedbackCount`.

### Single resource-bar display

`ResourceDisplay` renders current resource as **one always-visible horizontal fill bar** (`ResourceBarView`), not discrete pips or an orb:
- `fillAmount01 = maximumParts > 0 ? currentParts / maximumParts : 0` — zero when capacity is zero, never `NaN`.
- The bar's `CanvasGroup` is forced to `alpha = 1` unconditionally (`IsVisible` is hardcoded `true`) — it remains visible at zero resource.
- `StateApplied`/`Reset` refresh with no feedback; `Gain`, `Spend`, `MaximumChanged`, and `Cleared` all count as real gameplay feedback.

The earlier discrete pip/orb presentation (`ResourcePipView.cs`/`.prefab`) has been removed as obsolete (Milestone 7 cleanup); no orb or pip presentation remains anywhere in the project.

## Save/load

`SaveManager` gathers/applies `PlayerAbilityState`, `PlayerHealthState`, `PlayerResourceState` (Inspector-ordered list) with no gameplay rules of its own. Each state's `ApplySaveData`:
- Uninitialized/missing section → authored fresh defaults, reason `StateApplied`.
- Initialized section → values clamped (`maximumHealth >= 1`, `currentHealth` in `[0, maximum]`, `bonusHealth >= 0`; `maximumParts >= 0`, `currentParts` in `[0, maximum]`), reason `StateApplied`, `forceNotify: true` so the HUD always refreshes even if the numbers happen to match.

## New Game

`SaveManager.CreateFreshSave(slot)` builds a bare `new SaveData()` (health/resource `initialized = false` by default) and calls `ApplySaveData()` on every target — which always lands in the uninitialized branch above, i.e. full health, zero bonus health, zero resource, regardless of whatever a previously-loaded save had left in the runtime `ScriptableObject` fields. There is no partial/incremental state carry-over path, so a previously loaded save's runtime values cannot leak into a new game.

## Checkpoints

`CheckpointInteractable.Interact()`:
```
GameManager.SetActiveRespawnMarker(marker)   // marker mutation — happens first
  -> SaveManager.SetActiveRespawnPoint(...)  // in-memory only
SaveManager.Save()                            // writes to disk — the saved marker matches the just-activated one
```
No health/resource/bonus mutation anywhere in this path — current health, bonus health, and current resource are all preserved exactly as the policy requires. Bind is not cancelled by checkpoint activation: interaction runs on a fully independent path (`InteractManager`, gated only on `GameState.Playing` and proximity) that never touches `HeroActionController`/blackboard state, so an active Bind is undisturbed by a checkpoint save and requires no special-case cancellation.

## Room transitions

`SceneTransitionManager`/`HeroSceneEntry`/`TransitionPoint` never reference `PlayerHealthState`/`PlayerResourceState` — persistent values survive scene transitions untouched because they live on ScriptableObject assets, not scene objects. Bind is cancelled via `AddControlLock`'s built-in `CancelBind()` side effect (and explicitly again at the two `HeroController` scene-entry entry points). The Hero GameObject is destroyed and recreated by the `LoadSceneMode.Single` scene load, but since the new instance's serialized fields point at the same persistent assets, nothing resets on recreation. The HUD, living under the separate `DontDestroyOnLoad` `_GameCameras` root, is never recreated or resubscribed by a transition.

## Lifecycle policy table

| Lifecycle event | Current health | Bonus health | Current resource | Bind | HUD |
|---|---|---|---|---|---|
| New Game | Full (authored max) | 0 | 0 | N/A | Neutral refresh (`StateApplied`) |
| Ordinary save/load | Restored (clamped) | Restored (clamped) | Restored (clamped) | Not serialized | Neutral refresh |
| **Zero-health Continue** | Normalized to full **once**, before any scene/Hero exists | Cleared | Cleared | N/A | Neutral refresh (`StateApplied`, not `Heal`) |
| Room transition | Preserved | Preserved | Preserved | Cancelled | No rebuild/resubscribe |
| Checkpoint | Preserved | Preserved | Preserved | Not cancelled (not required) | Neutral refresh only if a save-apply occurs |
| Normal death | Restored to max once | Cleared once | Cleared once | Cancelled immediately, spends nothing | Ends at full/0/0 |
| Recoverable hazard | Preserved (reduced) | Preserved (already reduced if consumed) | Preserved | Cancelled | Reflects reduced health |
| Lethal recoverable hazard | Same as normal death | Same as normal death | Same as normal death | Cancelled | Same as normal death |
| Forced/instant-death hazard | Same as normal death | Same as normal death | Same as normal death | Cancelled | Same as normal death |
| Application quit | N/A (in-memory) | N/A | N/A | N/A | N/A — save-on-quit persists whatever is currently in the state assets |

### Zero-health save protection — root cause and fix

**Root cause:** a save can capture `PlayerHealthState.CurrentHealth == 0` if `SaveManager.SaveOnQuit()` or `CheckpointInteractable.Interact()`'s unconditional `Save()` call fires during the brief window after a lethal hit but before `HeroController.ResetAfterRespawn()` restores health. `SaveDataMigrator` performs no health normalization, and `PlayerHealthState.ApplySaveData` legitimately allows `currentHealth == 0` (0 is a valid *momentary* gameplay value, just not a valid value to *resume play at*). No prior code path checked for this.

**Fix — exactly where and when normalization occurs:** `GameManager.ResolveLoadedHealthState()` is called exactly once, by `Bootstrap.Start()`, immediately after `SaveManager.Instance.LoadOrCreate(0)` and before `BeginSceneTransition` (i.e. before any gameplay scene or Hero exists). It calls `PlayerHealthState.NormalizeDepletedContinue()`, which:
- No-ops (returns `false`) unless `IsDepleted` — safe to call unconditionally, safe to call twice.
- Sets `CurrentHealth = MaximumHealth` and `BonusHealth = 0`, preserving the saved `MaximumHealth` (progression is never lost).
- Fires the neutral `StateApplied` reason (not `Heal`/`FullRestore`), so the HUD never presents this as player healing.
- Cannot trigger a second death or respawn: it runs before any `HeroController`/`HeroHealthComponent` exists, so no death event, animation, or respawn sequence can fire from it.

If (and only if) that normalization actually fired, `GameManager` also calls `PlayerResourceState.Clear()`, so a Continue never resumes with health restored but a stale pre-death resource amount — matching the normal death policy, where health and resource are always reset together.

## Configuration ownership

| Value | Owner | Saved? | Notes |
|---|---|---|---|
| Current/maximum/bonus health | `PlayerHealthState` | Yes | |
| Current/maximum resource parts | `PlayerResourceState` | Yes | |
| Bind cost/duration/heal amount | `PlayerResourceConfig` | No (tuning asset) | |
| Attack resource-gain policy/amount | Per-`HeroAttackModule` field | No | Travels with the attack module, not a central table |
| `HeroConfig.maxHealth` | `HeroConfig` (legacy) | Serialized but unread | `[Obsolete]`, retained for migration safety per its own tooltip; zero live callers confirmed by repo-wide search |

## Unity Editor setup

- `PlayerHealthState.asset`, `PlayerResourceState.asset` assigned to the `_SaveManager.prefab` ordered target list and to the Hero prefab's `HeroController.healthState`/`resourceState` fields.
- `PlayerResourceConfig.asset` assigned to `HeroController.resourceConfig`.
- `GameManager` (on `_GameManager.prefab`) now also references `PlayerHealthState` and `PlayerResourceState` directly (new in Milestone 7), solely for `ResolveLoadedHealthState()` — assigned to the same assets used elsewhere.
- Input System `Bind` action exists and is bound (verified in `InputSystem_Actions.inputactions`); the temporary direct-keyboard fallback in `HeroInputReader` has been removed.
- HUD hierarchy (`PersistentHudRoot` → `HealthDisplay`/`ResourceDisplay`) lives under `_GameCameras.prefab`; `HUDCamera` is configured as an Overlay camera stacked on `MainCamera`.

## Validation checklist

- [x] All 71 relevant Edit Mode tests pass (`PlayerPersistentStateTests`, `HeroHealthOwnershipTests`, `HeroAttackResultTests`, `HeroBindActionTests`, `HeroResourceGenerationTests`, `HudDisplayTests`).
- [ ] Play Mode: full death → respawn cycle, checkpoint save/reload, room transition, recoverable hazard, and a manually-forced zero-health save load — see the completion report for exactly what could and could not be exercised this session.
- [ ] Hero-feel manual regression checklist in `Docs/HeroFeelTuning.md` (Bind and Resource section added this milestone).

## Deferred work

Passive resource regeneration, partial-resource decay, parry resource generation, resource-capacity upgrade gameplay, maximum-health upgrade pickup gameplay, checkpoint healing, aerial Bind, staged Bind healing, resource reservation/refunds, final Bind animation/audio/VFX, final HUD artwork/animation polish — all explicitly out of scope, not started.

## Known technical debt

- `HeroHealthComponent.RestoreFullHealth()` (the `preserveBonus: true` overload) has no production caller — only exercised by `HeroBindActionTests`. Retained rather than removed since a test still depends on it.
- `GameManager` now holds direct `PlayerHealthState`/`PlayerResourceState` references for the zero-health-continue seam — a narrow, documented exception to "GameManager does not own health/resource values," not general ownership.
- `ResourceDisplay.Configure(PlayerResourceState, PlayerResourceConfig)` (the two-argument legacy overload) is retained only because `HudDisplayTests` still calls it; the `PlayerResourceConfig` argument is otherwise unused by the bar presentation.
