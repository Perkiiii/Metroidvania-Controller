using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossEncounterParticipant : MonoBehaviour
{
    public event Action<BossEncounterParticipant> Defeated;
    public event Action<BossEncounterParticipant> IntroCompleted;
    public event Action<BossEncounterParticipant> DefeatPresentationCompleted;

    [SerializeField] private GameObject actorRoot;
    [SerializeField] private EnemyHealthComponent health;
    [SerializeField] private MonoBehaviour behaviourSource;

    private IBossEncounterBehaviour behaviour;
    private bool subscribed;
    private bool defeated;
    private bool introComplete;
    private bool defeatPresentationComplete;
    private Renderer[] preparationRenderers;
    private bool[] preparationRendererStates;
    private Collider2D[] preparationColliders;
    private bool[] preparationColliderStates;
    private bool preparationSuppressed;

    public GameObject ActorRoot => actorRoot;
    public EnemyHealthComponent Health => health;
    public MonoBehaviour BehaviourSource => behaviourSource;
    public bool IsActorActive => actorRoot != null && actorRoot.activeSelf;
    public bool IsDefeated => defeated;
    public bool IsIntroComplete => introComplete;
    public bool IsDefeatPresentationComplete => defeatPresentationComplete;
    public bool HasValidBehaviour => ResolveBehaviour() != null;

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void ApplyDormantState()
    {
        defeated = false;
        introComplete = false;
        defeatPresentationComplete = false;
        RestorePreparedActorState();

        if (actorRoot != null)
        {
            actorRoot.SetActive(false);
        }
    }

    public bool ActivateAndPrepare()
    {
        IBossEncounterBehaviour resolvedBehaviour = ResolveBehaviour();
        if (actorRoot == null || health == null || resolvedBehaviour == null)
        {
            Debug.LogError($"[{nameof(BossEncounterParticipant)}] '{name}' is missing its actor root, health, or typed behaviour reference.", this);
            return false;
        }

        Subscribe();
        defeated = false;
        introComplete = false;
        defeatPresentationComplete = false;
        CaptureAndSuppressPreparedActorState();
        actorRoot.SetActive(true);

        if (!health.IsInitialized)
        {
            Debug.LogError($"[{nameof(BossEncounterParticipant)}] '{name}' activated actor '{actorRoot.name}', but its health component did not initialize.", this);
            RestorePreparedActorState();
            actorRoot.SetActive(false);
            return false;
        }

        bool prepared = resolvedBehaviour.TryPrepareForEncounter();
        ApplyPreparationSuppression();
        if (prepared)
        {
            return true;
        }

        return false;
    }

    public void PlayIntro()
    {
        RestorePreparedActorState();
        ResolveBehaviour()?.PlayIntro();
    }

    public void BeginCombat()
    {
        ResolveBehaviour()?.BeginCombat();
    }

    public void Interrupt()
    {
        ResolveBehaviour()?.InterruptEncounter();
        RestorePreparedActorState();
        if (actorRoot != null)
        {
            actorRoot.SetActive(false);
        }
    }

    public void NotifyEncounterCompleted()
    {
        ResolveBehaviour()?.NotifyEncounterCompleted();
    }

    private IBossEncounterBehaviour ResolveBehaviour()
    {
        if (behaviour == null && behaviourSource != null)
        {
            behaviour = behaviourSource as IBossEncounterBehaviour;
        }

        return behaviour;
    }

    private void CaptureAndSuppressPreparedActorState()
    {
        if (actorRoot == null)
        {
            return;
        }

        preparationRenderers = actorRoot.GetComponentsInChildren<Renderer>(true);
        preparationRendererStates = new bool[preparationRenderers.Length];
        for (int i = 0; i < preparationRenderers.Length; i++)
        {
            preparationRendererStates[i] = preparationRenderers[i] != null && preparationRenderers[i].enabled;
        }

        preparationColliders = actorRoot.GetComponentsInChildren<Collider2D>(true);
        preparationColliderStates = new bool[preparationColliders.Length];
        for (int i = 0; i < preparationColliders.Length; i++)
        {
            preparationColliderStates[i] = preparationColliders[i] != null && preparationColliders[i].enabled;
        }

        preparationSuppressed = true;
        ApplyPreparationSuppression();
    }

    private void ApplyPreparationSuppression()
    {
        if (!preparationSuppressed)
        {
            return;
        }

        for (int i = 0; preparationRenderers != null && i < preparationRenderers.Length; i++)
        {
            if (preparationRenderers[i] != null)
            {
                preparationRenderers[i].enabled = false;
            }
        }

        for (int i = 0; preparationColliders != null && i < preparationColliders.Length; i++)
        {
            if (preparationColliders[i] != null)
            {
                preparationColliders[i].enabled = false;
            }
        }
    }

    private void RestorePreparedActorState()
    {
        if (!preparationSuppressed)
        {
            return;
        }

        for (int i = 0; preparationRenderers != null && i < preparationRenderers.Length; i++)
        {
            if (preparationRenderers[i] != null)
            {
                preparationRenderers[i].enabled = preparationRendererStates[i];
            }
        }

        for (int i = 0; preparationColliders != null && i < preparationColliders.Length; i++)
        {
            if (preparationColliders[i] != null)
            {
                preparationColliders[i].enabled = preparationColliderStates[i];
            }
        }

        preparationRenderers = null;
        preparationRendererStates = null;
        preparationColliders = null;
        preparationColliderStates = null;
        preparationSuppressed = false;
    }

    private void Subscribe()
    {
        if (subscribed || health == null)
        {
            return;
        }

        IBossEncounterBehaviour resolvedBehaviour = ResolveBehaviour();
        if (resolvedBehaviour == null)
        {
            return;
        }

        health.OnDeath += HandleDefeated;
        resolvedBehaviour.IntroCompleted += HandleIntroCompleted;
        resolvedBehaviour.DefeatPresentationCompleted += HandleDefeatPresentationCompleted;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (health != null)
        {
            health.OnDeath -= HandleDefeated;
        }

        IBossEncounterBehaviour resolvedBehaviour = ResolveBehaviour();
        if (resolvedBehaviour != null)
        {
            resolvedBehaviour.IntroCompleted -= HandleIntroCompleted;
            resolvedBehaviour.DefeatPresentationCompleted -= HandleDefeatPresentationCompleted;
        }

        subscribed = false;
    }

    private void HandleDefeated()
    {
        if (defeated)
        {
            return;
        }

        defeated = true;
        Defeated?.Invoke(this);
    }

    private void HandleIntroCompleted()
    {
        if (introComplete)
        {
            return;
        }

        introComplete = true;
        IntroCompleted?.Invoke(this);
    }

    private void HandleDefeatPresentationCompleted()
    {
        if (defeatPresentationComplete)
        {
            return;
        }

        defeatPresentationComplete = true;
        DefeatPresentationCompleted?.Invoke(this);
    }
}
