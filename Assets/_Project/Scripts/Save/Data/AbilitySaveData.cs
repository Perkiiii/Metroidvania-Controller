using System;

/// <summary>
/// Permanent ability ownership as persisted in a save slot.
///
/// Every field defaults to locked: a true new game owns no permanent ability, and Dash/Wall Cling
/// are progression unlocks rather than starting Gear (see Docs/FeatureSpecs/Abilities.md and
/// Docs/FeatureSpecs/Gear.md). These initializers are therefore the authoritative *fresh-save*
/// contract and are also what a save whose ability section is missing or null falls back to after
/// <see cref="SaveDataMigrator"/> constructs one.
///
/// Existing saves are unaffected: <see cref="PlayerAbilityState.GatherSaveData"/> writes all eight
/// booleans explicitly, so deserialization overwrites every initializer with the stored value.
/// </summary>
[Serializable]
public class AbilitySaveData
{
    public bool dashUnlocked       = false;
    public bool wallClingUnlocked  = false;
    public bool sprintUnlocked     = false;
    public bool wallLatchUnlocked  = false;
    public bool doubleJumpUnlocked = false;
    public bool driftCloakUnlocked = false;
    public bool spiritCastUnlocked = false;
    public bool bindUnlocked       = false;
}
