# Ledge Climb

## Status and scope

The first playable static-terrain ledge climb is implemented. It is an always-available core hero
action, not an unlock flag and not part of Wall Latch.

Included:

- automatic airborne entry while deliberately approaching a valid ledge;
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
| `HeroActionController` | Evaluates ledge ownership before wall slide and prevents same-step action overwrites |
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
- a front-wall contact exists;
- horizontal input points toward the wall, or the active dash owns that direction;
- `HeroSensors.TryFindLedge` succeeds.

The fixed-step order is ledge query → wall slide → wall jump → jump → dash → attack. When ledge
ownership is accepted, the controller returns immediately. An established wall slide is never
queried for automatic mantle.

A dash may have started on the ground, but ledge entry is still airborne-only. A grounded dash into
a wall remains a dash. A successful dash handoff calls typed
`HeroDashAction.Cancel(HeroDashEndReason.LedgeClimb)`;
cooldown and `airDashUsed` are preserved. Invalid geometry never alters dash state.

A Wildstride jump carry may supply ordinary horizontal approach intent. Successful ledge entry
cancels Dash, grounded Wildstride, and jump carry and disarms the shared Dash/Wildstride command
until physical release. Completing a mantle while Dash remains held cannot begin Wildstride;
release and a later fresh qualifying Dash are required.

## Geometry contract

The strict query uses `HeroConfig.ledgeSurfaceLayers`, serialized as Terrain only. It checks:

- eligible non-trigger front and top contacts;
- ledge height relative to the current feet position;
- two compatible top samples and minimum upward normals;
- a complete hero-width support sample set;
- catch, crest, standing, and interpolated corridor body clearance;
- a collision-valid final standing placement;
- absence of enabled `NoLedgeClimbVolume` and hazard markers.

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
