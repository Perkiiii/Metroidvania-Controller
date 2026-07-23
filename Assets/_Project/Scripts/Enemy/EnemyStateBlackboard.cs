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

    // Set once, only by EnemyController's one-time initialization path, when the enemy persistence
    // participant reports it should not be active for this scene visit (RespawnableTimed with a
    // valid death record, or PermanentEncounter already defeated). Distinct from dead: this enemy
    // never lived this session at all, rather than having died during it.
    public bool suppressed;
}
