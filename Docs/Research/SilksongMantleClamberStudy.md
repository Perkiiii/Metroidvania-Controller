# **1\. Executive Summary**

The feature is successfully located at the C\# boundary, but its central PlayMaker graph is absent. Silksong calls the behaviour **mantle/clamber**, not “ledge grab.”

`HeroController` detects an airborne, wall-adjacent candidate and sends `"AIR MANTLE"` to its serialized `mantleFSM`. `CheckHeroClamberLedge` exposes a second, stricter geometry test to that FSM. The FSM graph—presumably responsible for the actual grab/pull-up placement, primary animation, and completion—is not included in this workspace.

Most important sources:

* `HeroController.cs` — entry dispatch and the confirmed geometry test.  
* `CheckHeroClamberLedge.cs` — FSM bridge for the geometry test.  
* `NoClamberRegion.cs` / `AutoNoClamberRegion.cs` — authored no-clamber restrictions.  
* `HeroAnimationController.cs` — mantle recovery/cancel presentation.  
* `Movement Flow.md` — best available narrative explanation, explicitly noting absent FSM wiring.

Largest uncertainty: the missing `mantleFSM` serialization prevents verification of hang duration, pull-up motion, main animation clips, exact placement, input exits, and cleanup sequence.

# **2\. Search Method and Scope**

* Documentation terms searched: `ledge`, `grab`, `hang`, `climb`, `pull up`, `mantle`, `clamber`, `edge`, `wall cling`, `wall latch`, `top probe`, `snap point`, and naming variants.  
* Code symbols searched after documentation: `mantleFSM`, `"AIR MANTLE"`, `CheckClamberLedge`, `CheckHeroClamberLedge`, `NoClamberRegion`, `mantling`, `mantleRecovery`, `AllowMantle`.  
* Resources searched: prefab, scene, asset, animation, controller, and FSM file extensions.  
* C\# files opened: approximately 20, all named candidates or direct dependencies.  
* Deliberately excluded: generic player, combat, movement, and PlayMaker action files without a discovered mantle/clamber connection.  
* Constraint result: complied. The C\# repository was not exhaustively inspected.

The `Silksong_Study_ReadOnly` export contains only `Assembly-CSharp`, an `.sln`, and documentation—no Unity serialized assets.

# **3\. Relevant Documents**

| Document | Relevance | What It Contributes | Important Symbols or Terms | Confidence |
| ----- | ----- | ----- | ----- | ----- |
| `Metroidvania Systems Study/02 Player Controller/Movement Flow.md` | Explicit discussion | Best narrative lead: says there are signs of “mantle, clamber, wall scramble, and no-clamber regions,” and explicitly warns that FSM/scene wiring is unavailable. | `HeroController`, `HeroBoxWallSlide`, mantle, clamber, no-clamber | Confirmed |
| `Silksong_Study_ReadOnly/Assembly-CSharp/HERO_CONTROLLER_SUMMARY.md` | Indirect source map | Identifies `mantleFSM` as “ledge mantle.” Also uses `LEDGE_BUFFER_STEPS`, but that is coyote time, not ledge grabbing. | `mantleFSM`, `LEDGE_BUFFER_STEPS` | Confirmed |
| `Player Dependencies.md` | Indirect architecture | Lists mantle among `HeroController`’s PlayMaker FSM dependencies. | `mantle` FSM, `HeroControllerStates`, `HeroAnimationController` | Confirmed |
| `Player State Model.md` | Indirect state information | Lists `mantling` and `mantleRecovery` alongside wall states. | `mantling`, `mantleRecovery` | Confirmed |
| `Animator Setup.md` | Indirect / partial | Documents C\#-driven TK2D animation and event model, but not the main mantle clip. | `HeroAnimationController`, `AnimationCompleted` | Confirmed |
| `Underbrew - Ability Gate Design.md` | Terminology-only false positive | Uses “ledge” for level-design height gates, not runtime mantle behaviour. | `WallLatch`, height gate | Unrelated |

There is no document that fully explains ledge grabbing. `Movement Flow.md` is the best primary explanation, precisely because it identifies the mantle/clamber trail and honestly records the missing FSM-data limitation.

# **4\. Runtime Implementation Map**

| File | Classification | Responsibility | Evidence | Connected Files | Confidence |
| ----- | ----- | ----- | ----- | ----- | ----- |
| `HeroController.cs` | Core detection / orchestration | Sends `"AIR MANTLE"`; owns `CheckClamberLedge`; holds `mantleFSM`. | Entry predicate at line \~1417; `CheckClamberLedge` at \~9945; serialized `PlayMakerFSM mantleFSM`. | `CheckHeroClamberLedge`, `NoClamberRegion`, `SlideSurface` | Confirmed |
| `CheckHeroClamberLedge.cs` | Core detection | Calls `HeroController.instance.CheckClamberLedge`, stores ledge Y and hit collider into FSM variables. | Direct call and `StoreY` / `StoreCollider`. | `HeroController`, missing mantle FSM graph | Confirmed |
| `AllowMantle.cs` | Core FSM bridge | Temporarily permits mantle while control is relinquished. | Calls `AllowMantle(true)` on enter and `false` on exit. | `HeroController`, missing FSM graph | Confirmed |
| `CheckIsClamberBlocked.cs` | Supporting eligibility | Exposes global no-clamber state to PlayMaker. | Returns `NoClamberRegion.IsClamberBlocked`. | `NoClamberRegion` | Confirmed |
| `NoClamberRegion.cs` | Supporting authored restriction | Trigger-driven global block list; cleans itself up when disabled. | Maintains `InsideRegions`; `IsClamberBlocked`; `OnDisable` removal. | `TrackTriggerObjects`, `HeroController` | Confirmed |
| `AutoNoClamberRegion.cs` | Supporting authored restriction | Blocks clamber only while the region is moving and the hero is inside it. | Adds/removes itself based on transform motion. | `NoClamberRegion` | Confirmed |
| `TrackTriggerObjects.cs` | Indirect dependency | Supplies collider-overlap tracking for `NoClamberRegion`. | Uses attached `Collider2D`s and filtered overlap tests. | `NoClamberRegion` | Confirmed |
| `HeroControllerStates.cs` | Supporting state | Declares `mantling` and `mantleRecovery`. | Public flags, reset to false. | `HeroController`, `SlideSurface`, FSM actions | Confirmed |
| `HeroAnimationController.cs` | Animation / cancellation recovery | Plays mantle-cancel and post-mantle landing transitions. | `Mantle Cancel To Jump`, backwards variant, `Mantle Land To Idle/Run`. | `HeroControllerStates`, TK2D clips | Confirmed |
| `SlideSurface.cs` | Indirect dependency | Refuses to attach the hero to a slope-slide while `cState.mantling` is true; hero entry also rejects `SlideSurface.IsHeroInside`. | Both checks are explicit. | `HeroController`, `HeroControllerStates` | Confirmed |
| `HeroBox.cs` | Indirect dependency | Provides normal/wall collider presets, but no mantle-specific shape. | No `HeroBoxMantle`; normal reset occurs on control regain. | Generic PlayMaker `HeroBoxControl` | Strongly indicated |
| `DockClamberCheck.cs` | Specialized possible variant | Performs a separate two-ray platform check and sends a configured event. | Hero-above-object, grounded, two terrain hits, `Platform` tag. | Unknown owner/FSM/scene | Possible |
| `HeroControllerMethods.cs` | False positive | Generic PlayMaker-to-controller dispatcher; contains no mantle operation. | No `SetStartFromMantle`/mantle enum entry. | Many FSMs | Unrelated |
| `WallClinger.cs` and `WallClinger*` actions | False positive | Enemy-AI wall-clinging system. | Actions are categorized “Enemy AI.” | Enemy prefabs/FSMs | Unrelated |
| `NoWallClingRegion.cs` | False positive | Blocks wall cling, not clamber. | Separate static `IsWallClingBlocked`. | Wall-cling system | Unrelated |
| `HeroControllerConfig.cs` | False positive for tuning | Has no mantle/clamber settings. | No discovered mantle field or property. | General hero config | Unrelated |

# **5\. Supporting Assets and Unity Setup**

Confirmed in code, but actual serialized instances are unavailable:

* Hero GameObject:  
  * `HeroController` with a `Rigidbody2D`, main `BoxCollider2D`, `HeroControllerStates`, input handler, animation controller, and serialized `mantleFSM`.  
* PlayMaker:  
  * A `PlayMakerFSM` assigned to `HeroController.mantleFSM`.  
  * It must receive `"AIR MANTLE"`.  
  * It likely uses `CheckHeroClamberLedge`, `CheckIsClamberBlocked`, and `AllowMantle`, but those action-to-state assignments cannot be verified without FSM data.  
* Collision:  
  * `CheckClamberLedge` uses mask `8448`: layer 8 `TERRAIN` plus layer 13 `TERRAIN_DETECTOR`.  
  * Its helper is named `IsRayHittingNoTriggers`, so trigger colliders are excluded from its ledge geometry.  
* No-clamber volumes:  
  * `NoClamberRegion` requires collider-bearing scene objects through `TrackTriggerObjects`.  
  * Inspector options can filter layers and tags, but actual values are unavailable.  
* Animation:  
  * Confirmed clip names: `Mantle Cancel To Jump`, `Mantle Cancel To Jump Backwards`, `Mantle Land To Idle`, `Mantle Land To Run`, and `Sprint Backflip`.  
  * No confirmed `Mantle Grab`, `Mantle Hang`, or primary pull-up clip name.  
* Specialized dock variant:  
  * `DockClamberCheck` expects two configured child transforms, `clamberRayL` and `clamberRayR`, checks the named `Terrain` layer, and accepts objects tagged `Platform`.

No prefabs, scenes, clips, animator controllers, ScriptableObject instances, audio assets, or PlayMaker graph files exist in the reference export.

# **6\. Behaviour Flow**

1. While airborne, `HeroController` checks whether Hornet is pressing toward a still-touching left/right wall. A dash-held approach can also qualify when prior horizontal velocity points into that wall.  
2. It rejects the candidate if a `NoClamberRegion` is active; she is grounded, attacking, wall-sliding, double-jumping, downspike-bouncing, recoiling, in hazard death/respawn, on a `SlideSurface`, moving upward at `>= 5`, or ordinary ground is detected.  
3. If eligible, `HeroController` sends `"AIR MANTLE"` to `mantleFSM`. This is automatic candidate dispatch; the missing graph determines whether it immediately starts a mantle.  
4. If the FSM uses `CheckHeroClamberLedge`, that action calls `CheckClamberLedge(out y, out collider)` and stores the top Y and collider for later FSM actions.  
5. The confirmed check requires a clear front space, two aligned downward surface hits, overhead clearance at both hit points, no nearby roof, and sufficient elevation above ground below the hero.  
6. The missing FSM then presumably owns hero placement, gravity/control changes, primary animation, and transition to standing. Those steps are not verified.  
7. A known exit path exists: `SetStartWithFlipJump()` and `SetStartWithBackflipJump()` both call `SetStartFromMantle()`. On control regain, `HeroController` performs the jump and sets `cState.mantleRecovery`.  
8. During recovery, `HeroAnimationController` selects mantle-cancel jump or mantle-landing-to-idle/run clips. Completion clears `mantleRecovery`.

# **7\. Dependency Diagram**

HeroController.Update  
    → eligibility predicate  
        → NoClamberRegion.IsClamberBlocked  
        → CheckStillTouchingWall / input / velocity  
        → \!SlideSurface.IsHeroInside  
    → mantleFSM.SendEvent("AIR MANTLE")  
        → \[missing PlayMaker mantle FSM graph\]  
            → CheckHeroClamberLedge  
                → HeroController.CheckClamberLedge  
                    → TERRAIN \+ TERRAIN\_DETECTOR raycasts  
            → AllowMantle  
                → HeroController.allowMantle  
            → \[unverified placement / main animation / completion\]

SetStartWithFlipJump or SetStartWithBackflipJump  
    → SetStartFromMantle  
        → cState.mantleRecovery  
            → HeroAnimationController  
                → Mantle Cancel / Mantle Land clips

# **8\. Detection and Geometry Rules**

Confirmed:

* Candidate entry requires contact with a left/right wall and movement held toward it; dash-held velocity direction is an alternative.  
* `rb2d.linearVelocity.y < 5f`.  
* No `NoClamberRegion`; no slide-surface occupancy; not on ground.  
* Disallowed: normal/up/down attack, wall slide, double jump, downspike bounce, hazard death/respawn, recoil/recoil freeze.  
* `CheckClamberLedge` checks:  
  * no nearby roof via four upward/diagonal rays;  
  * a clear horizontal front ray at `(0, 0.67)` for `0.75`;  
  * two downward probes from facing-relative X offsets `0.77` and `0.37`, each length `2.26`;  
  * upward clearance from both detected surface points, `2.16`;  
  * two detected top points within Y tolerance `0.1`;  
  * a lower-ground comparison that rejects ledges less than `1.5` above ground beneath the hero.  
* All core rays use mask `8448` (`TERRAIN | TERRAIN_DETECTOR`) and ignore triggers.

Visible but uncertain:

* `DockClamberCheck` has unused constants `jumpHeightMax = 10`, `platThickMax = 10`, and `clamberClearance = 4.5`; they do not affect the shown code.  
* The returned collider from `CheckClamberLedge` likely supports moving-platform or placement logic, but no consumer is available.

Inferred only:

* Two top probes and the Y tolerance likely reject uneven/sloped or too-narrow landing surfaces.  
* The absence of one-way/hazard/tag logic in `CheckClamberLedge` means any such rules would need to be represented by layers or the missing FSM.

# **9\. Animation and Timing Contract**

Confirmed:

* Recovery clips: `Mantle Cancel To Jump`, `Mantle Cancel To Jump Backwards`, `Mantle Land To Idle`, `Mantle Land To Run`, and optional `Sprint Backflip`.  
* `HeroAnimationController.TryPlayMantleCancelJump()` plays mantle-cancel clips when `mantleRecovery` is active and Hornet is airborne.  
* Its general `AnimationCompleted` callback clears recovery after the identified mantle recovery clips.  
* `SlideSurface` ignores attachment when `cState.mantling` is true.

Not verified:

* Grab/hang/pull-up clips.  
* Any event that moves the hero, changes collider state, starts a pull-up, or ends the primary mantle.  
* Timeouts/fallbacks if FSM animation events fail.  
* Audio, VFX, dust, camera, or haptic hooks for mantle itself.  
* Whether a hang can persist indefinitely.

# **10\. Minimal Source Set**

### **Essential documents**

* `Movement Flow.md`  
* `HERO_CONTROLLER_SUMMARY.md`  
* `Player Dependencies.md`  
* `Player State Model.md`

### **Essential code**

* `HeroController.cs`  
* `CheckHeroClamberLedge.cs`  
* `AllowMantle.cs`  
* `CheckIsClamberBlocked.cs`  
* `NoClamberRegion.cs`  
* `HeroControllerStates.cs`  
* `HeroAnimationController.cs`

### **Supporting resources**

* `AutoNoClamberRegion.cs`  
* `TrackTriggerObjects.cs`  
* `SlideSurface.cs`  
* `GlobalEnums/PhysLayers.cs`  
* The absent hero prefab and assigned `mantleFSM` graph.  
* The absent TK2D animation library containing mantle clips.

# **11\. False Positives**

* `LEDGE_BUFFER_STEPS` in `HeroController` and `HERO_CONTROLLER_SUMMARY.md`: ground-leave coyote time, not ledge grabbing.  
* `WallClinger.cs` and `WallClinger*` PlayMaker actions: enemy AI.  
* `NoWallClingRegion.cs`: wall-cling restriction; distinct from clamber restriction.  
* `HeroControllerMethods.cs`: generic action bridge with no mantle operation.  
* `HeroControllerConfig.cs`: no mantle tuning.  
* `Underbrew - Ability Gate Design.md`: “ledge” is level-design terminology.  
* `My Player Kit Design.md`: “ledge correction” is a generic proposed motor responsibility, not Silksong runtime evidence.  
* `Animator Setup.md`: useful for animation architecture, but its explicit wall-cling material is not mantle implementation.

# **12\. Unanswered Questions**

* Which PlayMaker states receive `"AIR MANTLE"` and which actions they run.  
* Whether `CheckHeroClamberLedge` is actually wired into `mantleFSM`.  
* How the hero is snapped, tweened, root-motioned, or otherwise moved.  
* Whether `cState.mantling` is set by an FSM generic-state action; no C\# assignment was found.  
* Pull-up input requirement, drop option, hang duration, repeat-grab lockout, and moving-platform handling.  
* Primary clips, animation events, audio/VFX, timings, and fallback completion.  
* Exact prefab hierarchy, transforms, colliders, tags, and inspector values.

# **13\. Underbrew Translation Notes**

Likely ownership:

* `HeroActionController`: owns a plain C\# ledge-grab/climb action and its cancellation/priority rules.  
* `HeroMotor`: exclusively performs probe queries, velocity changes, gravity changes, and snap/pull-up placement.  
* `HeroStateBlackboard`: communicates candidate, active, recovery, and interruption state.  
* `HeroAnimationController`: plays Animancer clips directly; no Animator-parameter state machine.  
* `HeroAbilityConfig`: owns ability-specific probes, offsets, timings, filters, and lockouts if the action is an unlock; otherwise core movement config owns always-available behaviour.  
* Unity Editor work: terrain layers/masks, hero sensor references, collision setup, clips, and Inspector assignments must be explicitly planned.

`WallLatch` should remain separate pending design confirmation. The reference shows mantle as a distinct `mantleFSM` entered from wall-adjacent air movement; that establishes overlap in sensors, not identity of actions.

Before implementation planning, answer:

1. Is ledge grab always available or gated by `PlayerAbilityState`?  
2. Is it an automatic pull-up, an input-held hang, or both?  
3. Which collision layers/surfaces are valid in Underbrew?  
4. Does `WallLatch` provide hanging only, while ledge climb provides cresting?  
5. What exact cancellation, damage, moving-platform, and scene-transition contract is required?  
6. Which authored sensors/transforms and clips must exist in the Unity scene/prefab?

