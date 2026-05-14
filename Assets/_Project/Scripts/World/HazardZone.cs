using UnityEngine;

// Place on a trigger Collider2D at the bottom of pits or kill zones.
// Calls TriggerHazardDeath which bypasses health and i-frames entirely.
[RequireComponent(typeof(Collider2D))]
public sealed class HazardZone : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        HeroBox heroBox = other.GetComponent<HeroBox>();
        if (heroBox == null)
        {
            return;
        }

        heroBox.TriggerHazardDeath();
    }
}
