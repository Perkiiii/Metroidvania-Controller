using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BossHealthDisplay : MonoBehaviour
{
    [SerializeField] private CanvasGroup visibilityGroup;
    [SerializeField] private Image fillImage;
    [SerializeField] private Text nameLabel;
    [SerializeField] private Image iconImage;

    private readonly List<EnemyHealthComponent> healthRoster = new List<EnemyHealthComponent>();
    private object activeSource;
    private bool serviceSubscribed;

    public int CurrentHealth { get; private set; }
    public int MaximumHealth { get; private set; }
    public float FillAmount01 { get; private set; }
    public bool IsVisible => activeSource != null;
    public object ActiveSource => activeSource;
    public int BoundHealthSourceCount => healthRoster.Count;

    private void OnEnable()
    {
        SubscribeService();
        ApplyVisibility(false);
    }

    private void OnDisable()
    {
        UnsubscribeService();
        ClearBinding();
    }

    private void OnDestroy()
    {
        UnsubscribeService();
        ClearBinding();
    }

    private void SubscribeService()
    {
        if (serviceSubscribed)
        {
            return;
        }

        BossHudEventService.ShowRequested += HandleShowRequested;
        BossHudEventService.HideRequested += HandleHideRequested;
        serviceSubscribed = true;
    }

    private void UnsubscribeService()
    {
        if (!serviceSubscribed)
        {
            return;
        }

        BossHudEventService.ShowRequested -= HandleShowRequested;
        BossHudEventService.HideRequested -= HandleHideRequested;
        serviceSubscribed = false;
    }

    private void HandleShowRequested(BossHudShowRequest request)
    {
        if (request.Source == null)
        {
            return;
        }

        ClearBinding();
        activeSource = request.Source;

        HashSet<EnemyHealthComponent> unique = new HashSet<EnemyHealthComponent>();
        IReadOnlyList<EnemyHealthComponent> requestedRoster = request.HealthRoster;
        for (int i = 0; requestedRoster != null && i < requestedRoster.Count; i++)
        {
            EnemyHealthComponent health = requestedRoster[i];
            if (health == null || !health.IsInitialized || !unique.Add(health))
            {
                continue;
            }

            health.OnHealthChanged += HandleHealthChanged;
            healthRoster.Add(health);
        }

        if (healthRoster.Count == 0)
        {
            ClearBinding();
            return;
        }

        if (nameLabel != null)
        {
            nameLabel.text = request.Definition != null ? request.Definition.DisplayName : "";
        }

        if (iconImage != null)
        {
            Sprite icon = request.Definition != null ? request.Definition.DisplayIcon : null;
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        RefreshAggregate();
        ApplyVisibility(true);
    }

    private void HandleHideRequested(object source)
    {
        if (activeSource == null || !ReferenceEquals(activeSource, source))
        {
            return;
        }

        ClearBinding();
    }

    private void HandleHealthChanged(int current, int maximum)
    {
        RefreshAggregate();
    }

    private void RefreshAggregate()
    {
        int current = 0;
        int maximum = 0;
        for (int i = 0; i < healthRoster.Count; i++)
        {
            EnemyHealthComponent health = healthRoster[i];
            if (health == null)
            {
                continue;
            }

            current += Mathf.Max(0, health.CurrentHealth);
            maximum += Mathf.Max(0, health.MaximumHealth);
        }

        CurrentHealth = current;
        MaximumHealth = maximum;
        FillAmount01 = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0f;
        if (fillImage != null)
        {
            fillImage.fillAmount = FillAmount01;
        }
    }

    private void ClearBinding()
    {
        for (int i = 0; i < healthRoster.Count; i++)
        {
            if (healthRoster[i] != null)
            {
                healthRoster[i].OnHealthChanged -= HandleHealthChanged;
            }
        }

        healthRoster.Clear();
        activeSource = null;
        CurrentHealth = 0;
        MaximumHealth = 0;
        FillAmount01 = 0f;
        if (fillImage != null)
        {
            fillImage.fillAmount = 0f;
        }

        ApplyVisibility(false);
    }

    private void ApplyVisibility(bool visible)
    {
        if (visibilityGroup == null)
        {
            return;
        }

        visibilityGroup.alpha = visible ? 1f : 0f;
        visibilityGroup.interactable = false;
        visibilityGroup.blocksRaycasts = false;
    }
}
