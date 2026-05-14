# Feature Spec — Enemy AI

## Responsibilities

Enemy perception, decision-making, movement, and reaction to hero attacks.

---

## Current State

No enemy system exists. This spec describes the intended design.

---

## Design Goals

- Enemies are self-contained; they do not need to know about `HeroController` internals
- Hit reactions are received through the established interface layer (`IHeroAttackReceiver` etc.)
- Enemy configuration is data-driven via ScriptableObjects
- Behaviour is readable from the outside — favour simple state machines over complex trees for prototype scope

---

## Intended Architecture

```
EnemyController (MonoBehaviour — per-enemy coordinator)
├── EnemyConfig (SO)          movement speed, detection range, attack data, health
├── EnemyStateBlackboard      runtime flags (alerted, attacking, hurt, recoiling, dead)
├── EnemyMotor                Rigidbody2D or NavMeshAgent velocity control
├── EnemyPerception           detect hero via overlap / raycast; write to blackboard
├── EnemyBehaviour            state machine: Idle → Patrol → Chase → Attack → Hurt → Dead
├── EnemyHealthComponent      MonoBehaviour; implements IHeroAttackReceiver
├── EnemyRecoil               MonoBehaviour; hit freeze / knockback / stun recovery
├── DamageHero                MonoBehaviour; damage metadata for hero-hurting colliders
└── EnemyContactDamage        MonoBehaviour; persistent body-touch damage behaviour
```

### Hit-Reaction Integration

`EnemyHealthComponent` implements `IHeroAttackReceiver`:
- Subtract `hit.Damage` from current health
- Play hit feedback and forward the hit to `EnemyRecoil`
- Trigger death handling when health reaches zero
- Raise `OnDamaged` for non-lethal hits and `OnDeath` when death starts, so enemy-specific behaviour scripts can handle visuals without owning health rules

`EnemyRecoil` owns hit reaction:
- Apply `hit.ForceDirection` as a knockback impulse, or freeze in place for enemies configured that way
- Transition blackboard to Hurt / Recoiling state; return to behaviour after stun duration
- Write recoil velocity directly to the enemy `Rigidbody2D`; movement behaviours must skip their normal velocity writes while `blackboard.recoiling` is true
- Expose `EnemyRecoilState` (`Ready`, `Frozen`, `Recoiling`), `IsRecoiling`, and `OnRecoilEnded` for debugging and behaviour coordination
- Support config toggles for `preventUpwardRecoil` and `stopHorizontalVelocityOnUpwardRecoil`

`DamageHero` marks a collider as capable of hurting the hero and stores shared damage metadata. Behaviour-specific scripts consume it:
- `EnemyContactDamage` handles always-on body touch damage, including cooldown and contact knockback
- Future attack hitbox scripts can use the same `DamageHero` data with attack-window timing instead of contact cooldown timing

`EnemyHealthComponent` may also implement `IHeroDownslashResponder` if the enemy should launch the hero upward on a downslash (e.g. bouncy enemies, head-stomp mechanic).

### Clash Behaviour (optional)

If an enemy can parry or deflect the hero, its collider registers as `IHeroAttackClashReceiver`. The clash response is defined per enemy type.

---

## Enemy Configuration SO

```csharp
[CreateAssetMenu(menuName = "Enemy/Enemy Config")]
public sealed class EnemyConfig : ScriptableObject
{
    public int maxHealth;
    public float moveSpeed;
    public float detectionRange;
    public float attackRange;
    public float attackDamage;
    public float stunDuration;
    public float deathDestroyDelay;
    public bool freezeOnHit;
    public bool preventUpwardRecoil;
    public bool stopHorizontalVelocityOnUpwardRecoil;
    // ... additional per-type fields
}
```

---

## Perception

- `EnemyPerception` casts an overlap circle or raycast towards the hero's last known position.
- On detection, set `blackboard.alerted = true`.
- Line-of-sight check against terrain layers recommended to prevent detection through walls.
- TODO: decide whether enemies share a global alert system or operate independently.

---

## State Machine States

| State | Transitions |
|---|---|
| Idle | → Patrol (timer), → Chase (hero detected) |
| Patrol | → Chase (hero detected) |
| Chase | → Attack (in range), → Idle (lost hero) |
| Attack | → Chase (attack complete), → Hurt (hit during attack) |
| Hurt | → Chase (stun expired), → Dead (health ≤ 0) |
| Dead | terminal — play death animation, spawn drops, disable |

---

## Dependencies

- `IHeroAttackReceiver` / `IHeroDownslashResponder` — defined in `Scripts/Hero/Combat/`
- `EnemyConfig` SO (TODO)
- Hero `Transform` (read only, for detection targeting)

---

## Extension Points

- **New enemy type** — new `EnemyConfig` SO + override of `EnemyBehaviour` state machine, or a subclass.
- **Boss** — extend `EnemyBehaviour` with phase transitions keyed on health thresholds.
- **New hit-reaction interface** — add to `Scripts/Hero/Combat/` and implement in `EnemyHealthComponent`.

---

## Rules

- `EnemyController` must not call any method on `HeroController` or any hero subsystem except through the defined interfaces.
- Enemy health and configuration are fully data-driven through `EnemyConfig` SO — no hard-coded values in MonoBehaviours.
- Death must be handled gracefully: disable physics and AI before destroying the GameObject (avoid one-frame physics glitches).
