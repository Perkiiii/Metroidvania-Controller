# Hero Feel Tuning — Baseline

**Established:** 2026-05-19
**Status:** Baseline — hero movement and combat feel validated in playtest. Do not casually change these values.

---

## Purpose

This document records the values that produce the current validated movement feel. It exists so that future polish work, ability additions, or animation integration can be done without accidentally drifting the feel and losing the tuned baseline. If you need to change a value, read the notes below first, run the regression checklist after, and update this document if you keep the change.

Core movement values are a snapshot of `Assets/_Project/ScriptableObjects/Hero/HeroConfig.asset`.
Dash, wall-slide, wall-jump, and double-jump values live in `Assets/_Project/ScriptableObjects/Hero/HeroAbilityConfig.asset`.

## Wildstride — HeroAbilityConfig (provisional)

```text
sprintSpeed:                  10
sprintJumpSpeed:              10
sprintLedgeJumpBufferTime:     0.08 seconds
```

These corrected Pass 1/2 values are automated-test inputs, not validated feel targets. Both speeds
are above `runSpeed` (6.5) and below `dashSpeed` (18). Ordinary movement now explicitly uses
`walkSpeed` (4.32); `runSpeed` remains serialized compatibility data and is not a default movement
target. Grounded Wildstride uses the explicit Sprint animation slot. A previous hands-on Play Mode
test found grounded keyboard reversal exited Wildstride because opposite physical keys collapsed to
a false neutral input sample. The corrected input seam preserves authorization while the existing
motor decelerates toward zero, changes facing, and accelerates in the requested direction.
Subsequent hands-on testing found that a genuine short release gap still cancelled in
`HeroSprintAction`. Active grounded Wildstride now remembers and continues its last valid direction
through neutral input while Dash remains held and armed; this behavioural correction adds no tuning
value.

Base Wildstride is resource-free. The 0.08-second Sprint/ledge Jump buffer is provisional and
unvalidated, chosen near the existing 0.08-second coyote-time scale. It does not change normal
`coyoteTime` or `jumpBufferTime`. Airborne Wildstride Jump captures launch direction; opposite input
cancels carry into ordinary motor-owned air steering instead of reversing full carry speed.

No validated baseline movement, gravity, Jump, Dash, wall, ledge, attack, pogo, hurt, or Bind value
changed in this pass. Manual distance measurement, turn/buffer feel review, and the complete
regression checklist remain required before recording the Wildstride values as validated.

---

## Gravity and Jump Arc

```
jumpSpeed:                16
maxJumpSustainSteps:       6
minJumpReleaseSteps:       0
jumpCutVelocityMultiplier: 0.35
coyoteTime:               0.08
jumpBufferTime:            0.1

baseGravityScale:          0.79
riseGravityScale:          0.79
apexGravityScale:          0.55
fallGravityScale:          1.4

normalizeGravityToReference: true
referenceGravityY:        -29.8   (effective multiplier: scale × (29.8 / projectGravityY))
apexVelocityThreshold:     1.0    (m/s — velocity window treated as apex)
maxFallSpeed:             15.0
```

### Notes

**`riseGravityScale` (0.79)**
Gravity applied while the hero is rising with jump sustain active. Kept equal to `baseGravityScale` so the initial launch feels consistent with standing-still gravity. Lowering this makes the hero float upward; raising it collapses the peak height.

**`apexGravityScale` (0.55)**
Gravity applied when vertical speed is within `apexVelocityThreshold` (±1 m/s) and the hero is not grounded. This creates a brief hang at the top of the arc — the hero slows down, hangs, then falls. Raising this toward 0.79 eliminates the hang. Lowering it toward 0 extends it into a float. Do not set lower than 0.4 without testing wall jump timing.

**`fallGravityScale` (1.4)**
Gravity applied while falling (velocity.y < −0.01). Higher than rise gravity — this is what makes the arc feel asymmetric and intentional rather than floaty. At 1.4, fall is ~1.77× faster than rise. Raising this further makes the game feel more punishing; lowering it back toward 0.79 restores the floaty symmetric arc.

**`maxJumpSustainSteps` (6)**
Number of FixedUpdate steps during which the motor re-applies `jumpSpeed` upward while jump is held. At 6 steps (~0.12s at 50Hz), holding jump adds meaningful height over a tap. At 1 (the original value), there was almost no variable height. Do not lower below 4 without retesting short-hop vs full-jump height range.

**`jumpCutVelocityMultiplier` (0.35)**
When jump is released early, vertical velocity is multiplied by this value. At 0.35, a tap jump reaches roughly 35% of the full-hold height. Lowering this makes short hops shorter; raising it toward 1.0 removes variable jump height entirely.

**`coyoteTime` (0.08s) / `jumpBufferTime` (0.1s)**
Standard input-assist values. Coyote lets the player jump just after walking off a ledge. Buffer lets a jump press register up to 0.1s before landing. Do not lower these — they are below the threshold of being noticeable to players but prevent frustrating missed jumps.

---

## Dash — HeroAbilityConfig

```
dashSpeed:    18
dashDuration:  0.22
dashCooldown:  0.45
```

### Notes

**`dashSpeed` (18)**
Horizontal velocity set during the dash. At 18 units/s × 0.22s = 3.96 units of travel per dash. This is roughly 3× `runSpeed` (6.5). Increasing speed without shortening duration makes the dash cover too much ground.

**`dashDuration` (0.22)**
How long the dash velocity is held. Gravity is zeroed during this window, making the dash fully horizontal. At 0.22s the dash reads as a quick dodge rather than a glide. The old value was 0.4s (7.2 units) — that felt like sliding, not dashing.

**`dashCooldown` (0.45)**
Time after a dash begins before another can start. At 0.45s the player can dash roughly twice per second if grounded. Raising this makes the dash feel more precious; lowering it makes it spammable.

---

## Attack

```
attackCooldown:            0.4
attackRecovery:            0.22
attackBufferTime:          0.1
groundAttackMoveMultiplier: 0.75
attackDirectionThreshold:  0.5
attackHitStopDuration:     0.04
attackClashHitStopDuration: 0.05
```

### Notes

**`attackCooldown` (0.4)**
Minimum time between the start of one attack and the start of the next. This is the rhythm of the basic attack chain — at 0.4s, attacks feel deliberate rather than spammable.

**`attackRecovery` (0.22)**
How long `attackRecovering = true` persists after an attack begins. At 0.22s (shorter than `attackCooldown`) `attackRecovering` clears before the next attack can fire, which removes the dead zone where the player was in recovery but not yet allowed to start the next attack. Critical for wall jump feel: `CanStartWallJump` checks `!attackRecovering`, so recovery outlasting cooldown would block wall jumps mid-combo.

**`groundAttackMoveMultiplier` (0.75)**
Horizontal speed multiplied by this while attacking on the ground. 0.75 keeps momentum without letting the player skate through enemies. Do not set above 1.0.

---

## Wall Slide — HeroAbilityConfig

```
wallSlideInitialHoldTime:  0.25
wallSlideInitialSpeed:     0.1
wallSlideAcceleration:     16
wallSlideSpeed:           -3.0
wallSlideInputThreshold:   0.3
```

### Notes

**`wallSlideInitialHoldTime` (0.25s) + `wallSlideInitialSpeed` (0.1)**
When the hero first grabs a wall, vertical velocity is capped at 0.1 m/s (near zero) for 0.25s before the acceleration phase begins. This creates the physical cling moment — the hero decelerates hard on contact. Lowering `initialHoldTime` makes the grab feel less sticky. Lowering `initialSpeed` makes the initial cling tighter but can cause a jarring snap.

**`wallSlideAcceleration` (16)**
Rate at which vertical velocity accelerates toward `wallSlideSpeed` after the initial hold. At 16, the hero reaches full slide speed in roughly 0.2s. Higher values make the slide speed feel more immediate; lower values make it feel like the hero struggles against gravity.

**`wallSlideSpeed` (-3.0)**
Terminal slide velocity (downward). -3 is slow enough to give the player time to react and wall jump. Do not set below -5 or the wall slide loses its utility as a stall mechanic.

---

## Wall Jump — HeroAbilityConfig

```
wallJumpHorizontalSpeed:  10
wallJumpVerticalSpeed:    16
wallJumpRelatchLockout:    0.35
```

### Notes

**`wallJumpHorizontalSpeed` (10) + `wallJumpVerticalSpeed` (16)**
The velocity impulse applied on wall jump. Horizontal is ~1.5× run speed, giving a strong push away from the wall. Vertical matches `jumpSpeed` so the wall jump arc height is similar to a ground jump. If you lower `wallJumpHorizontalSpeed` below 8, the hero can drift back into the wall before `relatchLockout` expires, making wall climbing trivial.

**`wallJumpRelatchLockout` (0.35s)**
After a wall jump, `blackboard.wallJumping = true` for this duration, which blocks re-entering `wallSliding`. At 0.35s the player cannot re-grab the same wall until they have meaningfully moved away. The original value was 0.15s — too short to prevent repeated grabbing. Do not lower below 0.25s.

---

## Double Jump — HeroAbilityConfig

```
doubleJumpSpeed:           14
resetDoubleJumpOnWallSlide: false
```

### Notes

**`doubleJumpSpeed` (14)**
Vertical velocity applied on a valid double jump. At 14 the double jump reaches slightly less height than a full ground jump (jumpSpeed 16 / 18 in HeroConfig). This is intentional — the double jump is a mid-air recovery tool, not a height extender. If you raise this above 16 the double jump becomes stronger than a ground jump; lower than 10 and it feels unreliable for clearing standard gaps.

**`resetDoubleJumpOnWallSlide` (false)**
When true, entering wall slide restores the double jump. Currently off — wall latch will serve the "reset on wall" use case once implemented. Do not enable until wall latch is in place or wall climbing becomes trivial.

---

## Downslash Pogo

```
downslashBounceVelocity: 8
```

### Notes

**`downslashBounceVelocity` (8)**
Upward velocity applied to the hero immediately after an airborne downslash hits a valid `IHeroDownslashResponder` (currently: `MushroomEnemy`). At 8 the bounce reaches roughly half of a full jump height — enough for a reliable chain but not so powerful that the pogo sequence becomes trivially safe. The bounce consumes once per swing (`downslashBounceConsumedThisAttack`), so multi-hit downslashes still only bounce once.

---

## Ledge Climb — HeroConfig

```
ledgeSurfaceLayers:              Terrain
ledgeMaxUpwardSpeed:             5
ledgeMinimumHeightFromFeet:      0.25
ledgeMaximumHeightFromFeet:      1.35
ledgeTopProbeExtraHeight:        0.3
ledgeTopSampleInset:             0.04
ledgeSurfaceHeightTolerance:     0.08
ledgeMinimumUpNormal:            0.85
ledgeSupportGapTolerance:        0.08
ledgePlacementSkin:              0.02
ledgeCatchDrop:                  0.1
ledgeCatchDuration:              0.08
ledgePullUpDuration:             0.28
ledgeSettleDuration:             0.05
```

These values are an isolated first-pass ledge package derived from the current hero collider and
representative static Terrain geometry. The three phase durations total 0.41 seconds to align with
the current placeholder presentation, but code timing—not animation—is authoritative. No existing
movement, dash, wall, combat, hurt, pogo, Bind, or resource value was modified.

The serialized HeroConfig/HeroAbilityConfig values currently differ from values recorded in
HeroFeelTuning.md. This discrepancy predates ledge-climb work and must be resolved in a dedicated
feel-baseline reconciliation pass.

### Ledge-specific manual checks

- [ ] Fall toward a valid wall top while pressing toward it; Catch should win before wall slide.
- [ ] Remain on a tall wall without a valid top; existing wall slide and wall jump feel must remain unchanged.
- [ ] Confirm an established wall slide never pulls into mantle.
- [ ] Confirm a ground-initiated dash can mantle only after leaving the ground.
- [ ] Confirm a grounded dash into a wall base cannot mantle.
- [ ] Confirm an air dash into a valid ledge stays consumed and keeps its cooldown.
- [ ] Confirm narrow, low-ceiling, Breakable, hazardous, moving, and authored-excluded candidates reject.
- [ ] Confirm aligned static seams work and visible unsupported gaps reject.
- [ ] Interrupt each phase with damage, hazard, death, control lock, and scene transition; gravity and movement must recover.
- [ ] Remove the ledge clip and repeat; gameplay must still complete.
- [ ] Rerun every existing Movement, Dash, Wall, Combat, Damage, and Bind/Resource check below.

---

## WARNING — Do Not Casually Change These Values

The following values are load-bearing for the feel of the entire hero. Changing any of them shifts multiple interacting systems simultaneously:

- `fallGravityScale` — changes feel of every aerial moment: dash exit, wall jump arc, pogo timing, hurt knockback hang time
- `maxJumpSustainSteps` — changes short-hop vs full-jump height range; also changes how much height the player can build during a wall jump combo
- `wallJumpRelatchLockout` — going below 0.25 risks trivial wall climbing
- `attackRecovery` — must stay ≤ `attackCooldown` (0.4) or wall jumps become blocked mid-combo
- `jumpCutVelocityMultiplier` — the short-hop multiplier; changing this invalidates all jump-height-sensitive level geometry

If you change one of these and the regression checklist below passes, update the values in this document before committing.

---

## Manual Regression Checklist

Run this against the Mushroom test room after any HeroConfig change. All items must pass before the change is considered stable.

### Movement

- [ ] **Short hop** — tap jump, release immediately. Hero should reach roughly knee height on a standard wall. Multiplier is `jumpCutVelocityMultiplier` (0.35). If short hop reaches full jump height, `minJumpReleaseSteps` or the cut multiplier is wrong.
- [ ] **Full jump** — hold jump to apex. Hero should reach clearly higher than short hop. The arc should hang briefly at the top before falling. If rise and fall feel symmetric (no hang, same speed both ways), `apexGravityScale` and `fallGravityScale` have drifted.
- [ ] **Fall speed** — walk off a tall ledge, do not jump. Hero should fall noticeably faster than it rises. If fall feels identical to rise speed, `fallGravityScale` has been lowered.
- [ ] **Coyote jump** — run off a ledge edge, wait a beat, then press jump. Should still jump for approximately 0.08s after leaving the edge.
- [ ] **Buffered jump** — press jump just before landing; hero should jump immediately on contact.

### Dash

- [ ] **Dash distance** — ground dash should cover roughly 4 units (dash from one tile, land on the next standard gap). If it overshoots reliably, `dashDuration` or `dashSpeed` is too high.
- [ ] **Air dash** — dash once in air, land, dash again — each air-dash should only be available once before grounding. `airDashUsed` resets on landing.
- [ ] **Attack into dash** — attack, then immediately press dash. Dash should fire after `attackCooldown` (0.4s). Should not be blocked by `attackRecovering` (which expires at 0.22s).

### Wall

- [ ] **Wall slide** — fall into wall while pressing into it. Hero should momentarily cling (near-zero speed for 0.25s) then accelerate to -3 m/s slide. Should NOT snap instantly to slide speed on contact.
- [ ] **Wall jump** — wall slide then jump. Hero should launch away from the wall and be unable to immediately re-grab the same wall. At 0.35s lockout, the player should need to drift noticeably away before a re-grab is possible.
- [ ] **Wall climb prevention** — stand next to a wall, repeatedly tap jump while pressing into the wall. Should not be able to climb the wall indefinitely without actually clearing it. If climbing is possible, `wallJumpRelatchLockout` needs to be raised.

### Combat

- [ ] **Attack chain** — press attack repeatedly. Four or five attacks should chain smoothly with the `attackCooldown` (0.4s) rhythm. No dead zone where input is ignored after recovery clears.
- [ ] **Downslash pogo on Mushroom** — in air above Mushroom, press down + attack. Hero should bounce upward with `downslashBounceVelocity` (8). Bounce should feel like a modest hop, not a full jump.
- [ ] **Repeated pogo chain** — bounce off Mushroom three times in a row without touching the ground. Should be mechanically consistent on each bounce. The bounce velocity (8) should be enough to stay airborne between downslashes.

### Damage

- [ ] **Hurt recovery** — take a hit from Mushroom contact damage. Hero should stagger for `hurtStunDuration` (0.35s), receive knockback, then regain control. I-frames should prevent a second hit during the stun.
- [ ] **Recoverable hazard** — touch a `RecoverLocal` hazard. Health should reduce (bonus health consumed first if present) and resource should be unaffected; hero repositions locally without a full death sequence.
- [ ] **Death and respawn** — let health reach zero. Hero should die, respawn at checkpoint, full health restored, bonus health cleared, and resource emptied (see `Docs/FeatureSpecs/PlayerHealthAndResource.md`).

### Bind and Resource

- [ ] **Bind start** — while grounded with resource ≥ the configured cost and health below maximum, hold the Bind input. Hero should hold stationary (movement suppressed) and play the Bind animation.
- [ ] **Bind completion** — hold Bind for the full configured duration without interruption. Resource should spend and health should heal exactly once, at completion only.
- [ ] **Bind cancel — input release** — start Bind, release the input before completion. No resource spent, no health healed, movement suppression released immediately.
- [ ] **Bind cancel — damage/hazard** — start Bind, take a hit or hazard damage before completion. Bind cancels immediately with no spend/heal.
- [ ] **Bind cancel — loss of ground** — start Bind, get knocked/pushed airborne before completion. Bind cancels immediately.
- [ ] **Bind cancel — scene transition** — start Bind, cross a transition gate before completion. Bind cancels without leaking movement suppression into the new scene.
- [ ] **Resource generation** — land an accepted hit on an enemy configured for resource generation. Resource bar should visibly fill by the configured amount; a blocked/invulnerable/ignored hit should not generate resource.
- [ ] **Resource bar at empty** — spend resource to zero. The bar should remain visible (not hidden) and show empty.
