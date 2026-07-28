using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Development-only UI Sandbox controller. Creates isolated runtime state, wires the real shared
/// HUD/menu presentation components to it, and exposes named fixture presets. Never touches
/// production save files, production ScriptableObject assets, or persistent production managers.
/// See Docs/FeatureSpecs/UISandbox.md.
///
/// Root open/close goes through the Sandbox's own <see cref="UIFlowController"/> rather than calling
/// a screen's <c>Show</c>/<c>Hide</c> directly, so every close route the player has — Continue, the
/// Gameplay Menu's Close control, Back/Cancel, and the open-action toggle — is exercised here
/// against the real production sequence. The Sandbox supplies the <see cref="IUIFlowHost"/> seam
/// because it deliberately has no <c>GameManager</c>, no Hero, and no scene transitions.
/// </summary>
public sealed class UISandboxController : MonoBehaviour, IUIFlowHost
{
    /// <summary>Health capacity every health fixture resets to, so presets stay deterministic.</summary>
    private const int HealthFixtureMaximum = 5;

    /// <summary>Resource capacity every resource fixture resets to.</summary>
    private const int ResourceFixtureMaximum = 10;

    /// <summary>One click of Damage/Heal moves exactly this much. Never fractional.</summary>
    private const int HealthStepPerClick = 1;

    /// <summary>One click of Add/Spend moves exactly this many parts.</summary>
    private const int ResourceStepPerClick = 1;

    [Header("HUD presentation (isolated state)")]
    [SerializeField] private HealthDisplay healthDisplay;
    [SerializeField] private ResourceDisplay resourceDisplay;
    [SerializeField] private BossHealthDisplay bossHealthDisplay;

    [Header("Boss fixture identities (Sandbox-only assets, never production)")]
    [SerializeField] private BossEncounterDefinition shortNameBossFixture;
    [SerializeField] private BossEncounterDefinition longNameBossFixture;

    [Header("Menu presentation")]
    [Tooltip("The Sandbox-local flow controller that owns root open/close, exactly as the " +
        "persistent MenuRoot one does in production.")]
    [SerializeField] private UIFlowController uiFlow;
    [SerializeField] private PauseMenuScreen pauseMenuScreen;
    [SerializeField] private SandboxOptionsPreviewPanel optionsPreviewPanel;

    [Header("Gameplay Menu (real production prefab, isolated fixture data)")]
    [SerializeField] private GameplayMenuScreen gameplayMenuScreen;
    [SerializeField] private GearScreen gearScreen;

    [Header("Aspect / safe-area preview")]
    [SerializeField] private RectTransform aspectFrame;
    [SerializeField] private RectTransform safeAreaGuide;

    [Header("Health fixture buttons (wired in Awake; optional)")]
    [Tooltip("Removes exactly one health per click, clamped at zero.")]
    [SerializeField] private Button healthDamageOneButton;
    [Tooltip("Restores exactly one health per click, clamped at maximum.")]
    [SerializeField] private Button healthHealOneButton;
    [SerializeField] private Button healthFullButton;
    [SerializeField] private Button healthDamagedButton;
    [SerializeField] private Button healthBonusButton;
    [SerializeField] private Button healthEmptyButton;
    [SerializeField] private Button healthLargeCapacityButton;

    [Header("Resource fixture buttons")]
    [SerializeField] private Button resourceEmptyButton;
    [Tooltip("Gains exactly one part per click, clamped at capacity.")]
    [SerializeField] private Button resourceAddButton;
    [SerializeField] private Button resourcePartialButton;
    [SerializeField] private Button resourceFullButton;
    [Tooltip("Spends exactly one part per click, clamped at zero.")]
    [SerializeField] private Button resourceSpendButton;
    [Tooltip("Forfeits all current parts while preserving capacity (the death-forfeit path).")]
    [SerializeField] private Button resourceClearButton;
    [SerializeField] private Button resourceZeroCapacityButton;

    [Header("Boss / menu / Gear / aspect fixture buttons")]
    [SerializeField] private Button bossHiddenButton;
    [SerializeField] private Button bossSingleButton;
    [SerializeField] private Button bossAggregateButton;
    [SerializeField] private Button openPauseButton;
    [SerializeField] private Button openGameplayMenuButton;
    [SerializeField] private Toggle optionsPreviewToggle;
    [SerializeField] private Toggle quitSeamToggle;
    [SerializeField] private Button gearNoneButton;
    [SerializeField] private Button gearSingleButton;
    [SerializeField] private Button gearMultipleButton;
    [SerializeField] private Button gearLiveUnlockButton;
    [SerializeField] private Button gearMissingDefinitionButton;
    [SerializeField] private Button aspect16x9Button;
    [SerializeField] private Button aspect16x10Button;
    [SerializeField] private Button aspect21x9Button;
    [SerializeField] private Button aspect4x3Button;

    private PlayerHealthState sandboxHealthState;
    private PlayerResourceState sandboxResourceState;
    private PlayerAbilityState sandboxAbilityState;
    private GearDisplayCatalog sandboxGearCatalog;
    private readonly List<GearDisplayDefinition> sandboxGearDefinitions = new List<GearDisplayDefinition>();
    private readonly List<GameObject> bossFixtureSources = new List<GameObject>();
    private readonly List<EnemyConfig> bossFixtureConfigs = new List<EnemyConfig>();
    private bool fixtureControlsWired;

    // -------------------------------------------------------------------------
    // IUIFlowHost
    //
    // The Sandbox has no GameManager, no scene transitions, and no post-transition lockout, so root
    // availability is simply "yes". UIFlowController still enforces its own rules on top: no second
    // root behind the first, and nothing opens mid-transition of its own state machine.
    // -------------------------------------------------------------------------

    public bool CanOpenPausingRoot => true;

    /// <summary>
    /// The Sandbox's fixture and developer panels need pointer/navigation input with no root open,
    /// so the UI action map stays live through close here. Production leaves it disabled.
    /// </summary>
    public bool KeepUiNavigationEnabledWhileClosed => true;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Runtime-created, isolated instances only — never SaveManager-registered, never the
        // mutable production assets under ScriptableObjects/Hero/.
        sandboxHealthState = ScriptableObject.CreateInstance<PlayerHealthState>();
        sandboxResourceState = ScriptableObject.CreateInstance<PlayerResourceState>();

        if (healthDisplay != null) healthDisplay.Configure(sandboxHealthState);
        if (resourceDisplay != null) resourceDisplay.Configure(sandboxResourceState);

        BuildGearFixtures();
        ConfigureFixtureControls();

        if (pauseMenuScreen != null) pauseMenuScreen.OptionsRequested += HandleOptionsRequested;
        if (optionsPreviewPanel != null) optionsPreviewPanel.CloseRequested += HandleOptionsPreviewClosed;

        ApplyHealthFixture_Full();
        ApplyResourceFixture_Partial();
    }

    /// <summary>
    /// Installed in Start, not Awake: <see cref="UIFlowController.Awake"/> resolves its action maps
    /// and disables UI navigation, and Start is the first point that is guaranteed to run after it
    /// regardless of scene ordering.
    /// </summary>
    private void Start()
    {
        uiFlow?.ConfigureHost(this);
    }

    /// <summary>
    /// Wires every fixture control. Idempotent by construction: each listener is removed before it
    /// is added, so repeated configuration can never make one click apply a fixture twice.
    /// </summary>
    public void ConfigureFixtureControls()
    {
        WireButton(healthDamageOneButton, ApplyHealthFixture_DamageOne);
        WireButton(healthHealOneButton, ApplyHealthFixture_HealOne);
        WireButton(healthFullButton, ApplyHealthFixture_Full);
        WireButton(healthDamagedButton, ApplyHealthFixture_Damaged);
        WireButton(healthBonusButton, ApplyHealthFixture_Bonus);
        WireButton(healthEmptyButton, ApplyHealthFixture_EmptyDeathFrame);
        WireButton(healthLargeCapacityButton, ApplyHealthFixture_LargeCapacity);

        WireButton(resourceEmptyButton, ApplyResourceFixture_Empty);
        WireButton(resourceAddButton, ApplyResourceFixture_AddOne);
        WireButton(resourcePartialButton, ApplyResourceFixture_Partial);
        WireButton(resourceFullButton, ApplyResourceFixture_Full);
        WireButton(resourceSpendButton, ApplyResourceFixture_SpendOne);
        WireButton(resourceClearButton, ApplyResourceFixture_Clear);
        WireButton(resourceZeroCapacityButton, ApplyResourceFixture_ZeroCapacity);

        WireButton(bossHiddenButton, ApplyBossFixture_Hidden);
        WireButton(bossSingleButton, ApplyBossFixture_SingleSource);
        WireButton(bossAggregateButton, ApplyBossFixture_AggregateSources);

        WireButton(openPauseButton, OpenPauseMenuPreview);
        WireButton(openGameplayMenuButton, OpenGameplayMenuPreview);

        WireButton(gearNoneButton, ApplyGearFixture_None);
        WireButton(gearSingleButton, ApplyGearFixture_Single);
        WireButton(gearMultipleButton, ApplyGearFixture_Multiple);
        WireButton(gearLiveUnlockButton, ApplyGearFixture_LiveUnlock);
        WireButton(gearMissingDefinitionButton, ApplyGearFixture_UnlockedWithNoDefinition);

        WireButton(aspect16x9Button, SetAspect16x9);
        WireButton(aspect16x10Button, SetAspect16x10);
        WireButton(aspect21x9Button, SetAspect21x9);
        WireButton(aspect4x3Button, SetAspect4x3);

        WireToggle(optionsPreviewToggle, SetOptionsPreviewEnabled);
        WireToggle(quitSeamToggle, SetQuitRequestSeamEnabled);

        fixtureControlsWired = true;
    }

    /// <summary>Editor/test accessor: whether fixture controls have been wired at least once.</summary>
    public bool FixtureControlsWired => fixtureControlsWired;

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        // Remove-then-add is what makes repeated configuration safe. A stale duplicate here is
        // exactly how "one Damage click removed two health" would come back.
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void WireToggle(Toggle toggle, UnityEngine.Events.UnityAction<bool> action)
    {
        if (toggle == null)
        {
            return;
        }

        toggle.onValueChanged.RemoveListener(action);
        toggle.onValueChanged.AddListener(action);
    }

    private void OnDestroy()
    {
        HideBossFixture();
        if (sandboxHealthState != null) Destroy(sandboxHealthState);
        if (sandboxResourceState != null) Destroy(sandboxResourceState);

        // Runtime-created Gear fixture data is Sandbox-only and must never outlive the Sandbox.
        if (sandboxAbilityState != null) Destroy(sandboxAbilityState);
        if (sandboxGearCatalog != null) Destroy(sandboxGearCatalog);
        for (int i = 0; i < sandboxGearDefinitions.Count; i++)
        {
            if (sandboxGearDefinitions[i] != null) Destroy(sandboxGearDefinitions[i]);
        }
        sandboxGearDefinitions.Clear();

        if (pauseMenuScreen != null) pauseMenuScreen.OptionsRequested -= HandleOptionsRequested;
        if (optionsPreviewPanel != null) optionsPreviewPanel.CloseRequested -= HandleOptionsPreviewClosed;
        if (uiFlow != null) uiFlow.ConfigureHost(null);
    }

    private void HandleOptionsRequested()
    {
        optionsPreviewPanel?.Open();
    }

    private void HandleOptionsPreviewClosed()
    {
        pauseMenuScreen?.RestoreOptionsSelection();
    }

    // -------------------------------------------------------------------------
    // Health fixtures
    //
    // Damage/Heal are incremental single-step actions on the isolated state. The named presets below
    // reset capacity first so they stay deterministic across repeated clicks.
    // -------------------------------------------------------------------------

    /// <summary>Removes exactly one health. Clamped at zero by <see cref="PlayerHealthState"/>.</summary>
    public void ApplyHealthFixture_DamageOne()
    {
        sandboxHealthState?.ApplyDamage(HealthStepPerClick);
    }

    /// <summary>Restores exactly one health. Clamped at maximum by <see cref="PlayerHealthState"/>.</summary>
    public void ApplyHealthFixture_HealOne()
    {
        sandboxHealthState?.Heal(HealthStepPerClick);
    }

    public void ApplyHealthFixture_Full()
    {
        sandboxHealthState.SetMaximumHealth(HealthFixtureMaximum, restoreToFull: true);
        sandboxHealthState.ClearBonusHealth();
    }

    public void ApplyHealthFixture_Damaged()
    {
        sandboxHealthState.SetMaximumHealth(HealthFixtureMaximum, restoreToFull: true);
        sandboxHealthState.ClearBonusHealth();
        sandboxHealthState.ApplyDamage(2);
    }

    public void ApplyHealthFixture_Bonus()
    {
        sandboxHealthState.SetMaximumHealth(HealthFixtureMaximum, restoreToFull: true);
        sandboxHealthState.ClearBonusHealth();
        sandboxHealthState.GrantBonusHealth(2);
    }

    public void ApplyHealthFixture_EmptyDeathFrame()
    {
        sandboxHealthState.SetMaximumHealth(HealthFixtureMaximum, restoreToFull: true);
        sandboxHealthState.ForceDeplete();
    }

    public void ApplyHealthFixture_LargeCapacity()
    {
        sandboxHealthState.SetMaximumHealth(9, restoreToFull: true);
        sandboxHealthState.ClearBonusHealth();
    }

    // -------------------------------------------------------------------------
    // Resource fixtures
    // -------------------------------------------------------------------------

    /// <summary>Resets capacity and empties the bar. Deterministic across repeated clicks.</summary>
    public void ApplyResourceFixture_Empty()
    {
        sandboxResourceState.SetMaximumParts(ResourceFixtureMaximum);
        sandboxResourceState.Clear();
    }

    /// <summary>Gains exactly one part. Clamped at capacity by <see cref="PlayerResourceState"/>.</summary>
    public void ApplyResourceFixture_AddOne()
    {
        if (sandboxResourceState == null) return;

        // Capacity zero would make Add silently do nothing forever, which reads as a broken button
        // rather than a clamp; restore the fixture capacity first.
        if (sandboxResourceState.MaximumParts <= 0)
        {
            sandboxResourceState.SetMaximumParts(ResourceFixtureMaximum);
        }

        sandboxResourceState.Gain(ResourceStepPerClick);
    }

    public void ApplyResourceFixture_Partial()
    {
        sandboxResourceState.SetMaximumParts(ResourceFixtureMaximum);
        sandboxResourceState.Clear();
        sandboxResourceState.Gain(6);
    }

    public void ApplyResourceFixture_Full()
    {
        sandboxResourceState.SetMaximumParts(ResourceFixtureMaximum);
        sandboxResourceState.Clear();
        sandboxResourceState.Gain(ResourceFixtureMaximum);
    }

    /// <summary>Spends exactly one part. Clamped at zero: an unaffordable spend is simply refused.</summary>
    public void ApplyResourceFixture_SpendOne()
    {
        sandboxResourceState?.TrySpend(ResourceStepPerClick);
    }

    /// <summary>
    /// Forfeits every current part while preserving capacity — the same path death takes. Distinct
    /// from Empty, which also resets capacity.
    /// </summary>
    public void ApplyResourceFixture_Clear()
    {
        sandboxResourceState?.Clear();
    }

    public void ApplyResourceFixture_ZeroCapacity()
    {
        sandboxResourceState.SetMaximumParts(0);
    }

    // -------------------------------------------------------------------------
    // Boss HUD fixtures
    // -------------------------------------------------------------------------

    public void ApplyBossFixture_Hidden()
    {
        HideBossFixture();
    }

    public void ApplyBossFixture_SingleSource()
    {
        HideBossFixture();
        EnemyHealthComponent source = CreateBossFixtureSource(40, 40);
        BossHudEventService.RequestShow(this, shortNameBossFixture, new[] { source });
    }

    public void ApplyBossFixture_AggregateSources()
    {
        HideBossFixture();
        EnemyHealthComponent a = CreateBossFixtureSource(30, 60);
        EnemyHealthComponent b = CreateBossFixtureSource(15, 40);
        BossHudEventService.RequestShow(this, longNameBossFixture, new[] { a, b });
    }

    private void HideBossFixture()
    {
        BossHudEventService.RequestHide(this);
        for (int i = 0; i < bossFixtureSources.Count; i++)
        {
            if (bossFixtureSources[i] != null) Destroy(bossFixtureSources[i]);
        }
        bossFixtureSources.Clear();

        // Runtime-created EnemyConfig ScriptableObjects are Sandbox-only fixture data, never a
        // production asset — they must not leak past the fixture that created them.
        for (int i = 0; i < bossFixtureConfigs.Count; i++)
        {
            if (bossFixtureConfigs[i] != null) Destroy(bossFixtureConfigs[i]);
        }
        bossFixtureConfigs.Clear();
    }

    private EnemyHealthComponent CreateBossFixtureSource(int currentHealth, int maxHealth)
    {
        GameObject go = new GameObject("SandboxBossFixtureSource");
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        EnemyStateBlackboard blackboard = go.AddComponent<EnemyStateBlackboard>();
        EnemyRecoil recoil = go.AddComponent<EnemyRecoil>();
        EnemyConfig config = ScriptableObject.CreateInstance<EnemyConfig>();
        config.maxHealth = maxHealth;
        recoil.Initialize(config, blackboard, rb);

        EnemyHealthComponent health = go.AddComponent<EnemyHealthComponent>();
        health.Initialize(config, blackboard, rb, recoil);

        int damage = maxHealth - Mathf.Clamp(currentHealth, 0, maxHealth);
        if (damage > 0)
        {
            health.ReceiveHeroAttack(new HeroAttackHit(gameObject, HeroAttackDirection.Side, damage, go.transform.position, Vector2.right));
        }

        bossFixtureSources.Add(go);
        bossFixtureConfigs.Add(config);
        return health;
    }

    // -------------------------------------------------------------------------
    // Root menu preview
    //
    // Every route goes through the Sandbox's UIFlowController, so opening and closing here runs the
    // production sequence: root exclusivity, UI-map mode, selection entry, and (in production) the
    // GameManager pause and Hero input suspend/resume. The Sandbox never calls a root screen's
    // Show/Hide itself — that bypass is what left the Pause root unclosable.
    // -------------------------------------------------------------------------

    public void OpenPauseMenuPreview() => RequestOpenRoot(UIRootKind.Pause);

    public void OpenGameplayMenuPreview() => RequestOpenRoot(UIRootKind.GameplayMenu);

    /// <summary>Closes whichever root is open through the normal flow sequence.</summary>
    public bool CloseActiveRootPreview()
    {
        if (uiFlow == null)
        {
            LogMissingFlowController();
            return false;
        }

        return uiFlow.RequestCloseRoot();
    }

    private bool RequestOpenRoot(UIRootKind kind)
    {
        if (uiFlow == null)
        {
            LogMissingFlowController();
            return false;
        }

        return uiFlow.RequestOpenRoot(kind);
    }

    private void LogMissingFlowController()
    {
        Debug.LogError(
            "[UISandboxController] No UIFlowController is assigned, so root open/close cannot be " +
            "previewed. Assign the Sandbox's own flow controller — the Sandbox must never drive a " +
            "root screen's Show/Hide directly.",
            this);
    }

    public void SetOptionsPreviewEnabled(bool enabled)
    {
        pauseMenuScreen?.SetOptionsPreviewEnabled(enabled);
    }

    public void SetQuitRequestSeamEnabled(bool enabled)
    {
        pauseMenuScreen?.SetQuitRequestSeamEnabled(enabled);
    }

    /// <summary>
    /// Tab selection is the Gameplay Menu's own concern, not root flow, so it is requested directly
    /// on the screen. It is refused while the root is closed.
    /// </summary>
    public void SelectGameplayMenuTab(GameplayMenuTabId tabId)
    {
        gameplayMenuScreen?.SelectTab(tabId);
    }

    // -------------------------------------------------------------------------
    // Gear fixtures
    //
    // Fixture definitions and their catalogue are runtime-created ScriptableObjects. They are
    // never project assets, never referenced by production prefabs, and never persisted. The
    // production GearDisplayCatalog stays empty until real physical Gear identities are approved.
    // -------------------------------------------------------------------------

    private void BuildGearFixtures()
    {
        sandboxAbilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        sandboxAbilityState.ResetToDefaults();

        sandboxGearCatalog = ScriptableObject.CreateInstance<GearDisplayCatalog>();

        sandboxGearDefinitions.Add(CreateGearFixture(
            "sandbox_gear_dash", AbilityId.Dash, "Traveller's Coil",
            "Sandbox fixture",
            "A wound spring worn at the hip. Releases in a short, sharp burst of ground speed.",
            "Wound tight by someone in a hurry."));

        sandboxGearDefinitions.Add(CreateGearFixture(
            "sandbox_gear_wallcling", AbilityId.WallCling, "Bramble Cord",
            "Sandbox fixture",
            "Barbed cording that bites into rough stone, holding fast against a wall.",
            "Smells faintly of sap and rust."));

        sandboxGearDefinitions.Add(CreateGearFixture(
            "sandbox_gear_doublejump", AbilityId.DoubleJump, "Hollow Lantern",
            "Sandbox fixture",
            "Light enough to lift you a second time, if you swing it right.",
            "Whatever burned in it went out long ago."));

        sandboxGearCatalog.SetDefinitions(sandboxGearDefinitions);

        gearScreen?.Configure(sandboxAbilityState, sandboxGearCatalog);
        ApplyGearFixture_None();
    }

    private static GearDisplayDefinition CreateGearFixture(
        string key, AbilityId ability, string name, string category, string description, string flavour)
    {
        GearDisplayDefinition definition = ScriptableObject.CreateInstance<GearDisplayDefinition>();
        definition.name = key;
        definition.SetFixtureContent(key, ability, name, category, description, flavour);
        return definition;
    }

    /// <summary>True-new-game Gear: nothing acquired, authored empty state.</summary>
    public void ApplyGearFixture_None()
    {
        if (sandboxAbilityState == null) return;
        sandboxAbilityState.ResetToDefaults();
        gearScreen?.Rebuild();
    }

    public void ApplyGearFixture_Single()
    {
        if (sandboxAbilityState == null) return;
        sandboxAbilityState.ResetToDefaults();
        sandboxAbilityState.Unlock(AbilityId.Dash);
        gearScreen?.Rebuild();
    }

    public void ApplyGearFixture_Multiple()
    {
        if (sandboxAbilityState == null) return;
        sandboxAbilityState.ResetToDefaults();
        sandboxAbilityState.Unlock(AbilityId.Dash);
        sandboxAbilityState.Unlock(AbilityId.WallCling);
        sandboxAbilityState.Unlock(AbilityId.DoubleJump);
        gearScreen?.Rebuild();
    }

    /// <summary>
    /// Unlocks one more ability without an explicit rebuild, exercising the live
    /// <c>AbilityChanged</c> path while the tab is open.
    /// </summary>
    public void ApplyGearFixture_LiveUnlock()
    {
        if (sandboxAbilityState == null) return;
        if (!sandboxAbilityState.IsUnlocked(AbilityId.Dash)) { sandboxAbilityState.Unlock(AbilityId.Dash); return; }
        if (!sandboxAbilityState.IsUnlocked(AbilityId.WallCling)) { sandboxAbilityState.Unlock(AbilityId.WallCling); return; }
        sandboxAbilityState.Unlock(AbilityId.DoubleJump);
    }

    /// <summary>
    /// Unlocks an ability the fixture catalogue has no definition for, so the omit-and-log-once
    /// content-gap path can be reviewed.
    /// </summary>
    public void ApplyGearFixture_UnlockedWithNoDefinition()
    {
        if (sandboxAbilityState == null) return;
        sandboxAbilityState.Unlock(AbilityId.SpiritCast);
    }

    // -------------------------------------------------------------------------
    // Aspect ratio / safe-area preview
    // -------------------------------------------------------------------------

    public void SetAspectPreview(float width, float height)
    {
        if (aspectFrame == null) return;
        aspectFrame.sizeDelta = new Vector2(width, height);
    }

    public void SetAspect16x9() => SetAspectPreview(1920, 1080);
    public void SetAspect16x10() => SetAspectPreview(1920, 1200);
    public void SetAspect21x9() => SetAspectPreview(2560, 1080);
    public void SetAspect4x3() => SetAspectPreview(1600, 1200);
}
