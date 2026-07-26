using UnityEngine;
using UnityEngine.Playables;

// Scene-side camera presentation adapter for one boss encounter.
//
// It is deliberately the only place that knows both "this encounter" and "the camera": the
// encounter controller and the concrete boss actor stay camera-unaware, and the camera system
// stays boss-unaware. This component only reads Transforms and the existing neutral lifecycle
// events, then acquires generic camera presentation requests.
//
// It never commits persistence, never touches reward ownership, never changes barriers, control
// locks, HUD, or the arena CameraLockArea (which remains authoritative underneath every request).
[DisallowMultipleComponent]
public sealed class BossEncounterCameraPresenter : MonoBehaviour
{
    [Header("Encounter")]
    [SerializeField] private BossEncounterController encounter;

    [Tooltip("Optional component implementing IBossPresentationPhaseSource, used for a brief phase-change focus.")]
    [SerializeField] private MonoBehaviour phaseSource;

    [Header("Focus Targets")]
    [Tooltip("Transform framed by the intro, phase, and defeat presentations. Usually the boss actor root.")]
    [SerializeField] private Transform bossFocus;

    [Tooltip("Optional explicit reward focus. Falls back to the encounter's reward root when empty.")]
    [SerializeField] private Transform rewardFocus;

    [SerializeField] private bool requireBossFocus = true;
    [SerializeField] private bool requireRewardFocus;

    [Header("Intro")]
    [Tooltip("Optional Timeline director carrying a CameraPresentationTrack. When assigned, Timeline owns the intro request instead of this component.")]
    [SerializeField] private PlayableDirector introDirector;
    [SerializeField] private int introPriority = 30;
    [SerializeField] private CameraPresentationSettings introSettings =
        CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);

    [Header("Combat Framing")]
    [SerializeField] private bool enableCombatFraming = true;
    [SerializeField] private int combatPriority = 10;
    [SerializeField] private CameraPresentationSettings combatSettings =
        CameraPresentationSettings.Default(CameraPresentationMode.FrameTargets);

    [Header("Phase Change")]
    [SerializeField] private bool enablePhaseFocus = true;
    [SerializeField] private int phasePriority = 20;
    [SerializeField] private float phaseFocusDuration = 1.25f;
    [SerializeField] private CameraPresentationSettings phaseSettings =
        CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);

    [Header("Defeat")]
    [SerializeField] private bool enableDefeatFocus = true;
    [SerializeField] private int defeatPriority = 40;
    [SerializeField] private CameraPresentationSettings defeatSettings =
        CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);

    [Header("Reward Reveal")]
    [SerializeField] private bool enableRewardReveal = true;
    [SerializeField] private int rewardPriority = 40;
    [SerializeField] private float rewardRevealDuration = 2.5f;
    [SerializeField] private CameraPresentationSettings rewardSettings =
        CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);

    private CameraPresentationHandle introHandle;
    private CameraPresentationHandle combatHandle;
    private CameraPresentationHandle phaseHandle;
    private CameraPresentationHandle defeatHandle;
    private CameraPresentationHandle rewardHandle;

    private IBossPresentationPhaseSource resolvedPhaseSource;
    private Transform heroTransform;
    private bool encounterSubscribed;
    private bool phaseSubscribed;

    public BossEncounterController Encounter => encounter;
    public Transform BossFocus => bossFocus;
    public Transform RewardFocus => rewardFocus;
    public bool RequiresBossFocus => requireBossFocus;
    public bool RequiresRewardFocus => requireRewardFocus;
    public PlayableDirector IntroDirector => introDirector;
    public MonoBehaviour PhaseSource => phaseSource;

    // Read-only diagnostics for tests and the Inspector.
    public bool HasIntroPresentation => introHandle.IsRegistered;
    public bool HasCombatPresentation => combatHandle.IsRegistered;
    public bool HasPhasePresentation => phaseHandle.IsRegistered;
    public bool HasDefeatPresentation => defeatHandle.IsRegistered;
    public bool HasRewardPresentation => rewardHandle.IsRegistered;

    private void Awake()
    {
        ResolvePhaseSource();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ReleaseAll();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        ReleaseAll();
    }

    private void Subscribe()
    {
        if (!encounterSubscribed && encounter != null)
        {
            encounter.EncounterStarting += HandleEncounterStarting;
            encounter.EncounterActivated += HandleEncounterActivated;
            encounter.BossesDefeated += HandleBossesDefeated;
            encounter.EncounterCompleted += HandleEncounterCompleted;
            encounter.EncounterInterrupted += HandleEncounterInterrupted;
            encounterSubscribed = true;
        }

        if (!phaseSubscribed && ResolvePhaseSource() != null)
        {
            resolvedPhaseSource.PresentationPhaseChanged += HandlePresentationPhaseChanged;
            phaseSubscribed = true;
        }
    }

    private void Unsubscribe()
    {
        if (encounterSubscribed && encounter != null)
        {
            encounter.EncounterStarting -= HandleEncounterStarting;
            encounter.EncounterActivated -= HandleEncounterActivated;
            encounter.BossesDefeated -= HandleBossesDefeated;
            encounter.EncounterCompleted -= HandleEncounterCompleted;
            encounter.EncounterInterrupted -= HandleEncounterInterrupted;
        }

        encounterSubscribed = false;

        if (phaseSubscribed && resolvedPhaseSource != null)
        {
            resolvedPhaseSource.PresentationPhaseChanged -= HandlePresentationPhaseChanged;
        }

        phaseSubscribed = false;
    }

    private IBossPresentationPhaseSource ResolvePhaseSource()
    {
        if (resolvedPhaseSource == null && phaseSource != null)
        {
            resolvedPhaseSource = phaseSource as IBossPresentationPhaseSource;
        }

        return resolvedPhaseSource;
    }

    private void HandleEncounterStarting()
    {
        // Camera systems only ever read the hero Transform; nothing here touches HeroController.
        GameObject hero = GameObject.FindWithTag("Player");
        heroTransform = hero != null ? hero.transform : null;

        if (introDirector != null)
        {
            // Timeline owns its own request through CameraPresentationReceiver. Acquiring a second
            // intro request here would double-own the same beat.
            introDirector.Play();
            return;
        }

        if (bossFocus == null)
        {
            return;
        }

        introHandle = Acquire(introSettings, new[] { bossFocus }, introPriority);
    }

    private void HandleEncounterActivated()
    {
        StopIntroDirector();
        Release(ref introHandle);

        if (!enableCombatFraming || bossFocus == null)
        {
            return;
        }

        Transform[] targets = heroTransform != null
            ? new[] { heroTransform, bossFocus }
            : new[] { bossFocus };

        combatHandle = Acquire(combatSettings, targets, combatPriority);
    }

    private void HandlePresentationPhaseChanged(int phase)
    {
        if (!enablePhaseFocus || bossFocus == null || !combatHandle.IsRegistered)
        {
            return;
        }

        // A short, self-expiring bias. When it ends, selection falls back to the still-registered
        // combat framing request; nothing has to remember to release it.
        Release(ref phaseHandle);
        phaseHandle = Acquire(
            phaseSettings,
            new[] { bossFocus },
            phasePriority,
            Mathf.Max(0f, phaseFocusDuration));
    }

    private void HandleBossesDefeated()
    {
        Release(ref phaseHandle);

        if (!enableDefeatFocus || bossFocus == null)
        {
            return;
        }

        defeatHandle = Acquire(defeatSettings, new[] { bossFocus }, defeatPriority);
        Release(ref combatHandle);
    }

    private void HandleEncounterCompleted()
    {
        Release(ref introHandle);
        Release(ref combatHandle);
        Release(ref phaseHandle);
        Release(ref defeatHandle);
        StopIntroDirector();

        Transform reward = ResolveRewardFocus();
        if (!enableRewardReveal || reward == null)
        {
            return;
        }

        rewardHandle = Acquire(
            rewardSettings,
            new[] { reward },
            rewardPriority,
            Mathf.Max(0f, rewardRevealDuration));
    }

    private void HandleEncounterInterrupted()
    {
        StopIntroDirector();
        ReleaseAll();
    }

    private Transform ResolveRewardFocus()
    {
        if (rewardFocus != null)
        {
            return rewardFocus;
        }

        GameObject rewardRoot = encounter != null ? encounter.RewardRoot : null;
        return rewardRoot != null ? rewardRoot.transform : null;
    }

    private CameraPresentationHandle Acquire(
        in CameraPresentationSettings settings,
        Transform[] targets,
        int priority,
        float duration = -1f)
    {
        return CameraEventService.AcquirePresentation(
            settings,
            targets,
            this,
            CameraRequestLifetime.Scene,
            priority,
            duration);
    }

    private static void Release(ref CameraPresentationHandle handle)
    {
        handle.Release();
        handle = default;
    }

    private void StopIntroDirector()
    {
        if (introDirector != null && introDirector.state == PlayState.Playing)
        {
            // Stopping the director tears down the graph, which releases the Timeline-owned
            // request through the mixer's OnGraphStop/OnPlayableDestroy path.
            introDirector.Stop();
        }
    }

    private void ReleaseAll()
    {
        Release(ref introHandle);
        Release(ref combatHandle);
        Release(ref phaseHandle);
        Release(ref defeatHandle);
        Release(ref rewardHandle);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (phaseSource != null && phaseSource as IBossPresentationPhaseSource == null)
        {
            Debug.LogWarning(
                $"[{nameof(BossEncounterCameraPresenter)}] '{name}' phaseSource '{phaseSource.GetType().Name}' does not implement {nameof(IBossPresentationPhaseSource)}.",
                this);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (bossFocus != null)
        {
            Gizmos.color = new Color(1f, 0.35f, 0.85f, 0.9f);
            Gizmos.DrawWireSphere(bossFocus.position, 0.5f);
            Gizmos.DrawLine(transform.position, bossFocus.position);
        }

        Transform reward = rewardFocus != null
            ? rewardFocus
            : encounter != null && encounter.RewardRoot != null
                ? encounter.RewardRoot.transform
                : null;
        if (reward != null)
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(reward.position, 0.5f);
            Gizmos.DrawLine(transform.position, reward.position);
        }
    }
#endif
}
