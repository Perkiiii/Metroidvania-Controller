using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Package A2 coverage for the Gear tab: acquired-only filtering, the zero-entry empty state,
/// stable-key selection across rebuilds, open/close subscription lifecycle, and the read-only
/// ownership boundary against <see cref="PlayerAbilityState"/>.
///
/// Follows the project's EditMode convention: components are added directly and driven through
/// <see cref="GearScreen.Configure"/>, since Unity does not invoke Awake for AddComponent in
/// Edit Mode.
/// </summary>
public sealed class GearScreenTests
{
    private readonly List<GameObject> spawned = new List<GameObject>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();

    private GearScreen gear;
    private GameObject contentRoot;
    private GameObject populatedRoot;
    private GameObject emptyStateRoot;
    private RectTransform entryContainer;
    private GearEntryView entryTemplate;
    private GearDetailsPanel detailsPanel;
    private GameObject detailsContent;
    private TMP_Text detailsName;
    private PlayerAbilityState abilityState;
    private GearDisplayCatalog catalog;

    private const string DashKey = "test_gear_dash";
    private const string ClingKey = "test_gear_wallcling";
    private const string JumpKey = "test_gear_doublejump";

    [SetUp]
    public void SetUp()
    {
        GameObject root = NewObject("GearTab Test");
        gear = root.AddComponent<GearScreen>();

        contentRoot = Child(root, "Content");
        populatedRoot = Child(contentRoot, "Populated");
        emptyStateRoot = Child(contentRoot, "EmptyState");

        GameObject containerGo = Child(populatedRoot, "EntryContainer");
        entryContainer = containerGo.AddComponent<RectTransform>();

        GameObject detailsGo = Child(populatedRoot, "Details");
        detailsPanel = detailsGo.AddComponent<GearDetailsPanel>();
        detailsContent = Child(detailsGo, "DetailsContent");
        detailsName = Child(detailsContent, "Name").AddComponent<TextMeshProUGUI>();
        SetPrivate(detailsPanel, "contentRoot", detailsContent);
        SetPrivate(detailsPanel, "nameLabel", detailsName);

        entryTemplate = CreateEntryTemplate();

        SetPrivate(gear, "contentRoot", contentRoot);
        SetPrivate(gear, "populatedRoot", populatedRoot);
        SetPrivate(gear, "emptyStateRoot", emptyStateRoot);
        SetPrivate(gear, "entryContainer", entryContainer);
        SetPrivate(gear, "entryPrefab", entryTemplate);
        SetPrivate(gear, "detailsPanel", detailsPanel);

        abilityState = CreateAsset<PlayerAbilityState>();
        abilityState.ResetToDefaults();

        catalog = CreateAsset<GearDisplayCatalog>();
        catalog.SetDefinitions(new[]
        {
            Definition(DashKey, AbilityId.Dash, "Traveller's Coil"),
            Definition(ClingKey, AbilityId.WallCling, "Bramble Cord"),
            Definition(JumpKey, AbilityId.DoubleJump, "Hollow Lantern")
        });

        gear.Configure(abilityState, catalog);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null) UnityEngine.Object.DestroyImmediate(spawned[i]);
        }
        spawned.Clear();

        for (int i = assets.Count - 1; i >= 0; i--)
        {
            if (assets[i] != null) UnityEngine.Object.DestroyImmediate(assets[i]);
        }
        assets.Clear();
    }

    private GameObject NewObject(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private static GameObject Child(GameObject parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private GearEntryView CreateEntryTemplate()
    {
        GameObject go = NewObject("GearEntryView Template");
        Button button = go.AddComponent<Button>();
        GearEntryView view = go.AddComponent<GearEntryView>();
        SetPrivate(view, "button", button);
        SetPrivate(view, "nameLabel", Child(go, "Name").AddComponent<TextMeshProUGUI>());
        return view;
    }

    private T CreateAsset<T>() where T : ScriptableObject
    {
        T instance = ScriptableObject.CreateInstance<T>();
        assets.Add(instance);
        return instance;
    }

    private GearDisplayDefinition Definition(string key, AbilityId ability, string displayName)
    {
        GearDisplayDefinition definition = CreateAsset<GearDisplayDefinition>();
        definition.name = key;
        definition.SetFixtureContent(key, ability, displayName, "Test fixture", "Description.", "Flavour.");
        return definition;
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, "Field not found: " + field);
        info.SetValue(target, value);
    }

    private static T GetPrivate<T>(object target, string field)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, "Field not found: " + field);
        return (T)info.GetValue(target);
    }

    private static int AbilityChangedSubscriberCount(PlayerAbilityState state)
    {
        FieldInfo field = typeof(PlayerAbilityState)
            .GetField("AbilityChanged", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "AbilityChanged backing field not found.");
        Delegate handler = field.GetValue(state) as Delegate;
        return handler == null ? 0 : handler.GetInvocationList().Length;
    }

    private List<string> EntryKeys()
    {
        List<string> keys = new List<string>();
        foreach (GearEntryView entry in GetPrivate<List<GearEntryView>>(gear, "activeEntries"))
        {
            keys.Add(entry.StableKey);
        }
        return keys;
    }

    // -------------------------------------------------------------------------
    // Acquired-only filtering
    // -------------------------------------------------------------------------

    [Test]
    public void TrueNewGameShowsNoGearEntries()
    {
        gear.Show();

        Assert.That(gear.EntryCount, Is.Zero, "A true new game owns no ability, so Gear must be empty.");
        Assert.That(entryContainer.childCount, Is.Zero);
    }

    [Test]
    public void ZeroEntriesShowsTheAuthoredEmptyStateAndClearsDetails()
    {
        detailsName.text = "stale";
        gear.Show();

        Assert.That(emptyStateRoot.activeSelf, Is.True);
        Assert.That(populatedRoot.activeSelf, Is.False, "An empty Gear must not present an empty collection frame.");
        Assert.That(detailsContent.activeSelf, Is.False);
        Assert.That(detailsName.text, Is.Empty);
        Assert.That(gear.SelectedStableKey, Is.Null);
    }

    [Test]
    public void LockedAbilitiesCreateNoEntryPlaceholderOrSilhouette()
    {
        abilityState.Unlock(AbilityId.Dash);
        gear.Show();

        Assert.That(gear.EntryCount, Is.EqualTo(1));
        Assert.That(entryContainer.childCount, Is.EqualTo(1),
            "Undiscovered Gear is absent: locked abilities must instantiate nothing at all.");
    }

    [Test]
    public void FirstUnlockShowsThatGearAndSelectsIt()
    {
        abilityState.Unlock(AbilityId.WallCling);
        gear.Show();

        Assert.That(EntryKeys(), Is.EqualTo(new[] { ClingKey }));
        Assert.That(gear.SelectedStableKey, Is.EqualTo(ClingKey));
        Assert.That(emptyStateRoot.activeSelf, Is.False);
        Assert.That(populatedRoot.activeSelf, Is.True);
    }

    [Test]
    public void MultipleAcquiredGearUsesDeterministicCatalogueOrder()
    {
        abilityState.Unlock(AbilityId.DoubleJump);
        abilityState.Unlock(AbilityId.Dash);
        gear.Show();

        Assert.That(EntryKeys(), Is.EqualTo(new[] { DashKey, JumpKey }),
            "Presentation order is the catalogue's authored order, not unlock order.");
    }

    // -------------------------------------------------------------------------
    // Selection
    // -------------------------------------------------------------------------

    [Test]
    public void SelectedStableKeySurvivesARebuild()
    {
        abilityState.Unlock(AbilityId.Dash);
        abilityState.Unlock(AbilityId.DoubleJump);
        gear.Show();

        gear.Rebuild();
        Assert.That(gear.SelectedStableKey, Is.EqualTo(DashKey));

        // Select the second entry, then force a rebuild: the key, not the object reference, wins.
        GetPrivate<List<GearEntryView>>(gear, "activeEntries")[1].NotifySelected();
        Assert.That(gear.SelectedStableKey, Is.EqualTo(JumpKey));

        gear.Rebuild();
        Assert.That(gear.SelectedStableKey, Is.EqualTo(JumpKey),
            "Entry objects are rebuilt, so selection must restore by stable key.");
    }

    [Test]
    public void SelectedGearThatIsNoLongerOwnedFallsBackToTheFirstVisibleEntry()
    {
        abilityState.Unlock(AbilityId.Dash);
        abilityState.Unlock(AbilityId.DoubleJump);
        gear.Show();
        GetPrivate<List<GearEntryView>>(gear, "activeEntries")[1].NotifySelected();
        Assert.That(gear.SelectedStableKey, Is.EqualTo(JumpKey));

        abilityState.Lock(AbilityId.DoubleJump);

        Assert.That(gear.SelectedStableKey, Is.EqualTo(DashKey));
    }

    [Test]
    public void FirstSelectionIsNullWhileEmptySoTheRootFallsBackToTheStrip()
    {
        gear.Show();
        Assert.That(gear.FirstSelection, Is.Null);
    }

    [Test]
    public void FirstSelectionIsTheFirstAcquiredEntry()
    {
        abilityState.Unlock(AbilityId.Dash);
        gear.Show();

        Assert.That(gear.FirstSelection, Is.SameAs(GetPrivate<List<GearEntryView>>(gear, "activeEntries")[0].Selectable));
    }

    [Test]
    public void EntryNavigationChainsVerticallyAndReturnsUpToTheStrip()
    {
        GameObject stripTargetGo = NewObject("Gear Tab Button");
        Selectable stripTarget = stripTargetGo.AddComponent<Button>();
        gear.SetTabStripReturnTarget(stripTarget);

        abilityState.Unlock(AbilityId.Dash);
        abilityState.Unlock(AbilityId.WallCling);
        gear.Show();

        List<GearEntryView> entries = GetPrivate<List<GearEntryView>>(gear, "activeEntries");
        Assert.That(entries[0].Selectable.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
        Assert.That(entries[0].Selectable.navigation.selectOnDown, Is.SameAs(entries[1].Selectable));
        Assert.That(entries[1].Selectable.navigation.selectOnUp, Is.SameAs(entries[0].Selectable));
        Assert.That(entries[0].Selectable.navigation.selectOnUp, Is.SameAs(stripTarget));
    }

    // -------------------------------------------------------------------------
    // Refresh and subscription lifecycle
    // -------------------------------------------------------------------------

    [Test]
    public void EveryOpenPerformsAFullRefreshFromTheCurrentSnapshot()
    {
        gear.Show();
        Assert.That(gear.EntryCount, Is.Zero);
        gear.Hide();

        // Ownership changes while the tab is closed (e.g. a pickup, or a save being applied).
        abilityState.Unlock(AbilityId.Dash);

        gear.Show();
        Assert.That(gear.EntryCount, Is.EqualTo(1), "Reopening must re-read the snapshot, not rely on events.");
    }

    [Test]
    public void LiveAbilityChangeWhileShownRefreshesNeutrally()
    {
        gear.Show();
        Assert.That(gear.EntryCount, Is.Zero);

        abilityState.Unlock(AbilityId.Dash);

        Assert.That(gear.EntryCount, Is.EqualTo(1));
        Assert.That(gear.SelectedStableKey, Is.EqualTo(DashKey));
    }

    [Test]
    public void HidingUnsubscribesSoLaterOwnershipChangesDoNotRebuildTheHiddenTab()
    {
        gear.Show();
        gear.Hide();

        Assert.That(AbilityChangedSubscriberCount(abilityState), Is.Zero);

        abilityState.Unlock(AbilityId.Dash);
        Assert.That(gear.EntryCount, Is.Zero, "A hidden tab must not rebuild in response to ownership changes.");
    }

    [Test]
    public void RepeatedOpensDoNotAccumulateOwnershipSubscriptions()
    {
        for (int i = 0; i < 4; i++)
        {
            gear.Show();
        }

        Assert.That(AbilityChangedSubscriberCount(abilityState), Is.EqualTo(1));
    }

    [Test]
    public void ReconfiguringSwapsTheOwnershipSourceWithoutLeakingTheOldSubscription()
    {
        gear.Show();
        PlayerAbilityState replacement = CreateAsset<PlayerAbilityState>();
        replacement.ResetToDefaults();

        gear.Configure(replacement, catalog);

        Assert.That(AbilityChangedSubscriberCount(abilityState), Is.Zero);
        Assert.That(AbilityChangedSubscriberCount(replacement), Is.EqualTo(1));
    }

    // -------------------------------------------------------------------------
    // Ownership boundary
    // -------------------------------------------------------------------------

    [Test]
    public void GearNeverMutatesOwnership()
    {
        abilityState.Unlock(AbilityId.Dash);

        int changes = 0;
        abilityState.AbilityChanged += (_, __) => changes++;

        gear.Show();
        gear.Rebuild();
        GetPrivate<List<GearEntryView>>(gear, "activeEntries")[0].NotifySelected();
        gear.Hide();
        gear.Show();

        Assert.That(changes, Is.Zero, "Gear reads ownership only; it must never unlock, lock, or reset.");
        Assert.That(abilityState.IsUnlocked(AbilityId.Dash), Is.True);
        Assert.That(abilityState.IsUnlocked(AbilityId.WallCling), Is.False);
    }

    [Test]
    public void GearScreenExposesNoOwnershipMutationApi()
    {
        foreach (MethodInfo method in typeof(GearScreen).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            Assert.That(method.Name, Is.Not.EqualTo("Unlock"));
            Assert.That(method.Name, Is.Not.EqualTo("Lock"));
            Assert.That(method.Name, Is.Not.EqualTo("SetUnlocked"));
            Assert.That(method.Name, Is.Not.EqualTo("ResetToDefaults"));
        }
    }

    [Test]
    public void ApplyingASaveSnapshotIsNeutralStateRestorationNotAnAcquisition()
    {
        abilityState.Unlock(AbilityId.Dash);
        gear.Show();
        int before = gear.EntryCount;

        // Re-applying the identical snapshot must change nothing visible and raise no event.
        SaveData data = new SaveData();
        abilityState.GatherSaveData(data);
        int changes = 0;
        abilityState.AbilityChanged += (_, __) => changes++;
        abilityState.ApplySaveData(data);

        Assert.That(changes, Is.Zero);
        Assert.That(gear.EntryCount, Is.EqualTo(before));
        Assert.That(gear.SelectedStableKey, Is.EqualTo(DashKey));
    }

    // -------------------------------------------------------------------------
    // Catalogue content gaps and validation
    // -------------------------------------------------------------------------

    [Test]
    public void UnlockedAbilityWithNoDefinitionIsOmittedSafelyAndLoggedOnce()
    {
        abilityState.Unlock(AbilityId.SpiritCast);

        gear.Show();
        gear.Rebuild();
        gear.Rebuild();

        Assert.That(gear.EntryCount, Is.Zero, "A content gap must omit the entry, never fabricate one.");
        Assert.That(GetPrivate<HashSet<AbilityId>>(gear, "loggedMissingDefinitions"),
            Has.Count.EqualTo(1).And.Contains(AbilityId.SpiritCast),
            "The content gap is reported once per ability, not once per rebuild.");
    }

    [Test]
    public void EmptyProductionCatalogueIsAValidStateAndDoesNotThrow()
    {
        GearDisplayCatalog empty = CreateAsset<GearDisplayCatalog>();
        gear.Configure(abilityState, empty);

        Assert.DoesNotThrow(() => gear.Show());
        Assert.That(gear.EntryCount, Is.Zero);
        Assert.That(emptyStateRoot.activeSelf, Is.True);
    }

    [Test]
    public void CatalogueValidationReportsNullEntriesDuplicateKeysAndDuplicateAbilities()
    {
        GearDisplayCatalog broken = CreateAsset<GearDisplayCatalog>();
        broken.SetDefinitions(new[]
        {
            Definition("dup", AbilityId.Dash, "First"),
            Definition("dup", AbilityId.Dash, "Second"),
            null,
            Definition("", AbilityId.Sprint, "No key")
        });

        List<string> issues = new List<string>();
        broken.CollectValidationIssues(issues);

        Assert.That(issues, Has.Some.Contains("Duplicate stable key"));
        Assert.That(issues, Has.Some.Contains("Duplicate ability mapping"));
        Assert.That(issues, Has.Some.Contains("is null"));
        Assert.That(issues, Has.Some.Contains("no stable key"));
    }

    [Test]
    public void ProductionGearCatalogueHasNoAuthoringProblems()
    {
        GearDisplayCatalog production = UnityEditor.AssetDatabase.LoadAssetAtPath<GearDisplayCatalog>(
            "Assets/_Project/ScriptableObjects/UI/Gear/GearDisplayCatalog.asset");
        Assert.That(production, Is.Not.Null, "The production Gear catalogue asset is missing.");

        List<string> issues = new List<string>();
        production.CollectValidationIssues(issues);

        Assert.That(issues, Is.Empty, string.Join(" | ", issues));
    }

    [Test]
    public void CatalogueLookupsResolveByStableKeyAndByAbility()
    {
        Assert.That(catalog.FindByStableKey(ClingKey).RequiredAbility, Is.EqualTo(AbilityId.WallCling));
        Assert.That(catalog.FindByAbility(AbilityId.DoubleJump).StableKey, Is.EqualTo(JumpKey));
        Assert.That(catalog.FindByStableKey("missing"), Is.Null);
        Assert.That(catalog.FindByAbility(AbilityId.DriftCloak), Is.Null);
    }
}
