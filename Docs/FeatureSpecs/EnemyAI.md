# Feature Spec — Enemy AI

## Responsibilities

Enemy perception, decision-making, movement, and reaction to hero attacks.

---

## Current State

The first enemy path is implemented and prefab-ready for Mushroom enemies:

- `EnemyController` coordinates `EnemyConfig`, `EnemyStateBlackboard`, `EnemyRecoil`, and `EnemyHealthComponent`.
- `MushroomEnemy` is the current movement/animation behaviour and implements `IEnemyBehaviour`.
- `EnemyHealthComponent` owns damage, death, flash, hurt/death audio, enemy-local feedback, recoil forwarding, and downslash response.
- `EnemyFeedbackController` owns enemy-local hit, pogo, body-hit, and death MMF players.
- `DamageHero` and `EnemyContactDamage` provide body-touch damage to the hero.
- `Assets/_Project/Prefabs/Enemies/Mushroom.prefab` is the reusable authoring pattern created from the validated `SampleScene` setup.

Enemy perception/chase/attack behaviours are still future work; the current Mushroom patrol behaviour is intentionally simple.

---

## Design Goals

- Enemies are self-contained; they do not need to know about `HeroController` internals
- Hit reactions are received through the established interface layer (`IHeroAttackReceiver` etc.)
- Enemy configuration is data-driven via ScriptableObjects
- Behaviour is readable from the outside — favour simple state machines over complex trees for prototype scope

---

## Implemented Architecture

```
EnemyController (MonoBehaviour — per-enemy coordinator)
├── EnemyConfig (SO)          movement speed, detection range, attack data, health
├── EnemyStateBlackboard      runtime flags (alerted, attacking, hurt, recoiling, dead)
├── IEnemyBehaviour           current implementation: MushroomEnemy patrol behaviour
├── EnemyHealthComponent      MonoBehaviour; implements IHeroAttackReceiver
├── EnemyRecoil               MonoBehaviour; hit freeze / knockback / stun recovery
├── DamageHero                MonoBehaviour; damage metadata for hero-hurting colliders
├── EnemyContactDamage        MonoBehaviour; persistent body-touch damage behaviour
└── EnemyFeedbackController   MonoBehaviour; enemy-local MMF feedback players
```

### Hit-Reaction Integration

`EnemyHealthComponent` implements `IHeroAttackReceiver`:
- Subtract `hit.Damage` from current health
- Play hit flash/audio/feedback and forward non-lethal hits to `EnemyRecoil`
- Trigger death handling when health reaches zero
- Raise `OnDamaged` for non-lethal hits and `OnDeath` when death starts, so enemy-specific behaviour scripts can handle visuals without owning health rules
- Implement `IHeroDownslashResponder` so downslash/pogo feedback can play through the same enemy-local feedback controller

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
    public float knockbackForce;
    public float knockbackLift;
    public float stunDuration;
    public float hitStopDuration;
    public bool freezeOnHit;
    public bool preventUpwardRecoil;
    public bool stopHorizontalVelocityOnUpwardRecoil;
    public EnemyDeathType deathType;
    public float deathDestroyDelay;
    public AudioClip hurtSfx;
    public AudioClip deathSfx;
}
```

---

## Perception

Not implemented yet.

- `EnemyPerception` casts an overlap circle or raycast towards the hero's last known position.
- On detection, set `blackboard.alerted = true`.
- Line-of-sight check against terrain layers recommended to prevent detection through walls.
- TODO: decide whether enemies share a global alert system or operate independently.

---

## State Machine States

The current Mushroom behaviour is a patrol/turn loop with hurt/recoil/death suppression. Chase and attack states are still future work.

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
- `EnemyConfig` SO
- Hero `Transform` (read only, for detection targeting)

---

## Extension Points

- **New enemy type** — new `EnemyConfig` SO + a component implementing `IEnemyBehaviour`.
- **Boss** — extend `EnemyBehaviour` with phase transitions keyed on health thresholds.
- **New hit-reaction interface** — add to `Scripts/Hero/Combat/` and implement in `EnemyHealthComponent`.
- **Enemy-specific impact profiles** — defer until there are at least 2-3 enemy families or a boss that needs distinct hit/death feedback.

---

## Rules

- `EnemyController` must not call any method on `HeroController` or any hero subsystem except through the defined interfaces.
- Enemy health and configuration are fully data-driven through `EnemyConfig` SO — no hard-coded values in MonoBehaviours.
- Death must be handled gracefully: disable physics and AI before destroying the GameObject (avoid one-frame physics glitches).
- Enemy body colliders live on the `Enemies` layer. Physics 2D disables `Enemies` vs `Enemies`, `Enemies` vs `Enemy Attack`, and `Enemy Attack` vs `Enemy Attack`; enemies still collide with `Terrain`, `Hero Box`, and `Hero Attack`.
- Enemy prefabs should set the root and all current physics children to `Enemies` recursively. Future attack hitbox children should use `Enemy Attack` and should normally be triggers.
