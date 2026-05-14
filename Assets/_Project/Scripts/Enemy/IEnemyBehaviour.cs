using UnityEngine;

public interface IEnemyBehaviour
{
    void Initialize(EnemyStateBlackboard blackboard, Rigidbody2D body);
}
