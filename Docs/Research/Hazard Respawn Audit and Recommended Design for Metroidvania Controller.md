# Hazard Respawn Audit and Recommended Design for Metroidvania Controller

> Historical / superseded note: this research audit predates the implemented local hazard recovery system. Statements below about the "current" hazard path refer to the older instant-death-only implementation. Current runtime behaviour is documented in `Docs/Architecture.md` and `Docs/ImplementationPlan.md`.

## Audit findings

The current hazard path is a straight instant-death path. `HazardZone.OnTriggerEnter2D` looks for a `HeroBox` on the collider that entered the trigger and, if found, immediately calls `HeroBox.TriggerHazardDeath()`. `HeroBox.TriggerHazardDeath()` clears any pending buffered damage and forwards to `HeroHealthComponent.TriggerHazardDeath()`, which sets `currentHealth = 0` and fires `OnDeath` without checking invincibility or applying normal damage behaviour. fileciteturn6file0L3-L3 fileciteturn8file0L3-L3 fileciteturn9file0L3-L3

Once `OnDeath` fires, `HeroController` runs the same path used for any real death: it sets the blackboard state to `Dead`, adds a control lock, zeroes velocity, makes the body kinematic, disables the collider, plays death audio, and waits for either the death animation end-event or the fallback timer to call `GameManager.BeginRespawnSequence()`. `HeroAnimationController` is the piece that turns `HeroActorState.Dead` into the death clip and raises `DeathAnimationComplete` when that clip ends. fileciteturn19file0L3-L3 fileciteturn36file0L3-L3

`GameManager.BeginRespawnSequence()` currently performs a full normal-death respawn only. Its routine fades out, moves the hero to `_activeRespawnMarker` if one exists or the nearest normal `RespawnMarker` if not, restores full health via `HeroHealthComponent.RestoreFullHealth()`, calls `HeroController.ResetAfterRespawn()`, snaps the camera, waits briefly in real time, then fades back in. There is no separate local hazard recovery branch in `GameManager` today. fileciteturn10file0L3-L3 fileciteturn11file0L3-L3 fileciteturn9file0L3-L3

Checkpoint behaviour is already separate and sensible. `CheckpointInteractable` assigns a normal `RespawnMarker` through `GameManager.SetActiveRespawnMarker(respawnMarker)` and then saves immediately. `GameManager.SetActiveRespawnMarker` stores the live marker reference and forwards `marker.Key` into `SaveManager`, which persists `activeRespawnMarkerKey`. On scene load, `GameManager.ResolveActiveRespawnMarkerFromSave()` resolves the saved key back to a live `RespawnMarker`. That means your checkpoint/full-death path already has both in-session and cross-session support. fileciteturn17file0L3-L3 fileciteturn10file0L3-L3 fileciteturn13file0L3-L3 fileciteturn15file0L3-L3

The save layer is more mature than the current hazard system. `SaveManager` already exposes `ActiveHazardRespawnMarkerKey` and `SetActiveHazardRespawnMarkerKey`, `PlayerSaveData` already contains `activeHazardRespawnMarkerKey`, and `SaveDataMigrator` already normalises that field. However, no corresponding hazard-marker runtime resolution exists in `GameManager`, and the docs still describe hazard marker support as deferred or reserved for a future milestone. In other words, the save schema is ready, but the gameplay/runtime integration is not. fileciteturn13file0L3-L3 fileciteturn15file0L3-L3 fileciteturn16file0L3-L3 fileciteturn21file0L3-L3 fileciteturn22file0L3-L3 fileciteturn23file0L3-L3

There are two especially important constraints visible in the current code. First, routing hazards through `HeroHealthComponent.TakeDamage()` would not behave the way you want: `TakeDamage()` exits early if `IsInvincible` is true, grants i-frames keyed by source, and fires `OnDamaged`; `HeroController.HandleDamaged()` then sets `Hurt`, flashes, plays hurt audio, applies knockback, suppresses normal movement, and starts a hurt-recovery coroutine. Even a zero knockback argument becomes the controller’s default knockback force. That is the opposite of “damage + local reposition”. fileciteturn9file0L3-L3 fileciteturn19file0L3-L3

Second, `HeroController.ResetAfterRespawn()` is not yet a fully general “safe reposition” reset. It clears some death-related state, but it does not stop the hurt coroutine, does not cancel attacks, does not zero velocity, and does not clear transient blackboard flags such as `attacking`, `dashing`, `wallSliding`, or `wallJumping`. Because `HeroAnimationController` selects clips directly from those blackboard flags, a local hazard recovery path that reuses this method without hardening it could leave the hero recovering into the wrong movement or animation state. fileciteturn19file0L3-L3 fileciteturn31file0L3-L3 fileciteturn34file0L3-L3 fileciteturn36file0L3-L3

A final audit note: `GameManager.TransitionRoutine()` writes `State = Loading` and later `State = Playing`, but `RespawnRoutine()` does not change `State` at all. That means `GameState` by itself is not a reliable guard against retriggering hazards during same-scene respawn or future local hazard recovery; you will want a dedicated recovery/respawn-in-progress guard. fileciteturn10file0L3-L3 fileciteturn38file0L3-L3

## Recommended architecture

The cleanest adaptation is to introduce a **dedicated hazard path**, not to overload normal damage. Keep health subtraction inside `HeroHealthComponent`, keep fade/camera/placement inside `GameManager`, keep the hurtbox entry point in `HeroBox`, and keep respawn-marker ownership out of `HeroController`. That matches the current project boundaries: `HeroController` is already the coordinator, `HeroHealthComponent` already owns health, `GameManager` already owns same-scene respawn flow, and the player-controller spec explicitly treats `HeroBox` as the hurtbox ingress for enemy contact and hazards. fileciteturn39file0L3-L3 fileciteturn21file0L3-L3 fileciteturn22file0L3-L3

My recommendation is:

```csharp
// HeroBox
public void HandleHazard(HazardZone hazard);

// HeroHealthComponent
public bool ApplyHazardDamage(int amount); // returns true if health reached zero

// GameManager
public void BeginHazardRecoverySequence(HazardRespawnMarker marker);
```

That API split keeps each responsibility narrow. `HandleHazard` is the ingress and orchestration point, `ApplyHazardDamage` owns health arithmetic and death signalling, and `BeginHazardRecoverySequence` owns the scene-flow aspects of local recovery. This is smaller and safer than trying to force hazards through `TakeDamage()` or by broadening `TriggerHazardDeath()` to mean two different things. It also lets `TriggerHazardDeath()` remain available for true instant-kill hazards. The project docs already accept that `GameManager` temporarily owns respawn-related tech debt, so adding a local hazard recovery routine there is consistent with the repo’s current stage. fileciteturn8file0L3-L3 fileciteturn9file0L3-L3 fileciteturn10file0L3-L3 fileciteturn21file0L3-L3 fileciteturn22file0L3-L3

For the **minimal first implementation**, I would not make local hazard recovery depend on an active saved hazard marker at all. Instead, make each recoverable `HazardZone` optionally reference a `HazardRespawnMarker` directly. That gets the behaviour working with the fewest moving parts: no extra runtime state, no scene-load resolution, no cross-room stale marker bug, and no new save dependency. Once that path works, add an optional `HazardRespawnTrigger` authoring pass as a convenience layer on top. This gives you both patterns eventually, but does not make the feature depend on the more complex one. fileciteturn10file0L3-L3 fileciteturn21file0L3-L3 fileciteturn22file0L3-L3

If and when you add trigger-updated local hazard markers, the live active marker should sit in **`GameManager`**, not in `HeroController`, not in `SaveManager`, and not in a new `RespawnService` yet. `GameManager` already owns respawn flow and scene transitions; `HeroController` should remain marker-state-free; and a separate service would be overbuilding at this stage. `SaveManager` is stable enough to become an optional seam later because the save schema and setter already exist, but I would keep that field dormant in the first pass so the feature can land without adding persistence complexity. fileciteturn10file0L3-L3 fileciteturn13file0L3-L3 fileciteturn15file0L3-L3 fileciteturn16file0L3-L3

The right behaviour split is also **not** `HazardType` yet. The only behaviour branch you need now is “recover locally” versus “kill immediately”. A small `HazardRecoveryMode` enum is enough for that first decision. A broad `HazardType` taxonomy becomes useful later for type-specific VFX, audio, resistances, damage rules, or analytics, but it is not required to get the Hollow Knight-style health-drain-and-reposition loop working. In fact, type and recovery mode are orthogonal: a lava volume could be recoverable in one room and instant-death in another, so behaviour should not be forced into the type enum. fileciteturn21file0L3-L3 fileciteturn22file0L3-L3 fileciteturn23file0L3-L3

I would make the “create now or defer” decisions like this:

| Item | Decision | Reason |
|---|---|---|
| `HazardRespawnMarker` | Create now | Required for authored local recovery points |
| `HazardRecoveryMode` | Create now | Needed to distinguish recoverable vs instant-death hazards cleanly |
| `HazardRespawnTrigger` | Create next, not necessarily in the first commit | Valuable authoring convenience, but not required for the first working behaviour |
| `HazardType` | Defer | Adds taxonomy before you have type-specific rules |

A subtle but important implementation detail is the marker component shape. Do **not** port a complex reference-game marker verbatim if the repo already has a simpler, cleaner authoring pattern. Your existing `RespawnMarker` is just `key`, `spawnPoint`, and `facingRight`; a first-pass `HazardRespawnMarker` should mirror that style rather than introducing a separate ground-raycast spawn derivation model. That stays consistent with the repository’s established authoring conventions and keeps the component cheap to understand. fileciteturn11file0L3-L3

## Proposed runtime flows

The recommended runtime flows below deliberately preserve the current full-death path and add a separate nonfatal hazard branch.

For a **recoverable hazard hit with health remaining**, the flow should be:

1. `HazardZone.OnTriggerEnter2D` finds the hero hurtbox and calls a new `HeroBox.HandleHazard(this)` entry point instead of always calling `TriggerHazardDeath()`. `HeroBox` should still clear pending buffered damage first, exactly as it does now for instant hazard death, so delayed enemy/contact damage does not apply after reposition. fileciteturn6file0L3-L3 fileciteturn8file0L3-L3  
2. `HeroBox.HandleHazard()` checks a GameManager recovery-in-progress/dead-state guard, reads the zone’s `HazardRecoveryMode`, and for the recoverable branch calls `HeroHealthComponent.ApplyHazardDamage(hazardDamage)`. That method subtracts health **without** normal i-frames, **without** `OnDamaged`, and **without** knockback. If health stays above zero, it returns nonfatal and does not invoke `OnDeath`. This bypass is necessary because `TakeDamage()` currently exits during invincibility and its `OnDamaged` path always drives hurt knockback/stun. fileciteturn9file0L3-L3 fileciteturn19file0L3-L3  
3. `HeroBox` then asks `GameManager` for local recovery, passing either the zone’s direct `HazardRespawnMarker` or a resolved fallback. `GameManager.BeginHazardRecoverySequence()` performs a short fade-out, repositions the hero to the hazard marker, calls a hardened hero reset method, snaps the camera, and fades back in. It does **not** call `RestoreFullHealth()`. fileciteturn10file0L3-L3 fileciteturn9file0L3-L3  
4. The hardened hero reset should clear transient action/hurt/death state, zero velocity inside the hero reset path, restore the body to a valid controllable form, and clear stale blackboard flags. This is important because current reset logic is not yet sufficient for nondeath repositioning. fileciteturn19file0L3-L3 fileciteturn31file0L3-L3 fileciteturn36file0L3-L3  

For a **recoverable hazard hit that reduces health to zero**, the flow should be:

1. `HazardZone` still enters through the same `HeroBox.HandleHazard()` path. fileciteturn6file0L3-L3 fileciteturn8file0L3-L3  
2. `HeroHealthComponent.ApplyHazardDamage()` subtracts the hazard damage. If health reaches zero, it invokes `OnDeath` exactly once. At that point, `HeroBox` does **not** start local hazard recovery; it simply lets the existing death path continue. fileciteturn9file0L3-L3  
3. `HeroController.HandleDeath()` runs unchanged: dead state, control lock, velocity zero, kinematic body, collider disable, death audio, animation/fallback timer. `HeroAnimationController` finishes the death clip and raises `DeathAnimationComplete`, which leads to `GameManager.BeginRespawnSequence()`. fileciteturn19file0L3-L3 fileciteturn36file0L3-L3  
4. `GameManager.RespawnRoutine()` performs the existing checkpoint/full respawn, including full-health restore. That means the fifth pit/spike/acid/lava failure behaves as a true death, exactly as you want. fileciteturn10file0L3-L3 fileciteturn9file0L3-L3  

For a **full checkpoint death** from ordinary damage, the path should remain as it is:

1. `HeroBox.FixedUpdate()` flushes buffered pending damage into `HeroHealthComponent.TakeDamage(damage, source, knockback)`. fileciteturn8file0L3-L3  
2. `TakeDamage()` applies normal rules: respect invincibility, subtract health, grant i-frames for the source, fire `OnDamaged` if health remains or `OnDeath` if not. `HeroController.HandleDamaged()` then handles knockback/hurt state/control lock, while `HandleDeath()` handles the death branch. fileciteturn9file0L3-L3 fileciteturn19file0L3-L3  
3. `GameManager.BeginRespawnSequence()` remains the only full respawn path and remains the only place that restores full health. That separation is worth preserving. fileciteturn10file0L3-L3

The fallback rule I recommend for local hazard placement is:

1. active trigger-updated hazard marker, if that system exists and the marker is valid in the current scene;  
2. otherwise the `HazardZone`’s direct fallback marker;  
3. otherwise the nearest normal `RespawnMarker`, preserving reduced health;  
4. otherwise log a warning and fall back to the hero’s current position only as a last-resort failsafe.  

That fallback order keeps the authored local behaviour primary, gives you robustness when a trigger is missing, and avoids silently converting a nonfatal hazard into a full heal/death path just because a marker was misconfigured. The nearest-normal-marker fallback also reuses logic `GameManager` already has today. fileciteturn10file0L3-L3 fileciteturn11file0L3-L3

## Files and commit plan

The smallest useful implementation surface is this:

| File | Change | Why |
|---|---|---|
| `Assets/_Project/Scripts/World/HazardZone.cs` | Add `HazardRecoveryMode`, `hazardDamage`, and optional direct `HazardRespawnMarker`; call `HeroBox.HandleHazard(this)` | Hazard authoring and contact entry |
| `Assets/_Project/Scripts/Hero/Core/HeroBox.cs` | Add explicit hazard handler; keep clearing pending damage; keep `TriggerHazardDeath()` for instant-death mode | Preserve hurtbox ingress and buffered-damage safety |
| `Assets/_Project/Scripts/Hero/Core/HeroHealthComponent.cs` | Add `ApplyHazardDamage(int amount)` that bypasses normal i-frames/knockback but still fires `OnDeath` on zero | Health remains owned here |
| `Assets/_Project/Scripts/World/GameManager.cs` | Add `BeginHazardRecoverySequence(...)` and a recovery-in-progress guard | Keep fade/camera/placement in one place |
| `Assets/_Project/Scripts/Hero/HeroController.cs` | Harden reset path so hazard reposition is safe: stop hurt/death coroutines as needed, cancel actions, zero velocity, clear transient flags | Prevent stuck motion/animation state after local recovery |
| `Assets/_Project/Scripts/World/HazardRespawnMarker.cs` | New component | Authored local recovery point |
| `Assets/_Project/Scripts/World/HazardRecoveryMode.cs` | New enum | Clean behaviour branch |

Those changes are enough for the first “damage + local reposition until zero” pass and do **not** require any SaveManager changes. That is the safest minimal implementation. It respects your constraints, keeps `HeroController` free of marker ownership, and leaves checkpoint/full respawn untouched. fileciteturn6file0L3-L3 fileciteturn8file0L3-L3 fileciteturn9file0L3-L3 fileciteturn10file0L3-L3 fileciteturn19file0L3-L3

The **optional immediate follow-up** is:

| File | Change | Why |
|---|---|---|
| `Assets/_Project/Scripts/World/HazardRespawnTrigger.cs` | New component that updates a live active hazard marker on enter | Better Hollow Knight-style authoring across multi-hazard spaces |
| `Assets/_Project/Scripts/World/GameManager.cs` | Add `_activeHazardRespawnMarker`, setter, scene validation/clear-on-load | Runtime storage for trigger-based hazard respawn |

If you do add that second pass, the live active hazard marker should belong to `GameManager` only. Do not save on trigger enter, and do not let it replace or overwrite the normal checkpoint marker. The project’s save docs are clear that checkpoint save is the explicit save trigger, not arbitrary hero/enemy/world gameplay code. fileciteturn17file0L3-L3 fileciteturn23file0L3-L3

I would **defer changes** to these files for the first pass:

| File | Decision | Why |
|---|---|---|
| `Assets/_Project/Scripts/Save/SaveManager.cs` | Defer | Hazard key seam exists already, but first-pass local recovery does not need persistence |
| `Assets/_Project/Scripts/Save/Data/PlayerSaveData.cs` | Defer | Field already exists |
| `Assets/_Project/Scripts/Save/SaveDataMigrator.cs` | Defer | Field already normalised |
| `Assets/_Project/Scripts/World/RespawnMarker.cs` | Defer | Normal checkpoint marker already works |

The corresponding docs should be updated once the code lands, because right now the architecture and save docs partially describe hazard-marker concepts that are not yet implemented in runtime code. The most relevant docs are `Docs/Architecture.md`, `Docs/ImplementationPlan.md`, `Docs/FeatureSpecs/SaveSystem.md`, and `Docs/FeatureSpecs/PlayerController.md`. fileciteturn21file0L3-L3 fileciteturn22file0L3-L3 fileciteturn23file0L3-L3 fileciteturn39file0L3-L3

A sensible PR plan is:

| Commit | Scope |
|---|---|
| Commit A | Introduce `HazardRecoveryMode` and `HazardRespawnMarker`; extend `HazardZone` to support recoverable hazards with a direct marker reference, defaulting to `InstantDeath` for backwards safety |
| Commit B | Add `HeroHealthComponent.ApplyHazardDamage()` and `HeroBox.HandleHazard()`; wire recoverable hazards into the new health path while preserving `TriggerHazardDeath()` for instant-kill hazards |
| Commit C | Add `GameManager.BeginHazardRecoverySequence()` and a dedicated in-progress guard; ensure it does not restore health |
| Commit D | Harden `HeroController.ResetAfterRespawn()` or add a focused equivalent used by both full respawn and local recovery |
| Commit E | Add `HazardRespawnTrigger` and `GameManager` active hazard marker support if you want the authoring convenience right away |
| Commit F | Update architecture/save/player-controller docs to match the landed behaviour |

That split keeps the first behaviour change small, testable, and reversible. fileciteturn22file0L3-L3

## Unity setup and playtest

For the **minimal first pass**, authoring should stay simple. Place a `HazardRespawnMarker` near each recoverable pit/spike/acid/lava return point. Give each recoverable `HazardZone` a direct reference to the correct marker, set `HazardRecoveryMode = Recoverable`, and keep `hazardDamage = 1` unless you intentionally want a harsher zone. Leave true kill volumes on `InstantDeath`. This keeps setup explicit and makes scene-by-scene rollout safe because old hazards keep their current behaviour until you opt them in. That matters because the current system treats every hazard as full death. fileciteturn6file0L3-L3 fileciteturn9file0L3-L3

When you add the trigger-based refinement, use `HazardRespawnTrigger` only where it reduces authoring repetition. Typical examples are: one safe ledge that should serve several nearby spike/pit hazards, or a room where the “correct” local return point changes as the player traverses upward. In that version, keep the trigger-updated marker as primary and the zone’s direct marker as fallback. That gives level design the Hollow Knight feel without making every scene depend on fragile global state. This is also more in line with the project’s goal of keeping `HeroController` as a coordinator rather than a state owner. fileciteturn39file0L3-L3 fileciteturn21file0L3-L3

The manual playtest checklist I would use is:

1. Enter a recoverable hazard at full health and confirm: health drops by one, hero fades out, reappears at the local hazard marker, and does **not** full-heal. fileciteturn10file0L3-L3 fileciteturn9file0L3-L3  
2. Repeat until one health remains and confirm each recovery is still local and health keeps decreasing by one. fileciteturn9file0L3-L3  
3. Enter the same hazard at one health and confirm: health reaches zero, death animation/audio path runs, and the hero respawns at the normal checkpoint/full respawn marker with full health. fileciteturn19file0L3-L3 fileciteturn10file0L3-L3  
4. Take normal enemy/contact damage and confirm the ordinary `TakeDamage()` path still grants i-frames, hurt knockback, and hurt recovery exactly as before. fileciteturn8file0L3-L3 fileciteturn9file0L3-L3 fileciteturn19file0L3-L3  
5. Activate a checkpoint, die normally, and confirm the current full checkpoint respawn flow still restores full health and uses the saved normal respawn marker. fileciteturn17file0L3-L3 fileciteturn10file0L3-L3  
6. Hit a recoverable hazard while attacking, dashing, wall-sliding, or recoiling and confirm you do not reappear in a stuck movement/animation state. This is the test that validates the strengthened hero reset. fileciteturn31file0L3-L3 fileciteturn34file0L3-L3 fileciteturn36file0L3-L3  
7. Confirm repeated hazard entry during fade or while recovery is already in progress does not subtract health twice or start a second coroutine. This validates the new dedicated guard. fileciteturn10file0L3-L3 fileciteturn38file0L3-L3  
8. Confirm instant-death hazards remain instant death even above one health.  
9. Remove a hazard marker intentionally and confirm the fallback path is clear in behaviour and logs a warning.  
10. Transition between rooms/scenes and confirm normal checkpoint respawn is unaffected; if trigger-based hazard markers are added later, also confirm stale markers do not carry across scene loads. fileciteturn10file0L3-L3 fileciteturn13file0L3-L3

## Risks and follow-up improvements

The biggest short-term risk is that the current health events mix **health change** and **hurt reaction** too tightly. `OnDamaged` currently means “the hero should enter hurt state and receive knockback”, not just “health changed”. That is one more reason to keep recoverable hazards off the normal damage path, but it also means that once HUD work lands you will probably want a more neutral event such as `OnHealthChanged(int currentHealth)` or a richer damage-context event. Until then, the minimal implementation is fine because the HUD system has not started yet. fileciteturn9file0L3-L3 fileciteturn19file0L3-L3 fileciteturn22file0L3-L3

The second risk is reset completeness. As audited, the current reset path is just enough for post-death cleanup, not necessarily for mid-session local reposition. If you do not harden it, you risk lingering hurt coroutines, preserved velocity, stale attack flags, and wrong animation choice after hazard recovery. This is the most important code-hardening task in the feature. fileciteturn19file0L3-L3 fileciteturn31file0L3-L3 fileciteturn36file0L3-L3

The third risk is authoring failure: a marker placed inside a hazard, too close to the pit lip, or facing the wrong direction can create immediate relaunch loops. The minimal defence is the recovery-in-progress guard plus good marker placement. A later polish pass can add a short post-recovery hazard suppression window if level design makes that necessary. A trigger-based system can also help by moving the authoring focus away from every individual zone and onto safer shared return points.

A related design boundary is persistence. The repo does **not** currently persist current player health through saves; the architecture docs already note that a future `PlayerHealthState` would own that. So local hazard recovery should preserve reduced health **within the current scene/session only**, and full death should continue to restore full health during same-scene respawn. Do not try to fold save-backed health persistence into this feature. fileciteturn21file0L3-L3 fileciteturn22file0L3-L3 fileciteturn23file0L3-L3

Longer term, if the respawn system grows to handle checkpoints, hazard recovery, room-entry recovery, benches, and save-backed health state, then a small `RespawnService` would become reasonable. It is **not** the right move for this first pass. Right now, the safest move is to land the mechanic inside the boundaries the repo already has: health math in `HeroHealthComponent`, ingress in `HeroBox`, and placement/fade/camera in `GameManager`. fileciteturn21file0L3-L3 fileciteturn22file0L3-L3

The shortest path to the behaviour you want, without overbuilding, is therefore:

- create `HazardRespawnMarker` and `HazardRecoveryMode` now;  
- keep `TriggerHazardDeath()` for true instant-death hazards;  
- add a new explicit recoverable hazard path instead of reusing `TakeDamage()`;  
- start with direct zone-to-marker references;  
- add trigger-updated active hazard markers only after the direct path is proven;  
- keep SaveManager out of the first implementation, even though the schema already has room for it later.  

That lands the Hollow Knight-style local hazard loop in a way that fits the current repository rather than fighting it. fileciteturn6file0L3-L3 fileciteturn8file0L3-L3 fileciteturn9file0L3-L3 fileciteturn10file0L3-L3 fileciteturn13file0L3-L3
