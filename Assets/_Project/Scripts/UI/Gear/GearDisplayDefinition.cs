using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Display-only description of one physical Gear possession. This asset carries presentation
/// content and nothing else: no unlock/equip/purchase state, no tuning values, no cooldowns or
/// velocities, and no layout coordinates. Ownership is decided exclusively by
/// <see cref="PlayerAbilityState"/>.
///
/// Every Package A2 definition maps to exactly one <see cref="AbilityId"/>. <see cref="StableKey"/>
/// exists for selection restoration, validation, and tests — it is not a second ownership key.
///
/// Do not author a definition just because an enum member exists, and never use a raw AbilityId
/// name as a player-facing identity: each definition needs an approved Underbrew physical name,
/// artwork, and copy. See Docs/FeatureSpecs/Gear.md.
/// </summary>
[CreateAssetMenu(menuName = "Project/UI/Gear Display Definition", fileName = "GearDisplayDefinition")]
public sealed class GearDisplayDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable, unique key used for selection restoration and validation. Never shown to the player.")]
    [SerializeField] private string stableKey;

    [Tooltip("The single ability whose ownership makes this Gear appear.")]
    [SerializeField] private AbilityId requiredAbility = AbilityId.Dash;

    [Header("Approved player-facing content")]
    [SerializeField] private string displayName;

    [Tooltip("Short category/kind line, e.g. a physical grouping. Optional.")]
    [SerializeField] private string category;

    [SerializeField] private Sprite icon;

    [Tooltip("What this possession lets the player do, in player language.")]
    [TextArea(2, 5)]
    [SerializeField] private string functionalDescription;

    [TextArea(2, 5)]
    [SerializeField] private string flavourText;

    [Header("Control hint (optional)")]
    [Tooltip("Action used to resolve a live binding hint. Display only — never invoked.")]
    [SerializeField] private InputActionReference hintAction;

    [Tooltip("Hint template. '{0}' is replaced by the resolved binding text, e.g. 'Hold {0} to glide'.")]
    [SerializeField] private string hintTemplate;

    public string StableKey => stableKey;

    public AbilityId RequiredAbility => requiredAbility;

    public string DisplayName => displayName;

    public string Category => category;

    public Sprite Icon => icon;

    public string FunctionalDescription => functionalDescription;

    public string FlavourText => flavourText;

    public InputActionReference HintAction => hintAction;

    public string HintTemplate => hintTemplate;

    /// <summary>
    /// Composition seam for runtime-created fixture definitions in the UI Sandbox and EditMode
    /// tests. Never called by production code, and fixture instances created this way are never
    /// saved as project assets.
    /// </summary>
    public void SetFixtureContent(
        string key,
        AbilityId ability,
        string name,
        string categoryText = null,
        string description = null,
        string flavour = null)
    {
        stableKey = key;
        requiredAbility = ability;
        displayName = name;
        category = categoryText;
        functionalDescription = description;
        flavourText = flavour;
    }

    /// <summary>
    /// Resolves the authored control hint against the current bindings, or null when this
    /// definition has no hint. Uses Input System binding display strings only — Package A2 adds no
    /// glyph database, platform icon set, or localization layer.
    /// </summary>
    public string ResolveControlHint()
    {
        if (string.IsNullOrWhiteSpace(hintTemplate))
        {
            return null;
        }

        string binding = hintAction != null && hintAction.action != null
            ? hintAction.action.GetBindingDisplayString()
            : string.Empty;

        return string.Format(hintTemplate, binding);
    }
}
