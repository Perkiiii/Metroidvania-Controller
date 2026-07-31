# Ledge Climb

## Status and scope

The first playable static-terrain ledge climb is implemented. It is an always-available core hero
action, not an unlock flag and not part of Wall Latch.

Included:

- automatic airborne entry while deliberately approaching a valid ledge;
- a short pre-catch reservation when an otherwise-valid ledge is just below the minimum catch
  height;
- Catch → PullUp → Settle → Complete;
- pre-wall-slide arbitration;
- ground-initiated and airborne dash-to-mantle handoff, provided the hero is airborne at entry;
- static Terrain, aligned adjacent static Terrain colliders, and static CompositeCollider2D
  surfaces;
- authored `NoLedgeClimbVolume` exclusions;
- code-owned movement, timing, final placement, and cleanup;
- presentation through the optional `HeroAnimationLibrary.ledgeClimb` Animancer clip.

Excluded:

- grounded wall vaulting;
- established-wall-slide-to-mantle transitions;
- indefinite hanging, dropping, or jumping away;
- moving platforms and inherited platform velocity;
- one-way platforms, Breakable surfaces, hazards, and Wall Latch.

## Ownership

| Owner | Responsibility |
|---|---|
| `HeroActionController` | Evaluates ledge ownership before wall slide, prevents same-step action overwrites, and wires typed Wildstride start/end handoffs |
| `HeroLedgeClimbAction` | Entry rules, phases, timers, retained target, revalidation, cancellation, diagnostics |
| `HeroSensors` | Broad wall candidate and strict ledge geometry queries |
| `HeroMotor` | Gravity suspension, movement suppression, scripted positions, exact final placement |
| `HeroStateBlackboard` | Shared `ledgeClimbing` flag only |
| `HeroAnimationController` | Optional presentation and non-authoritative completion signal |

`HeroController` only injects dependencies and forwards existing lifecycle interruptions.

## Entry and arbitration

Entry requires:

- the hero is airborne;
- `wallSliding` and `wallJumping` are false;
- attack, Bind, recoil, control lock, and input block are inactive;
- vertical velocity is below `HeroConfig.ledgeMaxUpwardSpeed`;
- the directional ledge query finds a front-wall contact;
- horizontal input points toward the wall, or the active dash owns that direction;
- `HeroSensors.TryFindLedge` succeeds.

The fixed-step order is ledge query → wall slide → wall jump → jump → dash → attack. When ledge
ownership is accepted, the controller returns immediately. If the directional query has validated
all ledge geometry but the top is still below `ledgeMinimumHeightFromFeet`, it reports
`LedgeProbeFailure.PreCatchHeight` with a usable `LedgeProbeResult`. `HeroLedgeClimbAction` then
reserves the ledge for `HeroConfig.ledgePreCatchGraceDuration` while live approach input (or the
captured dash direction) continues. During that reservation `HeroActionController` suppresses only
new wall-slide entry; gravity and ordinary movement continue, and the strict query is retried every
fixed step. A valid catch begins as soon as the minimum height is reached. Invalid geometry,
unsupported surfaces, hazards, restrictions, and expired reservations do not receive this
protection. An established wall slide is never interrupted or queried for automatic mantle.

Ordinary approach direction comes from the current horizontal input. It is not required to match a
stale `FacingDirection`; the sensor query owns the authoritative wall direction. Dash approach uses
the dash action's captured `Direction`.

A dash may have started on the ground, but ledge entry is still airborne-only. A grounded dash into
a wall remains a dash. A successful dash handoff calls typed
`HeroDashAction.Cancel(HeroDashEndReason.LedgeClimb)`;
cooldown and `airDashUsed` are preserved. Invalid geometry never alters dash state.

A Wildstride jump carry may supply ordinary horizontal approach intent. When a validated ledge entry
begins from an authorized Wildstride sequence, `HeroActionController` notifies
`HeroSprintAction` once. The sprint action preserves the private Dash sequence version and
remembered direction, ends forced carry through `HeroMotor`, clears Wildstride locomotion and
presentation, and enters a private suspended phase. The climb owns movement; it never displays
Wildstride locomotion or applies Wildstride velocity. A non-authorized climb cannot create
Wildstride authorization. Dash itself is still handed off and cancelled as described above, but
the shared Dash/Wildstride command is not disarmed for an authorized suspension.

On successful completion, a typed end notification resumes grounded Wildstride when Sprint remains
unlocked and Dash remains held and armed. Current nonzero horizontal input determines the resumed
direction; neutral input falls back to the preserved remembered direction. The handoff occurs in the
same simulation step where practical and does not replay another Dash completion. Releasing Dash
during the climb consumes the preserved authorization and prevents resumption. Hurt, death, hazard,
control lock, input suspension, scene/scripted motion, unlock loss, wall states, Bind, or component
disable consume the authorization and follow the existing hard-cancellation/disarming contract.

While Wildstride is authorized, holding Dash maintains automatic grounded movement in the remembered
direction. Horizontal input steers or changes that remembered direction but does not need to remain
held.

## Geometry contract

The strict query uses `HeroConfig.ledgeSurfaceLayers`, serialized as Terrain only. It checks:

- eligible non-trigger front and top contacts;
- ledge height relative to the current feet position;
- two compatible top samples and minimum upward normals;
- a complete hero-width support sample set;
- catch, crest, standing, and interpolated corridor body clearance;
- a collision-valid final standing placement;
- absence of enabled `NoLedgeClimbVolume` and hazard markers.

The pre-catch result is only produced after the same surface, support, clearance, corridor, target,
restriction, and hazard checks pass. It is a reservation diagnostic, not an alternate acceptance
path: the normal strict minimum-height query must succeed before Catch begins.

The support may be one collider, one CompositeCollider2D, or two aligned adjacent static Terrain
colliders. A seam is accepted only when bounded samples across the standing footprint remain
supported within the configured height, normal, and gap tolerances.

Ordinary colliders with an attached Rigidbody2D are rejected with
`UnsupportedRigidbodySurface`. Unity requires every CompositeCollider2D to have a Rigidbody2D, so
the one engine-required exception is a CompositeCollider2D whose body is explicitly `Static`.
Dynamic and Kinematic composite bodies remain rejected.

## Target-relative data

`LedgeProbeResult` retains the primary collider, optional secondary support, optional target body,
and explicit `TargetFrame`. Surface, Catch, Crest, and Standing points are stored in that frame's
local coordinates and resolved every relevant `FixedTick`.

The current action records the initial frame pose and cancels if a supposedly static target moves.
This representation is ready for a later moving-platform policy without implementing parenting,
velocity inheritance, or platform attachment now.

## Movement and animation

Gameplay timing comes from `HeroConfig`:

| Phase | Initial value |
|---|---:|
| Catch | 0.08 s |
| PullUp | 0.28 s |
| Settle | 0.05 s |

`HeroMotor` suppresses normal movement, suspends gravity, and moves deterministically between the
resolved targets. Completion performs one final path/placement validation.
- The climb itself owns movement and presentation for its full active interval; Wildstride is
  suspended rather than ticked as a locomotion owner during Catch, PullUp, and Settle.

`HeroLedge.anim` is a non-looping presentation clip assigned to
`HeroAnimationLibrary.ledgeClimb`. Playback speed is aligned with the configured total duration.
Its Animancer end callback is informational only. Missing, looping, short, long, or interrupted
animation cannot prevent timer-owned completion or cleanup.

## Cancellation

One idempotent action cleanup path handles control locks, damage, hurt, hazards, death, scene entry,
scene transitions, target invalidation or movement, component disable, and hero destruction. It:

- stops ledge-owned motor movement;
- restores gravity and normal movement;
- clears `ledgeClimbing` and retained target data;
- stops the ledge presentation;
- never resumes or refunds an interrupted dash;
- never resets wall-jump state.

Dash and Wildstride are not cancelled, and the Dash/Wildstride command is not disarmed, when a
pre-catch reservation is created, refreshed, rejected, or timed out. A successful ledge `Begin`
still cancels the active Dash handoff and calls the one-time Wildstride suspension notification.
Typed completion/cancellation then decides whether the preserved authorization resumes or is
consumed. This does not alter pre-catch geometry validation or reservation timing.

## Authored exclusion

`NoLedgeClimbVolume` is a marker on a trigger Collider2D. It has no registry, singleton, manager,
polling, progression rules, or generic traversal framework. Live overlap queries make disabling,
destroying, or unloading it sufficient to stop its effect.

## Validation

Automated EditMode coverage lives in:

- `HeroLedgeClimbTests`;
- `HeroLedgeClimbAssetTests`.

Manual validation must additionally cover feel, presentation transitions, damage/hazard/death
during each phase, scene transition and scripted entry, and real room geometry seams.
