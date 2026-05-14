# Feature Spec — Combat

## Responsibilities

Melee combat for the hero: swing initiation, directional hit detection, VFX/SFX activation, hit delivery to targets, and clash detection.

---

## Key Classes

| Class | Role |
|---|---|
| `HeroAttackAction` | State machine for a single swing (plain C# class) |
| `HeroAttackModule` | Scene GameObject representing one directional hitbox |
| `HeroAttackHit` | Value type carrying hit data to receivers |
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

---

## Hit Delivery

`HeroAttackHit` is passed to `IHeroAttackReceiver.ReceiveHeroAttack`. It contains:
- `Source` — hero GameObject
- `Direction` — Side / Up / Down
- `Damage` — from `HeroConfig.attackDamage`
- `Point` — closest point on the hit collider to the damage reference point
- `ForceDirection` — normalised vector away from the hero (used for knockback)

If direction is Down and the hit target also implements `IHeroDownslashResponder`, `ReceiveHeroDownslash` is called on the same frame.

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

---

## Rules

- Never enable a `HeroAttackModule`'s damage collider outside of `HeroAttackAction`.
- Do not query `attackHitLayers` directly from outside `HeroAttackAction` or `HeroAttackModule`.
- Enemy implementations of `IHeroAttackReceiver` must be idempotent — the same hit struct may arrive once or not at all, but never twice per swing per target.
