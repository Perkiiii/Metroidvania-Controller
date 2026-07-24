# Boss Encounter Implementation Brief

**Proposed repository path:** `Docs/ImplementationPlans/BossEncounter.md`  
**Purpose:** Planning handoff for Codex. This document defines the intended first-pass boss encounter system for **Metroidvania Controller / Underbrew Rebuild**. It is not an implementation and does not claim that any boss code, prefab wiring, Unity validation, or playtesting has already been completed.

---

## 1. Planning objective

Plan and later implement one reusable, production-feasible boss encounter foundation plus one playable first-boss vertical slice.

The first pass must support:

- entering an authored boss arena;
- starting the encounter once;
- temporarily sealing the arena;
- temporarily applying a camera lock;
- an optional authored intro sequence;
- activating one or more boss participants;
- boss health presentation;
- boss-specific AI, attacks, phases, animation, and death presentation;
- detecting when all configured bosses are defeated;
- separating **bosses defeated** from **encounter fully completed**;
- permanently recording encounter completion through `WorldStateRegistry`;
- revealing a reward without duplicating the reward's ownership or collected state;
- restoring the room quietly when the encounter was already completed;
- normal-death retry through the existing checkpoint/respawn flow;
- cleanup of camera, control locks, HUD binding, and temporary arena state on interruption or scene unload.

The first pass must **not** implement Silksong's boss-rush, statue, tier, binding, no-hit, summary-board, or refight systems.

---

## 2. Sources of truth Codex must inspect

Before proposing file changes, read and reconcile:

1. `AGENTS.md`
2. `Docs/Architecture.md`
3. `Docs/ImplementationPlan.md`
4. `Docs/FeatureSpecs/EnemyAI.md`
5. `Docs/FeatureSpecs/Combat.md`
6. `Docs/FeatureSpecs/Camera.md`
7. `Docs/FeatureSpecs/HUD.md`
8. `Docs/FeatureSpecs/Audio.md`
9. `Docs/FeatureSpecs/SaveSystem.md`
10. `Docs/ImplementationPlans/WorldPersistence.md`
11. The actual scripts and prefabs listed in this brief.

The actual codebase is ground truth for what currently exists. Documentation is the intended architecture unless the code contradicts it. Any contradiction must be called out in the plan rather than silently resolved.

### Repository state confirmed while preparing this brief

At the repository revision inspected for this handoff:

- `WorldStateRegistry` is implemented and serialized through `ISaveTarget`.
- `WorldStateRegistry.IsEncounterDefeated(string)` and `MarkEncounterDefeated(string)` already exist.
- `EnemyPersistenceMode.PermanentEncounter` exists and is tested for simple one-enemy permanent encounters.
- `EnemyController`, `EnemyMotor`, `EnemyPerception`, `EnemyAttackController`, `EnemyHealthComponent`, `EnemyFeedbackController`, and the Mushroom behaviour are implemented.
- `EnemyHealthComponent` exposes `OnDamaged` and `OnDeath`, but does not currently expose a neutral current/max-health notification suitable for a boss HUD.
- The persistent UGUI HUD foundation exists under `_GameCameras`.
- `CameraLockArea` and `CameraEventService` exist.
- `HeroController.AddControlLock(object)` and `RemoveControlLock(object)` are the approved control-suppression seam.
- `AudioManager` exists, but current documentation assigns music routing to `GameManager`, not gameplay scene MonoBehaviours.
- Real boss content remains explicitly unimplemented.

Codex must verify all of the above against the current working tree before relying on it.

---

## 3. Architectural principles

### 3.1 The scene encounter controller is a thin coordinator

The scene-level boss controller coordinates lifecycle and presentation. It does not own boss movement, attack selection, phase logic, hitboxes, animation decisions, or Rigidbody2D velocity.

It may own:

- encounter state;
- stable encounter identity;
- configured boss roster;
- start trigger handling or a reference to a dedicated trigger;
- arena barrier commands;
- camera-lock activation;
- hero control locks used during authored intro/outro moments;
- boss HUD show/hide requests;
- encounter music requests through the approved music owner;
- all-bosses-defeated tracking;
- encounter-completion sequencing;
- permanent completion handoff to `WorldStateRegistry`;
- reward-root visibility;
- lifecycle events for bespoke scene presentation.

It must not own:

- boss Rigidbody2D movement;
- attack timing or hitbox evaluation;
- phase thresholds;
- boss animation playback;
- boss-local hurt/death feedback;
- player health/resource state;
- direct save-file writes;
- scene loading;
- camera internals;
- hero blackboard fields.

### 3.2 Boss gameplay remains actor-owned

The boss-specific behaviour should build on the existing enemy foundation and use composition.

Preferred shape:

```text
Boss prefab root
├── EnemyController                 existing coordinator
├── EnemyStateBlackboard            existing runtime bus
├── EnemyHealthComponent            existing damage/death owner, extended only as needed
├── EnemyMotor                      movement authority
├── EnemyPerception                 optional targeting/perception
├── EnemyAttackController(s)        existing attack-window machinery where suitable
├── EnemyRecoil                     configured or bypassed according to boss design
├── EnemyFeedbackController         local hit/death feedback
├── EnemyPersistence                normally absent or RoomRuntime for encounter-controlled bosses
├── BossEncounterParticipant        proposed encounter-facing adapter
└── <FirstBoss>Behaviour             boss-specific IEnemyBehaviour + encounter lifecycle implementation
```

The first boss behaviour owns:

- internal boss states;
- attack choice;
- phase changes;
- movement requests through `EnemyMotor`;
- attack-controller activation;
- Animancer playback;
- intro-ready and defeat-presentation completion signals;
- boss-specific VFX/SFX requests;
- any boss-specific arena mechanics delegated to dedicated components.

Do not add boss behaviour to `HeroController`, `GameManager`, or `BossEncounterController`.

### 3.3 Encounter completion is encounter-owned

A critical ownership decision:

> Bosses controlled by a multi-participant `BossEncounterController` should not each independently write permanent encounter completion on their own death.

The existing `EnemyPersistence` implementation subscribes to an individual `EnemyHealthComponent.OnDeath` and immediately calls `WorldStateRegistry.MarkEncounterDefeated(worldObjectId)` for `PermanentEncounter` mode. That is correct for a simple one-enemy encounter where enemy death and encounter completion are identical. It is too early for a coordinated boss encounter because:

- a multi-boss encounter would complete after the first individual death if IDs were shared;
- separate IDs would record individual enemies rather than the encounter;
- completion would be written before the death presentation/outro is ready;
- the boss controller could not reliably distinguish `OnBossesDefeated` from `OnEncounterCompleted`.

For the first reusable boss system:

- `BossEncounterController` owns one stable `encounterId` through a definition asset or explicit serialized field;
- controlled boss instances must not independently mark that same encounter complete;
- the encounter controller calls `WorldStateRegistry.MarkEncounterDefeated(encounterId)` only when the completion gate has been satisfied;
- `EnemyPersistenceMode.PermanentEncounter` remains available for simple minibosses or one-time enemies where individual death truly equals encounter completion.

Codex should validate the cleanest way to enforce this and add Editor validation that detects conflicting ownership.

### 3.4 Reward collection is a separate fact

Do not add a generic `rewardCollected` field to the boss encounter system.

The encounter owns only the physical fact that the encounter is defeated. The reward owns its own domain state:

- ability reward: `PlayerAbilityState` owns the ability; `AbilityPickup`/`WorldStateRegistry.collectedPickupIds` owns physical pickup consumption;
- item/inventory reward: the future inventory domain owns the item; the physical pickup records consumption;
- currency reward: the currency domain owns the amount; the physical source records consumption if needed.

The encounter may enable a `rewardRoot` after completion. On a later scene load:

1. the controller sees that the encounter is defeated;
2. it quietly applies the completed-room state;
3. it enables the reward root;
4. the reward participant suppresses itself if already collected.

This preserves the distinction between **boss defeated** and **reward collected** without duplicating ownership.

### 3.5 No direct save call from the boss

Current project rules allow `SaveManager.Save()` only from approved checkpoint/save-on-quit flows. The boss controller must not save directly.

Permanent encounter state is updated in the in-memory `WorldStateRegistry` immediately. It reaches disk through the existing save policy. If an immediate post-boss autosave is later desired, that is a separate product and architecture decision requiring updates to the save specification.

---

## 4. Silksong reference: what the supplied code actually shows

The supplied C# files mostly cover boss-scene orchestration, challenge sequences, statues, completion records, and presentation. They do **not** expose the individual bosses' PlayMaker FSM attack graphs. Therefore they are useful for encounter architecture, but not a complete source for attack AI.

The snippets below are reference evidence, not code to copy verbatim.

### 4.1 Thin coordinator subscribes to boss death

**Source:** supplied `BossSceneController.cs`, `Setup()`.

```csharp
private void Setup()
{
    for (int i = 0; i < this.bosses.Length; i++)
    {
        if (this.bosses[i])
        {
            this.bossesLeft++;
            this.bosses[i].OnDeath += delegate()
            {
                this.bossesLeft--;
                this.CheckBossesDead();
            };
        }
    }
}
```

**What this demonstrates**

- The scene controller receives an authored boss roster.
- It listens to health/death owners rather than running boss AI.
- Multiple bosses are supported through a count.
- Completion is based on the configured encounter roster, not a scene-wide search.

**Underbrew adaptation**

Use an authored `BossEncounterParticipant[]` or equivalent typed roster. Do not use `FindObjectsOfType` to discover bosses. Subscribe and unsubscribe explicitly.

### 4.2 Defeated and scene-complete are separate events

**Source:** supplied `BossSceneController.cs`, `EndBossScene()` and `EndSceneDelayed()`.

```csharp
public void EndBossScene()
{
    if (!this.endedScene)
    {
        this.endedScene = true;
        if (this.OnBossesDead != null)
        {
            this.OnBossesDead();
        }
        base.StartCoroutine(this.EndSceneDelayed());
    }
}
```

```csharp
while (waitingForTransition ||
       HeroController.instance.cState.hazardRespawning ||
       HeroController.instance.cState.hazardDeath ||
       HeroController.instance.cState.spellQuake ||
       !this.CanTransition)
{
    yield return null;
}

if (this.OnBossSceneComplete != null)
{
    this.OnBossSceneComplete();
}
```

**What this demonstrates**

- Health reaching zero is not the final progression boundary.
- Death presentation, hero state, and transition readiness can delay final completion.
- The controller exposes two lifecycle moments: bosses dead and scene complete.

**Underbrew adaptation**

Expose equivalent typed lifecycle events, for example:

```csharp
public event Action BossesDefeated;
public event Action EncounterCompleted;
```

Do not read hero internals as Silksong does. Use approved Underbrew seams: control locks, neutral events, participant completion signals, and existing death/transition state where an explicit API exists.

### 4.3 Explicit health roster for presentation

**Source:** supplied `BossSceneController.cs`, `ReportHealth(...)`.

```csharp
BossSceneController.Instance.BossHealthLookup[healthManager] =
    new BossSceneController.BossHealthDetails
    {
        baseHP = baseHP,
        adjustedHP = adjustedHP
    };
```

**What this demonstrates**

- Boss health presentation is based on an explicit encounter roster.
- The controller can support adjusted health and multiple health owners.
- The UI does not need to search the scene for a boss.

**Underbrew adaptation**

Do not reproduce a static singleton dictionary. Have the encounter provide a typed HUD payload containing display data and the configured health sources. Add a neutral health-change contract to `EnemyHealthComponent` if needed.

### 4.4 ScriptableObject boss identity and variants

**Source:** supplied `BossScene.cs`.

```csharp
[CreateAssetMenu(fileName = "New Boss Scene", menuName = "Hollow Knight/Boss Scene")]
public class BossScene : ScriptableObject
{
    public string sceneName;
    public BossScene baseBoss;
    public bool substituteBoss;
    public bool isHidden;

    [SerializeField] private BossScene tier1Scene;
    [SerializeField] private BossScene tier2Scene;
    [SerializeField] private BossScene tier3Scene;
}
```

**What this demonstrates**

- A boss/refight entry has stable authored data independent of the live boss GameObject.
- Scene variants and presentation metadata are data-driven.

**Underbrew adaptation**

Create a much smaller first-pass `BossEncounterDefinition` ScriptableObject for stable identity and presentation only. Do not add refight tiers, substitute bosses, hidden sequence entries, or unlock-test arrays yet.

### 4.5 Completion facts are not collapsed into one boolean

**Source:** supplied `BossStatue.cs`, nested `Completion` struct.

```csharp
[Serializable]
public struct Completion
{
    public bool hasBeenSeen;
    public bool isUnlocked;
    public bool completedTier1;
    public bool completedTier2;
    public bool completedTier3;
    public bool seenTier3Unlock;
    public bool usingAltVersion;
}
```

**What this demonstrates**

- Unlock, discovery, completion quality, and selected variant are separate facts.
- A later refight/challenge layer can grow without redefining ordinary world completion.

**Underbrew adaptation**

The first pass stores only world encounter completion in `WorldStateRegistry`. Future refight/no-hit/tier records should live in a separate boss-challenge domain rather than expanding the physical world registry indiscriminately.

### 4.6 Challenge sequence state is a separate higher layer

**Source:** supplied `BossSequenceController.cs`, `FinishLastBossScene(...)`.

```csharp
BossSequenceDoor.Completion previousCompletion =
    BossSequenceController.currentData.previousCompletion;

previousCompletion.completed = true;

if (BossSequenceController.BoundNail)
{
    previousCompletion.boundNail = true;
}

if (!BossSequenceController.KnightDamaged)
{
    previousCompletion.noHits = true;
}
```

**What this demonstrates**

- Sequence/challenge completion is run-level metadata above ordinary boss death.
- No-hit and restriction clears are recorded only when the whole sequence finishes.

**Underbrew adaptation**

Explicitly defer this layer. Do not add bindings, boss-rush timers, no-hit records, or statue tiers to the first boss implementation.

---

## 5. Silksong patterns we should not copy

### Static one-shot setup injection

Silksong uses `BossSceneController.SetupEvent`, a static delegate consumed by the next controller's `Awake`. Do not recreate this. Underbrew should use explicit scene references, typed runtime context, or an owned persistent state service when future cross-scene boss sequences are introduced.

### PlayMaker string contracts

The reference code sends and reads string events and variables such as:

- `"ROAR ENTER"`
- `"ROAR EXIT"`
- `"DREAM RETURN"`
- `"To Scene"`
- `"Entry Door"`
- FSM names such as `"Roar and Wound States"`.

Underbrew uses typed C# APIs and Animancer. Do not add Animator parameter state machines or string-named FSM contracts.

### Reflection over PlayerData fields

`BossSequenceBindingsDisplay` and `BossSequenceController` scan `PlayerData` fields by reflection to count completion records. Underbrew save state should remain explicit and typed through ScriptableObject `ISaveTarget` owners.

### Scene controller reading hero internals

Silksong's completion coroutine checks specific hero state fields directly. Underbrew's camera, HUD, and world systems must not reach into `HeroStateBlackboard` or action internals. Add a narrow public contract only if a genuine orchestration requirement cannot be met through existing events/control locks.

### Direct music calls from gameplay objects

Current Underbrew audio rules assign music routing to `GameManager`. `BossEncounterController` must not call `AudioManager.PlayMusic` directly unless the architecture/spec is intentionally changed and documented.

---

## 6. Proposed runtime architecture

```text
BossEncounterDefinition (ScriptableObject)
└── stable ID + display/presentation metadata

BossEncounterController (scene MonoBehaviour, thin coordinator)
├── BossEncounterDefinition
├── WorldStateRegistry
├── BossEncounterTrigger or authored start call
├── BossEncounterParticipant[]
├── BossArenaBarrier[]
├── CameraLockArea reference/root
├── rewardRoot
├── optional intro/outro presentation hooks
└── boss HUD/music event requests

BossEncounterParticipant (boss-root adapter)
├── EnemyHealthComponent
├── boss-specific lifecycle implementation
└── optional presentation completion signal

<FirstBoss>Behaviour (IEnemyBehaviour + boss encounter lifecycle)
├── EnemyMotor
├── EnemyPerception or explicit target Transform seam
├── EnemyAttackController(s)
├── AnimancerComponent
└── boss-specific config

Persistent HUD
└── BossHealthDisplay
    └── binds through a neutral encounter HUD event/service
```

The exact participant/event names may change after Codex inspects existing naming conventions. The ownership boundaries must remain.

---

## 7. Proposed types and contracts

### 7.1 `BossEncounterDefinition`

**Suggested path:** `Assets/_Project/Scripts/Boss/BossEncounterDefinition.cs`  
**Suggested asset path:** `Assets/_Project/ScriptableObjects/Boss/`

First-pass fields:

```csharp
[CreateAssetMenu(menuName = "Boss/Boss Encounter Definition")]
public sealed class BossEncounterDefinition : ScriptableObject
{
    [SerializeField] private string encounterId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite displayIcon;
    [SerializeField] private AudioClip bossMusic;

    public string EncounterId => encounterId;
    public string DisplayName => displayName;
    public Sprite DisplayIcon => displayIcon;
    public AudioClip BossMusic => bossMusic;
}
```

This is illustrative, not final implementation code.

Rules:

- `encounterId` is stable and must not change when the scene or asset is renamed.
- It must be non-empty and globally unique among boss encounters.
- The asset does not own phase/attack tuning.
- The asset does not own reward collection state.
- The asset does not own save/load logic.
- Localization can replace `displayName` later; do not build a localization framework in this pass.

### 7.2 `BossEncounterState`

Suggested states:

```csharp
public enum BossEncounterState
{
    Dormant,
    Starting,
    Active,
    BossesDefeated,
    Completing,
    Completed
}
```

Required semantics:

- `Dormant`: first-time encounter waiting for activation.
- `Starting`: arena is sealing and intro presentation is running.
- `Active`: boss gameplay and HUD are active.
- `BossesDefeated`: all configured boss health owners have died.
- `Completing`: post-death presentation is finishing; permanent completion has not yet been written.
- `Completed`: permanent completion is recorded and completed-room state is applied.

Already-defeated scene initialization should apply `Completed` silently without replaying intro, barrier, camera, reward, or victory feedback.

### 7.3 `BossEncounterController`

**Suggested path:** `Assets/_Project/Scripts/Boss/BossEncounterController.cs`

Suggested serialized dependencies:

```text
BossEncounterDefinition definition
WorldStateRegistry worldStateRegistry
BossEncounterParticipant[] participants
BossArenaBarrier[] barriers
GameObject cameraLockRoot or CameraLockArea cameraLock
GameObject rewardRoot
bool beginOnPlayerTrigger / BossEncounterTrigger trigger
optional intro/outro presentation source
```

Suggested public events:

```csharp
public event Action EncounterStarting;
public event Action EncounterActivated;
public event Action BossesDefeated;
public event Action EncounterCompleted;
```

Core responsibilities:

1. Validate references and stable identity.
2. Query `WorldStateRegistry.IsEncounterDefeated` once during initialization.
3. Apply the completed-room state silently if already defeated.
4. Otherwise prepare the first-time dormant state.
5. Start once from the authored trigger/call.
6. Acquire hero control lock for only the authored intro portion.
7. Close barriers and enable the camera lock.
8. Activate/prepare participants.
9. Publish HUD and music requests.
10. Release hero control when active gameplay begins.
11. Count participant deaths.
12. Enter `BossesDefeated` only when every configured participant is defeated.
13. Hide/settle boss HUD at the correct presentation moment.
14. Wait for the typed completion gate/presentation signal.
15. Mark the encounter defeated exactly once.
16. Enable the reward root.
17. Open barriers, release camera lock, release any remaining hero lock, and restore music through the approved owner.
18. Clean up subscriptions and temporary locks in `OnDisable`/`OnDestroy` without marking completion.

Do not:

- call `SaveManager.Save()`;
- call `SceneManager.LoadScene*`;
- write hero velocity;
- write `HeroStateBlackboard` fields;
- pick boss attacks;
- call `AudioManager.PlayMusic` directly under the current audio rules;
- discover participants through repeated scene searches;
- infer encounter identity from scene name or GameObject name.

### 7.4 `BossEncounterTrigger`

**Suggested path:** `Assets/_Project/Scripts/Boss/BossEncounterTrigger.cs`

A small optional trigger component may be preferable to putting physics callbacks on the coordinator.

Responsibilities:

- detect the Player layer/tag once;
- call `BossEncounterController.TryBeginEncounter()`;
- disable/consume itself after a successful start;
- do nothing when the encounter is already completed or starting/active;
- contain no arena, persistence, HUD, or boss behaviour logic.

If an equivalent reusable trigger already exists, use it rather than adding this type.

### 7.5 `BossEncounterParticipant`

**Suggested path:** `Assets/_Project/Scripts/Boss/BossEncounterParticipant.cs`

Purpose: provide a typed, reusable encounter-facing seam on each configured boss root.

Potential dependencies:

```text
EnemyHealthComponent health
MonoBehaviour behaviourSource implementing IBossEncounterBehaviour
GameObject activationRoot (only if different from the participant root)
```

Potential API:

```csharp
public EnemyHealthComponent Health { get; }
public bool IsDefeated { get; }

public void PrepareEncounter(BossEncounterContext context);
public void BeginEncounter();
public void NotifyEncounterCompleted();
```

Codex should decide whether the adapter adds enough value over directly storing `EnemyHealthComponent[]` plus a typed lifecycle interface. Do not introduce the adapter solely for theoretical flexibility. The required outcomes are explicit roster ownership, typed activation, and clean event subscription.

### 7.6 `IBossEncounterBehaviour`

A small typed lifecycle contract may be useful:

```csharp
public interface IBossEncounterBehaviour
{
    void PrepareForEncounter(BossEncounterContext context);
    void BeginEncounter();
    void OnEncounterCompleted();
}
```

The boss-specific component may implement both `IEnemyBehaviour` and this interface.

Do not create a generic abstract boss state-machine framework. The first boss should use a concrete behaviour component with its own states, following the composition approach used by `MushroomEnemy`.

### 7.7 First boss behaviour

**Suggested path:** `Assets/_Project/Scripts/Boss/<FirstBoss>/<FirstBoss>Behaviour.cs`

Codex must not invent the final boss theme, attacks, animation clips, or tuning if these are not already present in the repository. The plan should identify placeholders and Editor-authored content requirements.

Minimum behavioural states:

```text
Dormant / Intro
PhaseOne
PhaseTransition (only if needed by the first design)
PhaseTwo (only if needed by the first design)
Hurt or stagger policy
Defeated
```

Rules:

- Use Animancer directly.
- Movement commands go through `EnemyMotor`.
- Attack damage uses existing `EnemyAttackController` / `EnemyAttackHitbox` where it fits.
- More than one authored attack may require either multiple attack controllers/modules or a small extension that lets one controller receive an attack profile. Codex must inspect current attack architecture before choosing.
- Do not place boss-specific fields into `GameManager` or generic hero configs.
- Shared ordinary-enemy fields may remain in `EnemyConfig`; boss phase/attack selection tuning should live in a boss-specific config asset if needed.
- Preserve validated hero movement/combat feel values.

### 7.8 `BossArenaBarrier`

**Suggested path:** `Assets/_Project/Scripts/Boss/BossArenaBarrier.cs`

This is a temporary combat seal, not a `PersistentDoor`.

Required API semantics:

```csharp
void SetOpen(bool open, bool immediate);
```

or equivalent explicit methods:

```csharp
void Open(bool immediate);
void Close(bool immediate);
```

Responsibilities:

- enable/disable blocking collider(s);
- switch open/closed presentation roots or play authored animation;
- support immediate silent state application during scene initialization;
- support feedback when closing/opening during a live encounter;
- not write permanent state;
- not query `WorldStateRegistry` itself;
- not detect player input;
- not own encounter progression.

Required states:

- before first-time start: open;
- active fight: closed;
- after victory: open;
- already-completed room initialization: open immediately with no feedback;
- interrupted scene unload/death: cleanup is safe; scene reload restores authored state through the controller.

### 7.9 Boss health presentation

**Suggested path:** `Assets/_Project/Scripts/UI/BossHealthDisplay.cs`

The view belongs under the existing persistent HUD hierarchy on `_GameCameras`.

Requirements:

- presentation-only;
- no `Update` polling;
- no scene search;
- no mutation of health, encounter, save, hero, camera, or time state;
- explicit initial refresh when bound;
- explicit unsubscribe when encounter ends or the component disables;
- supports one boss now and an authored multi-boss roster without redesigning the data contract;
- hides or settles at a deliberate lifecycle moment after defeat;
- does not interpret load restoration as damage.

A neutral event/service is likely needed because scene controllers cannot serialize references to the persistent HUD. A possible payload:

```csharp
public readonly struct BossHudRequest
{
    public BossEncounterDefinition Definition { get; }
    public IReadOnlyList<EnemyHealthComponent> HealthSources { get; }
    public object Source { get; }
}
```

A possible service:

```csharp
public static class BossHudEventService
{
    public static event Action<BossHudRequest> ShowRequested;
    public static event Action<object> HideRequested;
}
```

This mirrors the project's explicit `CameraEventService` pattern. Codex should verify whether an existing generic UI event channel can be reused first.

### 7.10 `EnemyHealthComponent` extension

Current contract:

```csharp
public event Action OnDamaged;
public event Action OnDeath;
```

The boss HUD needs current and maximum values without polling private fields. Prefer a neutral additive extension such as:

```csharp
public event Action<int, int> OnHealthChanged;
public int CurrentHealth => currentHealth;
public int MaximumHealth => config != null ? config.maxHealth : 0;
```

Expected behaviour:

- `CurrentHealth` is initialized from `EnemyConfig.maxHealth`.
- `OnHealthChanged` fires once after an accepted damaging hit changes health.
- The view performs its own initial refresh from the properties when binding.
- Existing `OnDamaged` and `OnDeath` semantics remain intact.
- Ordinary enemies incur no per-frame cost.
- Suppressed/uninitialized enemies remain safe no-ops.

Codex must inspect tests and add regression coverage before changing this shared component.

### 7.11 Camera integration

Use the existing `CameraLockArea` and `CameraEventService`.

The simplest authored setup is likely:

- a `CameraLockArea` GameObject covering the arena;
- its root inactive before the encounter;
- controller enables it when the arena seals;
- controller disables it after completion;
- `CameraLockArea.OnDisable()` already raises lock-exited cleanup.

Do not add boss-specific camera logic to the hero. Boss-specific shakes should use `CameraEventService.RequestShake(...)` and authored `CameraShakeProfile` where appropriate.

Codex must verify whether a camera-lock object being activated while the player is already inside receives the required trigger callbacks. If not, the plan must propose a narrow explicit camera lock activation API rather than relying on uncertain trigger timing.

### 7.12 Hero control integration

Use only:

```csharp
HeroController.AddControlLock(object source);
HeroController.RemoveControlLock(object source);
```

The encounter controller may hold a lock during:

- a short arena-seal moment;
- an authored intro presentation;
- an authored victory/reward reveal moment, if required.

The hero must be controllable during the actual fight. Every lock acquisition must have cleanup in interruption paths. Do not write `blackboard.controlLocked` directly.

If the controller needs the live hero reference, obtain it through the project's existing scene initialization/manager seam. Do not introduce a new global `HeroController.Instance` singleton.

### 7.13 Audio and music

SFX:

- boss-local hit/death/attack SFX go through `AudioManager.PlaySFX`;
- hero sounds remain with `HeroAudioController`;
- barriers/world presentation use `AudioManager.PlaySFX`.

Music:

- current docs state that music is owned by `GameManager` and gameplay MonoBehaviours must not call `AudioManager.PlayMusic` directly;
- boss music is a mid-scene transition, so the existing scene-only routing is insufficient;
- Codex must plan the smallest compliant extension, such as a typed music request routed through the existing owner;
- do not add a large audio state machine solely for the first boss;
- restore the correct room music after victory or interruption.

Any new music-routing API requires corresponding updates to `Docs/FeatureSpecs/Audio.md` and `Docs/Architecture.md`.

### 7.14 Optional Timeline integration

Timeline may be useful for an authored intro or victory reveal, but it must not be a hard dependency of the boss framework.

Preferred rule:

- core encounter lifecycle is C# and event-driven;
- an optional presentation component may wrap a `PlayableDirector` and signal completion;
- the controller waits on the presentation contract, not on a hardcoded timeline duration;
- gameplay-critical state changes remain explicit C# calls/events;
- no boss AI or attack timing is authored entirely inside Timeline.

Codex should first confirm whether Timeline is installed and whether the project already has a presentation wrapper. If not, the first pass can use a simple Animancer/coroutine presentation hook and leave Timeline as optional Editor work.

---

## 8. Encounter lifecycle

### 8.1 First-time scene initialization

1. `BossEncounterController` validates its definition, registry, participant roster, barriers, camera, and reward references.
2. Query `worldStateRegistry.IsEncounterDefeated(definition.EncounterId)` exactly once.
3. Result is false.
4. Apply dormant state silently:
   - arena barriers open;
   - camera lock inactive;
   - reward root inactive;
   - boss HUD hidden;
   - boss roots/AI inactive or explicitly dormant;
   - trigger enabled;
   - no music change;
   - no hero control lock.

Prefer authoring boss gameplay roots inactive until the controller activates them. This avoids initializing and then suppressing a permanently defeated boss, but Codex must validate prefab/scene ordering and choose the safest implementation.

### 8.2 Encounter start

1. Player enters the authored start trigger or another explicit authored call starts the encounter.
2. `TryBeginEncounter()` guards against duplicate start.
3. State becomes `Starting`.
4. Consume/disable the trigger.
5. Acquire a hero control lock if the seal/intro requires it.
6. Close barriers with live feedback.
7. Enable the arena camera lock.
8. Request boss music through the approved music owner.
9. Activate/prepare boss participants.
10. Show the boss HUD with an explicit roster.
11. Run intro presentation.
12. Boss participants enter active gameplay.
13. State becomes `Active`.
14. Release the intro control lock.

### 8.3 Active fight

- Boss behaviour owns AI and attack selection.
- `EnemyMotor` owns boss Rigidbody2D movement writes.
- Existing combat hit contracts remain unchanged.
- The controller only observes participant lifecycle/death.
- A recoverable hazard recovery does not reset the fight or reload the room.
- A lethal hit or forced hazard death enters the existing normal-death flow.

### 8.4 Bosses defeated

1. A participant's `EnemyHealthComponent.OnDeath` fires.
2. The controller marks only that participant defeated.
3. If any configured participant remains alive, the encounter stays active.
4. When all are defeated:
   - guard against repeat completion;
   - state becomes `BossesDefeated`;
   - fire `BossesDefeated` event;
   - begin/continue boss-specific death and victory presentation;
   - close or settle the HUD according to the presentation contract;
   - do **not** mark the registry yet.

### 8.5 Encounter completion

1. All bosses are defeated.
2. Required defeat/outro presentation reports completion through a typed contract.
3. State becomes `Completing`.
4. Re-check that the hero/scene is still in a valid completion path through existing public APIs; do not inspect hero blackboard internals.
5. Call `worldStateRegistry.MarkEncounterDefeated(encounterId)` exactly once.
6. Enable the reward root.
7. Open barriers with live feedback.
8. Disable the camera lock.
9. Restore room music through the approved owner.
10. Release any remaining control lock.
11. Hide/unbind the boss HUD.
12. State becomes `Completed`.
13. Fire `EncounterCompleted`.

### 8.6 Hero death during the fight

- The encounter does not mark permanent completion.
- The controller cleans up event subscriptions and temporary locks when the scene unloads.
- Existing `GameManager.BeginRespawnSequence` loads the saved checkpoint scene.
- Normal death clears ordinary transient enemy records but preserves permanent encounter facts.
- On re-entering the boss scene, the registry still reports incomplete, so the encounter is available again.
- Do not resurrect/reset the boss in place with a coroutine or timer.

### 8.7 Scene unload or interruption

`OnDisable`/`OnDestroy` must safely:

- unsubscribe from participant health events;
- remove any hero control lock owned by the encounter;
- request HUD unbind/hide using a source token;
- release/cancel encounter-owned camera shake/freeze if used;
- not mark completion;
- avoid errors if persistent managers are already shutting down.

### 8.8 Already-completed room initialization

1. Registry returns true for the encounter ID.
2. Apply completed state silently before the player can interact:
   - boss roots remain inactive/suppressed;
   - trigger disabled;
   - barriers open immediately;
   - camera lock inactive;
   - boss HUD hidden;
   - boss music not requested;
   - reward root enabled;
   - no victory VFX, sound, shake, dialogue, or animation replay.
3. The reward participant independently suppresses itself if already collected.

This quiet restore is required. Loaded state is not gameplay feedback.

---

## 9. Existing code integration points

### Enemy health

`Assets/_Project/Scripts/Enemy/EnemyHealthComponent.cs`

Current useful events:

```csharp
public event Action OnDamaged;
public event Action OnDeath;
```

Current death path disables attacks/contact/physics before raising `OnDeath`, then destroys after `EnemyConfig.deathDestroyDelay`. The boss plan must account for the boss-specific death presentation. Prefer a narrow opt-in extension or correctly coordinated delay rather than a broad rewrite of ordinary enemy death.

### Enemy coordinator

`Assets/_Project/Scripts/Enemy/EnemyController.cs`

It initializes persistence first, then motor/perception/recoil/health/attacks, then `IEnemyBehaviour` components last. Boss setup should preserve this ordering.

### Enemy movement

`Assets/_Project/Scripts/Enemy/EnemyMotor.cs`

Boss movement must route through this authority. Do not repeat `MushroomEnemy`'s emergency Rigidbody2D fallback in new boss code; the boss prefab should require a valid motor and fail clearly if it is missing.

### Enemy attacks

`Assets/_Project/Scripts/Enemy/EnemyAttackController.cs`

It already provides:

- Startup → Active → Recovery → Cooldown;
- active-window hitbox evaluation;
- duplicate-hit prevention;
- hurt/death interruption;
- attack lifecycle events.

Codex must determine whether multiple boss attacks are best represented by:

- multiple authored `EnemyAttackController` components/children;
- one controller with a new attack-profile input;
- a thin boss-specific attack wrapper around existing controllers.

Avoid rewriting the validated ordinary-enemy attack controller unless a small general extension is demonstrably cleaner.

### World persistence

`Assets/_Project/Scripts/World/Persistence/Core/WorldStateRegistry.cs`

Use existing APIs:

```csharp
public bool IsEncounterDefeated(string encounterId)
public void MarkEncounterDefeated(string encounterId)
```

Do not add a parallel boss save list.

### Hero control

`Assets/_Project/Scripts/Hero/HeroController.cs`

Use:

```csharp
public void AddControlLock(object source)
public void RemoveControlLock(object source)
```

### Camera

- `Assets/_Project/Scripts/Camera/CameraLockArea.cs`
- `Assets/_Project/Scripts/Camera/CameraEventService.cs`

Use existing authored lock areas and event requests. Do not reference hero internals from camera systems.

### HUD

- `Assets/_Project/Scripts/UI/PersistentHudRoot.cs`
- `Assets/_Project/Scripts/UI/HealthDisplay.cs`
- `Assets/_Project/Scripts/UI/ResourceDisplay.cs`
- `Docs/FeatureSpecs/HUD.md`

Follow the established subscription lifecycle: subscribe once, explicit initial refresh, unsubscribe cleanly, no polling.

### Audio

- `Assets/_Project/Scripts/Audio/AudioManager.cs`
- `Docs/FeatureSpecs/Audio.md`

Generic SFX use `AudioManager`; boss music requires a planned extension to the current GameManager-owned routing policy.

---

## 10. Proposed files likely involved

Codex must confirm exact paths and existing equivalents before finalizing the plan.

### New runtime files

```text
Assets/_Project/Scripts/Boss/
├── BossEncounterController.cs
├── BossEncounterDefinition.cs
├── BossEncounterState.cs
├── BossEncounterTrigger.cs                 if no existing trigger fits
├── BossEncounterParticipant.cs             only if the adapter is justified
├── IBossEncounterBehaviour.cs              only if needed
├── BossArenaBarrier.cs
├── BossHudEventService.cs                  or reuse an existing UI event channel
└── <FirstBoss>/
    ├── <FirstBoss>Behaviour.cs
    └── <FirstBoss>Config.cs                 if boss-specific tuning requires it

Assets/_Project/Scripts/UI/
└── BossHealthDisplay.cs
```

### Existing runtime files likely modified

```text
Assets/_Project/Scripts/Enemy/EnemyHealthComponent.cs
Assets/_Project/Scripts/Enemy/EnemyAttackController.cs   only if a minimal multi-attack extension is required
Assets/_Project/Scripts/Managers/GameManager.cs          only for the narrow approved boss-music seam
Assets/_Project/Scripts/Audio/AudioManager.cs            only if the approved music seam needs additive support
Assets/_Project/Scripts/UI/PersistentHudRoot.cs          only if composition/wiring code requires it
```

Avoid modifying `HeroController` unless an existing public seam is genuinely insufficient. The current control-lock API should be enough.

### Editor/validation/tests

```text
Assets/_Project/Scripts/Editor/Validation/BossEncounterValidator.cs
Assets/_Project/Scripts/Editor/Tests/Boss/
├── BossEncounterControllerTests.cs
├── BossHealthDisplayTests.cs
└── BossEncounterValidationTests.cs
```

Codex should decide whether encounter-ID validation belongs in the existing `WorldPersistenceValidator` rather than a separate validator.

### Documentation

```text
Docs/Architecture.md
Docs/ImplementationPlan.md
Docs/FeatureSpecs/EnemyAI.md
Docs/FeatureSpecs/HUD.md
Docs/FeatureSpecs/Audio.md
Docs/FeatureSpecs/SaveSystem.md or WorldPersistence.md only if contracts change
Docs/FeatureSpecs/BossEncounters.md                 recommended new feature spec
```

---

## 11. Unity Editor work required

The later implementation is not complete until the following Editor work is performed and reported.

### Data assets

- Create `BossEncounterDefinition` asset for the first boss.
- Assign a stable globally unique encounter ID.
- Assign display name/icon placeholders.
- Assign boss music only after the music-routing seam is implemented.
- Create a boss-specific config asset if required.
- Use an `EnemyConfig` asset for shared health/hurt/death/movement fields.

### Boss prefab

- Add and assign existing enemy foundation components.
- Add boss participant/lifecycle component if used.
- Add the concrete boss behaviour.
- Assign Animancer component and all animation clips.
- Assign attack controllers/hitboxes and hero hurtbox layer masks.
- Assign perception/terrain masks.
- Assign local SFX/VFX/feedback references.
- Ensure all movement writes route through `EnemyMotor`.
- Configure recoil/stagger policy.
- Ensure death presentation can finish before root destruction.
- Do not set encounter-controlled boss persistence to independently mark the same permanent encounter.

### Arena scene

- Place one active `BossEncounterController`.
- Assign definition and shared `WorldStateRegistry.asset`.
- Assign every boss participant explicitly.
- Place/assign an authored start trigger.
- Place/assign temporary `BossArenaBarrier` objects and blocking colliders.
- Place/assign `CameraLockArea` bounds.
- Assign reward root and its actual pickup/domain participant.
- Author intro/outro presentation references.
- Ensure dormant/completed initial active states match the controller's initialization contract.
- Confirm no persistent manager prefab is duplicated in the gameplay scene.

### Persistent HUD prefab

- Add boss-health UI under the existing HUD Canvas on `_GameCameras.prefab`.
- Assign bar, name text, icon, CanvasGroup/animation references.
- Keep `HUDCamera` as URP Overlay with no AudioListener.
- Verify only one boss HUD exists project-wide.

### Layers and physics

- Verify Player, enemy hitbox, hero hurtbox, terrain, barrier, and trigger layer masks.
- Verify boss barrier colliders do not interfere with camera trigger geometry.
- Verify attack and contact damage cannot unfairly double-hit.
- Verify downslash/pogo remains reliable.

### Scene/build settings

- Add the boss room scene to Build Settings if it is a new scene.
- Add/verify `RoomVisitReporter` with a stable room ID.
- Add World Graph Editor graph/port data and Underbrew `TransitionPoint` links if the room is connected to the existing loop.
- Keep WGE runtime transition ownership disabled as documented.

---

## 12. Validation and acceptance tests

### Automated tests

At minimum, plan tests for:

1. Incomplete encounter initializes dormant.
2. Completed encounter initializes quietly completed.
3. Encounter starts only once.
4. Every configured participant is required before `BossesDefeated`.
5. One participant dying in a multi-participant encounter does not mark permanent completion.
6. Permanent completion is written only after the completion/presentation gate.
7. Completion is written exactly once.
8. Scene unload before completion does not mark the encounter defeated.
9. `EnemyHealthComponent` health properties/events report correct current/max values.
10. Boss HUD performs initial refresh, reacts to damage, and unsubscribes on hide/disable.
11. Completed-room reward root is enabled while the reward participant independently handles collected state.
12. No boss path calls `SaveManager.Save()`.
13. Control locks are released on completion, interruption, and disable/destroy.
14. Duplicate/missing encounter IDs are reported by validation.
15. Encounter-controlled bosses with conflicting `PermanentEncounter` persistence are reported.
16. Existing Mushroom enemy health/attack/death tests continue to pass.

### Manual Unity validation

Do not claim these passed unless they are actually run in the Unity Editor.

#### First encounter

- Enter room before completion: boss and reward are not incorrectly active.
- Cross trigger once: barriers seal, camera locks, intro runs, boss music starts.
- Hero input is locked only for the authored intro.
- Boss HUD shows correct initial/max health.
- Boss activates after the intended cue.

#### Combat

- Side/up/down attacks damage the boss correctly.
- Downslash/pogo works.
- Boss hitboxes damage the hero once per active window.
- Contact and authored attack damage do not unfairly double-hit.
- Hurt/stagger policy behaves as designed.
- Camera shake/hit-stop preserve existing global combat rules.
- No hero feel values change.

#### Retry

- Die during the fight.
- Existing respawn sequence returns to the active checkpoint.
- Re-enter the boss room: encounter is available again.
- No stale barrier, camera lock, HUD, music, or control lock persists.

#### Hazard

- Recoverable hazard inside arena uses local recovery and does not silently reset the fight.
- Lethal/forced hazard death follows the normal retry path.

#### Victory

- Final boss death fires defeat presentation.
- Barriers do not open and permanent state does not write too early.
- Completion occurs after the authored presentation signal.
- Reward becomes available.
- Camera and hero control return correctly.
- Room music restores according to the planned policy.

#### Persistence

- Leave and re-enter after victory: no boss respawn, barriers open quietly, reward available if uncollected.
- Collect reward, leave, and return: reward remains suppressed by its own domain/pickup persistence.
- Save at checkpoint, quit, and Continue: completed room restores correctly.
- Normal death after victory but before a checkpoint preserves the in-session permanent encounter state.

#### Multi-boss readiness

Even if the first content uses one boss, perform a temporary two-participant validation or automated equivalent:

- first death does not complete;
- second death begins completion;
- HUD policy is deterministic.

---

## 13. Required Editor validation rules

The final implementation plan should include checks for:

- missing `BossEncounterDefinition`;
- empty encounter ID;
- duplicate encounter IDs across enabled gameplay scenes/assets;
- missing `WorldStateRegistry` assignment;
- zero participants;
- null/duplicate participant references;
- participant missing `EnemyHealthComponent`;
- participant configured to independently persist the same encounter ID;
- boss root active-state mismatch if inactive-root initialization is required;
- missing barrier collider/presentation references;
- missing reward root when the encounter expects a reward;
- missing/invalid camera lock;
- missing boss HUD on persistent HUD prefab;
- missing attack hitbox layer masks;
- missing Animancer clips/component;
- boss Rigidbody2D movement bypassing `EnemyMotor` where statically detectable;
- missing room ID/scene build configuration where applicable.

Validation must report actionable scene object and asset context. It must not auto-fix stable IDs silently.

---

## 14. Implementation sequencing

The user prefers an integrated implementation rather than many stop/start milestones. Codex should produce one ordered implementation plan with coherent checkpoints, not a large abstract roadmap.

Recommended order:

1. **Audit and contracts**
   - verify current code/docs/prefabs/scenes;
   - resolve the participant seam, health event, music seam, and death-presentation contract;
   - list exact files before editing.

2. **Core encounter lifecycle**
   - definition/state/controller/trigger;
   - registry ownership;
   - quiet completed-room initialization;
   - barriers/camera/control lock;
   - event cleanup.

3. **Boss actor vertical slice**
   - first boss prefab and concrete behaviour;
   - existing enemy motor/attacks/health integration;
   - phases and Animancer presentation;
   - death-presentation completion signal.

4. **HUD, music, reward**
   - neutral boss HUD binding;
   - minimal compliant music routing;
   - reward-root/domain integration.

5. **Validation, tests, docs, Editor wiring**
   - automated tests;
   - validator;
   - prefab/scene/SO assignments;
   - manual Unity checklist;
   - source-of-truth documentation updates.

These are ordered work packages within one implementation effort, not separate feature initiatives.

---

## 15. Explicit non-goals

Do not include these in the first implementation plan except as future notes:

- boss statues;
- boss summary board;
- boss-rush sequences;
- cross-scene boss sequence resume;
- bindings/restrictions;
- tier 1/2/3 challenge scenes;
- no-hit completion records;
- challenge timers;
- alternate/dream boss toggles;
- achievements;
- global boss catalogue;
- localization framework;
- generic behaviour-tree/GOAP framework;
- generic abstract boss state-machine base class;
- pooling bosses;
- live boss respawn timers;
- additive scene loading refactor;
- dialogue system implementation;
- final UI/art polish.

---

## 16. Questions Codex must resolve in its plan

Codex should answer these after inspecting the repository and Unity assets:

1. Is a `BossEncounterParticipant` adapter justified, or can the controller use existing health components plus one smaller typed lifecycle interface?
2. How should an encounter-controlled boss be kept uninitialized/suppressed when already completed: inactive authored root, initialization gate, or a narrow extension to existing enemy persistence?
3. What is the smallest safe change to `EnemyHealthComponent` for boss HUD values?
4. How should the existing attack-window system support multiple distinct boss attacks without disrupting Mushroom?
5. How will boss death presentation signal readiness before `EnemyHealthComponent` destroys the root?
6. How will a persistent boss HUD bind without scene searches or direct scene-to-prefab references?
7. How should boss music start and room music restore while preserving the documented GameManager ownership rule?
8. Does `CameraLockArea` behave correctly when enabled while the hero is already inside its collider?
9. Which current scene and prefab should host the first boss vertical slice?
10. Which reward type is already available in the repository, and which domain owns it?
11. Should encounter validation extend `WorldPersistenceValidator` or remain a focused boss validator?
12. Which docs are stale after the actual implementation and must be updated?

Where evidence is missing, Codex must label the item as an Editor/content decision rather than inventing an implementation fact.

---

## 17. Expected Codex planning output

Codex should return a plan only. It must not edit files during the planning pass.

The plan should contain:

1. **Current-state audit**
   - relevant existing classes/assets/scenes/prefabs;
   - implemented vs missing;
   - doc/code contradictions.

2. **Recommended architecture**
   - exact ownership boundaries;
   - lifecycle diagram;
   - chosen answers to the questions above;
   - alternatives rejected and why.

3. **File-by-file change plan**
   - new files;
   - modified files;
   - purpose and API changes for each;
   - files intentionally untouched.

4. **Implementation order**
   - one integrated ordered sequence;
   - dependencies between steps;
   - no more than two broad implementation passes if separation is truly necessary.

5. **Unity Editor work**
   - exact prefab/scene/SO/Inspector/layer/build-setting work;
   - what Unity MCP can inspect or create;
   - what still needs manual art/content authoring.

6. **Tests and validation**
   - automated tests;
   - validator changes;
   - manual Unity playtest checklist;
   - no claims that tests have passed.

7. **Risks and architecture checks**
   - death timing;
   - duplicate persistence ownership;
   - static event cleanup;
   - HUD/music lifetime;
   - multi-boss correctness;
   - regression risk to Mushroom and hero feel.

8. **Documentation updates**
   - exact source-of-truth docs and sections to change after implementation.

9. **Implementation-ready prompt outline**
   - a concise follow-on prompt that can be used to implement the approved plan after review.

---

## 18. Definition of done for the planned first pass

The implementation is complete only when:

- a first boss encounter can be entered, fought, lost, retried, won, and revisited;
- the encounter controller remains thin;
- boss AI is concrete and actor-owned;
- boss movement uses `EnemyMotor`;
- attacks use the established combat contracts;
- the boss HUD is event-driven and persistent-HUD-compatible;
- barriers and camera locks restore correctly;
- hero control locks cannot leak;
- all configured boss participants are required for completion;
- permanent completion is encounter-owned and written at the correct lifecycle boundary;
- the reward uses its own domain/pickup persistence;
- completed rooms initialize quietly;
- no boss code calls `SaveManager.Save()`;
- no Silksong PlayMaker/string/static-reflection patterns are copied;
- automated tests and Editor validation are added;
- required Unity Editor work is completed and reported;
- source-of-truth docs are updated;
- no one claims Unity compilation, tests, or playtests passed unless they were actually run.
