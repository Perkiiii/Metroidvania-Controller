using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class EnemyController : MonoBehaviour
{
    [SerializeField] private EnemyConfig config;

    private EnemyStateBlackboard blackboard;
    private EnemyHealthComponent health;
    private EnemyRecoil recoil;
    private Rigidbody2D body;

    // EnemyMotor, EnemyPerception, and EnemyBehaviour will be wired here in Milestone 2.

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
        recoil.Initialize(config, blackboard, body);
        health.Initialize(config, blackboard, body, recoil);

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IEnemyBehaviour behaviour)
            {
                behaviour.Initialize(blackboard, body);
            }
        }
    }

    private T GetOrAdd<T>() where T : Component
    {
        return TryGetComponent(out T component) ? component : gameObject.AddComponent<T>();
    }
}
