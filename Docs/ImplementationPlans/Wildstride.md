# Wildstride Implementation

**Player-facing name:** Wildstride  
**Internal identity:** Sprint (`AbilityId.Sprint`, `PlayerAbilityState.sprintUnlocked`)  
**Status:** corrected Swift Step-style movement loop implemented, neutral Dash/carry direction
continuity hardened, ordinary-fall and ledge-climb continuity hardened, and Double Jump hard
cancellation implemented; attack, final presentation, and human feel approval remain outstanding.

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

The authorized sequence owns its remembered direction. A natural grounded Dash completion uses
current nonzero steering when available and otherwise uses the direction captured by the Dash;
therefore a neutral Dash can continue into Wildstride without allowing a held Dash to manufacture a
new sequence. A natural air-Dash stores its completion direction with its one-shot landing
authorization and uses it when landing input is neutral. While Dash remains held, neutral input
does not cancel `AirborneCarry`; only opposite steering, normal falling, or an approved hard
interruption ends forced carry. After forced carry ends, authorized airborne phases resolve neutral
horizontal intent to the remembered direction at ordinary air-steering speed.

## Runtime model and ownership

`HeroSprintAction` owns the private phase model:

```text
None
PendingAirDashLanding
Grounded
LedgeJumpBuffered
AirborneCarry
AirborneAuthorised
LedgeClimbSuspended
DisarmedUntilRelease
```

`AirborneCarry` means forced horizontal Wildstride carry is active and the current sequence retains
landing authorisation. `AirborneAuthorised` means forced carry has ended, ordinary airborne
movement/gravity owns the hero, and that same sequence still owns permission for its first landing.
Dash sequence versions, pending landing authorization and its captured direction, captured carry direction, the short
ledge-jump timer, suspension state, and disarm state remain private. Only grounded Sprint, active
carry, carry presentation, and the current signed direction are mirrored to `HeroStateBlackboard`.

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

Initial grounded Wildstride entry uses current nonzero steering when available and otherwise the
direction captured by the natural Dash completion. Releasing movement while Dash remains held and
armed preserves `Grounded` and continues at Wildstride speed in the last valid direction. A later
opposite input enters the same motor-owned turn. Most-recent digital direction wins while both
sides overlap. Dash release still ends the sequence immediately, and an idle held Dash without a
qualifying Dash/Wildstride sequence cannot create one.

While Wildstride is authorized, holding Dash maintains automatic grounded movement in the remembered
direction. Horizontal input steers or changes that remembered direction but does not need to remain
held.

Digital movement overlap is resolved from the currently enabled `Move` action's named composite
parts (`left`/`right`) when they are digital `ButtonControl`s, plus a directly bound
`DpadControl`. This covers default A/D, arrow keys, custom keyboard composite rebindings, and the
production Gamepad D-pad: newest press wins, releasing it restores the older held direction, and
releasing both returns true neutral. Analogue stick values are never fed through this press memory.
Other non-composite/custom controls remain under the Input System's normal value resolution rather
than fragile generic reflection; the raw keyboard fallback is limited to the legacy path when no
`Move` action is available, so removed default bindings do not remain active after rebinding.

## Wildstride Jump direction

Wildstride Jump uses the unchanged normal vertical Jump path. At launch, it captures one signed
horizontal carry direction from current nonzero input, falling back to the remembered authorised
direction. Same-direction or neutral input while Dash remains held maintains `sprintJumpSpeed`.
Opposite-direction input ends only the forced carry; `HeroMotor.EndWildstrideCarry()` then lets
ordinary air steering and gravity take over. The phase becomes `AirborneAuthorised`, so the same
sequence can still resume Wildstride on its first landing. Neutral input in that ordinary
authorized phase resolves to the remembered direction at walk/air-steering speed. Normal falling
ends forced carry automatically; the carry does not apply `sprintJumpSpeed` through an arbitrarily
long descent. A normal Wildstride Jump therefore preserves first-landing authorization after
forced carry ends. Double Jump is different: it hard cancels the current Wildstride sequence,
clears landing authorization and the jump buffer, ends carry through `HeroMotor`, and disarms Dash
until physical release. Double Jump vertical speed and the normal Double Jump path remain unchanged.

## Sprint/ledge Jump buffer

Leaving the ground from active grounded Wildstride creates one private, single-use
`LedgeJumpBuffered` authorization. Its provisional authored duration is:

```text
sprintLedgeJumpBufferTime: 0.08 seconds
```

A normal Jump that begins before expiry consumes the buffer and starts captured Wildstride Jump
carry. Neutral input does not erase the valid buffer; launch still falls back to its remembered
direction. Landing before expiry resumes grounded Wildstride and consumes the sequence once. If the
buffer expires without Jump, only the specialized Jump opportunity expires: the phase becomes
`AirborneAuthorised`, ordinary airborne movement and gravity take over, and the first valid landing
still resumes Wildstride. Current nonzero input may update the landing direction; neutral input uses
the remembered direction. The buffer does not start grounded Wildstride or create another Dash.
Dash release, Attack, Bind, hurt/recoil, death, hazard/respawn flow, control/input loss, wall
states, pogo, scene/scripted motion, component disable, and Sprint unlock loss clear authorization
and any buffer. Ordinary walk-offs create the buffer only when leaving active grounded Wildstride.

The Sprint buffer is separate from and does not change normal `HeroConfig.coyoteTime` or
`jumpBufferTime`. `HeroActionController.FixedTick` prepares the Wildstride buffer after wall
arbitration and before `HeroJumpAction`, then performs the existing post-Dash/landing reconciliation
before `HeroMotor.FixedTick`.

## Ledge-climb suspension

When an authorized Wildstride sequence enters a validated ledge climb, `HeroActionController` hands
off once to `HeroSprintAction`. The sprint action preserves its private Dash sequence version and
remembered direction, ends forced carry through `HeroMotor`, clears Wildstride locomotion and
presentation, and enters `LedgeClimbSuspended`. The climb owns movement and never displays or applies
Wildstride locomotion. A climb with no authorization cannot create one.

On successful completion, the typed ledge end notification resumes grounded Wildstride in the same
simulation handoff when Sprint remains unlocked and Dash is still held and armed. Current horizontal
input wins when nonzero; otherwise the preserved remembered direction is used. No new Dash
completion is started. Releasing Dash during the climb consumes the preserved authorization and
prevents resumption. Hurt, death, hazards, control lock, input suspension, scene/scripted motion,
unlock loss, wall states, Bind, and component disable remain hard cancellations and disarm where
the existing interruption contract requires it.

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

Automated tests cover resource independence, grounded automatic movement and steering, captured
carry versus persistent landing authorisation, ordinary short and long walk-offs, natural falling,
normal Wildstride Jump continuity, Double Jump hard cancellation, neutral remembered direction,
opposite-air-input continuation, the short ledge-jump buffer, typed Dash handoffs, temporary
ledge-climb suspension/resumption, same-step landing reconciliation, default and rebound digital
input, D-pad/analogue separation, input safety, production asset wiring, and the real
action/motor/animation PlayMode order. Automated correctness does not replace hands-on feel
validation.
