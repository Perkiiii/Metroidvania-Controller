using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the approved display metadata of the selected Gear possession. Presentation only: it
/// never shows raw <see cref="AbilityId"/> names, tuning values, or internal keys, and it never
/// reads ownership.
/// </summary>
[DisallowMultipleComponent]
public sealed class GearDetailsPanel : MonoBehaviour
{
    [Tooltip("Content shown only while a Gear entry is selected.")]
    [SerializeField] private GameObject contentRoot;

    [SerializeField] private Image artwork;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text categoryLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text controlHintLabel;
    [SerializeField] private TMP_Text flavourLabel;

    public void Show(GearDisplayDefinition definition)
    {
        if (definition == null)
        {
            Clear();
            return;
        }

        if (contentRoot != null) contentRoot.SetActive(true);

        if (nameLabel != null) nameLabel.text = definition.DisplayName;
        if (categoryLabel != null) categoryLabel.text = definition.Category;
        if (descriptionLabel != null) descriptionLabel.text = definition.FunctionalDescription;
        if (flavourLabel != null) flavourLabel.text = definition.FlavourText;

        if (artwork != null)
        {
            artwork.sprite = definition.Icon;
            artwork.enabled = definition.Icon != null;
        }

        if (controlHintLabel != null)
        {
            string hint = definition.ResolveControlHint();
            controlHintLabel.text = hint ?? string.Empty;
            controlHintLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(hint));
        }
    }

    /// <summary>Clears the panel — used for the zero-entry empty state and on Hide.</summary>
    public void Clear()
    {
        if (nameLabel != null) nameLabel.text = string.Empty;
        if (categoryLabel != null) categoryLabel.text = string.Empty;
        if (descriptionLabel != null) descriptionLabel.text = string.Empty;
        if (flavourLabel != null) flavourLabel.text = string.Empty;

        if (controlHintLabel != null)
        {
            controlHintLabel.text = string.Empty;
            controlHintLabel.gameObject.SetActive(false);
        }

        if (artwork != null)
        {
            artwork.sprite = null;
            artwork.enabled = false;
        }

        if (contentRoot != null) contentRoot.SetActive(false);
    }
}
