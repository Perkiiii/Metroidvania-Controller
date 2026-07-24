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
        actorRoot.SetActive(true);

        if (!health.IsInitialized)
        {
            Debug.LogError($"[{nameof(BossEncounterParticipant)}] '{name}' activated actor '{actorRoot.name}', but its health component did not initialize.", this);
            actorRoot.SetActive(false);
            return false;
        }

        resolvedBehaviour.PrepareForEncounter();
        return true;
    }

    public void PlayIntro()
    {
        ResolveBehaviour()?.PlayIntro();
    }

    public void BeginCombat()
    {
        ResolveBehaviour()?.BeginCombat();
    }

    public void Interrupt()
    {
        ResolveBehaviour()?.InterruptEncounter();
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
