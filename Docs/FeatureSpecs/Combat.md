# Feature Spec — Combat

**Last audited:** 2026-07-21

## Responsibilities

Melee combat for the hero: swing initiation, directional hit detection, VFX/SFX activation, hit delivery to targets, and clash detection.

---

## Key Classes

| Class | Role |
|---|---|
| `HeroAttackAction` | State machine for a single swing (plain C# class) |
| `HeroAttackModule` | Scene GameObject representing one directional hitbox |
| `HeroResourceGenerationMode` | Per-attack resource award policy |
| `HeroAttackHit` | Value type carrying hit data to receivers |
| `HeroAttackResult` | Result value returned by attack receivers |
| `IHeroAttackReceiver` | Interface for anything that can take damage |
| `IHeroAttackClashReceiver` | Interface for parry/block reactions |
| `IHeroDownslashResponder` | Interface for downslash-specific reactions (e.g. bounce) |

---

## Attack Flow

1. `HeroAttackAction.Tick` checks `AttackPressedThisFrame` and `CanStartAttack` each `Update`.
2. On start: determine direction (Side / Up / Down) from `MoveVector.y` vs `attackDirectionThreshold`.
3. Find the matching `HeroAttackModule` by direction.
4. Activate the module: enable visual, play VFX animation, play SFX, mirror geometry for facing.
5. `HeroAnimationController` detects `blackboard.attacking` and plays the corresponding body clip.
6. The animation end-event calls `CompleteAttackFromAnimation` → `StopAttack`.
7. A fail-safe timer forces the attack to end if the animation event does not arrive.

### Hit Window

- The damage `PolygonCollider2D` is enabled only during the declared hit window.
- The window can be opened/closed by animation events routed through `HeroActionController.BeginAttackWindow` / `EndAttackWindow`.
- Without explicit animation events, `HeroAttackAction.FixedTick` enables the collider while `attackWindowActive` is true.
- `PolygonCollider2D.Overlap` is called every `FixedUpdate` during the window.
- Each `IHeroAttackReceiver` is added to a `HashSet` on first hit; subsequent overlaps the same swing are ignored.

---

## Attack Direction

```
MoveVector.y >= attackDirectionThreshold  →  Up
MoveVector.y <= -attackDirectionThreshold →  Down
otherwise                                 →  Side
```

Direction is locked at swing start. The active `HeroAttackModule` is the first module in the array whose `direction` matches.

---

## Attack Modules (Scene Setup)

Each `HeroAttackModule` lives under an "Attacks" child of the hero. Defaults:

| Name | Direction | mirrorWithFacing |
|---|---|---|
| SlashSide | Side | true |
| SlashUp | Up | false |
| SlashDown | Down | false |

Use **Context Menu → "Create Default Attack Modules"** on `HeroActionController` to scaffold them. Each module needs:
- `PolygonCollider2D` (isTrigger, starts disabled)
- Optional `clashCollider` (separate PolygonCollider2D for parry zone)
- `AnimancerComponent` for the VFX animation clip
- Layer masks: `damageLayers`, `clashLayers`

### Resource Generation

Each attack module carries a `HeroResourceGenerationMode` and integer `resourceGainParts` value. `None` never awards resource. `PerSuccessfulTarget` awards the configured parts once for each distinct receiver whose `HeroAttackResult` is accepted and resource-eligible. `FirstSuccessfulHitPerAttack` awards once for the first eligible result in the attack execution, including across later hit windows. The tracker resets when a new attack starts.

`HeroAttackAction` owns the award decision and receives the injected `PlayerResourceState`; receivers never call back into hero-side resource code. Existing per-swing receiver deduplication happens before result handling, so multiple colliders on one target cannot award more than once. Awards are clamped by `PlayerResourceState.Gain`, and no award occurs for misses, rejected results, clashes, pogo by itself, or non-positive configured gain. Resource generation does not change damage, feedback, death, or attack timing.

---

## Hit Delivery

`HeroAttackHit` is passed to `IHeroAttackReceiver.ReceiveHeroAttack`, which returns a `HeroAttackResult`. It contains:
- `Source` — hero GameObject
- `Direction` — Side / Up / Down
- `Damage` — from `HeroConfig.attackDamage`
- `Point` — closest point on the hit collider to the damage reference point
- `ForceDirection` — normalised vector away from the hero (used for knockback)

`HeroAttackResult.Outcome` is `Ignored`, `Blocked`, `Invulnerable`, `Damaged`, or `Killed`. The current repository's `EnemyHealthComponent` returns `Ignored` for invalid or already-dead targets, `Damaged` for accepted nonlethal damage, and `Killed` when accepted damage causes death. `Blocked` and `Invulnerable` are reserved for future receiver implementations; no blocking or target-invulnerability system is implemented here. `DamageApplied` is the positive amount actually accepted, and `ResourceEligible` is true only for accepted `Damaged` or `Killed` outcomes. `HeroAttackResult` itself never mutates `PlayerResourceState` — the receiver only reports eligibility; `HeroAttackAction` reads that flag and decides whether to call `PlayerResourceState.Gain` per its configured `HeroResourceGenerationMode` (see Resource Generation above).

If direction is Down and the hit target also implements `IHeroDownslashResponder`, `ReceiveHeroDownslash` is called on the same frame.

### First-Connect Impact Feel

`HeroAttackAction` owns global attack-connect feel through `connectFeedbackPlayedThisSwing`. On the first accepted enemy hit (`Damaged` or `Killed`) or clash in a swing, it:

- calls `GameManager.HitStop` using `HeroConfig.attackHitStopDuration` or `attackClashHitStopDuration`
- requests `CameraShakeIntensity.Small` through `CameraEventService`
- calls `HeroAttackImpactFeedbackController.PlayConnectFeedback` for optional non-camera feedback

Enemy health components must not trigger generic player-attack hit-stop or camera shake. They own target-local results such as damage, flash, hurt/death audio, enemy feedback, recoil, and death.

The receiver is added to the per-swing `HashSet` before the result is returned. This preserves one interaction per target and terrain-impact suppression even when a receiver returns `Ignored`. Downslash notification and pogo eligibility remain independent of the result; clash receivers continue through their separate contract.

Terrain hits are handled separately by `HeroAttackAction.EvaluateTerrainImpact()`, which plays terrain impact feedback and calls `HeroAudioController.PlayTerrainImpact()` once per swing when the attack whiffs into terrain.

---

## Timers

| Timer | Config field | Purpose |
|---|---|---|
| `cooldownTimer` | `attackCooldown` | Minimum time between swing starts |
| `recoveryTimer` | `attackRecovery` | Post-attack window where combo chaining is blocked |
| `attackFallbackTimeout` | `attackFailSafeTimeout` (field on `HeroActionController`) | Safety net if animation event never fires |

---

## Extension Points

- **New hit-reaction type** — add a new interface (e.g. `IHeroUpslashResponder`) following the same pattern as `IHeroDownslashResponder`.
- **Combo system** — `HeroAttackAction.attackVersion` increments each swing; the animation controller already keys off it. A combo system could inspect version and recovery state.
- **Projectile** — would be a new Action class, not a new module type.

## Stage 2 Placeholder VFX

Combat impact VFX are authored as lightweight ParticleSystem prefabs under `Assets/_Project/Prefabs/VFX/Combat/` with URP-compatible materials under `Assets/_Project/Materials/VFX/`.

- Enemy-local hit, pogo, body-hit, and death visuals are triggered by `EnemyFeedbackController` through MMF players.
- Terrain-only slash impacts are triggered by `HeroAttackImpactFeedbackController` through its directional terrain MMF players.
- Terrain impact positions are resolved from the active slash collider toward the attack direction so impacts land on the terrain surface edge, not at the middle of the overlap. If the slash starts inside thin terrain, the impact falls back to the near directional bounds edge (top edge for downslash, underside for upslash, near wall face for side slash).
- These feedbacks must not include camera shake or hit-stop; global attack-connect feel remains owned by `HeroAttackAction`.

## Stage 3 Slash Arc Presentation

The hero slash arcs remain owned by the existing `SlashSide`, `SlashUp`, and `SlashDown` `HeroAttackModule` GameObjects. Each module keeps its hitbox, root transform, Animancer component, animation clip, slash SFX, and attack-window animation events.

For visual-only polish, each module has a `SlashArcVisual` child containing the visible `SpriteRenderer`. The slash animation clips target this child by path, while animation events still fire on the module root so hit-window timing remains unchanged.

- `SlashArcVisual` handles tint, alpha fade, and sorting.
- `SlashArcVisual` should stay at neutral local position and scale unless the attack polygon is intentionally adjusted at the same time.
- Module root `SpriteRenderer` components are disabled to avoid duplicate arcs.
- Slash arc sorting is above hero/enemy sprites and below the Stage 2 hit spark particles.
- Visual arc placement and the attack polygon must stay aligned. If the arc needs major repositioning, move or reauthor the module root/collider together with the visual.

---

## Rules

- Never enable a `HeroAttackModule`'s damage collider outside of `HeroAttackAction`.
- Do not query `attackHitLayers` directly from outside `HeroAttackAction` or `HeroAttackModule`.
- Enemy implementations of `IHeroAttackReceiver` must be idempotent — the same hit struct may arrive once or not at all, but never twice per swing per target.
