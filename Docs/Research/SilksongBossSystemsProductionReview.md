# **1\. Executive verdict**

| Area | Verdict | Assessment |
| ----- | ----- | ----- |
| Boss encounter framework | **Strong vertical-slice foundation** | Clean ownership, explicit participants, separate death/presentation gates, idempotent completion, and reliable cleanup. Two narrow hardening items should precede boss two. |
| Concrete boss actor architecture | **Production-shaped** | `UndeadExecutionerBehaviour` owns its decisions, attacks, phases, Animancer callbacks, auxiliary spirit, and local presentation without creating a generic boss framework. |
| Enemy/combat foundation | **Adequate but needs hardening** | Strong melee foundation. Invulnerability, stagger, death interception, projectiles, and aerial movement should remain content-driven extensions. |
| Camera integration | **Strong vertical-slice foundation** | Priority stacking and explicit encounter entry/exit are robust. Dynamic framing and cinematic camera requests are optional future seams, not current defects. |
| HUD | **Strong vertical-slice foundation** | Correct source ownership and scene-reference cleanup. Aggregate health is enough now but will strain for independently presented multi-boss encounters. |
| Persistence/save/retry | **Production-shaped** | Clear completion ownership and sound full-scene reconstruction. Emergency in-place respawn contradicts the absolute retry guarantee and must be documented accurately. |
| Scene flow | **Production-shaped** | Boot, persistent managers, checkpoint placement, scene reload, and encounter reconstruction have clean boundaries. |
| Audio/presentation | **Insufficient for the next polished boss, but not for the next functional boss** | Playback exists; music routing, restoration, and reusable encounter presentation do not. These should be added outside the boss controller. |
| Authoring/debugging | **Adequate but needs hardening** | Validators and boss-specific tooling are substantial. Cross-scene encounter-placement uniqueness and runtime preparation failure are not covered correctly. |

“Ready for another boss” means a second grounded boss can be authored without redesigning the lifecycle, HUD, camera, persistence, or hero contracts.

“Ready for a shipped metroidvania” additionally requires deliberate music routing, presentation sequencing, broader Play Mode automation, production reward policy, content validation across many rooms, and evidence from multiple materially different bosses.

No project files were edited. No commits were created.

Validation status at inspection:

* Git branch: `main`, HEAD `e13448628c0d213f08c4b4babe373af3861db04f`.  
* The working tree contained user changes; they were preserved.  
* Unity discovered **186 EditMode tests**, not the previously reported 184\. The two additional tests correspond to the current debug-clear changes.  
* I did not run the suite because the connected Editor was already in Play Mode.  
* I found no persisted test result proving that either 184 or 186 currently passes.  
* The current Editor console had no compilation errors and did show checkpoint load, save, defeated-record clearing, scene reload, and completed-room restoration.  
* The reported full Play Mode lifecycle is therefore **reported and partially corroborated**, not independently rerun.  
* Combat feel, fairness, readability, visual quality, and final tuning remain **subjectively unvalidated**.

# **2\. Complete system map**

Boot.unity  
  → Bootstrap  
  → persistent GameManager / SaveManager / AudioManager / GameCameras  
  → SaveManager applies ScriptableObject ISaveTarget state  
  → SceneTransitionManager loads checkpoint/startup scene  
  → GameManager caches and places Hero at RespawnMarker  
  → Camera/room systems rebind

SampleScene4 room entry  
  → BossEncounterTrigger.TryBeginEncounter()  
  → BossEncounterController  
      → checks WorldStateRegistry and required flags  
      → disables trigger  
      → source-locks HeroController  
      → BossArenaBarrier.SetOpen(false)  
      → activates CameraLockArea and explicitly raises lock entry  
      → activates BossEncounterParticipant wrapper actor  
      → requests BossHealthDisplay through BossHudEventService  
      → calls IBossEncounterBehaviour.PlayIntro()  
  → each participant reports IntroCompleted  
  → controller calls BeginCombat() and releases intro control lock

Active combat  
  → UndeadExecutionerBehaviour chooses actor-local actions  
  → EnemyMotor owns locomotion  
  → EnemyAttackController owns startup/active/recovery/cooldown  
  → animation events open/close EnemyAttackHitbox  
  → DamageHero → HeroBox → HeroHealthComponent  
  → HeroAttackAction → IHeroAttackReceiver → EnemyHealthComponent  
  → HeroAttackResult controls hit feedback/resource  
  → IHeroDownslashResponder controls pogo response  
  → health threshold queues executioner phase transition  
  → spirit remains a boss-owned, non-participant auxiliary actor

Victory  
  → EnemyHealthComponent.StartDeath()  
  → participant reports Defeated  
  → controller raises BossesDefeated  
  → retained actor plays death presentation  
  → participant reports DefeatPresentationCompleted  
  → BossEncounterController synchronously:  
      → WorldStateRegistry.MarkEncounterDefeated()  
      → reveals RewardRoot  
      → opens barriers  
      → releases camera/HUD/control requests  
      → notifies actor completion  
      → enters Completed

Loss  
  → HeroHealthComponent.OnDeath  
  → BossEncounterController enters Interrupted  
  → interrupts/deactivates participants  
  → opens barriers  
  → releases encounter-owned camera/HUD/control state  
  → does not write completion  
  → GameManager normally initiates a full scene reload  
  → fresh scene objects reconstruct health, physics, subscriptions and encounter state

Quiet revisit / Continue  
  → SaveManager applies defeated encounter IDs to WorldStateRegistry  
  → BossEncounterController.InitializeEncounter()  
  → sees completed fact  
  → disables trigger and participant  
  → opens barriers and shows RewardRoot  
  → no combat HUD or camera lock

Important dependencies and risks:

* `BossEncounterController` directly owns the scene-authored roster, barriers, trigger, camera area, reward root, registry, and hero control-lock source. This is appropriate for a thin coordinator.  
* `BossHudEventService` is static but stateless. The persistent view owns transient scene references and clears them on hide/disable.  
* Encounter camera entry is explicit; it does not depend on Unity sending a new trigger event after an inactive lock volume is enabled.  
* Completion is synchronous in `WorldStateRegistry`, but disk persistence waits for the normal checkpoint/quit policy.  
* Retry depends on scene reconstruction. The emergency in-place respawn fallback does not reconstruct or rearm an interrupted encounter.  
* `AbilityPickup` keeps ability ownership in `PlayerAbilityState` and physical consumption in `WorldStateRegistry`, making it suitable as a child of the neutral reward container.  
* Unity initialization assumes activating `ActorRoot` runs the enemy foundation before `ActivateAndPrepare()` checks `health.IsInitialized`. That check is good, but boss-specific preparation failure is currently invisible.  
* Static camera freeze requests contain a source, but the persistent camera handler currently behaves as a single global freeze operation rather than a source-owned lease.

# **3\. Underbrew versus Silksong comparison matrix**

| Subsystem | Underbrew evidence | Silksong/reference evidence | Difference and recommendation | Urgency |
| ----- | ----- | ----- | ----- | ----- |
| Explicit encounter roster | `Assets/_Project/Scripts/Boss/BossEncounterController.cs`, `TryBeginEncounter`, `TryCommitCompletion`; `BossEncounterParticipant.cs` | **Direct Silksong evidence:** `Silksong_Study_ReadOnly/Assembly-CSharp/BossSceneController.cs`, `Setup`, `CheckBossesDead`, `ReportHealth`: explicit `HealthManager[]`, death subscriptions, and registered health details. | Underbrew already uses the same production-shaped principle with typed participants and health sources. Keep it. | None |
| Death versus completed scene | Controller separates `BossesDefeated` from all `DefeatPresentationCompleted` before commit. | **Direct Silksong evidence:** `BossSceneController.cs`, `EndSceneDelayed`: boss death and scene completion are distinct, with presentation/transition gating. | Underbrew’s typed gate is cleaner than string/FSM coordination and should not be collapsed. | None |
| Arena lifecycle | `BossArenaBarrier.cs`; `BossEncounterController.SetCameraLockActive` and completion/interruption cleanup. | **Direct Silksong evidence:** `BattleScene.cs`, `DoStartBattle`, `EndBattle`, `BattleCompleted`: closes gates/camera locks on start and restores completed-room state. | Underbrew covers the core lifecycle. Its barrier presentation is instantaneous and `immediate` is unused. Add animation only when a room requires it. | Phase B/content-triggered |
| Camera stacking | `CameraController.EnterLockArea`, `ExitLockArea`, `RefreshActiveLock`; `CameraLockArea.Priority`. | **Direct Silksong evidence:** `CameraLockArea.cs`, `OnInsideStateChanged`; `CameraController.cs`, `LockToArea`, `ReleaseLock`: priority list and fallback to another active lock. | Architecturally comparable. Underbrew’s explicit entry solves activation-around-an-already-inside-hero. | None |
| Boss health HUD | `BossHudEventService.cs`; `BossHealthDisplay.HandleShowRequested`, `HandleHideRequested`. | **Direct Silksong evidence:** `BossSceneController.cs`, `ReportHealth`, `BossHealthDetails`: explicit health-source registration. | Underbrew avoids scene search and owns transient references safely. Aggregate presentation is a conscious limitation. | Defer independent bars |
| Health capability | `EnemyHealthComponent.cs`, hit handling and private `StartDeath`; `HeroAttackResult`. | **Direct Silksong evidence:** `HealthManager.cs`, `Hit`, `TakeDamage`, `ApplyStunDamage`, `Die`: blocking, immunity, modifiers, stun damage, and special death interception. | Underbrew has enough for the executioner and another ordinary-health boss, but no clean phase-gated death interception. Add only when a boss actually requires it. | Deferred |
| Attack composition | `EnemyAttackController.cs`; three sibling controllers on executioner prefab; animation events plus timer fallback. | **Unknown:** supplied code does not expose the complete boss PlayMaker attack graphs. | No evidence justifies copying Silksong’s action/FSM structure. Underbrew’s boss-owned state decisions are appropriate. | None |
| Persistence | `WorldStateRegistry.MarkEncounterDefeated`; `BossEncounterController.TryCommitCompletion`; `AbilityPickup.cs`. | **Direct Silksong evidence:** `PersistentItem.cs`, `PersistentBoolItem.cs`, and `BattleScene.CheckCompletion`: persisted scene/object facts restore quiet state. | Underbrew’s stable explicit IDs and ScriptableObject save targets are cleaner than fallback scene/name identity and FSM events. | None |
| Challenge/refight state | No challenge domain; world encounter fact is a string ID. | **Direct Silksong evidence:** `BossScene.cs`; `BossChallengeUI.Setup/LoadBoss`; `BossStatue.cs` completion tiers; `BossSequence.cs`. | These solve refight, tier, sequence and challenge-record problems Underbrew does not yet have. Keep those states separate from world completion. | Future layer |
| Music routing | `AudioManager.PlayMusic` provides abrupt single-clip playback; no current scene routing. | **Direct Silksong evidence:** `BattleScene.DoStartBattle/EndBattle`; `MusicRegion.FadeIn/FadeOut`; `MusicCue.cs` and `MusicChannels.cs`: global cues, snapshots, multiple channels, and restoration data. | Underbrew needs a small persistent music-policy owner with source/priority restoration, not the full Silksong channel system. | Phase B |
| Intro/outro sequencing | Typed actor intro and death-completion events; controller owns short control locks. | **Direct Silksong evidence:** `BossSceneController.EndSceneDelayed` waits for transition-safe conditions; `BattleScene` coordinates gates, cues, and state changes. | Underbrew lacks a reusable presentation owner for camera/audio/VFX sequencing. Add a narrow optional component when producing polished boss presentation. | Phase B |
| Retry | Normal path fully reloads scene through `GameManager.BeginRespawnSequence`. | **Strong inference:** `BossSceneController.OnDestroy` cleanup and challenge-scene transitions show reconstruction/abnormal-destroy handling, but supplied code does not establish the complete player-death retry policy. | Full reconstruction is sound. Do not claim exact Silksong equivalence. Document Underbrew’s emergency in-place limitation. | Documentation now |
| Waves/non-health completion | Current roster activates all participants together and completes only after all health deaths/presentations. | **Direct Silksong evidence:** `BattleScene.StartWave`, `DecrementEnemy`, `WaveEnd` support sequential activation. | Current seam is intentionally narrower. Extract an objective/wave seam only when a real boss room requires it. | Deferred |
| Reward separation | Controller reveals neutral `RewardRoot`; child pickup can own collection independently. | **Strong inference:** reference separates battle completion, player-data facts, challenge completion, and scene restoration, but no directly equivalent Underbrew-style reward-root contract is visible. | Keep encounter completion and reward consumption separate; avoid granting abilities directly in the encounter controller. | None |

# **4\. What Underbrew already gets right**

These systems should not be rewritten:

* `BossEncounterController` is genuinely thin. Actor actions, phase selection, movement, and local presentation remain outside it.  
* `IEnemyBehaviour` plus `IBossEncounterBehaviour` is enough. A generic boss base class or universal phase/action framework would add indirection without solving an observed problem.  
* Explicit participant and health rosters are safer than scene searches.  
* The separate “all dead” and “all death presentation complete” gates are production-quality.  
* Completion is synchronous and idempotent before barriers/reward are released.  
* Source-owned hero control locks avoid one system clearing another system’s lock.  
* Camera lock entry and release are explicit and idempotent.  
* The persistent boss HUD does not retain scene objects after a matching hide.  
* `UndeadExecutionerBehaviour` uses Animancer callbacks with duration fail-safes; gameplay does not depend exclusively on an animation event firing.  
* The executioner’s spirit is correctly boss-owned rather than falsely modeled as a required encounter participant.  
* `EnemyMotor` remains normal locomotion authority.  
* `HeroAttackAction`, `IHeroAttackReceiver`, `HeroAttackResult`, `IHeroDownslashResponder`, and `HeroBox` already provide adequate narrow hero-facing seams.  
* `HeroBox` coalesces simultaneous incoming hits in a physics step, reducing attack/contact double damage.  
* Full scene reconstruction is an acceptable and often safer retry policy for single-room, non-additive encounters.  
* Stable world IDs and explicit save targets are cleaner than the name/scene fallback visible in `PersistentItem`.  
* World completion, ability ownership, and physical reward consumption remain separate responsibilities.

# **5\. Must-fix before the second boss**

## **A. Make actor preparation fail closed**

**Evidence:** `IBossEncounterBehaviour.PrepareForEncounter()` returns `void`. `BossEncounterParticipant.ActivateAndPrepare()` returns success immediately after calling it. `UndeadExecutionerBehaviour.PrepareForEncounter()` can set `prepared = false` when references are missing or attack-controller configuration fails.

**Risk:** The controller proceeds to barriers, camera, HUD, and intro while the boss silently refuses to complete its intro. The encounter can remain indefinitely in `Starting`, holding presentation and control state.

**Smallest fix:** Replace the preparation call with an explicit success contract, such as `bool TryPrepareForEncounter()`. Propagate failure through `ActivateAndPrepare()`, then make the controller abort cleanly: interrupt prepared participants, open barriers, release camera/HUD/control, log one actionable error, and remain non-completed.

**Files:**

* `Assets/_Project/Scripts/Boss/IBossEncounterBehaviour.cs`  
* `Assets/_Project/Scripts/Boss/BossEncounterParticipant.cs`  
* `Assets/_Project/Scripts/Boss/BossEncounterController.cs`  
* `Assets/_Project/Scripts/Boss/UndeadExecutioner/UndeadExecutionerBehaviour.cs`

**Editor work:** None beyond checking existing serialized behaviour references.

**Tests:** Add preparation-failure tests to `BossEncounterFoundationTests.cs` and executioner configuration-failure coverage to `UndeadExecutionerBehaviourTests.cs`.

**Docs:** Update the preparation/lifecycle contract in `BossEncounters.md` and `Architecture.md`.

## **B. Validate encounter placements, not only definition assets**

**Evidence:** `BossEncounterValidator.ValidateDefinitions` catches duplicate IDs among definition assets. `ValidateScene` validates each controller, but the local `string id` read from its definition is unused. Two controllers in different enabled scenes can therefore reference the same definition and silently share one world-completion fact.

**Risk:** Defeating one world boss can suppress another placement. Refights should not solve this by reusing the world encounter controller; they belong to a separate future domain.

**Smallest fix:** During the existing enabled-build-scene pass, collect controller placements by `EncounterId` and report multiple world placements. Do not generate or repair IDs automatically.

**File:** `Assets/_Project/Scripts/Editor/Validation/BossEncounterValidator.cs`.

**Editor work:** Run the validator after the change; no scene mutation should occur.

**Tests:** Add same-scene and cross-scene duplicate-placement cases to `BossEncounterValidatorTests.cs`.

**Docs:** Expand the validator guarantees in `BossEncounters.md`.

The editor-only defeated-record command calling `SaveManager.Save()` is a boundary inconsistency, but not a player-build correctness defect because it is editor-only. Move that operation to editor tooling or explicitly document the exception during the same hardening pass.

# **6\. Recommended improvements before broader production**

## **Shared-system improvements**

* Add a persistent music policy/director with source-owned prioritized overrides and room-cue restoration.  
* Add a narrow optional encounter-presentation component that subscribes to typed lifecycle events and owns camera/audio/VFX sequencing.  
* Correct the documented respawn guarantee: full reload is the normal production path, not an absolute path.  
* Extend barrier validation to require at least one configured blocker and valid presentation references where used.  
* Correct the spirit-participant validator condition so any forbidden participant beneath the spirit hierarchy is reliably detected.  
* Add Play Mode coverage for one complete real-scene victory and one loss/reload/retry path.  
* Decide and document whether boss completion should wait for a checkpoint/quit or request a product-level post-boss autosave.

## **Authoring improvements**

* Provide a small participant prefab template containing the active wrapper, inactive `ActorRoot`, `BossEncounterParticipant`, and expected foundation layout.  
* Add a read-only encounter summary Inspector: ID, participant roster, health sources, barriers, camera lock, reward root, and current validation state.  
* Keep ID creation manual; validators should detect missing/duplicate IDs rather than silently generating them.  
* Add a Boot-compatible “launch this boss room” editor command that uses the existing startup/transition path instead of directly loading the gameplay scene as the primary workflow.  
* Provide targeted debug operations: start encounter, set health, force phase, toggle invulnerability, show hitboxes, and reload checkpoint. Avoid a general cheat-console project for now.

## **Concrete Undead Executioner polish**

* Subjectively review anticipation, hit readability, close-range overlap, attack recovery, phase-transition clarity, spirit telegraph, and death timing.  
* Add final SFX/VFX and a deliberate reward presentation.  
* Decide whether ordinary hits should interrupt an attack. Currently hitboxes close through shared attack interruption while the behaviour can remain in its attack state until animation completion/fail-safe.  
* Review provisional configuration values rather than treating validator success as tuning approval.  
* Wire the optional encounter icon only if the final HUD design needs it; the current `displayIcon` and `iconImage` are unused/null.

## **Wider-game infrastructure**

* Add room music routing before multiple rooms require different music states.  
* Add an explicit post-boss autosave policy only after the product decision is made.  
* Keep challenge/refight records separate from `WorldStateRegistry.defeatedEncounterIds`.  
* Define an additive/multi-room encounter lifetime only when such content enters scope.

# **7\. Defer until proven**

| Investment | Trigger |
| ----- | ----- |
| Generic boss action or phase framework | Two or more bosses exhibit the same orchestration problem that cannot be expressed cleanly in their concrete behaviours. |
| In-place encounter reset API | Additive loading, multi-room bosses, or measured scene-reload problems make reconstruction unsuitable. |
| Sequential-wave/objective abstraction | A real encounter must activate participant groups sequentially or complete without ordinary health death. |
| Multiple independent HUD bars | A real duo boss needs separate names/health readability rather than aggregate health. |
| Segments, stagger meters, delayed damage | A concrete health presentation design requires them. |
| Invulnerability/shield/poise framework | A real boss needs a reusable health policy. Simple temporary rejection can first be a narrow health extension. |
| Death interception/actor replacement | A fake-death or replacement-phase boss is selected. |
| Generic projectile system | At least one projectile boss defines actual lifetime, collision, pooling, ownership, and cancellation requirements. |
| Dynamic multi-target camera | An aerial, very large, or duo boss cannot be framed acceptably by an authored lock region. |
| Universal arena-mechanic framework | Two distinct rooms repeat the same authored hazard/gate lifecycle. |
| Refight statues, tiers, bindings, no-hit records | A refight/challenge milestone is approved. |
| Multi-channel Silksong-like music | Musical direction genuinely needs independent action/tension/sub channels. A source-owned override stack should come first. |
| Full custom boss editor | Ordinary Inspector, templates, validators, and gizmos prove inadequate across several bosses. |

# **8\. Camera deep-dive verdict**

The current camera lock/event architecture is sufficient for production-style static boss rooms.

Strengths:

* `CameraLockArea` has explicit axis settings, bounds, look clamps, and priority.  
* `CameraController` maintains overlapping lock areas and returns to the highest remaining valid lock.  
* `BossEncounterController.SetCameraLockActive(true)` activates the area and explicitly issues `LockEntered`, so entering the arena before activation is safe.  
* Repeated `OnTriggerStay` or disable/explicit-exit events are effectively idempotent.  
* Death, completion, unload, and quiet restoration all release or avoid the encounter lock.  
* The SampleScene4 lock encloses the executioner arena and has priority 50\.

Near-term refinements:

* Document tie behavior for equal-priority lock areas; today the effective selection depends on list order/latest entry.  
* Before cinematic freeze requests are used by multiple systems, make freeze ownership source-aware rather than one global coroutine.  
* Put intro/outro camera cues in an optional presentation component, not in the boss actor or encounter controller.  
* Add an authored transition profile only if room-to-arena entry visibly pops; ordinary damping may already be sufficient.

Dynamic zoom, multi-target framing, vertical target composition, and boss-specific look-ahead are content features. They are not prerequisites for the next grounded boss.

# **9\. Persistence/retry/reward deep-dive verdict**

**Full-scene retry:** Keep it. It produces a clean boss instance, resets Unity subscriptions and physics state, and avoids an error-prone universal reset contract.

**Important qualification:** `GameManager.BeginRespawnSequence()` can fall back to in-place respawn if the checkpoint scene is not loadable or the transition cannot start. An interrupted `BossEncounterController` does not rearm in that path. Production validation should guarantee checkpoint scenes are loadable, while documentation should describe the fallback as degraded recovery rather than a retry guarantee.

**Completion timing:** Marking the encounter defeated synchronously before revealing the reward/opening the arena is correct.

**Disk timing:** Waiting for checkpoint or quit is internally consistent. It means a crash or forced termination after victory but before another save can restore the boss. That is a product decision, not a lifecycle bug. If unacceptable, add a save-policy-owned post-boss autosave request; do not call `SaveManager.Save()` from the coordinated encounter.

**Reward ownership:** Keep `RewardRoot` neutral. A child `AbilityPickup`, currency chest, progression actor, or visual-only object should own its own collection state. For an ability, `PlayerAbilityState` remains authoritative and the pickup ID records physical consumption.

**Quiet restoration:** The current completed-room path correctly suppresses combat, opens barriers, and restores the reward container. A consumed child reward must suppress itself using its separate state.

**Developer reset/refight:** Clearing one completion fact and reloading through the real scene path is useful. Keep it editor-only and move direct saving/reloading orchestration out of the runtime encounter class where practical. A gameplay refight system should use a separate domain later.

# **10\. Audio and presentation recommendation**

The smallest production-feasible design is:

1. Keep `AudioManager` as the persistent playback backend.  
2. Add a persistent music-policy component, preferably beside the audio service rather than in `GameManager`.  
3. Give it typed requests containing source, cue, priority, transition/fade policy, and release behavior.  
4. Let scene flow establish the underlying room cue.  
5. Let a scene-authored encounter presentation component request a boss override.  
6. Release that source on interruption, completion, disable, or unload so the underlying room cue resumes.  
7. Play victory stingers as a separate one-shot/fanfare operation instead of making the boss actor replace global music state.  
8. Keep attack, hurt, roar, spirit, and death SFX actor-local through `AudioManager.PlaySFX`.  
9. Let an optional presentation component sequence intro camera cue, roar/VFX, music request, control-release signal, death cue, stinger, and reward reveal presentation.  
10. Timeline may drive presentation tracks, but typed gameplay completion must retain a fail-safe and cannot depend solely on Timeline reaching its final frame.

Do not implement Silksong’s multi-channel cue/snapshot system wholesale. Its code demonstrates why global routing and restoration matter, not the minimum shape Underbrew must copy.

# **11\. Future-boss authoring workflow**

1. Create a unique `BossEncounterDefinition`.  
2. Start from a validated participant-wrapper template with an inactive actor root.  
3. Add the normal enemy foundation: blackboard, motor, health, recoil, perception as needed, feedback, and explicit attack modules.  
4. Implement one concrete `IEnemyBehaviour` plus `IBossEncounterBehaviour`; keep phases and decisions local.  
5. Create boss-owned config and animation assets.  
6. Author animation events, but retain timer/clip-length fail-safes.  
7. Configure attack hitboxes disabled at authoring time and use the expected damage layers.  
8. Place the participant in the encounter roster.  
9. Author trigger, barriers, camera lock, arena limits, and reward container.  
10. Configure the encounter’s persistence registry and verify the ID is globally unique among enabled world placements.  
11. Add boss-specific validation for required references and content invariants.  
12. Add behaviour tests for preparation failure, interruption, death priority, phase thresholds, and exceptional animation paths.  
13. Run shared validators and the full EditMode suite.  
14. Exercise the room through Boot: first entry, loss, checkpoint reload, retry, victory, quiet revisit, save/quit/Continue.  
15. Perform separate subjective acceptance for fairness, telegraphs, readability, sound, camera comfort, and reward satisfaction.

Four-boss stress test:

| Shape | Reuse unchanged | Likely strain | Do not generalize yet |
| ----- | ----- | ----- | ----- |
| Grounded melee | Encounter lifecycle, health, HUD, persistence, camera lock, hero contracts, attack modules | Boss-specific locomotion/selection and attack timing | Generic action/phase framework |
| Projectile-heavy aerial | Lifecycle, health, persistence, HUD | Projectile lifetime/cancellation; vertical movement authority; possibly dynamic framing | Universal projectile/pooling system before requirements exist |
| Two participants | Explicit roster, death aggregation, aggregate HUD, separate actors | Independent bars/names; synchronized intros/attacks; framing both actors | Generic duo coordinator until one real duo proves patterns |
| Arena-owned mechanic | Encounter events, barriers, camera, persistence | Dedicated scene component for hazards/stages; possibly non-health completion | Put arena mechanics inside `BossEncounterController` |

# **12\. File-by-file recommendation plan**

| Path | Current responsibility | Proposed change | Contract / Editor / tests / docs |
| ----- | ----- | ----- | ----- |
| `Scripts/Boss/IBossEncounterBehaviour.cs` | Typed actor lifecycle | Make preparation explicitly report success | Public contract change; no serialized work; update all implementations/tests/docs |
| `Scripts/Boss/BossEncounterParticipant.cs` | Actor activation adapter | Propagate preparation failure | Public method behavior changes; foundation failure tests |
| `Scripts/Boss/BossEncounterController.cs` | Scene lifecycle coordinator | Add clean preparation-abort path; relocate editor-only save/reset orchestration | Runtime lifecycle change; no scene reauthoring expected; lifecycle tests |
| `Scripts/Boss/UndeadExecutioner/UndeadExecutionerBehaviour.cs` | Concrete boss decisions/presentation | Return preparation status; later tune interrupt policy and presentation | Interface change; behaviour tests; content review |
| `Scripts/Editor/Validation/BossEncounterValidator.cs` | Definition/controller/build-scene validation | Detect duplicate controller placements by encounter ID; validate basic barrier authoring | Editor-only; new validator fixtures; update validation docs |
| `Scripts/Editor/Validation/UndeadExecutionerValidator.cs` | Executioner-specific setup checks | Correct spirit participant check; optionally validate config relationships | Editor-only; validator tests |
| `Scripts/Boss/BossArenaBarrier.cs` | Static blocker/presentation toggle | Either document `immediate` as reserved or implement it only alongside animated gate presentation | Potential public behavior change; barrier tests if implemented |
| `Scripts/Audio/AudioManager.cs` | Persistent playback backend | Add only playback primitives needed by the music policy: fade/stop/crossfade as selected | Public audio API change; audio tests/docs |
| New persistent audio policy component | None currently | Own room cue plus source/priority boss overrides and restoration | New narrow public service; author persistent prefab; routing tests/docs |
| New optional encounter-presentation component | None currently | Subscribe to encounter events and own camera/audio/VFX/Timeline cues | Additive contract; scene authoring per boss; interruption/fail-safe tests |
| `Scripts/Camera/GameCameras.cs` / `CameraEventService.cs` | Persistent request routing | Make freeze release/overlap source-aware before cinematic use | Public request semantics; camera ownership tests |
| `Scripts/Managers/GameManager.cs` | Respawn coordinator | No reset framework; clarify/log degraded in-place encounter behavior | Prefer documentation/validation over code expansion |
| `Scripts/World/Persistence/Participants/AbilityPickup.cs` | Ability reward and physical consumption | No architectural change | Use unchanged when reward is an ability |
| `_GameCameras.prefab` | Persistent HUD/camera presentation | Add portrait/show animation only after UI design approval | Editor authoring; UI tests if hierarchy contract changes |
| `UndeadExecutionerParticipant.prefab` | Concrete actor composition | Final SFX/VFX/feedback authoring and approved tuning | Editor/content work; validator and Play Mode acceptance |
| `SampleScene4.unity` | Executioner room | Final reward and presentation objects; no lifecycle redesign | Editor work; real-scene Play Mode tests |
| `Scripts/Editor/Tests/Boss/*` | Lifecycle and concrete actor coverage | Add preparation failure, placement uniqueness and presentation interruption | No public runtime impact |
| New boss-room Play Mode tests | Currently one placeholder PlayMode test | Automate Boot-compatible loss/reload and victory/revisit smoke paths | Test scenes/build configuration required |

Paths above are relative to `Assets/_Project/` unless prefixed by `Docs/`.

# **13\. Prioritised roadmap**

## **Phase A — required before the second boss**

* Make boss preparation fail closed.  
* Detect duplicate encounter-controller placements across enabled build scenes.  
* Correct lifecycle documentation around emergency in-place respawn.  
* Resolve the editor-only reset/save ownership inconsistency.  
* Update stale documents that still say no real boss exists.

## **Phase B — shared production improvements**

* Add source-owned boss/room music routing and restoration.  
* Add an optional typed encounter-presentation component.  
* Add source-aware camera freeze semantics before cinematic use.  
* Harden barrier and executioner validators.  
* Add one real-scene victory test and one loss/reload/retry Play Mode test.  
* Add a small validated participant prefab template and encounter summary Inspector.

## **Phase C — Undead Executioner content and presentation**

* User-led feel, fairness, and readability review.  
* Final SFX/VFX, intro, phase-transition and death presentation.  
* Final reward authoring.  
* Decide hit-interruption policy.  
* Final HUD artwork/animation and optional portrait.  
* Final numeric tuning.

## **Phase D — prove with the second boss**

* Build a grounded melee boss first if the goal is framework validation.  
* Extract only repeated configuration, presentation, or validation patterns.  
* Use an aerial/projectile or duo boss later to prove camera, movement, projectile, and HUD expansion needs.  
* Revisit non-health completion only with a real arena-mechanic or wave boss.

## **Future shipped-game expansion layer**

* Refight/statue domain.  
* Difficulty tiers and alternate versions.  
* Bindings, no-hit records, timers, summary boards.  
* Boss sequences and gauntlets.  
* Multi-room/additive encounter lifetime.  
* Multi-channel adaptive music.  
* Catalogue/discovery/achievement state.

These solve mature content and replay-mode problems that Underbrew does not currently have.

# **14\. Documentation audit**

## **Accurate**

* `Docs/FeatureSpecs/BossEncounters.md`: broadly accurate for current ownership and lifecycle.  
* `Docs/Architecture.md`: boss, HUD, persistence, and deferred music descriptions are broadly accurate.  
* `Docs/FeatureSpecs/HUD.md`: accurately describes stateless requests and persistent-view ownership.  
* `Docs/FeatureSpecs/Combat.md`: broadly consistent with current hero/enemy combat flow.  
* `Docs/Integrations/WorldGraphEditorIntegration.md`: correctly preserves Underbrew runtime transition ownership.

## **Stale**

* `Docs/FeatureSpecs/EnemyAI.md`: still lists boss behaviours as planned and says no boss framework exists.  
* `Docs/FeatureSpecs/SaveSystem.md`: says no real boss content exists.  
* `Docs/ImplementationPlans/WorldPersistence.md`: top status says real boss content remains unauthored.  
* Early sections of `Docs/ImplementationPlan.md`: say real bosses/production boss content are still pending despite the completed boss phase later in the same file.  
* `Docs/FeatureSpecs/Camera.md`: does not describe the encounter-controlled priority lock integration.  
* `Docs/FeatureSpecs/Audio.md`: presents GameManager scene-music routing as if it exists; it remains unimplemented.

## **Incomplete**

* `BossEncounters.md`: preparation cannot currently report failure; retry wording is too absolute; editor debug save/reset exception is undocumented; validator guarantees overstate placement safety.  
* `HUD.md`: does not state that the current prefab has no icon image wired and the encounter icon is unused.  
* `Audio.md`: needs an explicit implemented-versus-planned status and the approved future music-override owner.  
* `Camera.md`: should describe activation around an already-inside hero, priority/stack behavior, and encounter cleanup.  
* `Architecture.md`: should mention the degraded in-place respawn fallback.

## **Contradictory**

* `ImplementationPlan.md` says the boss is implemented in its status table and boss phase, but earlier future-boundary and persistence text still says real/production bosses are pending.  
* `Audio.md` assigns scene music to `GameManager.BeginSceneTransition`, while `Architecture.md` correctly says routing is deferred and code has no such call.

## **Historical**

* `Docs/DocsAudit.md`.  
* `Docs/FeatureSpecs/CameraImplementationChecklist.md`.  
* `Docs/ImplementationPlans/BossEncounterImplementationBrief.md`.  
* `Docs/Research/ReferenceReviewHandoff.md`.  
* Much of `Docs/ImplementationPlans/WorldPersistence.md` is a completed implementation plan.

After implementation, update the lifecycle/preparation, validation, retry, audio ownership, HUD presentation, and current-status sections rather than rewriting the documents wholesale. Historical plans should receive a clear “implemented/superseded” banner.

# **15\. Unknowns and limitations**

* The supplied Silksong reference contains 2021 decompiled C\# but not the complete boss prefab, scene, and PlayMaker FSM assets. Concrete boss attack graphs, thresholds, interruption rules, and arena authoring details are therefore **Unknown**.  
* Silksong’s exact player-death world-boss retry policy is **Unknown** from the inspected sources.  
* Whether every referenced Silksong health capability is used by ordinary world bosses is **Unknown**; `HealthManager` only proves the capability exists.  
* The intended final Underbrew boss reward type is **Unknown**.  
* The desired product policy for saving immediately after boss victory is **Unknown**.  
* Final room/boss music direction and whether adaptive layers are wanted are **Unknown**.  
* The second boss’s actual shape is **Unknown**, so projectile, aerial movement, multi-target framing, and independent HUD bars remain conditional.  
* The user-reported 184-test pass could not be verified. Current discovery reports 186 tests, and no suite was run during this audit.  
* The reported Undead Executioner and Boss Encounter validator passes were not independently rerun.  
* The complete loss/retry/victory/revisit/save/quit/Continue sequence was not independently replayed. Current scene state and logs corroborated parts of it.  
* Subjective combat quality remains explicitly unapproved.  
* Performance under many encounters, additive scenes, addressable content, pooled bosses, or console memory constraints has not been measured.  
* Accessibility requirements for camera motion, flashes, HUD visibility, and cinematic skipping have not yet been defined.

