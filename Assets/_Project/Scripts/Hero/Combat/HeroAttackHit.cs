using UnityEngine;

public readonly struct HeroAttackHit
{
    public readonly GameObject Source;
    public readonly HeroAttackDirection Direction;
    public readonly int Damage;
    public readonly Vector2 Point;
    public readonly Vector2 ForceDirection;

    public HeroAttackHit(
        GameObject source,
        HeroAttackDirection direction,
        int damage,
        Vector2 point,
        Vector2 forceDirection)
    {
        Source = source;
        Direction = direction;
        Damage = damage;
        Point = point;
        ForceDirection = forceDirection;
    }
}
