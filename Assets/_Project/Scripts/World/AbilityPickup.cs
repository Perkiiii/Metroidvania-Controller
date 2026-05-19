using UnityEngine;

public sealed class AbilityPickup : MonoBehaviour
{
    [SerializeField] private PlayerAbilityState abilityState;
    [SerializeField] private AbilityId ability;
    [SerializeField] private bool disableAfterPickup = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool isHero = other.GetComponentInParent<HeroBox>() != null
                   || other.GetComponentInParent<HeroController>() != null;
        if (!isHero)
            return;

        if (abilityState == null)
        {
            Debug.LogWarning($"{nameof(AbilityPickup)} on {name} has no PlayerAbilityState assigned.", this);
            return;
        }

        // TODO: notify save/world-state system before unlocking
        abilityState.Unlock(ability);

        if (disableAfterPickup)
            gameObject.SetActive(false);
    }
}
