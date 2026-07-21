# Feature Spec — Enemy AI

**Last audited:** 2026-06-05

## Responsibilities

Enemy perception, decision-making, movement, and reaction to hero attacks.

---

## Current State

The enemy locomotion/reaction loop and reusable authored-attack runtime are implemented:

- `EnemyController` coordinates `EnemyConfig`, `EnemyStateBlackboard`, `EnemyRecoil`, `EnemyHealthComponent`, `EnemyMotor`, `EnemyPerception`, and optional `EnemyAttackController`.
- `EnemyMotor` centralized Rigidbody2D velocity control, handles horizontal movement, stopping, external velocity (knockback/recoil override), and handles visual sprite scale flipping.
- `EnemyPerception` handles line-of-sight based detection using overlap circles and obstruction-aware raycasting.
- `MushroomEnemy` is the first/basic concrete `IEnemyBehaviour` implementation and foundation validation target, coordinating a state machine (Idle, Patrol, Chase, Attack, Hurt, Dead).
- `EnemyAttackController` coordinates authored attack timing (`Startup -> Active -> Recovery -> Cooldown`), exposes animation-event methods, closes hitboxes on interrupt, and owns timer fallbacks.
- `EnemyAttackHitbox` uses `DamageHero` metadata, damages `HeroBox` only during active windows, and shares per-window duplicate-hit prevention through `EnemyAttackController`.
- `Tools/Project/Validate Enemy AI Foundation` validates the Mushroom prefab/config, contact damage setup, authored attack hitbox setup, and Physics 2D enemy attack matrix.
- `EnemyHealthComponent` owns damage, death, flash, hurt/death audio, enemy-local feedback, recoil forwarding, and downslash response.
- `EnemyFeedbackController` owns enemy-local hit, pogo, body-hit, and death MMF players.
- `DamageHero` and `EnemyContactDamage` provide body-touch damage to the hero.
- `Assets/_Project/Prefabs/Enemies/Mushroom.prefab` is the reusable authoring pattern and first explicit-attacker validation prefab.

Mushroom retains body-contact damage and now also implements a simple authored Attack state using one `Mushroom Attack` animation clip, `EnemyAttackController`, and a disabled-by-default child `EnemyAttackHitbox`. This foundation path has been manually validated in Unity.

### Manual Unity Validation

Manually checked in Unity on 2026-06-05:

- `Tools/Project/Validate Enemy AI Foundation` passed.
- Mushroom patrol works.
- Mushroom detection/chase works.
- Mushroom enters authored attack at range.
- Attack startup keeps the attack hitbox inactive.
- Active window enables the attack hitbox and damages the hero.
- Duplicate damage is blocked within one active window.
- Later attack windows can damage again after cooldown/i-frames.
- Contact damage remains intentional and works alongside authored attacks.
- Contact damage and authored attack damage do not double-hit unfairly.
- Hurt/death interrupt authored attacks and close attack hitboxes.
- Death disables contact and attack damage.
- Downslash pogo remains reliable.
- Hero feel values were not changed.

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
├── EnemyStateBlackboard      runtime flags: alerted, hurt, recoiling, attacking, attackWindowActive, dead
├── IEnemyBehaviour           current implementation: MushroomEnemy
├── EnemyHealthComponent      MonoBehaviour; implements IHeroAttackReceiver
├── EnemyRecoil               MonoBehaviour; hit freeze / knockback / stun recovery
├── EnemyAttackController     optional authored attack-window runtime
├── EnemyAttackHitbox         optional active-window hero damage collider
├── DamageHero                MonoBehaviour; damage metadata for hero-hurting colliders
├── EnemyContactDamage        MonoBehaviour; persistent body-touch damage behaviour
└── EnemyFeedbackController   MonoBehaviour; enemy-local MMF feedback players
```

### Movement Authority

`EnemyMotor` is the preferred authority for enemy `Rigidbody2D` velocity writes during normal gameplay.

- Enemy behaviours request patrol/chase movement through `EnemyMotor.MoveHorizontal`, `StopHorizontal`, and `Flip`.
- `EnemyRecoil` may temporarily override movement through `EnemyMotor.ApplyExternalVelocity`.
- `EnemyRecoil` must clear that override when recoil ends or death cancels recoil.
- Behaviour scripts must skip locomotion while `blackboard.recoiling`, `blackboard.dead`, or `motor.ExternalVelocityActive` is true.
- Behaviour scripts should not write `Rigidbody2D.linearVelocity` directly except as temporary legacy fallback when no `EnemyMotor` exists.
- Terminal death handling may zero velocity and disable/convert physics inside `EnemyHealthComponent`, because death is a physics shutdown rather than locomotion.

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
- Apply recoil velocity through `EnemyMotor` when present; movement behaviours must skip their normal velocity writes while `blackboard.recoiling` or `motor.ExternalVelocityActive` is true
- Expose `EnemyRecoilState` (`Ready`, `Frozen`, `Recoiling`), `IsRecoiling`, and `OnRecoilEnded` for debugging and behaviour coordination
- Support config toggles for `preventUpwardRecoil` and `stopHorizontalVelocityOnUpwardRecoil`

`DamageHero` marks a collider as capable of hurting the hero and stores shared damage metadata. Behaviour-specific scripts consume it:
- `EnemyContactDamage` handles always-on body touch damage, including cooldown and contact knockback
- `EnemyAttackHitbox` uses the same `DamageHero` data with attack-window timing instead of contact cooldown timing

`EnemyHealthComponent` may also implement `IHeroDownslashResponder` if the enemy should launch the hero upward on a downslash (e.g. bouncy enemies, head-stomp mechanic).

### Clash Behaviour (optional)

If an enemy can parry or deflect the hero, its collider registers as `IHeroAttackClashReceiver`. The clash response is defined per enemy type.

### Contact Damage and Authored Attacks

`EnemyContactDamage` is always-on body-touch damage with a local cooldown. It is appropriate for simple walkers like Mushroom.

Explicit authored attacks must not use contact-damage timing. They use attack hitboxes that are disabled by default and enabled only during the attack's active window.

Mushroom intentionally has both damage paths:

- Body contact damage: `EnemyContactDamage` + root `DamageHero` -> `HeroBox` -> `HeroHealthComponent`
- Authored attack damage: child `EnemyAttackHitbox` + child `DamageHero` -> `HeroBox` -> `HeroHealthComponent`

These paths must remain separate and may coexist safely. Neither path may reference `HeroController`. `EnemyContactDamage` has its own local cooldown to avoid physics-overlap spam. `EnemyAttackHitbox` has per-active-window duplicate-hit prevention. `HeroBox` buffers same-step enemy damage before forwarding one hit to `HeroHealthComponent`, so contact and authored attack damage that arrive in the same physics moment do not double-hit unfairly. `HeroHealthComponent` then grants i-frames so later contact/attack attempts during invincibility are ignored.

On death, `EnemyHealthComponent` must interrupt authored attacks, disable contact-damage components, and disable colliders so both body-contact and attack-hitbox damage are stopped.

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

    // Behaviour / detection
    public float patrolSpeed;
    public float chaseSpeed;
    public float patrolIdleTime;
    public float chaseTimeout;
    public float attackRange;
    public float attackCooldown;
    public float detectionRadius;
    public LayerMask terrainLayers;
}
```

---

## Perception

Implemented.

- `EnemyPerception` queries for target colliders in a radius defined by `EnemyConfig.detectionRadius` matching the `heroLayers` mask.
- If a target is found, it performs a linecast from `eyePoint` (or the enemy's collider center) to the target's collider bounds center.
- The linecast checks for blockers matching `resolvedObstructionMask` (derived from `lineOfSightBlockers`, falling back to `EnemyConfig.terrainLayers`, then `"Terrain"`).
- If clear line-of-sight is established, `IsHeroDetected` is set to `true`, `LastKnownHeroPosition` is updated, and the `HeroDetected` event is fired.
- If line-of-sight is lost or the target leaves the detection radius, `IsHeroDetected` is set to `false` and the `HeroLost` event is fired.
- Surfaces a startup configuration warning if the resolved obstruction mask is `0` (which would allow seeing through walls).

Future perception additions should preserve the Silksong-style split between proximity, visibility, and room activation:

- **Detection radius** answers whether a target is nearby enough to care about.
- **Line of sight** answers whether terrain blocks the target.
- **Attack range** belongs to behaviour/attack decisions, not the sensor itself.
- **Lost-sight grace** may be added to avoid flicker when a target crosses small collider seams.
- **Chase timeout** belongs to behaviour memory after the target is truly lost.
- **Wake/sleep** is a room or scene ownership concern and should not be folded into `EnemyPerception`.

---

## State Machine States

`MushroomEnemy` is implemented using a custom state machine and is the explicit-attacker validation archetype. It uses the patrol/chase/hurt/death pattern, stops at `EnemyConfig.attackRange`, starts `EnemyAttackController`, plays one authored attack clip, and returns to chase/patrol after completion. It remains intentionally small and enemy-specific; it proves the shared primitives without becoming a generic enemy brain.

| State | Status | Description & Transitions |
|---|---|---|
| Idle | Implemented | Enemy stops horizontal movement. Plays idle animation. Transitions to `Patrol` after `EnemyConfig.patrolIdleTime`. |
| Patrol | Implemented | Moves horizontally using `EnemyMotor` at `EnemyConfig.patrolSpeed`. Turns and transitions to `Idle` upon hitting a wall or ledge. Transitions to `Chase` if the hero is detected. |
| Chase | Implemented | Chases the hero at `EnemyConfig.chaseSpeed`. If the hero is in line-of-sight, moves toward the hero. If the hero is lost, continues to `LastKnownHeroPosition` and starts `chaseTimeout` countdown. Transitions to `Idle` on reaching the destination, upon timeout, or if a wall/ledge is reached. |
| Attack | Implemented for `MushroomEnemy` | Enters from Chase when the hero is detected inside `attackRange`. Stops movement, starts `EnemyAttackController`, opens only the child attack hitbox during Active, and returns to Chase/Patrol after Recovery/Cooldown. |
| Hurt | Implemented | Stops movement and plays hit animation. Suppresses motor input during recoil knockback. Exits back to `Chase`/`Patrol` via `OnRecoilEnded` or a safety fallback timer if recoil recovery fails. |
| Dead | Implemented | Terminal state. Disables physics/AI immediately. Plays death animation, applies vertical visual offset, and destroys after `EnemyConfig.deathDestroyDelay`. |

---

## Explicit Enemy Attack Windows

Enemies with authored attacks use a four-phase attack model:

| Phase | Purpose | Rules |
|---|---|---|
| Startup | Telegraph the attack. | Play windup animation/SFX/feedback. Hitboxes disabled. Movement stopped or constrained. |
| Active | The attack can damage the hero. | Enable `EnemyAttackHitbox` / attack group. Clear duplicate-hit tracking at the start of the window. |
| Recovery | The attack is no longer dangerous, but the enemy is committed. | Disable hitboxes. Keep the recovery readable before chase/patrol resumes. |
| Cooldown | Prevent immediate re-attack. | Enemy may move if behaviour allows, but cannot start the same attack until cooldown expires. |

Attack windows may be driven by animation/Animancer events, but every attack also has timer fallbacks so a missing animation event cannot leave a hitbox enabled or an enemy stuck attacking.

Mushroom uses one authored attack animation clip, `Mushroom Attack`, rather than separate Startup / Active / Recovery clips. The clip should contain these animation events:

- `OpenAttackWindow`
- `CloseAttackWindow`
- `CompleteAttack`

If the clip plays on the same GameObject as `EnemyAttackController`, those events can call the controller directly. If the clip plays on a child object, add `EnemyAttackAnimationEvents` to that animated child and assign or let it find the root `EnemyAttackController`; the bridge forwards the same three event methods.

Explicit attack hitboxes:

- Are disabled by default.
- Use `DamageHero` for damage metadata.
- Apply damage through the existing hero damage path (`HeroBox` / `HeroHealthComponent`), never by calling `HeroController`.
- Track damaged hero receivers in a per-window set so one attack window cannot hit the hero repeatedly.
- Disable immediately when the enemy is hurt, recoils, dies, sleeps, or otherwise interrupts the attack.
- Remain separate from `EnemyContactDamage`.

Implemented components:

```
EnemyAttackController
├── coordinates Startup / Active / Recovery / Cooldown
├── exposes animation-event methods: OpenAttackWindow, CloseAttackWindow, CompleteAttack
├── owns timer fallback / interrupt cleanup
└── drives one or more EnemyAttackHitbox components

EnemyAttackHitbox
├── requires DamageHero
├── disabled outside active windows
├── damages HeroBox during active windows
└── prevents duplicate hero damage per active window
```

Current limitations:
- Mushroom's single attack clip has working first-pass animation events; exact frame positions can still be tuned for feel during polish.
- `EnemyAttackController` fallback timings are absolute seconds. If an attack clip's length or playback speed changes, keep `startupDuration`, `activeDuration`, and `recoveryDuration` aligned so fallback timing still matches the visual attack.
- Impact feedback/audio for enemy-authored attack connects are still limited to the hero damage path and optional enemy-local animation/audio authoring. A future polish pass may add enemy attack telegraph/impact feedback events on `EnemyAttackController`.

Manual Mushroom regression checklist:

- Touching Mushroom damages the hero once, then hero i-frames prevent repeat hits.
- Staying overlapped with Mushroom contact damage does not spam damage before contact cooldown / i-frames expire.
- Mushroom authored attack damages only during the Active window.
- Standing inside the attack hitbox does not cause repeated damage within one Active window.
- Contact damage and attack damage do not double-hit unfairly in the same physics moment.
- A later contact or attack can damage again after i-frames/cooldowns expire.
- Hurt interrupts Mushroom's authored attack and closes the attack hitbox.
- Death interrupts Mushroom's authored attack, disables contact damage, and disables all damage colliders.

---

## Implemented

- Mushroom is the first/basic enemy archetype and Enemy AI foundation validation target.
- `EnemyController` wires config, blackboard, health, recoil, optional motor, optional perception, and optional authored attack runtime.
- `EnemyMotor` owns normal enemy movement velocity writes when present.
- `EnemyRecoil` routes recoil through `EnemyMotor` when present and clears recoil on recovery/death.
- `EnemyPerception` supports radius detection plus line-of-sight blockers.
- Mushroom supports Idle, Patrol, Chase, Attack, Hurt, and Dead.
- Mushroom keeps contact damage and uses authored attack windows.
- `EnemyAttackController` supports Startup, Active, Recovery, Cooldown, animation event methods, timer fallback, cooldown, duplicate-hit prevention, and interrupt cleanup.
- `EnemyAttackHitbox` damages through `HeroBox` using `DamageHero` metadata.
- Death disables contact damage and authored attack hitboxes/colliders.

## Planned

- Additional enemy archetypes.
- Boss-specific behaviours.
- Room-scale wake/sleep activation.
- Defeated-enemy persistence through future world-state save ownership.
- Pooling, if performance data later justifies it.
- Enemy-specific impact profiles/attack telegraph feedback beyond the current local feedback hooks.

## Missing

- No additional production enemy archetypes beyond Mushroom.
- No boss framework.
- No room-owned enemy wake/sleep layer.
- No defeated-enemy persistence.
- No pooling.

## Tech Debt

- Enemy-authored attack impact feedback/audio is still minimal.
- Mushroom attack event timing may need feel tuning after more combat encounters exist.
- Attack fallback timing is not automatically scaled from Animancer playback speed.
- Direct prefab/scene authoring remains required for attack hitbox shape, animation events, feedback, and audio hookups.

## Unity Editor Work Required

- Keep `Mushroom.prefab` as the foundation validation prefab.
- Verify root Mushroom remains on `Enemies`.
- Verify `AttackHitbox` child remains on `Enemy Attack`.
- Verify attack hitbox collider is a trigger and disabled by default.
- Verify root `EnemyContactDamage` + root `DamageHero` remain for body contact.
- Verify child `EnemyAttackHitbox` + child `DamageHero` remain for authored attacks.
- Verify `Mushroom Attack` clip contains `OpenAttackWindow`, `CloseAttackWindow`, and `CompleteAttack` events.
- If the attack clip plays on a child object, add `EnemyAttackAnimationEvents` to that child and assign or let it find the root `EnemyAttackController`.
- Verify Physics 2D layer matrix allows `Enemy Attack` vs `Hero Box`, and ignores `Enemy Attack` vs `Enemies` and `Enemy Attack` vs `Enemy Attack`.

---

## Dependencies

- `IHeroAttackReceiver` / `IHeroDownslashResponder` — defined in `Scripts/Hero/Combat/`
- `EnemyConfig` SO
- Hero `Transform` (read only, for detection targeting)
- `HeroBox` / `HeroHealthComponent` damage path for actually damaging the hero

---

## Extension Points

- **New enemy type** — new `EnemyConfig` SO + a component implementing `IEnemyBehaviour`.
- **Explicit attacker** — add an attack-window component and one enemy-specific behaviour that enters Startup / Active / Recovery / Cooldown from Chase.
- **Boss** — use a bespoke enemy behaviour with phase transitions keyed on health thresholds, while still using shared health, motor, perception, attack-hitbox, and feedback components.
- **New hit-reaction interface** — add to `Scripts/Hero/Combat/` and implement in `EnemyHealthComponent`.
- **Enemy-specific impact profiles** — defer until there are at least 2-3 enemy families or a boss that needs distinct hit/death feedback.
- **Wake/sleep** — future room-owned activation layer that enables/disables enemy brains and perception. Do not add room ownership to individual behaviour classes.
- **Persistent defeated enemies** — future `WorldStateRegistry` / save-target integration. Enemy MonoBehaviours must not call `SaveManager.Save()` directly.

---

## Rules

- `EnemyController` must not call any method on `HeroController` or any hero subsystem except through the defined interfaces.
- Enemy behaviours and attack hitboxes must not reference `HeroController`. They may read a hero `Transform` for targeting and may damage through the existing `HeroBox` / damage component path.
- `EnemyController` is a wiring/coordinator component only. Enemy-specific behaviour classes own state decisions.
- `EnemyMotor` owns normal enemy velocity writes. `EnemyRecoil` may override through `EnemyMotor`; behaviour scripts must not fight that override.
- Enemy health and configuration are fully data-driven through `EnemyConfig` SO — no hard-coded values in MonoBehaviours.
- Death must be handled gracefully: disable physics and AI before destroying the GameObject (avoid one-frame physics glitches).
- Death or hurt must interrupt authored attacks and disable all enemy attack hitboxes immediately.
- Dead enemies should not damage the hero or remain pogo/downslash responders unless a corpse/bouncy-environment behaviour is explicitly authored.
- Enemy MonoBehaviours must not call `SaveManager.Save()` directly. Future defeated-enemy persistence belongs behind a world-state save target.
- Enemy body colliders live on the `Enemies` layer. Physics 2D disables `Enemies` vs `Enemies`, `Enemies` vs `Enemy Attack`, and `Enemy Attack` vs `Enemy Attack`; enemies still collide with `Terrain`, `Hero Box`, and `Hero Attack`.
- Enemy prefabs should set the root and all current physics children to `Enemies` recursively. Future attack hitbox children should use `Enemy Attack` and should normally be triggers.
