using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered production catalogue of approved <see cref="GearDisplayDefinition"/> assets. List order
/// is the deterministic presentation order.
///
/// An empty catalogue is a valid production state: until physical Gear identities, names, artwork,
/// and copy are approved, Gear ships as a functional screen with an authored empty state rather
/// than definitions invented from AbilityId names.
/// </summary>
[CreateAssetMenu(menuName = "Project/UI/Gear Display Catalog", fileName = "GearDisplayCatalog")]
public sealed class GearDisplayCatalog : ScriptableObject
{
    [Tooltip("Approved Gear definitions in presentation order. Empty is valid.")]
    [SerializeField] private List<GearDisplayDefinition> definitions = new List<GearDisplayDefinition>();

    public IReadOnlyList<GearDisplayDefinition> Definitions => definitions;

    public int Count => definitions.Count;

    public GearDisplayDefinition FindByStableKey(string stableKey)
    {
        if (string.IsNullOrEmpty(stableKey))
        {
            return null;
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null && definitions[i].StableKey == stableKey)
            {
                return definitions[i];
            }
        }

        return null;
    }

    public GearDisplayDefinition FindByAbility(AbilityId ability)
    {
        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null && definitions[i].RequiredAbility == ability)
            {
                return definitions[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Runtime/test composition seam. Production authors the list in the Inspector; the UI Sandbox
    /// uses this to build a clearly isolated fixture catalogue that never becomes a project asset.
    /// </summary>
    public void SetDefinitions(IEnumerable<GearDisplayDefinition> source)
    {
        definitions.Clear();
        if (source != null)
        {
            definitions.AddRange(source);
        }
    }

    /// <summary>
    /// Collects authoring problems: null entries, missing/duplicate stable keys, and duplicate
    /// ability mappings. Used by both <c>UIFoundationValidator</c> and EditMode tests so there is
    /// exactly one owner for these rules.
    /// </summary>
    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
        {
            return;
        }

        HashSet<string> seenKeys = new HashSet<string>();
        HashSet<AbilityId> seenAbilities = new HashSet<AbilityId>();

        for (int i = 0; i < definitions.Count; i++)
        {
            GearDisplayDefinition definition = definitions[i];
            if (definition == null)
            {
                issues.Add($"Definition {i} is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(definition.StableKey))
            {
                issues.Add($"Definition {i} ('{definition.name}') has no stable key.");
            }
            else if (!seenKeys.Add(definition.StableKey))
            {
                issues.Add($"Duplicate stable key '{definition.StableKey}' at definition {i}.");
            }

            if (!seenAbilities.Add(definition.RequiredAbility))
            {
                issues.Add($"Duplicate ability mapping '{definition.RequiredAbility}' at definition {i}.");
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                issues.Add($"Definition {i} ('{definition.name}') has no player-facing display name.");
            }
        }
    }
}
