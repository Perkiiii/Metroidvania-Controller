using UnityEngine;

// The enemy persistence participant. EnemyController owns the one-time initialization sequence;
// this component only resolves suppression and records confirmed death against WorldStateRegistry.
// It never queries the registry outside that one-time path and never reactivates an enemy.
[DisallowMultipleComponent]
public sealed class EnemyPersistence : MonoBehaviour
{
    [Tooltip("Stable identity for this placed enemy instance. Must be unique per scene and must " +
        "not be reused when duplicating a prefab instance in the Editor.")]
    [SerializeField] private string worldObjectId;

    [SerializeField] private EnemyPersistenceMode mode = EnemyPersistenceMode.RoomRuntime;

    [SerializeField] private WorldStateRegistry registry;

    private EnemyConfig config;
    private bool initialized;

    public string WorldObjectId => worldObjectId;
    public EnemyPersistenceMode Mode => mode;

    // Called exactly once by EnemyController, before AI/combat/physics wiring is resolved.
    public void Initialize(EnemyConfig enemyConfig)
    {
        config = enemyConfig;
        initialized = true;

        if (registry == null)
        {
            Debug.LogError($"[EnemyPersistence] '{name}' has no WorldStateRegistry assigned. Treating as active (no suppression possible).", this);
            return;
        }

        if (string.IsNullOrEmpty(worldObjectId))
        {
            Debug.LogError($"[EnemyPersistence] '{name}' has no worldObjectId assigned. Treating as active (no suppression possible).", this);
            return;
        }

        if (mode == EnemyPersistenceMode.RespawnableTimed && (config == null || config.respawnDuration <= 0f))
        {
            Debug.LogError($"[EnemyPersistence] '{name}' is RespawnableTimed but its EnemyConfig.respawnDuration is not greater than 0. Treating as active for this session.", this);
        }
    }

    // Called exactly once by EnemyController's one-time initialization path.
    public bool ShouldSuppressOnInitialization()
    {
        if (!initialized || registry == null || string.IsNullOrEmpty(worldObjectId))
            return false;

        switch (mode)
        {
            case EnemyPersistenceMode.RoomRuntime:
                return false;

            case EnemyPersistenceMode.RespawnableTimed:
                if (config == null || config.respawnDuration <= 0f) return false;
                return registry.ShouldSuppressEnemyOnInitialization(worldObjectId);

            case EnemyPersistenceMode.PermanentEncounter:
                return registry.IsEncounterDefeated(worldObjectId);

            default:
                return false;
        }
    }

    // Subscribed by EnemyController to EnemyHealthComponent.OnDeath, only on the active
    // (non-suppressed) initialization path. Affects only a future scene initialization.
    public void RecordDeath()
    {
        if (registry == null || string.IsNullOrEmpty(worldObjectId)) return;

        switch (mode)
        {
            case EnemyPersistenceMode.RoomRuntime:
                break;

            case EnemyPersistenceMode.RespawnableTimed:
                registry.RecordRespawnableEnemyDeath(worldObjectId, config != null ? config.respawnDuration : 0f);
                break;

            case EnemyPersistenceMode.PermanentEncounter:
                registry.MarkEncounterDefeated(worldObjectId);
                break;
        }
    }
}
