using UnityEngine;

// Place on a trigger Collider2D at the bottom of pits or kill zones.
[RequireComponent(typeof(Collider2D))]
public sealed class HazardZone : MonoBehaviour
{
    [SerializeField] private HazardRecoveryMode recoveryMode = HazardRecoveryMode.InstantDeath;
    [SerializeField, Min(1)] private int hazardDamage = 1;
    [SerializeField] private HazardRespawnMarker fallbackRespawnMarker;

    private void OnTriggerEnter2D(Collider2D other)
    {
        HeroBox heroBox = other.GetComponent<HeroBox>();
        if (heroBox == null)
        {
            return;
        }

        heroBox.HandleHazard(new HazardContact(hazardDamage, recoveryMode, fallbackRespawnMarker, this));
    }
}
