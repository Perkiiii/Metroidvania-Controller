using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyStateBlackboard : MonoBehaviour
{
    public bool alerted;
    public bool hurt;
    public bool recoiling;
    public bool attacking;
    public bool attackWindowActive;
    public bool dead;
}
