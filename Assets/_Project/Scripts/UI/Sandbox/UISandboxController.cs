using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Development-only UI Sandbox controller. Creates isolated runtime state, wires the real shared
/// HUD/menu presentation components to it, and exposes named fixture presets. Never touches
/// production save files, production ScriptableObject assets, or persistent production managers.
/// See Docs/FeatureSpecs/UISandbox.md.
/// </summary>
public sealed class UISandboxController : MonoBehaviour
{
    [Header("HUD presentation (isolated state)")]
    [SerializeField] private HealthDisplay healthDisplay;
    [SerializeField] private ResourceDisplay resourceDisplay;
    [SerializeField] private BossHealthDisplay bossHealthDisplay;

    [Header("Boss fixture identities (Sandbox-only assets, never production)")]
    [SerializeField] private BossEncounterDefinition shortNameBossFixture;
    [SerializeField] private BossEncounterDefinition longNameBossFixture;

    [Header("Menu presentation")]
    [SerializeField] private PauseMenuScreen pauseMenuScreen;
    [SerializeField] private EventSystem sandboxEventSystem;
    [SerializeField] private SandboxOptionsPreviewPanel optionsPreviewPanel;

    [Header("Aspect / safe-area preview")]
    [SerializeField] private RectTransform aspectFrame;
    [SerializeField] private RectTransform safeAreaGuide;

    [Header("Fixture buttons (wired in Awake; optional)")]
    [SerializeField] private Button healthFullButton;
    [SerializeField] private Button healthDamagedButton;
    [SerializeField] private Button healthBonusButton;
    [SerializeField] private Button healthEmptyButton;
    [SerializeField] private Button healthLargeCapacityButton;
    [SerializeField] private Button resourceEmptyButton;
    [SerializeField] private Button resourcePartialButton;
    [SerializeField] private Button resourceFullButton;
    [SerializeField] private Button resourceZeroCapacityButton;
    [SerializeField] private Button bossHiddenButton;
    [SerializeField] private Button bossSingleButton;
    [SerializeField] private Button bossAggregateButton;
    [SerializeField] private Button openPauseButton;
    [SerializeField] private Button closePauseButton;
    [SerializeField] private Toggle optionsPreviewToggle;
    [SerializeField] private Toggle quitSeamToggle;
    [SerializeField] private Button aspect16x9Button;
    [SerializeField] private Button aspect16x10Button;
    [SerializeField] private Button aspect21x9Button;
    [SerializeField] private Button aspect4x3Button;

    private PlayerHealthState sandboxHealthState;
    private PlayerResourceState sandboxResourceState;
    private readonly List<GameObject> bossFixtureSources = new List<GameObject>();
    private readonly List<EnemyConfig> bossFixtureConfigs = new List<EnemyConfig>();

    private void Awake()
    {
        // Runtime-created, isolated instances only — never SaveManager-registered, never the
        // mutable production assets under ScriptableObjects/Hero/.
        sandboxHealthState = ScriptableObject.CreateInstance<PlayerHealthState>();
        sandboxResourceState = ScriptableObject.CreateInstance<PlayerResourceState>();

        if (healthDisplay != null) healthDisplay.Configure(sandboxHealthState);
        if (resourceDisplay != null) resourceDisplay.Configure(sandboxResourceState);

        WireButton(healthFullButton, ApplyHealthFixture_Full);
        WireButton(healthDamagedButton, ApplyHealthFixture_Damaged);
        WireButton(healthBonusButton, ApplyHealthFixture_Bonus);
        WireButton(healthEmptyButton, ApplyHealthFixture_EmptyDeathFrame);
        WireButton(healthLargeCapacityButton, ApplyHealthFixture_LargeCapacity);
        WireButton(resourceEmptyButton, ApplyResourceFixture_Empty);
        WireButton(resourcePartialButton, ApplyResourceFixture_Partial);
        WireButton(resourceFullButton, ApplyResourceFixture_Full);
        WireButton(resourceZeroCapacityButton, ApplyResourceFixture_ZeroCapacity);
        WireButton(bossHiddenButton, ApplyBossFixture_Hidden);
        WireButton(bossSingleButton, ApplyBossFixture_SingleSource);
        WireButton(bossAggregateButton, ApplyBossFixture_AggregateSources);
        WireButton(openPauseButton, OpenPauseMenuPreview);
        WireButton(closePauseButton, ClosePauseMenuPreview);
        WireButton(aspect16x9Button, SetAspect16x9);
        WireButton(aspect16x10Button, SetAspect16x10);
        WireButton(aspect21x9Button, SetAspect21x9);
        WireButton(aspect4x3Button, SetAspect4x3);
        if (optionsPreviewToggle != null) optionsPreviewToggle.onValueChanged.AddListener(SetOptionsPreviewEnabled);
        if (quitSeamToggle != null) quitSeamToggle.onValueChanged.AddListener(SetQuitRequestSeamEnabled);

        if (pauseMenuScreen != null) pauseMenuScreen.OptionsRequested += HandleOptionsRequested;
        if (optionsPreviewPanel != null) optionsPreviewPanel.CloseRequested += HandleOptionsPreviewClosed;

        ApplyHealthFixture_Full();
        ApplyResourceFixture_Partial();
    }

    private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }

    private void OnDestroy()
    {
        HideBossFixture();
        if (sandboxHealthState != null) Destroy(sandboxHealthState);
        if (sandboxResourceState != null) Destroy(sandboxResourceState);

        if (pauseMenuScreen != null) pauseMenuScreen.OptionsRequested -= HandleOptionsRequested;
        if (optionsPreviewPanel != null) optionsPreviewPanel.CloseRequested -= HandleOptionsPreviewClosed;
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
    // -------------------------------------------------------------------------

    public void ApplyHealthFixture_Full()
    {
        sandboxHealthState.SetMaximumHealth(5, restoreToFull: true);
        sandboxHealthState.ClearBonusHealth();
    }

    public void ApplyHealthFixture_Damaged()
    {
        sandboxHealthState.SetMaximumHealth(5, restoreToFull: true);
        sandboxHealthState.ClearBonusHealth();
        sandboxHealthState.ApplyDamage(2);
    }

    public void ApplyHealthFixture_Bonus()
    {
        sandboxHealthState.SetMaximumHealth(5, restoreToFull: true);
        sandboxHealthState.ClearBonusHealth();
        sandboxHealthState.GrantBonusHealth(2);
    }

    public void ApplyHealthFixture_EmptyDeathFrame()
    {
        sandboxHealthState.SetMaximumHealth(5, restoreToFull: true);
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

    public void ApplyResourceFixture_Empty()
    {
        sandboxResourceState.SetMaximumParts(10);
        sandboxResourceState.Clear();
    }

    public void ApplyResourceFixture_Partial()
    {
        sandboxResourceState.SetMaximumParts(10);
        sandboxResourceState.Clear();
        sandboxResourceState.Gain(6);
    }

    public void ApplyResourceFixture_Full()
    {
        sandboxResourceState.SetMaximumParts(10);
        sandboxResourceState.Clear();
        sandboxResourceState.Gain(10);
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
    // Menu preview
    // -------------------------------------------------------------------------

    public void OpenPauseMenuPreview()
    {
        if (pauseMenuScreen == null) return;
        pauseMenuScreen.Show();
        if (sandboxEventSystem != null)
        {
            UISelectionUtility.Select(sandboxEventSystem, pauseMenuScreen.FirstSelection);
        }
    }

    public void ClosePauseMenuPreview()
    {
        pauseMenuScreen?.Hide();
    }

    public void SetOptionsPreviewEnabled(bool enabled)
    {
        pauseMenuScreen?.SetOptionsPreviewEnabled(enabled);
    }

    public void SetQuitRequestSeamEnabled(bool enabled)
    {
        pauseMenuScreen?.SetQuitRequestSeamEnabled(enabled);
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
