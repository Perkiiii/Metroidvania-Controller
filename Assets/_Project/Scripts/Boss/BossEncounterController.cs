using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossEncounterController : MonoBehaviour
{
    public event Action EncounterStarting;
    public event Action EncounterActivated;
    public event Action BossesDefeated;
    public event Action EncounterCompleted;

    [Header("Identity and Persistence")]
    [SerializeField] private BossEncounterDefinition definition;
    [SerializeField] private WorldStateRegistry worldStateRegistry;

    [Header("Encounter Roster")]
    [SerializeField] private BossEncounterParticipant[] participants;

    [Header("Arena")]
    [SerializeField] private BossEncounterTrigger trigger;
    [SerializeField] private BossArenaBarrier[] barriers;
    [SerializeField] private CameraLockArea cameraLockArea;
    [SerializeField] private GameObject rewardRoot;

    [Header("Required Authoring")]
    [SerializeField] private bool requiresTrigger = true;
    [SerializeField] private bool requiresBarriers = true;
    [SerializeField] private bool requiresCameraLock = true;

    [Header("Control Locks")]
    [SerializeField] private bool lockHeroDuringIntro = true;
    [SerializeField] private bool lockHeroDuringOutro = true;

    private HeroController hero;
    private HeroHealthComponent heroHealth;
    private bool participantEventsSubscribed;
    private bool heroDeathSubscribed;
    private bool controlLockHeld;
    private bool cameraLockHeld;
    private bool completionCommitted;
    private bool shuttingDown;

    public BossEncounterState State { get; private set; }
    public BossEncounterDefinition Definition => definition;
    public WorldStateRegistry WorldStateRegistry => worldStateRegistry;
    public BossEncounterParticipant[] Participants => participants;
    public BossEncounterTrigger Trigger => trigger;
    public BossArenaBarrier[] Barriers => barriers;
    public CameraLockArea CameraLockArea => cameraLockArea;
    public GameObject RewardRoot => rewardRoot;
    public bool RequiresTrigger => requiresTrigger;
    public bool RequiresBarriers => requiresBarriers;
    public bool RequiresCameraLock => requiresCameraLock;
    public bool CompletionCommitted => completionCommitted;

    private void Awake()
    {
        InitializeEncounter();
    }

    private void OnDisable()
    {
        CleanupForDisableOrDestroy();
    }

    private void OnDestroy()
    {
        CleanupForDisableOrDestroy();
    }

    public bool TryBeginEncounter()
    {
        if (State != BossEncounterState.Dormant || completionCommitted || !HasRuntimeRequirements())
        {
            return false;
        }

        State = BossEncounterState.Starting;
        trigger?.SetAvailable(false);
        EncounterStarting?.Invoke();

        ResolveHeroReferences();
        if (heroHealth == null || ((lockHeroDuringIntro || lockHeroDuringOutro) && hero == null))
        {
            Debug.LogError($"[{nameof(BossEncounterController)}] '{name}' could not resolve the active hero health/control references.", this);
            AbortFailedStart();
            return false;
        }

        SubscribeHeroDeath();
        AcquireControlLock(lockHeroDuringIntro);
        SetBarriersOpen(false, false);
        SetCameraLockActive(true);

        for (int i = 0; i < participants.Length; i++)
        {
            if (!participants[i].ActivateAndPrepare())
            {
                AbortFailedStart();
                return false;
            }
        }

        BossHudEventService.RequestShow(this, definition, BuildHealthRoster());
        for (int i = 0; i < participants.Length; i++)
        {
            participants[i].PlayIntro();
        }

        TryActivateCombat();
        return true;
    }

    private void InitializeEncounter()
    {
        completionCommitted = false;
        shuttingDown = false;
        SubscribeParticipantEvents();

        bool alreadyCompleted = definition != null
            && worldStateRegistry != null
            && worldStateRegistry.IsEncounterDefeated(definition.EncounterId);

        for (int i = 0; participants != null && i < participants.Length; i++)
        {
            participants[i]?.ApplyDormantState();
        }

        SetCameraLockActive(false);
        SetBarriersOpen(true, true);

        if (alreadyCompleted)
        {
            completionCommitted = true;
            State = BossEncounterState.Completed;
            trigger?.SetAvailable(false);
            if (rewardRoot != null)
            {
                rewardRoot.SetActive(true);
            }
            return;
        }

        State = BossEncounterState.Dormant;
        trigger?.SetAvailable(true);
        if (rewardRoot != null)
        {
            rewardRoot.SetActive(false);
        }
    }

    private bool HasRuntimeRequirements()
    {
        if (definition == null || worldStateRegistry == null || participants == null || participants.Length == 0)
        {
            Debug.LogError($"[{nameof(BossEncounterController)}] '{name}' is missing its definition, registry, or participant roster.", this);
            return false;
        }

        if (requiresTrigger && trigger == null)
        {
            Debug.LogError($"[{nameof(BossEncounterController)}] '{name}' requires a start trigger.", this);
            return false;
        }

        if (requiresBarriers && (barriers == null || barriers.Length == 0))
        {
            Debug.LogError($"[{nameof(BossEncounterController)}] '{name}' requires at least one arena barrier.", this);
            return false;
        }

        if (requiresCameraLock && cameraLockArea == null)
        {
            Debug.LogError($"[{nameof(BossEncounterController)}] '{name}' requires a camera lock area.", this);
            return false;
        }

        for (int i = 0; i < participants.Length; i++)
        {
            if (participants[i] == null)
            {
                Debug.LogError($"[{nameof(BossEncounterController)}] '{name}' has a null participant at index {i}.", this);
                return false;
            }
        }

        return true;
    }

    private void SubscribeParticipantEvents()
    {
        if (participantEventsSubscribed || participants == null)
        {
            return;
        }

        for (int i = 0; i < participants.Length; i++)
        {
            BossEncounterParticipant participant = participants[i];
            if (participant == null)
            {
                continue;
            }

            participant.Defeated += HandleParticipantDefeated;
            participant.IntroCompleted += HandleParticipantIntroCompleted;
            participant.DefeatPresentationCompleted += HandleParticipantDefeatPresentationCompleted;
        }

        participantEventsSubscribed = true;
    }

    private void UnsubscribeParticipantEvents()
    {
        if (!participantEventsSubscribed || participants == null)
        {
            return;
        }

        for (int i = 0; i < participants.Length; i++)
        {
            BossEncounterParticipant participant = participants[i];
            if (participant == null)
            {
                continue;
            }

            participant.Defeated -= HandleParticipantDefeated;
            participant.IntroCompleted -= HandleParticipantIntroCompleted;
            participant.DefeatPresentationCompleted -= HandleParticipantDefeatPresentationCompleted;
        }

        participantEventsSubscribed = false;
    }

    private void HandleParticipantIntroCompleted(BossEncounterParticipant participant)
    {
        TryActivateCombat();
    }

    private void TryActivateCombat()
    {
        if (State != BossEncounterState.Starting || !AllParticipantsMatch(p => p.IsIntroComplete))
        {
            return;
        }

        for (int i = 0; i < participants.Length; i++)
        {
            participants[i].BeginCombat();
        }

        State = BossEncounterState.Active;
        ReleaseControlLock();
        EncounterActivated?.Invoke();
    }

    private void HandleParticipantDefeated(BossEncounterParticipant participant)
    {
        if (completionCommitted || State == BossEncounterState.Interrupted
            || (State != BossEncounterState.Active && State != BossEncounterState.Starting))
        {
            return;
        }

        if (!AllParticipantsMatch(p => p.IsDefeated))
        {
            return;
        }

        State = BossEncounterState.BossesDefeated;
        AcquireControlLock(lockHeroDuringOutro);
        BossesDefeated?.Invoke();
        TryCommitCompletion();
    }

    private void HandleParticipantDefeatPresentationCompleted(BossEncounterParticipant participant)
    {
        TryCommitCompletion();
    }

    private void TryCommitCompletion()
    {
        if (completionCommitted || State != BossEncounterState.BossesDefeated
            || !AllParticipantsMatch(p => p.IsDefeated)
            || !AllParticipantsMatch(p => p.IsDefeatPresentationComplete))
        {
            return;
        }

        completionCommitted = true;
        UnsubscribeHeroDeath();
        State = BossEncounterState.Completing;

        if (definition != null && worldStateRegistry != null)
        {
            worldStateRegistry.MarkEncounterDefeated(definition.EncounterId);
        }

        if (rewardRoot != null)
        {
            rewardRoot.SetActive(true);
        }

        SetBarriersOpen(true, false);
        SetCameraLockActive(false);
        BossHudEventService.RequestHide(this);
        ReleaseControlLock();

        for (int i = 0; i < participants.Length; i++)
        {
            participants[i]?.NotifyEncounterCompleted();
        }

        State = BossEncounterState.Completed;
        EncounterCompleted?.Invoke();
    }

    private bool AllParticipantsMatch(Predicate<BossEncounterParticipant> predicate)
    {
        if (participants == null || participants.Length == 0)
        {
            return false;
        }

        for (int i = 0; i < participants.Length; i++)
        {
            if (participants[i] == null || !predicate(participants[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void ResolveHeroReferences()
    {
        if (hero == null)
        {
            hero = GameManager.Instance?.CurrentHero;
        }

        if (heroHealth == null)
        {
            heroHealth = GameManager.Instance?.CurrentHeroHealth;
        }
    }

    private void SubscribeHeroDeath()
    {
        if (heroDeathSubscribed || heroHealth == null)
        {
            return;
        }

        heroHealth.OnDeath += HandleHeroDeath;
        heroDeathSubscribed = true;
    }

    private void UnsubscribeHeroDeath()
    {
        if (!heroDeathSubscribed || heroHealth == null)
        {
            return;
        }

        heroHealth.OnDeath -= HandleHeroDeath;
        heroDeathSubscribed = false;
    }

    // Deliberately does not restore State to Dormant or re-enable the trigger: this encounter can
    // only be re-armed by a fresh InitializeEncounter() run (i.e. a new Awake()). That is safe only
    // because GameManager.BeginRespawnSequence always performs a full SceneManager.LoadSceneAsync
    // reload of the respawn scene -- even for a same-scene checkpoint -- which destroys and
    // reconstructs this controller, every participant, and every actor from scratch. There is
    // currently no supported "reposition in place without a scene reload" respawn path. If one is
    // ever added, this controller (and the actor's own runtime state) will need an explicit
    // reset/rearm API instead of relying on this scene-reload contract. See BossEncounters.md.
    private void HandleHeroDeath()
    {
        if (completionCommitted || State == BossEncounterState.Completed || State == BossEncounterState.Interrupted)
        {
            return;
        }

        State = BossEncounterState.Interrupted;
        for (int i = 0; participants != null && i < participants.Length; i++)
        {
            participants[i]?.Interrupt();
        }

        SetBarriersOpen(true, true);
        SetCameraLockActive(false);
        BossHudEventService.RequestHide(this);
        ReleaseControlLock();
        UnsubscribeHeroDeath();
        UnsubscribeParticipantEvents();
    }

    private void AbortFailedStart()
    {
        State = BossEncounterState.Interrupted;
        for (int i = 0; participants != null && i < participants.Length; i++)
        {
            participants[i]?.Interrupt();
        }

        SetBarriersOpen(true, true);
        SetCameraLockActive(false);
        BossHudEventService.RequestHide(this);
        ReleaseControlLock();
        UnsubscribeHeroDeath();
        State = BossEncounterState.Dormant;
        trigger?.SetAvailable(true);
    }

    private void AcquireControlLock(bool shouldLock)
    {
        if (!shouldLock || controlLockHeld || hero == null)
        {
            return;
        }

        hero.AddControlLock(this);
        controlLockHeld = true;
    }

    private void ReleaseControlLock()
    {
        if (!controlLockHeld)
        {
            return;
        }

        hero?.RemoveControlLock(this);
        controlLockHeld = false;
    }

    private void SetBarriersOpen(bool open, bool immediate)
    {
        if (barriers == null)
        {
            return;
        }

        for (int i = 0; i < barriers.Length; i++)
        {
            barriers[i]?.SetOpen(open, immediate);
        }
    }

    private void SetCameraLockActive(bool active)
    {
        if (cameraLockArea == null)
        {
            cameraLockHeld = false;
            return;
        }

        if (active)
        {
            cameraLockArea.gameObject.SetActive(true);
            if (!cameraLockHeld)
            {
                CameraEventService.RaiseLockEntered(cameraLockArea);
                cameraLockHeld = true;
            }
            return;
        }

        if (cameraLockHeld)
        {
            CameraEventService.RaiseLockExited(cameraLockArea);
            cameraLockHeld = false;
        }

        cameraLockArea.gameObject.SetActive(false);
    }

    private EnemyHealthComponent[] BuildHealthRoster()
    {
        EnemyHealthComponent[] roster = new EnemyHealthComponent[participants.Length];
        for (int i = 0; i < participants.Length; i++)
        {
            roster[i] = participants[i]?.Health;
        }

        return roster;
    }

    // Same non-rearming contract as HandleHeroDeath: this only ever runs once per scene load
    // (guarded by shuttingDown), and a fresh instance is what re-arms the encounter on the next
    // scene load, not this method reactivating anything in place.
    private void CleanupForDisableOrDestroy()
    {
        if (shuttingDown)
        {
            return;
        }

        shuttingDown = true;

        if (!completionCommitted && State != BossEncounterState.Dormant
            && State != BossEncounterState.Interrupted)
        {
            State = BossEncounterState.Interrupted;
            for (int i = 0; participants != null && i < participants.Length; i++)
            {
                participants[i]?.Interrupt();
            }

            SetBarriersOpen(true, true);
        }

        ReleaseControlLock();
        SetCameraLockActive(false);
        BossHudEventService.RequestHide(this);
        UnsubscribeHeroDeath();
        UnsubscribeParticipantEvents();
    }
}
