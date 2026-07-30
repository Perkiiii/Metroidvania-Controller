using System.Collections.Generic;
using UnityEngine;

/// <summary>Presentation-only view of the persistent player health state.</summary>
public sealed class HealthDisplay : MonoBehaviour
{
    [SerializeField] private PlayerHealthState healthState;
    [SerializeField] private Transform normalSlotContainer;
    [SerializeField] private Transform bonusSlotContainer;
    [SerializeField] private HealthSlotView slotPrefab;

    private readonly List<HealthSlotView> normalSlots = new List<HealthSlotView>();
    private readonly List<HealthSlotView> bonusSlots = new List<HealthSlotView>();
    private bool subscribed;
    private bool hasLastChange;
    private PlayerHealthChangeReason lastChangeReason;
    private int currentHealth;
    private int maximumHealth;
    private int bonusHealth;

    public int CurrentHealth => currentHealth;
    public int MaximumHealth => maximumHealth;
    public int BonusHealth => bonusHealth;
    public int NormalSlotCount => maximumHealth;
    public int FilledNormalSlotCount => currentHealth;
    public int BonusSlotCount => bonusHealth;
    public bool HasLastChangeReason => hasLastChange;
    public PlayerHealthChangeReason LastChangeReason => lastChangeReason;
    public int GameplayFeedbackCount { get; private set; }
    public bool HasValidDependencies => healthState != null;

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    public void Configure(PlayerHealthState state)
    {
        if (healthState != state)
        {
            Unsubscribe();
            healthState = state;
        }

        Subscribe();
        Refresh();
    }

    public void Refresh()
    {
        if (healthState == null)
            return;

        ApplySnapshot(healthState.CurrentHealth, healthState.MaximumHealth, healthState.BonusHealth,
            PlayerHealthChangeReason.StateApplied, false);
    }

    private void Subscribe()
    {
        if (!isActiveAndEnabled || subscribed || healthState == null)
            return;

        healthState.Changed += OnHealthChanged;
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed || healthState == null)
            return;

        healthState.Changed -= OnHealthChanged;
        subscribed = false;
    }

    private void OnHealthChanged(PlayerHealthChangeInfo info)
    {
        bool gameplayChange = info.Reason != PlayerHealthChangeReason.StateApplied
            && info.Reason != PlayerHealthChangeReason.Reset;
        ApplySnapshot(info.CurrentHealth, info.MaximumHealth, info.BonusHealth, info.Reason, gameplayChange);
    }

    private void ApplySnapshot(int nextCurrent, int nextMaximum, int nextBonus,
        PlayerHealthChangeReason reason, bool gameplayChange)
    {
        int previousCurrent = currentHealth;
        int previousBonus = bonusHealth;

        maximumHealth = Mathf.Max(0, nextMaximum);
        currentHealth = Mathf.Clamp(nextCurrent, 0, maximumHealth);
        bonusHealth = Mathf.Max(0, nextBonus);
        lastChangeReason = reason;
        hasLastChange = true;

        if (gameplayChange)
            GameplayFeedbackCount++;

        RebuildSlots(normalSlots, normalSlotContainer, maximumHealth, HealthSlotVisualState.Empty);
        for (int i = 0; i < normalSlots.Count; i++)
        {
            HealthSlotVisualState nextState = i < currentHealth
                ? HealthSlotVisualState.Filled
                : HealthSlotVisualState.Empty;
            HealthSlotFeedback feedback = HealthSlotFeedback.None;
            if (gameplayChange && (i < previousCurrent) != (i < currentHealth))
            {
                feedback = i < currentHealth ? HealthSlotFeedback.Heal : HealthSlotFeedback.Damage;
            }

            normalSlots[i].SetState(nextState, feedback);
        }

        RebuildSlots(bonusSlots, bonusSlotContainer, bonusHealth, HealthSlotVisualState.Bonus);
        for (int i = 0; i < bonusSlots.Count; i++)
        {
            HealthSlotFeedback feedback = gameplayChange && i >= previousBonus
                ? HealthSlotFeedback.Bonus
                : HealthSlotFeedback.None;
            bonusSlots[i].SetState(HealthSlotVisualState.Bonus, feedback);
        }
    }

    private void RebuildSlots(List<HealthSlotView> slots, Transform container, int required,
        HealthSlotVisualState initialState)
    {
        if (container == null || slotPrefab == null)
            return;

        while (slots.Count < required)
        {
            HealthSlotView slot = Instantiate(slotPrefab, container);
            slot.SetState(initialState);
            slots.Add(slot);
        }

        while (slots.Count > required)
        {
            int last = slots.Count - 1;
            HealthSlotView slot = slots[last];
            slots.RemoveAt(last);
            if (slot != null)
            {
                if (Application.isPlaying)
                    Destroy(slot.gameObject);
                else
                    DestroyImmediate(slot.gameObject);
            }
        }
    }
}
