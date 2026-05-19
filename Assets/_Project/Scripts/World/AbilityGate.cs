using UnityEngine;

public sealed class AbilityGate : MonoBehaviour
{
    [SerializeField] private PlayerAbilityState abilityState;
    [SerializeField] private AbilityId requiredAbility;
    [SerializeField] private GameObject blocker;
    [SerializeField] private Collider2D blockerCollider;

    private void Awake() => Refresh();

    private void OnEnable()
    {
        Refresh();
        if (abilityState != null)
            abilityState.AbilityChanged += OnAbilityChanged;
    }

    private void OnDisable()
    {
        if (abilityState != null)
            abilityState.AbilityChanged -= OnAbilityChanged;
    }

    public void Refresh()
    {
        if (abilityState == null)
            return;

        bool unlocked = abilityState.IsUnlocked(requiredAbility);
        if (blocker != null) blocker.SetActive(!unlocked);
        if (blockerCollider != null) blockerCollider.enabled = !unlocked;
    }

    private void OnAbilityChanged(AbilityId ability, bool unlocked)
    {
        if (ability == requiredAbility)
            Refresh();
    }
}
