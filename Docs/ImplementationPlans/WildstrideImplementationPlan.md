# Wildstride — Full Implementation Plan

**Project:** Underbrew / Metroidvania Controller  
**Feature name:** Wildstride  
**Internal ability identity:** `Sprint`  
**Planning baseline inspected:** `Perkiiii/Metroidvania-Controller` at commit `0db8383d5c13b21e1e45a33c248360ae5e2e5c0d`  
**Reference basis:** supplied Hollow Knight: Silksong decompiled C# files and research notes  
**Status:** Historical planning baseline. Implemented behaviour is canonical in
`Docs/ImplementationPlans/Wildstride.md`; where this plan conflicts, the implementation document
and current feature specs supersede it.

> **Superseded cancellation assumptions:** this historical plan's claims that opposite/neutral
> airborne input prevents landing Wildstride, that carry must remain active through the landing,
> and that Double Jump destroys the complete sequence are no longer canonical. Underbrew now ends
> forced carry at normal falling or Double Jump while preserving an established first-landing
> authorisation; an ordinary Jump/Double Jump cannot create one.

---

## 1. Executive Recommendation

Implement **Wildstride** as a permanent traversal ability that upgrades the existing Dash input:

- A fresh Dash press continues to perform the current dash immediately.
- When `PlayerAbilityState.sprintUnlocked` is true, holding the same Dash input after a successful grounded dash transitions into Wildstride.
- Wildstride is grounded, directional, faster than normal running, has no stamina or resource cost, and can carry its horizontal momentum into a jump.
- Grounded reversal preserves Wildstride; airborne opposite input cancels captured jump carry.
  Release, true neutral input, incompatible traversal/combat states, damage, death, control loss,
  scene entry, or invalid terrain interaction end the relevant authorization.
- A dedicated forward **Wildstride attack** is available during Dash or Wildstride and uses the existing Underbrew attack, resource, hit-feedback, clash, and receiver pipelines.
- The player-facing name is **Wildstride**, while internal code remains `Sprint`, `HeroSprintAction`, `AbilityId.Sprint`, and `sprintUnlocked`. This avoids an unnecessary save/API rename while allowing the game’s UI and lore to use its own identity.

The implementation should preserve Underbrew’s current architecture:

```text
HeroInputReader
    ↓ typed Dash press / hold / release
HeroActionController
    ↓ ordered action arbitration
HeroDashAction ──typed dash completion signal──▶ HeroSprintAction
    ↓                                            ↓
HeroStateBlackboard                         locomotion request
    └──────────────────────────────▶ HeroMotor
                                         ↓
                                  Rigidbody2D velocity

HeroAttackAction
    ↓ selected Wildstride attack variant
HeroAttackModule
    ↓ existing receiver / resource / feedback pipeline
typed attack outcome
    ↓
HeroSprintAction / HeroMotor response
```

Do **not** reproduce Silksong’s monolithic `HeroController`, PlayMaker string events, reflection-based state access, direct ability-to-Rigidbody writes, or Crest/tool coupling.

---

## 2. What the Silksong Reference Actually Establishes

The supplied reference code closes several gaps left by the earlier research documents.

### 2.1 Dash and Sprint share one input

`HeroActions.cs` defines a `Dash` action but no separate Sprint action. `HeroController` checks the Dash action’s pressed and held states. This establishes that the player-facing tap/hold behaviour is built from one command rather than separate Dash and Sprint bindings.

Relevant supplied files:

```text
HeroActions.cs
InputHandler.cs
HeroController.cs
```

### 2.2 Dash is immediate; Sprint is a continuation

`HeroController.HeroDash(...)` starts the dash and sends `"DASHED"` to `sprintFSM`.

When dash finishes, `FinishedDashing(...)`:

- cancels Dash physics;
- restores gravity;
- sends `"TRY SPRINT"` when conditions permit;
- sends `"CANCEL SPRINT"` for incompatible down-dash outcomes.

This shows that Sprint is not selected instead of Dash by waiting for a hold threshold. Dash starts immediately, then Sprint is attempted as the continuation.

### 2.3 Sprint has explicit runtime state

`HeroControllerStates.cs` contains:

```text
isSprinting
isBackSprinting
shuttleCock
```

The exact Sprint transition graph remains in `sprintFSM`, which was not supplied. The C# still confirms that sprinting is explicit runtime state, not merely animation playback.

### 2.4 The longer jump is a dedicated horizontal carry

`HeroController.HeroJump(bool checkSprint)` calls `OnShuttleCockJump()` when a sprint/dash buffer, current dash, or active sprint is present.

`OnShuttleCockJump()`:

- sets `cState.shuttleCock`;
- captures a signed horizontal `SHUTTLECOCK_SPEED`;
- starts dedicated audio, vibration, and VFX;
- leaves the ordinary jump path responsible for vertical jumping.

During physics, the controller preserves the current vertical velocity while applying the captured horizontal shuttlecock speed. On cancellation, it converts the remaining movement into a decaying air velocity that can cancel on turn.

This is the most important mechanical lesson for Wildstride:

> Sprint jumping should be a distinct horizontal carry layered onto the normal jump, not a change to jump height, gravity, coyote time, or jump buffering.

### 2.5 Landing can re-enter Sprint only from authorised carry

While `shuttleCock` is active, landing cancels the carry. If the Dash input is still held, Silksong sends `"TRY SPRINT"`; otherwise it performs a soft landing.

Underbrew should adapt the principle, not the exact implementation:

- landing may resume Wildstride only when the hero is still in an authorised Wildstride jump-carry sequence;
- an arbitrary held Dash input must never start Wildstride from idle or after an interruption;
- cancellation or input suspension must require release before a new Wildstride sequence.

### 2.6 Sprint/Dash attacks use confirmed outcomes

`DashStabNailAttack.cs` listens to the damage system before a hit resolves and reports typed gameplay meaning back to the Sprint FSM through string events:

```text
"DASH HIT"
"DASH RECOIL"
```

It can delay the result until either an animation event or damage-window completion. The useful pattern is not the string event or FSM; it is this separation:

```text
attack starts
→ hitbox evaluates
→ confirmed outcome occurs
→ movement state reacts
```

Underbrew should use a typed outcome or callback instead of string events.

### 2.7 Silksong’s architecture is a reference, not a template

Silksong centralises movement, actions, damage, state, resources, equipment side effects, and direct Rigidbody writes in `HeroController`, with many PlayMaker FSMs and string events around it.

Underbrew’s architecture is already cleaner:

- `HeroController` coordinates.
- `HeroActionController` owns action orchestration.
- actions are plain C# classes.
- `HeroMotor` owns velocity.
- `HeroStateBlackboard` carries shared runtime facts.
- Animancer owns animation playback directly.
- `PlayerAbilityState` owns persistent unlocks.

Wildstride should strengthen those boundaries rather than imitate the reference structure.

---

## 3. Current Underbrew State

### 3.1 Implemented foundations

The repository already contains:

- `AbilityId.Sprint`.
- `PlayerAbilityState.sprintUnlocked`.
- save/load support for `sprintUnlocked`.
- an `AbilityPickup` path capable of granting `AbilityId.Sprint`.
- separate Dash and Sprint input actions.
- `HeroInputReader.DashPressedThisFrame`.
- `HeroInputReader.SprintHeld`.
- a robust input suspension and held-command rearming system.
- `HeroDashAction`.
- `HeroJumpAction`.
- `HeroAttackAction`.
- `HeroLedgeClimbAction`.
- `HeroMotor` as the intended velocity authority.
- `HeroAnimationController` using Animancer.
- attack modules with authored colliders, visuals, audio, resource rules, and animation-window callbacks.

No save schema expansion should be necessary because Sprint ownership already exists and is persisted. This must still be verified against current HEAD before implementation.

### 3.2 Current behaviour that conflicts with Wildstride

The existing Sprint contract is a separate hold-to-run modifier:

```text
SprintHeld && grounded && moving
```

`HeroActionController.ApplyLocomotionIntent()` currently treats `SprintHeld` as the request for ordinary `runSpeed`. `HeroMotor.SetDesiredMove(float, bool)` stores that decision as `runRequested`, and its speed choice is only Walk versus Run.

Wildstride requires three meaningful locomotion modes:

```text
Walk
Run
Wildstride
```

The current boolean request is therefore no longer expressive enough.

### 3.3 Current action conflicts

- `HeroAttackAction.CanStartAttack()` rejects all attacks while `blackboard.dashing`.
- `HeroJumpAction.CanStartJump()` rejects jump while dashing.
- `HeroLedgeClimbAction` directly receives `HeroDashAction` and cancels Dash on entry.
- `HeroController` cancels Attack, Bind, and Ledge Climb during several interruptions but does not consistently ask every action to clean itself up.
- `HeroController.HandleDeath()` directly writes `body.linearVelocity = Vector2.zero`, which conflicts with the documented rule that `HeroMotor` is the only Rigidbody velocity writer.
- `HeroController.ResetTransientHeroState()` manually clears many action-owned blackboard flags, which risks bypassing action-specific cleanup as the action set grows.

Wildstride should not add more manual flag clearing or another chain of actions reaching into one another.

---

## 4. Approved Player-Facing Wildstride Contract

### 4.1 Ability identity and progression

- Player-facing name: **Wildstride**.
- Internal ID: `AbilityId.Sprint`.
- Persistent owner: `PlayerAbilityState.sprintUnlocked`.
- Wildstride is a permanent traversal unlock.
- Dash remains a separate permanent ability governed by `dashUnlocked`.
- Acquiring Wildstride does not automatically grant Dash unless the progression design explicitly chooses to guarantee Dash is already owned before the pickup.
- The pickup should normally be placed after Dash in progression.
- The future Gear screen may display Wildstride as a physical progression object, but Gear remains presentation-only.

### 4.2 Input

- Use the existing Dash input for both Dash and Wildstride.
- A fresh press attempts Dash immediately.
- There is no hold threshold that delays Dash.
- Holding Dash after a valid grounded Dash authorises Wildstride.
- Remove the standalone gameplay Sprint input and its bindings after all references are migrated.
- Keep player rebinding terminology as **Dash / Wildstride** or simply **Dash**, depending on final UI wording.

### 4.3 Entry

Wildstride starts only when all of these are true:

1. `sprintUnlocked` is true.
2. A valid grounded Dash began in the current authorised sequence.
3. That Dash reaches its normal completion.
4. Dash remains physically held and is not disarmed.
5. Horizontal movement input is above the dead zone.
6. Horizontal input remains aligned with the Dash direction.
7. The hero is grounded.
8. No incompatible action or control state owns the hero.

Wildstride must **not** start merely because Dash is held while:

- standing idle;
- walking or running;
- Dash is on cooldown;
- Dash is locked;
- landing after an unrelated fall;
- recovering from a menu, cutscene, hurt, death, scene transition, or ledge climb;
- the ability becomes unlocked while the button is already held.

### 4.4 Active movement

While active:

- Wildstride is grounded-only.
- Horizontal target speed uses `HeroAbilityConfig.sprintSpeed`.
- Acceleration and deceleration remain inside `HeroMotor`.
- Direction is initially inherited from the qualifying Dash.
- Grounded input may reverse while preserving the same authorization.
- Wildstride does not consume resource or stamina.
- Base walk, run, jump, gravity, dash, wall, ledge, attack, and hurt tuning remain unchanged unless a specific Wildstride field controls the behaviour.

### 4.5 Cancellation

Wildstride ends immediately when:

- Dash is released.
- Horizontal input becomes neutral.
- Horizontal input reverses during airborne captured carry (grounded reversal remains active).
- the hero leaves the ground without beginning an authorised Wildstride jump.
- Attack begins.
- Bind begins.
- wall slide or wall jump begins.
- ledge climb begins.
- downslash bounce/pogo begins.
- double jump begins.
- damage, recoil, death, hazard recovery, or respawn occurs.
- control is locked or gameplay input is suspended.
- scene-entry or scripted movement begins.
- the action/controller is disabled.
- the ability is locked at runtime.

After interruption by ledge climb, control lock, UI suspension, scene transition, hurt, death, or scripted movement, Wildstride is disarmed until the Dash input is physically released.

### 4.6 Wildstride jump

When Jump starts during active Wildstride:

- vertical jump behaviour remains the ordinary `HeroJumpAction` and `HeroMotor.StartJump()` path;
- current Wildstride direction is captured;
- horizontal carry uses `HeroAbilityConfig.sprintJumpSpeed`;
- `blackboard.sprinting` becomes false;
- `blackboard.sprintJumpCarrying` becomes true;
- the carry is not an air sprint and cannot be entered from a normal jump.

The carry ends when:

- Dash is released;
- movement becomes neutral;
- movement reverses;
- an attack starts;
- Double Jump starts;
- wall slide or wall jump starts;
- ledge climb starts;
- pogo/downslash bounce starts;
- damage/recoil/death/control lock occurs;
- the hero collides with a blocking wall;
- the action is explicitly cancelled.

On a normal cancellation in air, horizontal velocity should decelerate through the motor rather than snap to zero. The initial implementation should use a configurable carry-decay/air-deceleration path.

On landing:

- if the carry remained valid, Dash is still held, aligned movement remains held, and Wildstride is still unlocked, the hero may re-enter Wildstride;
- if the carry was cancelled or disarmed, landing does not restart Wildstride;
- landing never creates a fresh Dash or Wildstride sequence by itself.

### 4.7 Wildstride attack

A complete Wildstride package includes one dedicated forward attack available:

- during a Dash; or
- during grounded Wildstride.

First-pass behaviour:

- Attack input selects a dedicated Wildstride forward attack module.
- It does not use Up or Down attack variants.
- It reuses the normal attack buffer.
- It reuses the established attack collider, receiver, resource, clash, terrain-impact, hit-stop, camera-shake, and SFX pipeline.
- Starting it ends active Wildstride or consumes the Dash into the attack.
- The motor applies a configured forward attack motion.
- On a confirmed accepted enemy hit, the attack requests a short stop/recoil response through `HeroMotor`.
- A miss completes normally without automatically chaining another attack.
- Resource may be awarded according to the module’s existing authored resource rules.
- Only one impact response may fire per swing.
- It has its own clip, collider geometry, fail-safe, recovery, and tuning.
- No Crest/loadout-specific variants are included.

This should feel inspired by Silksong’s run-slash without copying a particular Crest attack.

---

## 5. Runtime State Model

Add only coordination state that other systems genuinely need.

### 5.1 Blackboard fields

Add under a new `Wildstride` or `Traversal` header:

```csharp
public bool sprinting;
public bool sprintJumpCarrying;
public int sprintDirection;
public int dashSequenceVersion;
public int completedGroundDashVersion;
public HeroDashEndReason lastDashEndReason;
```

Optional debug fields:

```csharp
public HeroSprintCancelReason lastSprintCancelReason;
```

The version fields prevent stale one-frame booleans:

- `HeroDashAction` increments `dashSequenceVersion` when Dash starts.
- It records whether the Dash began grounded and its direction.
- On completion, it records `completedGroundDashVersion` only for a naturally completed qualifying ground Dash.
- `HeroSprintAction` caches the last handled completion version.
- A completion cannot be consumed twice or survive interruption/reset accidentally.

Do not store persistent ownership, tuning, timers, or save data in the blackboard.

### 5.2 New enums

Create:

```text
Assets/_Project/Scripts/Hero/Core/HeroLocomotionSpeed.cs
Assets/_Project/Scripts/Hero/Core/HeroDashEndReason.cs
Assets/_Project/Scripts/Hero/Core/HeroSprintCancelReason.cs
Assets/_Project/Scripts/Hero/Core/HeroAttackVariant.cs
```

Suggested values:

```csharp
public enum HeroLocomotionSpeed
{
    Walk,
    Run,
    Wildstride
}
```

```csharp
public enum HeroDashEndReason
{
    None,
    Completed,
    LedgeClimb,
    Attack,
    ControlLock,
    Hurt,
    Death,
    SceneEntry,
    ComponentDisabled
}
```

```csharp
public enum HeroSprintCancelReason
{
    None,
    InputReleased,
    NeutralInput,
    DirectionReversed,
    LeftGround,
    JumpCarryEnded,
    Attack,
    DoubleJump,
    DownslashBounce,
    WallState,
    LedgeClimb,
    Bind,
    Hurt,
    Death,
    ControlLock,
    InputSuspended,
    SceneEntry,
    AbilityLocked,
    ComponentDisabled
}
```

```csharp
public enum HeroAttackVariant
{
    Normal,
    Wildstride
}
```

Keep `HeroAttackDirection` as Side, Up, and Down. Wildstride is an attack **variant**, not a spatial direction.

---

## 6. Recommended Runtime Architecture

### 6.1 `HeroSprintAction`

Create:

```text
Assets/_Project/Scripts/Hero/Actions/HeroSprintAction.cs
```

Plain C# class owned by `HeroActionController`.

Responsibilities:

- read `sprintUnlocked`;
- observe qualifying grounded Dash completion;
- authorise one Dash-to-Wildstride sequence;
- start and stop grounded Wildstride;
- capture and manage Wildstride jump carry;
- expose the requested locomotion speed mode;
- disarm until Dash release after hard interruptions;
- record typed cancellation reasons;
- perform idempotent cleanup;
- never write Rigidbody velocity;
- never choose animation clips directly;
- never save ownership.

Suggested public surface:

```csharp
public bool IsSprinting { get; }
public bool IsJumpCarrying { get; }
public HeroLocomotionSpeed RequestedSpeed { get; }

public void Tick(float deltaTime);
public void FixedTick(float fixedDeltaTime);

public bool TryBeginJumpCarry();
public void NotifyLanded();
public void NotifyDoubleJump();
public void NotifyDownslashBounce();
public void NotifyAttackStarted();
public void Cancel(HeroSprintCancelReason reason, bool disarmUntilRelease);
```

Prefer notifications routed by `HeroActionController`, not direct references from one action to another.

### 6.2 `HeroDashAction`

Modify Dash to publish typed sequence facts.

Add:

- current sequence version;
- start-grounded fact;
- Dash direction;
- typed end reason;
- natural-completion signal.

Suggested properties:

```csharp
public int SequenceVersion { get; }
public bool StartedGrounded { get; }
public int Direction { get; }
public HeroDashEndReason LastEndReason { get; }
```

Suggested API:

```csharp
public void Cancel(HeroDashEndReason reason);
```

`FixedTick()` ending because `dashTimer <= 0` uses `Completed`.

Ledge climb uses `LedgeClimb`.

Hurt, death, scene entry, and controller disable use their matching reason.

Only a natural `Completed` ground Dash may authorise Wildstride. Cancelling a Dash into ledge climb, attack, hurt, or another state must not produce Sprint.

Preserve:

- Dash speed.
- Dash duration.
- Dash cooldown.
- air-Dash use/cooldown semantics.
- direction selection.
- ledge handoff preserving cooldown and `airDashUsed`.

### 6.3 `HeroMotor`

Replace:

```csharp
SetDesiredMove(float moveX, bool wantsRun)
```

with:

```csharp
SetDesiredMove(float moveX, HeroLocomotionSpeed speedMode)
```

Replace `runRequested` with `locomotionSpeed`.

`GetTargetSpeed()` becomes a typed switch:

```text
Walk       → walkSpeed
Run        → runSpeed
Wildstride → abilityConfig.sprintSpeed
```

The existing horizontal velocity pipeline remains authoritative:

```text
desired input
→ target speed
→ attack multiplier if applicable
→ grounded/air acceleration or deceleration
→ Rigidbody2D.linearVelocity
```

Add a motor-owned Wildstride jump carry request:

```csharp
SetSprintJumpCarry(bool active, int direction);
```

or equivalent typed API.

When active:

- horizontal target is `direction * sprintJumpSpeed`;
- `sprintJumpCarryAcceleration` controls how firmly the carry holds;
- vertical velocity is never changed by the carry;
- normal gravity and jump sustain remain untouched;
- cancellation transitions back to ordinary air acceleration/deceleration;
- action code does not assign velocity.

Do not use root motion.

### 6.4 `HeroActionController`

Instantiate `HeroSprintAction`.

Recommended `Tick()` ordering:

```text
1. Ledge-climb active early ownership
2. Bind
3. Dash
4. Sprint / Wildstride arbitration
5. Attack
6. Apply locomotion intent
```

Important detail:

- Dash must tick before Sprint so Sprint sees Dash starts/completions.
- Wildstride attack selection must occur before generic Sprint cancellation loses its context.
- The same Update must not insert one frame of normal Run between Dash completion and Wildstride entry.

Recommended `FixedTick()` ordering:

```text
1. Bind
2. Ledge climb
3. Wall slide
4. Wall jump
5. Dash
6. Jump
7. Sprint carry state
8. Attack
9. HeroMotor.FixedTick() remains called by HeroController
```

Exact ordering should be verified with tests because Jump, Dash completion, and Wildstride carry can occur on adjacent Update/FixedUpdate boundaries.

Replace `ApplyLocomotionIntent()`’s boolean `wantsRun` with the action’s typed request:

```text
Walk or Run from existing core movement rules
Wildstride only when HeroSprintAction owns it
```

When `requireSprintForRun` is false, ordinary movement remains Run. Wildstride is a third speed and no longer controls basic Run.

### 6.5 Central action cancellation

Add a unified action cleanup entry point:

```csharp
public void CancelAll(HeroActionCancelReason reason);
```

or a small set of typed methods:

```csharp
CancelForControlLock(...)
CancelForHurt(...)
CancelForDeath(...)
CancelForSceneEntry(...)
CancelForInputSuspension(...)
```

This should ask each action to clean up its own state:

- Attack
- Bind
- Dash
- Wildstride
- Ledge Climb
- Wall Slide/Wall Jump transient ownership where needed
- future actions

This is preferable to `HeroController` manually clearing blackboard flags.

Keep it narrow: do not introduce a generic framework, interfaces for every action, dependency injection container, or formal hierarchical FSM merely for this feature.

---

## 7. Input Migration

### 7.1 `HeroInputReader`

Modify:

```text
Assets/_Project/Scripts/Hero/Input/HeroInputReader.cs
```

Remove:

```text
sprintAction
SprintHeld
sprintDisarmed
ReadSprintFallback()
```

Expose from the Dash action:

```csharp
public bool DashPressedThisFrame { get; private set; }
public bool DashHeld { get; private set; }
public bool DashReleasedThisFrame { get; private set; }
```

Use one physical actuation read:

```text
Dash pressed
Dash held
Dash released
```

The existing Dash disarm flag must suppress the entire shared command after resume:

- held through Pause/Menu does not create Dash;
- held through resume does not create Wildstride;
- releasing clears disarm;
- a later fresh press works normally.

Update:

- `Tick()`
- `ClearTransientInput()`
- `BeginResumeGameplayInput()`
- action enabling/disabling
- fallback reads
- comments and tests

### 7.2 Input Action asset

Modify:

```text
Assets/_Project/Input/InputSystem_Actions.inputactions
```

After verifying all references:

- remove Player/Sprint;
- remove Sprint keyboard/gamepad bindings;
- retain Player/Dash;
- retain existing Dash bindings unless intentionally redesigned;
- update any generated input wrapper if the project generates one;
- update control-reminder/rebinding UI references if present.

Do not use Input System Tap/Hold interactions for arbitration. Dash should remain immediate and code should interpret continued physical hold.

### 7.3 Hero prefab

Modify:

```text
Assets/_Project/Prefabs/Hero.prefab
```

- remove the serialized Sprint `InputActionReference`;
- ensure the Dash reference is assigned;
- verify prefab overrides do not retain a missing reference;
- validate keyboard and gamepad behaviour in Play Mode.

---

## 8. Wildstride Movement and Tuning

Add a `Wildstride` header to:

```text
Assets/_Project/Scripts/Hero/Core/HeroAbilityConfig.cs
```

Recommended provisional values:

```csharp
[Header("Wildstride")]
public float sprintSpeed = 11f;
public float sprintJumpSpeed = 11f;
public float sprintGroundAcceleration = 100f;
public float sprintGroundDeceleration = 120f;
public float sprintJumpCarryAcceleration = 120f;
public float sprintJumpCarryDecay = 25f;
public float sprintTurnCancelThreshold = 0.3f;
public float sprintLandingResumeGrace = 0.08f;
```

These are first-pass tuning targets, not validated values.

Rationale:

- current Run is 6.5;
- current Dash is 18;
- a starting Wildstride speed around 11 provides a meaningful traversal upgrade while retaining Dash as the burst option;
- it should not blindly use Silksong’s publicly reported percentage because Underbrew’s scale, camera, rooms, jump arc, enemy spacing, and physics differ;
- `sprintJumpSpeed` should initially match `sprintSpeed` so the longer jump comes from preserved horizontal speed rather than an extra hidden boost.

Tuning ownership:

- Wildstride traversal tuning belongs in `HeroAbilityConfig`.
- Base Walk/Run, jump, gravity, coyote time, and attack defaults remain in `HeroConfig`.
- Wildstride attack-specific lunge/recoil values may live in `HeroAbilityConfig` for the first pass or in attack-module data if the project is ready to make attack motion authored per variant.
- Do not move persistent unlock state into a config asset.

Do not modify the validated base hero values during this feature pass.

---

## 9. Wildstride Jump Integration

### 9.1 `HeroJumpAction`

Modify:

```text
Assets/_Project/Scripts/Hero/Actions/HeroJumpAction.cs
```

Before calling `motor.StartJump()` for a grounded/coyote jump:

- ask `HeroSprintAction` or `HeroActionController` whether active Wildstride can convert into carry;
- if yes, capture direction and enable the motor carry;
- then execute the normal jump unchanged.

Do not make `HeroJumpAction` depend directly on `HeroSprintAction` if orchestration can remain in `HeroActionController`. A practical pattern is:

```text
ActionController observes a jump start request
→ SprintAction.TryBeginJumpCarry()
→ JumpAction performs normal StartJump()
```

or a narrow callback injected into JumpAction.

Coyote case decision:

- Wildstride carry should be available for a very short coyote jump after running off an edge only if the Sprint action was still authorised when ground was lost.
- Do not allow an arbitrary coyote jump after Sprint was cancelled to recreate carry.

### 9.2 Double Jump

On `HeroJumpAction` beginning Double Jump:

- cancel Wildstride carry;
- clear motor carry;
- preserve existing Double Jump speed and reset rules.

Do not restore Wildstride speed after Double Jump in the first pass.

### 9.3 Downslash bounce/pogo

`HeroMotor.ApplyDownslashBounce()` must clear Wildstride carry before applying bounce velocity.

Prefer orchestration through `HeroActionController` or a motor API that guarantees incompatible movement modes are cleared. Do not rely only on the animation layer noticing the bounce later.

### 9.4 Wall interaction

Entering Wall Slide or Wall Jump cancels carry.

A wall reached during carry should use the established wall/ledge arbitration:

- valid ledge candidate may enter Ledge Climb;
- otherwise eligible wall contact may enter Wall Slide;
- carry must not keep forcing X velocity into the wall.

---

## 10. Ledge-Climb Integration

The current ledge package correctly allows active Dash direction to supply ledge intent and cancels Dash before beginning its code-timed movement.

Wildstride rules:

- active grounded Wildstride cannot directly mantle because ledge climb is airborne-only;
- Wildstride jump carry can approach a ledge;
- the carry direction may count as intent only while the corresponding movement input is still aligned;
- successful ledge entry cancels Dash/Wildstride/carry;
- Dash/Wildstride becomes disarmed until the Dash button is released;
- completing the mantle must not automatically start Wildstride even if Dash remained physically held;
- the hero may move normally immediately after mantle, but a new Wildstride sequence requires release and a fresh Dash.

Modify:

```text
Assets/_Project/Scripts/Hero/Actions/HeroLedgeClimbAction.cs
Assets/_Project/Scripts/Hero/Actions/HeroActionController.cs
```

Prefer ActionController orchestration over adding `HeroSprintAction` as another direct constructor dependency of LedgeClimbAction.

---

## 11. Wildstride Attack Architecture

### 11.1 Do not overload direction

Do not add `WildstrideSide` or `SprintSide` to `HeroAttackDirection`.

Direction answers:

```text
Where is the attack aimed?
```

Variant answers:

```text
Which move is being performed?
```

Add `HeroAttackVariant` to `HeroAttackModule`:

```csharp
public HeroAttackVariant variant = HeroAttackVariant.Normal;
```

Module identity becomes:

```text
direction + variant + isAlt
```

Existing modules remain:

```text
Side + Normal + A/B
Up + Normal
Down + Normal
```

New module:

```text
Side + Wildstride
```

### 11.2 `HeroAttackAction`

Modify attack selection:

1. Resolve whether the hero is in eligible Dash/Wildstride context.
2. Resolve vertical input.
3. Wildstride variant only applies to a forward/neutral Side attack.
4. Up/Down input continues to use ordinary attacks unless the product design later adds special variants.
5. Find the matching module by direction, variant, and alt state.
6. Start attack through the existing pipeline.

Update `CanStartAttack()`:

- ordinary attacks remain blocked during Dash;
- a Wildstride attack is explicitly allowed during an eligible Dash;
- the action must ask the Dash/Sprint owner to convert or cancel movement cleanly;
- never simply remove `!blackboard.dashing` globally.

Suggested flow:

```text
Attack buffer available
→ Determine requested direction
→ Determine attack variant
→ if Wildstride variant:
     validate active Dash or Sprint context
     cancel/convert traversal through ActionController
     begin motor-owned Wildstride attack motion
→ activate authored module
→ normal attack lifecycle
```

### 11.3 Typed attack outcomes

The current attack system already receives `HeroAttackResult`. Add a narrow once-per-swing outcome callback for movement-reactive attacks:

```csharp
public event Action<HeroAttackOutcome> AttackOutcome;
```

or an injected callback owned by `HeroActionController`.

Suggested outcome:

```csharp
public readonly struct HeroAttackOutcome
{
    public HeroAttackVariant Variant { get; }
    public HeroAttackDirection Direction { get; }
    public HeroAttackResult Result { get; }
    public Vector2 ContactPoint { get; }
}
```

Emit only after an accepted result and prevent duplicate movement reactions from multiple colliders/targets.

For Wildstride:

- accepted enemy hit → motor stop/recoil response;
- clash → optional smaller stop response;
- miss → no hit response;
- killed target still counts as an accepted hit;
- environmental breakables can remain resource-ineligible but may still count as contact only if explicitly authored.

### 11.4 Motor-owned attack motion

Add to `HeroMotor`:

```csharp
BeginWildstrideAttack(int direction);
ApplyWildstrideAttackHitResponse(int direction);
EndWildstrideAttack();
```

Exact implementation may use a short code-timed target velocity or decaying velocity, but all Rigidbody writes remain in `HeroMotor`.

Suggested provisional tuning:

```text
wildstrideAttackSpeed: 12–14
wildstrideAttackDuration: 0.16–0.24 s
wildstrideAttackHitStopVelocityDuration: 0.04–0.08 s
wildstrideAttackHitRecoilSpeed: 2–4 units/s away from target
```

These must be tuned against the final animation and hitbox.

### 11.5 `HeroAttackModule`

Modify:

```text
Assets/_Project/Scripts/Hero/Combat/HeroAttackModule.cs
```

Add:

- `HeroAttackVariant variant`;
- optional attack-motion profile reference only if needed;
- preserve collider, visual, audio, and resource ownership.

Do not duplicate the damage engine for Wildstride.

---

## 12. Animation Plan

### 12.1 Animation library

Modify:

```text
Assets/_Project/Scripts/Hero/Animation/HeroAnimationLibrary.cs
Assets/_Project/ScriptableObjects/Hero/HeroAnimationLibrary.asset
```

Add:

```csharp
[Header("Wildstride")]
public AnimationClip sprint;
public AnimationClip sprintJump;
public AnimationClip attackWildstride;
```

Minimum viable asset requirement:

- `sprint`
- `attackWildstride`

`sprintJump` may temporarily fall back to normal Jump while gameplay is tested, but the missing clip must be explicit and warning-safe.

### 12.2 Animation controller

Modify visual priority:

```text
Dead
Hurt
Ledge Climb
Bind
Attack
Dash
Wall Jump
Wall Slide
Wildstride Jump
Jump
Fall
Wildstride
Normal locomotion
```

Important rules:

- Dash remains higher priority than Wildstride.
- Attack remains higher priority than Dash/Sprint when the Wildstride attack begins.
- Wildstride loop is presentation-only; code owns entry/exit.
- Wildstride jump uses the dedicated carry flag, not horizontal velocity alone.
- No root motion.
- No Animator parameter state machine.
- If a gameplay-timed attack motion and animation differ, gameplay timing remains authoritative and animation is speed-matched or authored to fit.

### 12.3 Animation events

Use existing Animancer/attack-module callbacks for:

- attack hit-window begin;
- attack hit-window end;
- attack completion.

Do not add string-named gameplay events.

---

## 13. Audio and VFX Plan

### 13.1 Audio ownership

Modify:

```text
Assets/_Project/Scripts/Hero/Audio/HeroAudioController.cs
Assets/_Project/Prefabs/Hero.prefab
```

Add optional authored sources/clips for:

- Wildstride start;
- Wildstride loop/footsteps;
- Wildstride jump;
- Wildstride stop/skid;
- Wildstride attack startup.

Keep:

- attack slash SFX on the attack module;
- enemy hurt/death audio on receivers;
- global first-connect feedback in the current attack impact system.

### 13.2 Footsteps

The current Hero audio path has one footstep source and relies primarily on animation events.

First-pass options:

1. Reuse the standard footstep source but use the Sprint animation’s denser events.
2. Add a Wildstride-specific clip/pitch range selected when `blackboard.sprinting`.

Recommendation: start with option 1 unless Wildstride clearly needs a distinct sonic identity. Avoid a new footstep scheduler.

### 13.3 VFX

First pass:

- small ground trail/dust while sprinting;
- burst on Dash-to-Wildstride handoff;
- directional burst on Wildstride jump;
- existing attack VFX structure for Wildstride attack.

VFX reads blackboard/action state and never controls movement.

---

## 14. Persistence and Pickup

No new save field is expected.

Existing path:

```text
AbilityPickup
→ PlayerAbilityState.Unlock(AbilityId.Sprint)
→ sprintUnlocked = true
→ AbilityChanged
→ SaveManager gathers PlayerAbilityState
```

Unity setup:

- create or configure an `AbilityPickup`;
- `ability = AbilityId.Sprint`;
- assign production `PlayerAbilityState.asset`;
- assign `WorldStateRegistry`;
- assign a globally unique, stable `worldObjectId`;
- author collection feedback;
- place it after Dash in the intended progression route.

Test inconsistent state recovery already handled by `AbilityPickup`:

- owned ability but missing collected record;
- collected record but ability not owned.

Do not make the Wildstride action call SaveManager directly.

---

## 15. File-by-File Plan

### New runtime files

| File | Responsibility |
|---|---|
| `Assets/_Project/Scripts/Hero/Actions/HeroSprintAction.cs` | Wildstride authorisation, active state, jump carry, disarm/rearm, cancellation |
| `Assets/_Project/Scripts/Hero/Core/HeroLocomotionSpeed.cs` | Typed Walk/Run/Wildstride request |
| `Assets/_Project/Scripts/Hero/Core/HeroDashEndReason.cs` | Typed Dash cleanup/completion reason |
| `Assets/_Project/Scripts/Hero/Core/HeroSprintCancelReason.cs` | Typed Wildstride cancellation/debug reason |
| `Assets/_Project/Scripts/Hero/Core/HeroAttackVariant.cs` | Normal versus Wildstride attack identity |
| `Assets/_Project/Scripts/Hero/Core/HeroAttackOutcome.cs` | Optional typed confirmed attack outcome, only if existing result cannot serve directly |

### Modified runtime files

| File | Planned change |
|---|---|
| `Assets/_Project/Scripts/Hero/Input/HeroInputReader.cs` | One Dash press/hold/release command; remove standalone Sprint |
| `Assets/_Project/Input/InputSystem_Actions.inputactions` | Remove Sprint action/bindings after reference audit |
| `Assets/_Project/Scripts/Hero/Actions/HeroActionController.cs` | Own SprintAction; order arbitration; typed locomotion request; central cancellation |
| `Assets/_Project/Scripts/Hero/Actions/HeroDashAction.cs` | Publish typed sequence/completion facts; typed cancellation |
| `Assets/_Project/Scripts/Hero/Actions/HeroJumpAction.cs` | Start/cancel Wildstride carry around normal/double jump |
| `Assets/_Project/Scripts/Hero/Actions/HeroAttackAction.cs` | Select Wildstride variant; allow eligible Dash attack; emit typed outcome |
| `Assets/_Project/Scripts/Hero/Actions/HeroLedgeClimbAction.cs` | Cancel/disarm Wildstride via orchestrator; preserve existing dash handoff |
| `Assets/_Project/Scripts/Hero/Movement/HeroMotor.cs` | Typed locomotion mode; sprint speed; jump carry; Wildstride attack motion |
| `Assets/_Project/Scripts/Hero/Core/HeroStateBlackboard.cs` | Minimal Sprint/carry/dash-sequence coordination fields |
| `Assets/_Project/Scripts/Hero/Core/HeroAbilityConfig.cs` | Wildstride traversal and attack-motion tuning |
| `Assets/_Project/Scripts/Hero/HeroController.cs` | Forward typed cancellation; remove direct death velocity write |
| `Assets/_Project/Scripts/Hero/Combat/HeroAttackModule.cs` | Attack variant identity |
| `Assets/_Project/Scripts/Hero/Animation/HeroAnimationLibrary.cs` | Sprint, Sprint Jump, Wildstride Attack clips |
| `Assets/_Project/Scripts/Hero/Animation/HeroAnimationController.cs` | Wildstride visual states and priority |
| `Assets/_Project/Scripts/Hero/Audio/HeroAudioController.cs` | Optional Wildstride start/jump/stop and state-aware footsteps |
| `Assets/_Project/Prefabs/Hero.prefab` | Input refs, attack module, colliders, audio, animation assets |
| `Assets/_Project/ScriptableObjects/Hero/HeroAbilityConfig.asset` | Initial tuning values |
| `Assets/_Project/ScriptableObjects/Hero/HeroAnimationLibrary.asset` | Clip assignments |

### Files intentionally not structurally changed

| File | Reason |
|---|---|
| `Assets/_Project/Scripts/Hero/Core/PlayerAbilityState.cs` | Sprint flag/save integration already exists |
| `Assets/_Project/Scripts/Hero/Core/AbilityId.cs` | `Sprint` already exists |
| `Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset` | Must remain fully locked for production new games |
| `Assets/_Project/Scripts/Hero/Core/HeroAttackDirection.cs` | Wildstride is a variant, not a direction |
| `Assets/_Project/Scripts/Managers/GameManager.cs` | No new Wildstride responsibility |
| `Assets/_Project/Scripts/Save/SaveManager.cs` | Existing ISaveTarget path is sufficient |
| Camera systems | Must remain decoupled from Wildstride internals |

Exact paths must be reverified against current HEAD before edits.

---

## 16. Integrated Implementation Order

Use four coherent passes rather than many stop/start micro-milestones.

### Pass 1 — Input, state, and grounded movement

Goal: Dash remains unchanged; holding a completed grounded Dash enters functional Wildstride.

Work:

1. Add enums and `HeroSprintAction`.
2. Migrate input to Dash press/hold/release.
3. Remove standalone Sprint gameplay action.
4. Add typed Dash sequence/completion data.
5. Add typed locomotion speed mode.
6. Add Wildstride tuning.
7. Add blackboard state.
8. Integrate action ordering.
9. Preserve all current Dash, Run, wall, and ledge behaviour.

Exit criteria:

- locked Sprint has no effect;
- Dash alone is unchanged;
- valid grounded Dash + hold enters Wildstride;
- air Dash cannot enter Sprint;
- idle hold cannot enter Sprint;
- release/neutral cancel; grounded reversal turns through the motor without exiting.

### Pass 2 — Jump carry and interruption hardening

Goal: Wildstride jump feels longer horizontally without touching vertical hero feel.

Work:

1. Add motor carry.
2. Start carry from active Wildstride jump.
3. Cancel carry on all approved states.
4. Add authorised landing resume.
5. Integrate wall/ledge/pogo/double-jump rules.
6. Add central action cancellation.
7. Move death zero-velocity request into HeroMotor.
8. Harden input suspension/rearm.

Exit criteria:

- normal and Wildstride jumps have matching vertical behaviour;
- Wildstride jump travels farther horizontally;
- no mantle auto-sprint;
- held-through-menu/control-lock does not leak;
- hurt/death/scene entry clean every movement request.

### Pass 3 — Wildstride attack and presentation

Goal: complete the core Wildstride fantasy.

Work:

1. Add attack variant identity.
2. Add authored Wildstride attack module.
3. Add Dash/Wildstride attack selection.
4. Add motor-owned attack motion.
5. Add typed hit outcome response.
6. Add animation clips/fallbacks.
7. Add SFX/VFX.
8. Validate existing normal attacks and resource behaviour.

Exit criteria:

- Wildstride attack is available only from Dash/Wildstride;
- normal Side/Up/Down attacks remain unchanged;
- hit, miss, clash, resource, and feedback paths remain correct;
- no duplicate hit response.

### Pass 4 — Progression, tests, tuning, and documentation

Goal: production-safe package.

Work:

1. Add/configure Sprint AbilityPickup.
2. Add automated tests.
3. Extend production asset validation.
4. Build a movement test room.
5. Tune speed/carry/attack.
6. run full regression checklist.
7. update docs.
8. record known asset gaps separately.

Exit criteria:

- save/load round trip works;
- production ability state stays locked;
- prefab/config/input validation passes;
- manual traversal and combat regression completed;
- documentation matches code.

---

## 17. Unity Editor Work

### Input

- Open `InputSystem_Actions`.
- Verify every Sprint reference before deletion.
- Remove Sprint action and bindings.
- Confirm Dash keyboard/gamepad bindings.
- Save/generate wrappers if applicable.
- Reassign Hero prefab Dash reference.
- Check prefab YAML for stale Sprint reference after saving.

### Hero prefab

Under `Attacks`, create an authored child such as:

```text
Attacks
└── SlashWildstride
    ├── HeroAttackModule
    ├── damage PolygonCollider2D
    ├── optional clash PolygonCollider2D
    ├── visual root
    ├── SpriteRenderer / Animator / Animancer as required by existing module pattern
    └── optional AudioSource or slash AudioClip
```

Configure:

- `direction = Side`
- `variant = Wildstride`
- facing mirroring
- damage layers
- clash layers
- resource mode and parts
- collider shape
- visual clip
- slash clip/pitch
- inactive/disabled hit colliders by default

Refresh/cache the module array on `HeroActionController`.

### ScriptableObjects

`HeroAbilityConfig.asset`:

- assign provisional Sprint and carry values;
- do not change Dash values.

`HeroAnimationLibrary.asset`:

- assign Sprint loop;
- assign Sprint Jump or document fallback;
- assign Wildstride attack.

`PlayerAbilityState.asset`:

- leave Sprint locked.

### Pickup

- add `AbilityPickup` in the approved room;
- set `AbilityId.Sprint`;
- author stable ID;
- assign state and registry;
- add collection presentation;
- verify revisiting/reloading suppresses collected pickup.

### Test environment

Create or use a test corridor containing:

- long flat ground;
- measured normal-run and Sprint sections;
- several gap widths;
- short and tall ledges;
- wall-slide entry;
- low ceiling;
- enemy target;
- clash target;
- breakable;
- scene-transition gate;
- AbilityPickup.

Do not tune only inside a blank sandbox; validate in representative room geometry and camera framing.

---

## 18. Automated Tests

### `HeroSprintActionTests.cs`

Create:

```text
Assets/_Project/Scripts/Editor/Tests/Hero/HeroSprintActionTests.cs
```

Cover:

- Sprint locked prevents entry.
- Dash unlocked without Sprint preserves Dash only.
- Natural grounded Dash completion + held input starts Sprint.
- Releasing before Dash completion prevents Sprint.
- Air Dash cannot start Sprint.
- Holding Dash during cooldown cannot start Sprint from idle.
- Neutral input cancels.
- grounded reversal preserves Sprint; airborne carry reversal cancels carry.
- control lock cancels and disarms.
- input suspension cancels and disarms.
- hurt/death/scene-entry reasons cancel.
- ledge-climb cancellation disarms until release.
- runtime ability lock cancels.
- repeated cancellation is idempotent.
- stale Dash completion version cannot be consumed twice.

### Input suspension PlayMode tests

Extend:

```text
Assets/_Project/Tests/PlayMode/UI/HeroInputSuspensionPlayModeTests.cs
```

Cover:

- Dash held through Pause does not fire on resume.
- Dash held through Pause does not start Wildstride.
- release after resume rearms.
- fresh Dash press works.
- movement stick/key may resume immediately without rearming.
- no standalone Sprint action remains.

### Movement PlayMode tests

Create:

```text
Assets/_Project/Tests/PlayMode/HeroWildstridePlayModeTests.cs
```

Cover:

- Dash distance and duration unchanged.
- normal Run speed unchanged.
- Wildstride reaches configured speed.
- Wildstride jump Y trajectory matches normal jump within tolerance.
- Wildstride jump X distance is greater.
- release/reversal cancels carry.
- Double Jump cancels carry.
- pogo cancels carry.
- wall contact cancels or hands off correctly.
- valid ledge climb cancels and does not auto-sprint on completion.
- authorised carry landing can resume.
- cancelled carry landing cannot resume.
- scene transition never preserves Sprint/carry.

### Attack tests

Extend or add tests around `HeroAttackAction`:

- Wildstride variant chosen only from valid Dash/Sprint state.
- ordinary Dash still blocks ordinary attack.
- normal Side A/B alternation unchanged.
- Up/Down selection unchanged.
- Wildstride module is required and warning-safe if missing.
- one accepted hit produces one movement response.
- multi-target swing does not duplicate global hit response.
- resource follows authored module mode.
- breakable resource eligibility remains respected.
- clash response is separate from enemy damage.
- animation fail-safe ends the action.

### Persistence tests

Extend:

```text
Assets/_Project/Scripts/Editor/Tests/Hero/PlayerPersistentStateTests.cs
```

Verify:

- `sprintUnlocked` gathers and applies.
- fresh data remains locked.
- production asset remains locked.
- no save version bump is introduced unless another schema change unexpectedly occurs.

---

## 19. Manual Playtest Checklist

### Input and Dash

- Tap Dash on ground: current Dash feel unchanged.
- Hold Dash without Wildstride: current Dash only.
- Hold Dash with Wildstride: transitions cleanly after Dash.
- Press during cooldown: no idle Sprint.
- Tap/hold in air: existing air Dash only.
- Release at different points during Dash.
- Change direction during Dash and immediately after.
- Test keyboard, D-pad, and analogue stick.

### Ground movement

- Sprint on long flat ground.
- Release to normal Run.
- neutral to stop.
- reverse direction.
- run into wall.
- run down/up slopes if supported by current physics.
- conveyors and moving platforms.
- edge cases around small seams and composite colliders.

### Jump

- normal jump baseline.
- Wildstride jump full hold.
- Wildstride jump early release.
- coyote jump from Sprint.
- buffered jump before landing.
- release/reversal in air.
- Double Jump.
- downslash/pogo.
- wall slide/wall jump.
- low ceiling.
- landing resume.

### Ledge climb

- Dash into valid ledge.
- Wildstride jump into valid ledge.
- invalid geometry leaves movement predictable.
- mantle completion while Dash remains held.
- mantle cancellation by hurt/control lock.
- no automatic Sprint after mantle until release/new press.

### Combat

- Dash attack.
- grounded Wildstride attack.
- attack enemy and miss.
- attack multiple enemies.
- clash.
- breakable.
- attack into wall.
- attack near ledge.
- attack then Jump/Dash at first legal frame.
- resource generation.
- hit-stop/camera shake/audio.

### Interruptions

- Pause while Dashing.
- Pause while Sprinting.
- Gameplay Menu while holding Dash.
- hurt/death during Dash, Sprint, carry, attack.
- scene transition.
- hazard recovery.
- Bind.
- controller/component disable.

### Regression

- Walk.
- Run.
- normal Jump.
- Dash and air Dash.
- Wall Slide.
- Wall Jump.
- Double Jump.
- Ledge Climb.
- Bind.
- Side/Up/Down attacks.
- pogo.
- hurt/i-frames.
- death/respawn.
- scene entry.
- camera and footsteps.

Do not mark this checklist complete without hands-on Unity validation.

---

## 20. Architecture Improvements Recommended from the Reference Review

### 20.1 Must-fix: central action cancellation

**Problem:** interruption code currently cancels selected actions manually and clears flags directly. Wildstride adds more state that could remain stale.

**Recommendation:** `HeroActionController` should expose typed, idempotent cancellation for control lock, hurt, death, scene entry, input suspension, respawn, and disable.

**Benefit:** each action owns its cleanup; `HeroController` remains thin; future abilities do not require editing every interruption site.

### 20.2 Must-fix: remove the direct death velocity write

`HeroController.HandleDeath()` directly zeros `Rigidbody2D.linearVelocity`.

Move this to a motor method such as:

```csharp
motor.EnterDeathState();
```

or:

```csharp
motor.HoldStationary();
```

This restores the documented single velocity authority.

### 20.3 Recommended: typed locomotion mode

The existing `bool wantsRun` works for two speeds but becomes ambiguous with Wildstride and future modifiers.

Use `HeroLocomotionSpeed` now rather than adding `wantsSprint`, `forceRun`, and more booleans later.

### 20.4 Recommended: typed action outcomes

Silksong’s DashStab reacts to confirmed `"DASH HIT"` and `"DASH RECOIL"` events. Underbrew should formalise the useful part with typed attack outcomes.

This will also support future:

- charged attacks;
- parry follow-ups;
- projectile deflect resets;
- ability-specific hit movement;
- enemy-specific accepted/blocked outcomes.

Do not build a global event bus. Keep the contract local to Hero actions.

### 20.5 Recommended: versioned transition signals

Underbrew already uses `AttackVersion` to distinguish animation completions for different swings.

Use the same pattern selectively for Dash completion rather than one-frame booleans. This is safer across Update/FixedUpdate boundaries and animation callbacks.

### 20.6 Recommended: locomotion modifiers remain explicit and ordered

Avoid a generic stack of anonymous speed multipliers for now.

Use explicit order:

```text
base locomotion mode
→ Wildstride carry/attack mode
→ grounded attack movement shaping
→ final motor target
```

If later abilities genuinely stack, introduce a typed modifier pipeline then. Do not overengineer it pre-emptively.

### 20.7 Keep Underbrew’s blackboard small and typed

Do not copy Silksong’s very large mutable state bag or reflection-based `GetState(string)` / `SetState(string)`.

Only add facts required for coordination and presentation. Keep timers and private mechanics in the owning action.

### 20.8 Do not copy Crest config inheritance

Silksong uses different `HeroControllerConfig` assets/classes for Crest-specific attack rules.

Underbrew currently has one hero kit. Use attack variants/modules and authored data rather than subclassing the whole controller config. A future weapon/loadout system can introduce a narrow move-set profile if it becomes real product scope.

### 20.9 Preserve input rearming

Underbrew’s held-command disarm/rearm after UI suspension is stronger and more explicit than the reference code.

Wildstride must integrate with it rather than replacing it with generic input queueing.

---

## 21. Risks and Architecture Checks

### Input ambiguity

A shared button can accidentally start Sprint after menus, mantles, damage, or landing. The authorised sequence plus disarm-until-release rule is essential.

### Update/FixedUpdate race

Dash completion, Jump buffering, and Sprint entry can occur across different loops. Use versioned signals and tests; do not rely on one-frame booleans that may be missed.

### Action ordering

Wildstride attack must know the hero was Dashing/Sprinting before traversal cleanup occurs. Central orchestration should capture the variant first, then cancel/convert traversal.

### Velocity ownership

Every new movement effect must be implemented in `HeroMotor`. Watch for seemingly harmless direct X/Y writes in actions, animation callbacks, and `HeroController`.

### Blackboard bloat

Do not place internal Sprint timers, raw input, save flags, or attack hit sets on the blackboard.

### Animation authority

Sprint loop can be presentation-driven, but movement timing cannot depend on a clip finishing. Wildstride attack may use authored events for hit windows and completion with an existing fail-safe.

### Save compatibility

No schema change is expected. Verify that removing the Sprint input action does not affect serialized user binding data or menu code.

### Level-design impact

Wildstride jump changes reachable horizontal distances. Before placing the permanent pickup, audit:

- intended gates;
- sequence breaks;
- room boundaries;
- boss arenas;
- camera confines;
- shortcut geometry;
- ledge-climb interactions.

### Feel regression

Do not alter base Run, Jump, Gravity, Dash, Wall, Ledge, Attack, or Pogo values while tuning Wildstride. Measure differences rather than “fixing” the baseline around the new ability.

---

## 22. Explicit Exclusions

Do not include in the first Wildstride implementation:

- a separate Sprint button;
- stamina;
- base resource drain;
- Silkspeed-style equipment (future optional enhanced-speed Gear remains deferred);
- down-dash;
- swimming Sprint;
- back-sprint or back-dash variants;
- Scuttle;
- Crest/loadout-specific run attacks;
- multiple Wildstride attack chains;
- slope-specific acceleration system;
- root motion;
- PlayMaker;
- Animator parameter FSMs;
- string gameplay events;
- reflection over blackboard fields;
- Sprint persistence through scene transitions;
- a generic ability framework rewrite;
- direct copying of decompiled Silksong code or exact constants.

---

## 23. Documentation Updates

Create:

```text
Docs/ImplementationPlans/Wildstride.md
```

Update:

```text
Docs/FeatureSpecs/Abilities.md
Docs/FeatureSpecs/PlayerController.md
Docs/FeatureSpecs/LedgeClimb.md
Docs/FeatureSpecs/Audio.md
Docs/Architecture.md
Docs/ImplementationPlan.md
Docs/HeroFeelTuning.md
Docs/FeatureSpecs/Gear.md
```

Required doc changes:

### `Abilities.md`

Replace the old separate hold-Sprint contract with:

- Wildstride player-facing name;
- shared Dash input;
- separate Dash and Sprint unlock flags;
- grounded Dash continuation;
- jump carry;
- attack variant;
- cancellation/rearm rules;
- motor-only velocity rule.

### `PlayerController.md`

Add:

- `HeroSprintAction`;
- typed locomotion speed;
- new blackboard fields;
- central cancellation;
- Wildstride animation priority.

### `LedgeClimb.md`

Add:

- Wildstride carry may approach ledge;
- ledge entry cancels/disarms;
- no auto-Sprint after completion.

### `Architecture.md`

Add action and data-flow ownership. Record direct death velocity cleanup.

### `ImplementationPlan.md`

Mark Sprint/Wildstride status accurately after each implemented pass. Do not mark complete before Unity validation.

### `HeroFeelTuning.md`

Add an isolated Wildstride section:

- final tuned values;
- measured run/sprint/jump distances;
- regression checklist;
- note that base hero values were unchanged.

### `Gear.md`

Map `AbilityId.Sprint` to player-facing **Wildstride** presentation if the Gear definition layer is ready. Keep it read-only.

---

## 24. Definition of Done

Wildstride is complete only when:

- one Dash input drives press, hold, and release;
- standalone Sprint input is removed safely;
- Dash behaviour is unchanged while Sprint is locked;
- a valid grounded Dash can transition to Wildstride while held;
- invalid/held/cooldown states cannot start Sprint;
- Sprint speed uses the normal motor pipeline;
- Wildstride jump preserves horizontal carry without changing the normal vertical arc;
- all interruption and rearm rules work;
- ledge climb never causes accidental post-mantle Sprint;
- the dedicated Wildstride attack uses the existing attack pipeline;
- hit/miss/clash/resource behaviour is correct;
- no new velocity write exists outside `HeroMotor`;
- save/load preserves `sprintUnlocked`;
- production ability state remains locked;
- prefab, input, clips, module, colliders, config, audio, VFX, and pickup are assigned;
- automated tests pass;
- manual Unity regression is completed;
- docs match the implementation;
- no claim is made that validation passed until it was actually run.

---

## 25. Recommended First Implementation Prompt Boundary

The implementation agent should receive:

- this plan;
- current repository HEAD;
- `Docs/Architecture.md`;
- `Docs/ImplementationPlan.md`;
- `Docs/FeatureSpecs/Abilities.md`;
- `Docs/FeatureSpecs/PlayerController.md`;
- `Docs/FeatureSpecs/LedgeClimb.md`;
- `Docs/HeroFeelTuning.md`;
- the supplied Silksong files as reference only.

It should be told to implement **Pass 1 and Pass 2 together** as one coherent movement package, then stop for code review and hands-on feel validation before adding the Wildstride attack and final presentation.

That gives the user a complete traversal ability early without mixing movement tuning problems with attack asset authoring.
