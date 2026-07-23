using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyController : MonoBehaviour
{
    [SerializeField] private EnemyConfig config;

    public EnemyConfig Config => config;

    private EnemyStateBlackboard blackboard;
    private EnemyHealthComponent health;
    private EnemyRecoil recoil;
    private Rigidbody2D body;

    private void Awake()
    {
        if (config == null)
        {
            Debug.LogError($"EnemyController on '{name}' has no EnemyConfig assigned.", this);
            return;
        }

        body = GetComponent<Rigidbody2D>();
        blackboard = GetOrAdd<EnemyStateBlackboard>();
        recoil = GetOrAdd<EnemyRecoil>();
        health = GetOrAdd<EnemyHealthComponent>();

        EnemyMotor motor = GetComponent<EnemyMotor>();
        EnemyPerception perception = GetComponent<EnemyPerception>();
        EnemyAttackController attackController = GetComponent<EnemyAttackController>();
        EnemyContactDamage[] contactDamage = GetComponentsInChildren<EnemyContactDamage>(true);
        EnemyPersistence persistence = GetComponent<EnemyPersistence>();
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();

        bool suppressed = false;
        if (persistence != null)
        {
            persistence.Initialize(config);
            suppressed = persistence.ShouldSuppressOnInitialization();
        }

        if (suppressed)
        {
            SuppressOnInitialization(motor, perception, attackController, contactDamage, behaviours);
            return;
        }

        if (motor != null)
        {
            motor.Initialize(config, body);
        }

        if (perception != null)
        {
            perception.Initialize(config);
        }

        recoil.Initialize(config, blackboard, body);
        health.Initialize(config, blackboard, body, recoil);

        if (attackController != null)
        {
            attackController.Initialize(config, blackboard, motor);
        }

        if (persistence != null)
        {
            health.OnDeath += persistence.RecordDeath;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemyBehaviour behaviour)
            {
                behaviour.Initialize(blackboard, body);
            }
        }
    }

    // Resolved once, only from this one-time initialization path. Never repeated for this
    // instance — not from OnEnable, pool reuse, or a second initialization pass. AI, perception,
    // movement, combat behaviour, and feedback are never initialized for a suppressed instance.
    private void SuppressOnInitialization(
        EnemyMotor motor,
        EnemyPerception perception,
        EnemyAttackController attackController,
        EnemyContactDamage[] contactDamage,
        MonoBehaviour[] behaviours)
    {
        blackboard.dead = true;
        blackboard.suppressed = true;

        if (body != null)
        {
            body.simulated = false;
            body.linearVelocity = Vector2.zero;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].enabled = false;
        }

        if (motor != null) motor.enabled = false;
        if (perception != null) perception.enabled = false;
        if (attackController != null) attackController.enabled = false;

        for (int i = 0; i < contactDamage.Length; i++)
        {
            if (contactDamage[i] != null)
                contactDamage[i].enabled = false;
        }

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i] is IEnemyBehaviour)
                behaviours[i].enabled = false;
        }

        // health and recoil are left enabled but never Initialize()'d — every entry point on
        // both guards on a null config/blackboard, so this is a safe no-op. EnemyController and
        // EnemyPersistence remain enabled for diagnostics, per the plan's first-pass constraint
        // against gameObject.SetActive(false) here.
    }

    private T GetOrAdd<T>() where T : Component
    {
        return TryGetComponent(out T component) ? component : gameObject.AddComponent<T>();
    }
}
