using UnityEngine;

// Place on a trigger Collider2D at the bottom of pits or kill zones.
[RequireComponent(typeof(Collider2D))]
public sealed class HazardZone : MonoBehaviour
{
    [Header("Hazard Behaviour")]
    [Tooltip("InstantDeath: bypasses health, triggers full death animation and checkpoint respawn.\nRecoverLocal: subtracts hazard damage, preserves reduced health, and repositions the hero at the assigned HazardRespawnMarker.")]
    [SerializeField] private HazardRecoveryMode recoveryMode = HazardRecoveryMode.InstantDeath;
    [SerializeField, Min(1)] private int hazardDamage = 1;
    [Tooltip("Required for RecoverLocal. Place near this hazard as the local return point.\nIf unassigned, falls back to the nearest RespawnMarker or the cached scene entry position.")]
    [SerializeField] private HazardRespawnMarker fallbackRespawnMarker;
    [Tooltip("RecoverLocal timing and protection tuning. If unassigned, GameManager uses built-in fallback values (0.18 s impact delay, 0.75 s i-frames).")]
    [SerializeField] private HazardRecoveryProfile recoveryProfile;

    private void OnTriggerEnter2D(Collider2D other)
    {
        HeroBox heroBox = other.GetComponent<HeroBox>();
        if (heroBox == null)
            return;

        heroBox.HandleHazard(new HazardContact(hazardDamage, recoveryMode, fallbackRespawnMarker, recoveryProfile, this));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (recoveryMode != HazardRecoveryMode.RecoverLocal)
            return;

        if (fallbackRespawnMarker == null)
            Debug.LogWarning($"[HazardZone] '{name}' is RecoverLocal but has no HazardRespawnMarker assigned. " +
                "Assign fallbackRespawnMarker or the hero will fall back to the nearest RespawnMarker.", this);

        if (recoveryProfile == null)
            Debug.LogWarning($"[HazardZone] '{name}' is RecoverLocal but has no HazardRecoveryProfile assigned. " +
                "Default recovery timings will be used (0.18 s impact delay, 0.75 s i-frames).", this);
    }
#endif
}
