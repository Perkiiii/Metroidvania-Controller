using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Project/World/Player Ability State", fileName = "PlayerAbilityState")]
public sealed class PlayerAbilityState : ScriptableObject, ISaveTarget
{
    [Header("Core Traversal")]
    public bool dashUnlocked = true;
    public bool wallClingUnlocked = true;

    [Header("Future Traversal")]
    public bool sprintUnlocked = false;
    public bool wallLatchUnlocked = false;
    public bool doubleJumpUnlocked = false;
    public bool driftCloakUnlocked = false;

    [Header("Combat / Utility")]
    public bool spiritCastUnlocked = false;

    public event Action<AbilityId, bool> AbilityChanged;

    public bool IsUnlocked(AbilityId ability)
    {
        return ability switch
        {
            AbilityId.Dash       => dashUnlocked,
            AbilityId.WallCling  => wallClingUnlocked,
            AbilityId.Sprint     => sprintUnlocked,
            AbilityId.WallLatch  => wallLatchUnlocked,
            AbilityId.DoubleJump => doubleJumpUnlocked,
            AbilityId.DriftCloak => driftCloakUnlocked,
            AbilityId.SpiritCast => spiritCastUnlocked,
            _                    => false
        };
    }

    public void Unlock(AbilityId ability) => SetUnlocked(ability, true);

    public void Lock(AbilityId ability) => SetUnlocked(ability, false);

    public void SetUnlocked(AbilityId ability, bool unlocked)
    {
        if (IsUnlocked(ability) == unlocked)
            return;

        switch (ability)
        {
            case AbilityId.Dash:       dashUnlocked       = unlocked; break;
            case AbilityId.WallCling:  wallClingUnlocked  = unlocked; break;
            case AbilityId.Sprint:     sprintUnlocked     = unlocked; break;
            case AbilityId.WallLatch:  wallLatchUnlocked  = unlocked; break;
            case AbilityId.DoubleJump: doubleJumpUnlocked = unlocked; break;
            case AbilityId.DriftCloak: driftCloakUnlocked = unlocked; break;
            case AbilityId.SpiritCast: spiritCastUnlocked = unlocked; break;
        }

        AbilityChanged?.Invoke(ability, unlocked);
    }

    public void ResetToDefaults()
    {
        // Does NOT fire AbilityChanged — intended for editor/test resets only.
        // Runtime systems (e.g. AbilityGate) should call Refresh() manually after reset if needed.
        dashUnlocked       = true;
        wallClingUnlocked  = true;
        sprintUnlocked     = false;
        wallLatchUnlocked  = false;
        doubleJumpUnlocked = false;
        driftCloakUnlocked = false;
        spiritCastUnlocked = false;
    }

    public void GatherSaveData(SaveData data)
    {
        if (data == null) return;
        if (data.abilities == null) data.abilities = new AbilitySaveData();

        data.abilities.dashUnlocked       = dashUnlocked;
        data.abilities.wallClingUnlocked  = wallClingUnlocked;
        data.abilities.sprintUnlocked     = sprintUnlocked;
        data.abilities.wallLatchUnlocked  = wallLatchUnlocked;
        data.abilities.doubleJumpUnlocked = doubleJumpUnlocked;
        data.abilities.driftCloakUnlocked = driftCloakUnlocked;
        data.abilities.spiritCastUnlocked = spiritCastUnlocked;
    }

    public void ApplySaveData(SaveData data)
    {
        if (data?.abilities == null) return;

        SetUnlocked(AbilityId.Dash,       data.abilities.dashUnlocked);
        SetUnlocked(AbilityId.WallCling,  data.abilities.wallClingUnlocked);
        SetUnlocked(AbilityId.Sprint,     data.abilities.sprintUnlocked);
        SetUnlocked(AbilityId.WallLatch,  data.abilities.wallLatchUnlocked);
        SetUnlocked(AbilityId.DoubleJump, data.abilities.doubleJumpUnlocked);
        SetUnlocked(AbilityId.DriftCloak, data.abilities.driftCloakUnlocked);
        SetUnlocked(AbilityId.SpiritCast, data.abilities.spiritCastUnlocked);
    }
}
