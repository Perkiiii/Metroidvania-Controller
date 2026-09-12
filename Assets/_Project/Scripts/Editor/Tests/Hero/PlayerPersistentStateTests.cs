using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerPersistentStateTests
{
    [Test]
    public void HealthFreshDataInitializesFullWithoutBonusHealth()
    {
        PlayerHealthState state = ScriptableObject.CreateInstance<PlayerHealthState>();
        try
        {
            PlayerHealthChangeReason? reason = null;
            state.Changed += info => reason = info.Reason;

            state.ApplySaveData(new SaveData());

            Assert.That(state.CurrentHealth, Is.EqualTo(state.MaximumHealth));
            Assert.That(state.MaximumHealth, Is.GreaterThan(0));
            Assert.That(state.BonusHealth, Is.Zero);
            Assert.That(reason, Is.EqualTo(PlayerHealthChangeReason.StateApplied));
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void HealthApplyClampsCurrentMaximumAndBonusValues()
    {
        PlayerHealthState state = ScriptableObject.CreateInstance<PlayerHealthState>();
        try
        {
            SaveData data = CreateInitializedHealthData(current: 99, maximum: 7, bonus: -4);
            state.ApplySaveData(data);

            Assert.That(state.CurrentHealth, Is.EqualTo(7));
            Assert.That(state.MaximumHealth, Is.EqualTo(7));
            Assert.That(state.BonusHealth, Is.Zero);

            data.health.currentHealth = -10;
            data.health.maximumHealth = -3;
            data.health.bonusHealth = -2;
            state.ApplySaveData(data);

            Assert.That(state.CurrentHealth, Is.Zero);
            Assert.That(state.MaximumHealth, Is.EqualTo(1));
            Assert.That(state.BonusHealth, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void HealthDamageConsumesBonusBeforeNormalHealth()
    {
        PlayerHealthState state = ScriptableObject.CreateInstance<PlayerHealthState>();
        try
        {
            state.ApplySaveData(CreateInitializedHealthData(current: 5, maximum: 5, bonus: 3));

            int applied = state.ApplyDamage(4);

            Assert.That(applied, Is.EqualTo(4));
            Assert.That(state.BonusHealth, Is.Zero);
            Assert.That(state.CurrentHealth, Is.EqualTo(4));
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void HealthSaveDataRoundTrips()
    {
        PlayerHealthState source = ScriptableObject.CreateInstance<PlayerHealthState>();
        PlayerHealthState destination = ScriptableObject.CreateInstance<PlayerHealthState>();
        try
        {
            source.ApplySaveData(CreateInitializedHealthData(current: 3, maximum: 8, bonus: 2));
            SaveData data = new SaveData();
            source.GatherSaveData(data);
            destination.ApplySaveData(data);

            Assert.That(data.health.initialized, Is.True);
            Assert.That(destination.CurrentHealth, Is.EqualTo(3));
            Assert.That(destination.MaximumHealth, Is.EqualTo(8));
            Assert.That(destination.BonusHealth, Is.EqualTo(2));
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(destination);
        }
    }

    [Test]
    public void ZeroHealthContinueNormalizesToFullHealthWithoutMisrepresentingAsHeal()
    {
        PlayerHealthState state = ScriptableObject.CreateInstance<PlayerHealthState>();
        try
        {
            state.ApplySaveData(CreateInitializedHealthData(current: 0, maximum: 9, bonus: 3));

            int notifyCount = 0;
            PlayerHealthChangeReason? reason = null;
            state.Changed += info =>
            {
                notifyCount++;
                reason = info.Reason;
            };

            bool normalized = state.NormalizeDepletedContinue();

            Assert.That(normalized, Is.True);
            Assert.That(state.CurrentHealth, Is.EqualTo(9));
            Assert.That(state.MaximumHealth, Is.EqualTo(9));
            Assert.That(state.BonusHealth, Is.Zero);
            Assert.That(notifyCount, Is.EqualTo(1));
            Assert.That(reason, Is.EqualTo(PlayerHealthChangeReason.StateApplied));
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void NormalizeDepletedContinueIsNoOpWhenAlreadyAlive()
    {
        PlayerHealthState state = ScriptableObject.CreateInstance<PlayerHealthState>();
        try
        {
            state.ApplySaveData(CreateInitializedHealthData(current: 4, maximum: 9, bonus: 1));
            int notifyCount = 0;
            state.Changed += _ => notifyCount++;

            bool normalized = state.NormalizeDepletedContinue();

            Assert.That(normalized, Is.False);
            Assert.That(state.CurrentHealth, Is.EqualTo(4));
            Assert.That(state.BonusHealth, Is.EqualTo(1));
            Assert.That(notifyCount, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void NormalizeDepletedContinueIsIdempotent()
    {
        PlayerHealthState state = ScriptableObject.CreateInstance<PlayerHealthState>();
        try
        {
            state.ApplySaveData(CreateInitializedHealthData(current: 0, maximum: 6, bonus: 0));

            Assert.That(state.NormalizeDepletedContinue(), Is.True);

            int notifyCount = 0;
            state.Changed += _ => notifyCount++;
            bool secondCall = state.NormalizeDepletedContinue();

            Assert.That(secondCall, Is.False);
            Assert.That(notifyCount, Is.Zero);
            Assert.That(state.CurrentHealth, Is.EqualTo(6));
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void ResourceFreshDataInitializesEmptyWithValidCapacity()
    {
        PlayerResourceState state = ScriptableObject.CreateInstance<PlayerResourceState>();
        try
        {
            PlayerResourceChangeReason? reason = null;
            state.Changed += info => reason = info.Reason;

            state.ApplySaveData(new SaveData());

            Assert.That(state.CurrentParts, Is.Zero);
            Assert.That(state.MaximumParts, Is.GreaterThanOrEqualTo(0));
            Assert.That(reason, Is.EqualTo(PlayerResourceChangeReason.StateApplied));
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void ResourceApplyClampsCurrentAndMaximumParts()
    {
        PlayerResourceState state = ScriptableObject.CreateInstance<PlayerResourceState>();
        try
        {
            SaveData data = CreateInitializedResourceData(current: 20, maximum: 8);
            state.ApplySaveData(data);

            Assert.That(state.CurrentParts, Is.EqualTo(8));
            Assert.That(state.MaximumParts, Is.EqualTo(8));

            data.resource.currentParts = 9;
            data.resource.maximumParts = -1;
            state.ApplySaveData(data);

            Assert.That(state.CurrentParts, Is.Zero);
            Assert.That(state.MaximumParts, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void ResourceSaveDataRoundTrips()
    {
        PlayerResourceState source = ScriptableObject.CreateInstance<PlayerResourceState>();
        PlayerResourceState destination = ScriptableObject.CreateInstance<PlayerResourceState>();
        try
        {
            source.ApplySaveData(CreateInitializedResourceData(current: 6, maximum: 12));
            SaveData data = new SaveData();
            source.GatherSaveData(data);
            destination.ApplySaveData(data);

            Assert.That(data.resource.initialized, Is.True);
            Assert.That(destination.CurrentParts, Is.EqualTo(6));
            Assert.That(destination.MaximumParts, Is.EqualTo(12));
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(destination);
        }
    }

    [Test]
    public void ResourceClearZeroesCurrentPartsButPreservesMaximum()
    {
        PlayerResourceState state = ScriptableObject.CreateInstance<PlayerResourceState>();
        try
        {
            state.ApplySaveData(CreateInitializedResourceData(current: 5, maximum: 9));

            int notifyCount = 0;
            PlayerResourceChangeReason? reason = null;
            state.Changed += info =>
            {
                notifyCount++;
                reason = info.Reason;
            };

            state.Clear();

            Assert.That(state.CurrentParts, Is.Zero);
            Assert.That(state.MaximumParts, Is.EqualTo(9));
            Assert.That(notifyCount, Is.EqualTo(1));
            Assert.That(reason, Is.EqualTo(PlayerResourceChangeReason.Cleared));
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void ResourceClearIsIdempotentWhenAlreadyEmpty()
    {
        PlayerResourceState state = ScriptableObject.CreateInstance<PlayerResourceState>();
        try
        {
            state.ApplySaveData(CreateInitializedResourceData(current: 0, maximum: 9));
            int notifyCount = 0;
            state.Changed += _ => notifyCount++;

            state.Clear();
            state.Clear();

            Assert.That(state.CurrentParts, Is.Zero);
            Assert.That(state.MaximumParts, Is.EqualTo(9));
            Assert.That(notifyCount, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void VersionTwoSaveGetsUninitializedStateSectionsForAssetDefaults()
    {
        const string json = "{\"meta\":{\"saveVersion\":2},\"abilities\":{},\"player\":{},\"world\":{}}";
        Assert.That(SaveSerializer.TryDeserialize(json, out SaveData data), Is.True);

        SaveDataMigrator.Migrate(data);

        Assert.That(data.meta.saveVersion, Is.EqualTo(6));
        Assert.That(data.health, Is.Not.Null);
        Assert.That(data.health.initialized, Is.False);
        Assert.That(data.resource, Is.Not.Null);
        Assert.That(data.resource.initialized, Is.False);
    }

    [Test]
    public void FreshDataResetsStatesAfterPreviouslyAppliedSave()
    {
        PlayerHealthState health = ScriptableObject.CreateInstance<PlayerHealthState>();
        PlayerResourceState resource = ScriptableObject.CreateInstance<PlayerResourceState>();
        try
        {
            health.ApplySaveData(CreateInitializedHealthData(current: 1, maximum: 12, bonus: 4));
            resource.ApplySaveData(CreateInitializedResourceData(current: 7, maximum: 9));

            SaveData fresh = new SaveData();
            SaveDataMigrator.Migrate(fresh);
            health.ApplySaveData(fresh);
            resource.ApplySaveData(fresh);

            Assert.That(health.CurrentHealth, Is.EqualTo(health.MaximumHealth));
            Assert.That(health.MaximumHealth, Is.EqualTo(5));
            Assert.That(health.BonusHealth, Is.Zero);
            Assert.That(resource.CurrentParts, Is.Zero);
            Assert.That(resource.MaximumParts, Is.Zero);
        }
        finally
        {
            Object.DestroyImmediate(health);
            Object.DestroyImmediate(resource);
        }
    }

    [Test]
    public void SaveTargetCachePreservesOrderAndSkipsInvalidEntries()
    {
        OrderedSaveTarget first = ScriptableObject.CreateInstance<OrderedSaveTarget>();
        OrderedSaveTarget second = ScriptableObject.CreateInstance<OrderedSaveTarget>();
        InvalidSaveTarget invalid = ScriptableObject.CreateInstance<InvalidSaveTarget>();
        GameObject gameObject = new GameObject("SaveManager Test");
        try
        {
            gameObject.SetActive(false);
            LogAssert.ignoreFailingMessages = true;
            SaveManager manager = gameObject.AddComponent<SaveManager>();
            LogAssert.ignoreFailingMessages = false;
            SetPrivateField(manager, "saveTargets", new List<ScriptableObject>
            {
                first,
                null,
                invalid,
                first,
                second
            });

            LogAssert.Expect(LogType.Warning, "[SaveManager] Save target at index 1 is missing and will be ignored.");
            LogAssert.Expect(LogType.Warning, $"[SaveManager] Assigned asset '{invalid.name}' at index 2 does not implement ISaveTarget and will be ignored.");
            LogAssert.Expect(LogType.Warning, $"[SaveManager] Duplicate save target '{first.name}' at index 3 will be ignored.");
            InvokePrivate(manager, "CacheSaveTargets", true);

            List<ISaveTarget> cached = GetPrivateField<List<ISaveTarget>>(manager, "registeredSaveTargets");
            Assert.That(cached, Has.Count.EqualTo(2));
            Assert.That(cached[0], Is.SameAs(first));
            Assert.That(cached[1], Is.SameAs(second));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
            Object.DestroyImmediate(invalid);
        }
    }

    [Test]
    public void GameManagerResolveLoadedHealthStateNormalizesZeroHealthOnce()
    {
        PlayerHealthState state = ScriptableObject.CreateInstance<PlayerHealthState>();
        GameObject gameObject = new GameObject("GameManager Test");
        try
        {
            state.ApplySaveData(CreateInitializedHealthData(current: 0, maximum: 8, bonus: 2));

            gameObject.SetActive(false);
            GameManager manager = gameObject.AddComponent<GameManager>();
            SetPrivateField(manager, "healthState", state);

            manager.ResolveLoadedHealthState();

            Assert.That(state.CurrentHealth, Is.EqualTo(8));
            Assert.That(state.BonusHealth, Is.Zero);

            int notifyCount = 0;
            state.Changed += _ => notifyCount++;
            manager.ResolveLoadedHealthState();

            Assert.That(notifyCount, Is.Zero, "A second resolve call must not re-trigger normalization.");
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void GameManagerResolveLoadedHealthStateClearsResourceWhenHealthWasDepleted()
    {
        PlayerHealthState health = ScriptableObject.CreateInstance<PlayerHealthState>();
        PlayerResourceState resource = ScriptableObject.CreateInstance<PlayerResourceState>();
        GameObject gameObject = new GameObject("GameManager Test");
        try
        {
            health.ApplySaveData(CreateInitializedHealthData(current: 0, maximum: 8, bonus: 2));
            resource.ApplySaveData(CreateInitializedResourceData(current: 6, maximum: 10));

            gameObject.SetActive(false);
            GameManager manager = gameObject.AddComponent<GameManager>();
            SetPrivateField(manager, "healthState", health);
            SetPrivateField(manager, "resourceState", resource);

            manager.ResolveLoadedHealthState();

            Assert.That(health.CurrentHealth, Is.EqualTo(8));
            Assert.That(resource.CurrentParts, Is.Zero, "A zero-health Continue must clear resource neutrally, matching normal death handling.");
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(health);
            Object.DestroyImmediate(resource);
        }
    }

    [Test]
    public void GameManagerResolveLoadedHealthStateLeavesResourceUntouchedWhenHealthNotDepleted()
    {
        PlayerHealthState health = ScriptableObject.CreateInstance<PlayerHealthState>();
        PlayerResourceState resource = ScriptableObject.CreateInstance<PlayerResourceState>();
        GameObject gameObject = new GameObject("GameManager Test");
        try
        {
            health.ApplySaveData(CreateInitializedHealthData(current: 5, maximum: 8, bonus: 1));
            resource.ApplySaveData(CreateInitializedResourceData(current: 6, maximum: 10));

            gameObject.SetActive(false);
            GameManager manager = gameObject.AddComponent<GameManager>();
            SetPrivateField(manager, "healthState", health);
            SetPrivateField(manager, "resourceState", resource);

            manager.ResolveLoadedHealthState();

            Assert.That(health.CurrentHealth, Is.EqualTo(5), "Non-depleted health must not be normalized.");
            Assert.That(resource.CurrentParts, Is.EqualTo(6), "Resource must only be cleared when the zero-health seam actually fires.");
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(health);
            Object.DestroyImmediate(resource);
        }
    }

    // -------------------------------------------------------------------------
    // Permanent ability defaults (Package A2 Stage 0)
    //
    // A true new game owns no permanent ability. These tests pin the three places that decide
    // that — the save-data initializers, the state asset's field initializers, and
    // ResetToDefaults — plus the compatibility rule that explicitly-stored save values always win.
    // -------------------------------------------------------------------------

    private static readonly AbilityId[] AllAbilities =
    {
        AbilityId.Dash,
        AbilityId.WallCling,
        AbilityId.Sprint,
        AbilityId.WallLatch,
        AbilityId.DoubleJump,
        AbilityId.DriftCloak,
        AbilityId.SpiritCast,
        AbilityId.Bind
    };

    [Test]
    public void NewAbilitySaveDataStartsFullyLocked()
    {
        AbilitySaveData data = new AbilitySaveData();

        Assert.That(data.dashUnlocked, Is.False);
        Assert.That(data.wallClingUnlocked, Is.False);
        Assert.That(data.sprintUnlocked, Is.False);
        Assert.That(data.wallLatchUnlocked, Is.False);
        Assert.That(data.doubleJumpUnlocked, Is.False);
        Assert.That(data.driftCloakUnlocked, Is.False);
        Assert.That(data.spiritCastUnlocked, Is.False);
        Assert.That(data.bindUnlocked, Is.False);
    }

    [Test]
    public void NewAbilityStateInstanceStartsFullyLocked()
    {
        PlayerAbilityState state = ScriptableObject.CreateInstance<PlayerAbilityState>();
        try
        {
            foreach (AbilityId ability in AllAbilities)
            {
                Assert.That(state.IsUnlocked(ability), Is.False, $"{ability} must start locked.");
            }
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void ResetToDefaultsLocksEveryAbility()
    {
        PlayerAbilityState state = ScriptableObject.CreateInstance<PlayerAbilityState>();
        try
        {
            foreach (AbilityId ability in AllAbilities)
            {
                state.Unlock(ability);
            }

            state.ResetToDefaults();

            foreach (AbilityId ability in AllAbilities)
            {
                Assert.That(state.IsUnlocked(ability), Is.False, $"{ability} must be locked after reset.");
            }
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    /// <summary>
    /// Mirrors <c>SaveManager.CreateFreshSave</c>'s composition (new SaveData -> Migrate -> apply)
    /// without touching the real save directory.
    /// </summary>
    [Test]
    public void FreshSaveCompositionAppliesAllAbilitiesLocked()
    {
        PlayerAbilityState state = ScriptableObject.CreateInstance<PlayerAbilityState>();
        try
        {
            foreach (AbilityId ability in AllAbilities)
            {
                state.Unlock(ability);
            }

            SaveData fresh = new SaveData();
            SaveDataMigrator.Migrate(fresh);
            state.ApplySaveData(fresh);

            foreach (AbilityId ability in AllAbilities)
            {
                Assert.That(state.IsUnlocked(ability), Is.False, $"{ability} must be locked in a fresh save.");
            }
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void SaveWithMissingAbilitySectionMigratesToAllLockedDefaults()
    {
        const string json = "{\"meta\":{\"saveVersion\":2},\"player\":{},\"world\":{}}";
        Assert.That(SaveSerializer.TryDeserialize(json, out SaveData data), Is.True);

        SaveDataMigrator.Migrate(data);

        Assert.That(data.abilities, Is.Not.Null);
        Assert.That(data.abilities.dashUnlocked, Is.False);
        Assert.That(data.abilities.wallClingUnlocked, Is.False);
        Assert.That(data.abilities.doubleJumpUnlocked, Is.False);
        Assert.That(data.abilities.bindUnlocked, Is.False);
        Assert.That(data.meta.saveVersion, Is.EqualTo(SaveDataMigrator.CurrentSaveVersion),
            "Changing boolean defaults alone must not require a schema version beyond the current one.");
    }

    [Test]
    public void SaveWithExplicitlyNullAbilitySectionMigratesToAllLockedDefaults()
    {
        SaveData data = new SaveData { abilities = null };

        SaveDataMigrator.Migrate(data);

        Assert.That(data.abilities, Is.Not.Null);
        Assert.That(data.abilities.dashUnlocked, Is.False);
        Assert.That(data.abilities.wallClingUnlocked, Is.False);
    }

    [Test]
    public void ExistingSaveWithExplicitUnlocksIsAppliedUnchanged()
    {
        // A real save written by GatherSaveData always contains all eight booleans explicitly.
        const string json =
            "{\"meta\":{\"saveVersion\":4}," +
            "\"abilities\":{\"dashUnlocked\":true,\"wallClingUnlocked\":false,\"sprintUnlocked\":true," +
            "\"wallLatchUnlocked\":false,\"doubleJumpUnlocked\":true,\"driftCloakUnlocked\":false," +
            "\"spiritCastUnlocked\":false,\"bindUnlocked\":true}}";
        Assert.That(SaveSerializer.TryDeserialize(json, out SaveData data), Is.True);
        SaveDataMigrator.Migrate(data);

        PlayerAbilityState state = ScriptableObject.CreateInstance<PlayerAbilityState>();
        try
        {
            state.ApplySaveData(data);

            Assert.That(state.IsUnlocked(AbilityId.Dash), Is.True, "An explicitly saved unlock must survive the new locked defaults.");
            Assert.That(state.IsUnlocked(AbilityId.WallCling), Is.False);
            Assert.That(state.IsUnlocked(AbilityId.Sprint), Is.True);
            Assert.That(state.IsUnlocked(AbilityId.WallLatch), Is.False);
            Assert.That(state.IsUnlocked(AbilityId.DoubleJump), Is.True);
            Assert.That(state.IsUnlocked(AbilityId.DriftCloak), Is.False);
            Assert.That(state.IsUnlocked(AbilityId.SpiritCast), Is.False);
            Assert.That(state.IsUnlocked(AbilityId.Bind), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(state);
        }
    }

    [Test]
    public void AbilitySaveDataRoundTripsEveryExplicitCombination()
    {
        PlayerAbilityState source = ScriptableObject.CreateInstance<PlayerAbilityState>();
        PlayerAbilityState destination = ScriptableObject.CreateInstance<PlayerAbilityState>();
        try
        {
            for (int mask = 0; mask < 1 << 8; mask++)
            {
                for (int bit = 0; bit < AllAbilities.Length; bit++)
                {
                    source.SetUnlocked(AllAbilities[bit], (mask & (1 << bit)) != 0);
                }

                SaveData data = new SaveData();
                source.GatherSaveData(data);

                Assert.That(SaveSerializer.TrySerialize(data, out string json), Is.True);
                Assert.That(SaveSerializer.TryDeserialize(json, out SaveData reloaded), Is.True);
                destination.ApplySaveData(reloaded);

                for (int bit = 0; bit < AllAbilities.Length; bit++)
                {
                    Assert.That(destination.IsUnlocked(AllAbilities[bit]),
                        Is.EqualTo((mask & (1 << bit)) != 0),
                        $"Mask {mask}: {AllAbilities[bit]} did not round-trip.");
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(destination);
        }
    }

    [Test]
    public void ProductionAbilityStateAssetShipsFullyLocked()
    {
        const string path = "Assets/_Project/ScriptableObjects/Hero/PlayerAbilityState.asset";
        PlayerAbilityState asset = UnityEditor.AssetDatabase.LoadAssetAtPath<PlayerAbilityState>(path);
        Assert.That(asset, Is.Not.Null, $"Expected the production ability state asset at {path}.");

        foreach (AbilityId ability in AllAbilities)
        {
            Assert.That(asset.IsUnlocked(ability), Is.False,
                $"{ability} is unlocked in the mutable production asset; a true new game must own no ability. " +
                "Use Sandbox fixtures or the ability pickups to test unlocked behaviour.");
        }
    }

    private static SaveData CreateInitializedHealthData(int current, int maximum, int bonus)
    {
        return new SaveData
        {
            health = new HealthSaveData
            {
                initialized = true,
                currentHealth = current,
                maximumHealth = maximum,
                bonusHealth = bonus
            }
        };
    }

    private static SaveData CreateInitializedResourceData(int current, int maximum)
    {
        return new SaveData
        {
            resource = new ResourceSaveData
            {
                initialized = true,
                currentParts = current,
                maximumParts = maximum
            }
        };
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        return (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
    }

    private sealed class OrderedSaveTarget : ScriptableObject, ISaveTarget
    {
        public void GatherSaveData(SaveData data) { }
        public void ApplySaveData(SaveData data) { }
    }

    private sealed class InvalidSaveTarget : ScriptableObject
    {
    }
}
