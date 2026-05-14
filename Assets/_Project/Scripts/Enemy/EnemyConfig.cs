using UnityEngine;

[CreateAssetMenu(menuName = "Enemy/Enemy Config", fileName = "EnemyConfig")]
public sealed class EnemyConfig : ScriptableObject
{
    [Header("Health")]
    public int maxHealth = 3;

    [Header("Hurt")]
    public float knockbackForce = 6f;
    public float knockbackLift = 3f;
    public float stunDuration = 0.4f;
    public float hitStopDuration = 0.06f;
    [Tooltip("Freeze in place on hit instead of flying back. Use for wall/ceiling enemies that should not leave their surface.")]
    public bool freezeOnHit = false;
    [Tooltip("Ignore upward recoil when the enemy is hit from below.")]
    public bool preventUpwardRecoil = false;
    [Tooltip("Clear horizontal velocity for upward recoil so the enemy pops up without sliding sideways.")]
    public bool stopHorizontalVelocityOnUpwardRecoil = true;

    [Header("Death")]
    public EnemyDeathType deathType = EnemyDeathType.Standard;
    public float deathDestroyDelay = 0.5f;

    [Header("Audio")]
    public AudioClip hurtSfx;
    public AudioClip deathSfx;
}
