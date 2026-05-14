using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyStateBlackboard : MonoBehaviour
{
    public bool hurt;
    public bool recoiling;
    public bool dead;
}
