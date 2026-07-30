using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BossHealthDisplay : MonoBehaviour
{
    [Tooltip("Production presentation path. Owns masked fills and local unscaled animation only.")]
    [SerializeField] private BossHealthBarView presentation;

    [Header("Migration fallback")]
    [SerializeField] private CanvasGroup visibilityGroup;
    [SerializeField] private Image fillImage;
    [SerializeField] private TMP_Text nameLabel;
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

        string displayName = request.Definition != null ? request.Definition.DisplayName : string.Empty;
        Sprite icon = request.Definition != null ? request.Definition.DisplayIcon : null;

        if (nameLabel != null)
        {
            nameLabel.text = displayName;
        }

        if (iconImage != null)
        {
            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }

        RefreshAggregate(initialSnapshot: true);
        if (presentation != null)
        {
            presentation.ShowSnapshot(displayName, icon, FillAmount01);
        }
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
        RefreshAggregate(initialSnapshot: false);
    }

    private void RefreshAggregate(bool initialSnapshot)
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

        if (!initialSnapshot)
        {
            if (presentation != null)
            {
                presentation.SetHealth(FillAmount01);
            }
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

        if (presentation != null)
        {
            presentation.HideImmediate();
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
