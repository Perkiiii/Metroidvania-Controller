# Wildstride Implementation

**Player-facing name:** Wildstride  
**Internal identity:** Sprint (`AbilityId.Sprint`, `PlayerAbilityState.sprintUnlocked`)  
**Status:** corrected Swift Step-style movement loop implemented; attack, final presentation, and
human feel approval remain outstanding.

## Implemented correction

Base Wildstride is resource-free. Zero resource cannot block entry, maintenance, jump carry, or
landing resumption, and `HeroSprintAction` has no `PlayerResourceState` dependency. Dash remains
resource-free. Bind spending, attack resource generation, HUD presentation, save/load, and
death/reset resource rules are unchanged.

Hands-on Play Mode testing found two related grounded handoff defects. First, the keyboard
`Dpad(mode=1)` Move composite and the old raw fallback resolved simultaneous opposite keys to zero;
`HeroInputReader` now resolves them to the most recently pressed physical direction. A later manual
check then confirmed that a genuine short release gap still exited Wildstride. That remaining defect
was the explicit shared neutral-input cancellation in `HeroSprintAction`, followed by
`HeroActionController` forwarding raw zero movement to the motor. Active grounded Wildstride now
keeps its last valid signed direction while held and armed Dash preserves the authorization, and
the controller resolves grounded neutral movement to that remembered direction.

## Runtime model and ownership

`HeroSprintAction` owns the private phase model:

```text
None
PendingAirDashLanding
Grounded
LedgeJumpBuffered
JumpCarry
DisarmedUntilRelease
```

Dash sequence versions, pending landing authorization, captured carry direction, the short
ledge-jump timer, and disarm state remain private. Only grounded Sprint, active jump carry, and the
current signed direction are mirrored to `HeroStateBlackboard`.

`HeroDashAction` publishes a typed, versioned `HeroDashCompletion` with ground/air origin,
direction, and typed end reason. Natural grounded completion may enter Wildstride in the same fixed
step. Natural ordinary air-Dash completion may create one pending landing authorization; the first
landing consumes that exact Dash version whether entry succeeds or fails.

## Grounded movement and animation

- Ordinary movement explicitly requests `HeroLocomotionSpeed.Walk` and `HeroConfig.walkSpeed`.
- Grounded Wildstride explicitly requests `HeroLocomotionSpeed.Wildstride` and
  `HeroAbilityConfig.sprintSpeed`.
- Grounded Wildstride selects `HeroAnimationLibrary.sprint`; ordinary motion selects `walk`.
- `Run`, `runSpeed`, `requireSprintForRun`, and the `run` clip slot remain serialized compatibility
  data and are not selected by ordinary movement.
- `HeroMotor` remains the sole `Rigidbody2D` velocity writer.

Grounded reversal stays in `Grounded`. Sprint authorization and the shared armed Dash command are
preserved; `blackboard.sprinting` stays true; Wildstride speed and animation remain requested.
`HeroMotor` decelerates the old signed velocity toward zero with existing motor tuning, changes
facing at the established zero/sign seam, then accelerates toward `sprintSpeed` in the new
direction. No Walk/Run request or animation frame is inserted.

Grounded horizontal input is required for initial Wildstride entry, but not for maintenance.
Releasing movement while Dash remains held and armed preserves `Grounded` and continues at
Wildstride speed in the last valid direction. A later opposite input enters the same motor-owned
turn. Most-recent digital direction wins while both sides overlap. Dash release still ends the
sequence immediately, and an idle held Dash without a qualifying Dash/Wildstride sequence cannot
create one.

## Wildstride Jump direction

Wildstride Jump uses the unchanged normal vertical Jump path. At launch, it captures one signed
horizontal carry direction. Same-direction input maintains `sprintJumpSpeed`. Opposite-direction
input cancels the locked carry instead of changing its sign. The motor then decays the remaining
momentum and resumes ordinary air steering through the normal locomotion pipeline. A cancelled
carry cannot resume Wildstride on landing; an authorized same-direction carry may still resume in
the landing fixed step.

## Sprint/ledge Jump buffer

Leaving the ground from active grounded Wildstride creates one private, single-use
`LedgeJumpBuffered` authorization. Its provisional authored duration is:

```text
sprintLedgeJumpBufferTime: 0.08 seconds
```

A normal Jump that begins before expiry consumes the buffer and starts captured Wildstride Jump
carry. The buffer does not start grounded Wildstride, create another Dash, or authorize a later
landing. Ordinary falls, Walk ledge exits, cancelled Dashes, and stale sequences do not create it.
Dash release, neutral input while buffered, Attack, Bind, hurt/recoil, death, hazard/respawn flow, control/input
loss, wall states, ledge climb, pogo, scene/scripted motion, component disable, and Sprint unlock
loss clear it.

The Sprint buffer is separate from and does not change normal `HeroConfig.coyoteTime` or
`jumpBufferTime`. `HeroActionController.FixedTick` prepares the Wildstride buffer after wall
arbitration and before `HeroJumpAction`, then performs the existing post-Dash/landing reconciliation
before `HeroMotor.FixedTick`.

## Deferred optional Gear modifier

A future optional Gear modifier inspired by Silkspeed Anklets may increase grounded Wildstride
speed. Resource would drain only while that enhanced grounded speed is active. Base Wildstride must
remain available at empty resource, and airborne Wildstride Jump receives neither the bonus nor
the drain. Exact speed/cost values, grace periods, persistence, and Gear ownership are deferred.
This modifier is not implemented.

## Remaining parity work

The full feature is not complete. Still deferred:

- down-dash;
- dedicated Wildstride run-slash;
- final skid animation;
- final back-Sprint animation;
- final VFX;
- final audio;
- swimming integration;
- optional resource-draining enhanced-speed Gear;
- final distance/timing tuning and human Play Mode feel approval.

Automated tests cover resource independence, both grounded reversal directions, grounded neutral
direction retention and digital-key arbitration, captured air carry, the short ledge-jump buffer,
typed Dash handoffs, same-step landing reconciliation, input safety, production asset wiring, and
the real action/motor/animation PlayMode order. Automated correctness does not replace hands-on feel
validation.
