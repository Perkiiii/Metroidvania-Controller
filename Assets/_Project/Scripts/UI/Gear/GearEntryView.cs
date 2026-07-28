using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One acquired Gear possession in the collection list. Pure presentation: it renders approved
/// display metadata and reports selection/activation. It never reads or writes ownership.
/// </summary>
[DisallowMultipleComponent]
public sealed class GearEntryView : MonoBehaviour, ISelectHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image icon;
    [SerializeField] private Text nameLabel;
    [SerializeField] private Text categoryLabel;

    /// <summary>Raised with this entry's stable key when the entry is selected or activated.</summary>
    public event Action<string> Activated;

    public Button Button => button;

    public Selectable Selectable => button;

    /// <summary>Stable key of the bound definition. Used for selection restoration across rebuilds.</summary>
    public string StableKey { get; private set; }

    private void Awake()
    {
        if (button != null)
        {
            button.onClick.AddListener(HandleActivated);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleActivated);
        }
    }

    public void Bind(GearDisplayDefinition definition)
    {
        if (definition == null)
        {
            StableKey = null;
            return;
        }

        StableKey = definition.StableKey;

        if (nameLabel != null) nameLabel.text = definition.DisplayName;
        if (categoryLabel != null) categoryLabel.text = definition.Category;

        if (icon != null)
        {
            icon.sprite = definition.Icon;
            icon.enabled = definition.Icon != null;
        }
    }

    /// <summary>
    /// Keyboard/controller focus landing on this entry shows its details, matching a mouse click.
    /// </summary>
    public void OnSelect(BaseEventData eventData)
    {
        HandleActivated();
    }

    /// <summary>Programmatic/test seam equivalent to focus landing on this entry.</summary>
    public void NotifySelected()
    {
        HandleActivated();
    }

    private void HandleActivated()
    {
        Activated?.Invoke(StableKey);
    }
}
