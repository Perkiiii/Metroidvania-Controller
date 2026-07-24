using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class BossEncounterTrigger : MonoBehaviour
{
    [SerializeField] private BossEncounterController encounter;

    private Collider2D triggerCollider;

    public BossEncounterController Encounter => encounter;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
    }

    public void SetAvailable(bool available)
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = available;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || encounter == null)
        {
            return;
        }

        if (encounter.TryBeginEncounter())
        {
            SetAvailable(false);
        }
    }
}
