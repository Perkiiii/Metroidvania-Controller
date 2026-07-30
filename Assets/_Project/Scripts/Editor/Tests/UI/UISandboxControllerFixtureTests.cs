using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Focused regression coverage for the Sandbox's isolated health/resource fixture controls.
/// These tests exercise the actual Button events so duplicate runtime listeners are observable.
/// </summary>
public sealed class UISandboxControllerFixtureTests
{
    private GameObject controllerObject;
    private UISandboxController controller;
    private PlayerHealthState healthState;
    private PlayerResourceState resourceState;
    private HealthDisplay healthDisplay;
    private ResourceDisplay resourceDisplay;
    private ResourceBarView resourceBar;
    private Button damageButton;
    private Button healButton;
    private Button resourceEmptyButton;
    private Button resourceAddButton;
    private Button resourcePartialButton;
    private Button resourceFullButton;
    private Button resourceSpendButton;
    private Button resourceClearButton;

    [SetUp]
    public void SetUp()
    {
        controllerObject = new GameObject("UISandboxController Test");
        controller = controllerObject.AddComponent<UISandboxController>();

        healthState = ScriptableObject.CreateInstance<PlayerHealthState>();
        resourceState = ScriptableObject.CreateInstance<PlayerResourceState>();
        SetPrivate(controller, "sandboxHealthState", healthState);
        SetPrivate(controller, "sandboxResourceState", resourceState);

        healthDisplay = new GameObject("HealthDisplay Test").AddComponent<HealthDisplay>();
        resourceDisplay = new GameObject("ResourceDisplay Test").AddComponent<ResourceDisplay>();
        resourceBar = new GameObject("ResourceBar Test").AddComponent<ResourceBarView>();
        resourceBar.ConfigureDurations(0f, 0f, 0f);
        SetPrivate(resourceDisplay, "barView", resourceBar);
        healthDisplay.Configure(healthState);
        resourceDisplay.Configure(resourceState);

        damageButton = NewButton("Damage -1");
        healButton = NewButton("Heal +1");
        resourceEmptyButton = NewButton("Resource Empty");
        resourceAddButton = NewButton("Resource Add +1");
        resourcePartialButton = NewButton("Resource Partial");
        resourceFullButton = NewButton("Resource Full");
        resourceSpendButton = NewButton("Resource Spend -1");
        resourceClearButton = NewButton("Resource Clear");

        SetPrivate(controller, "healthDamageOneButton", damageButton);
        SetPrivate(controller, "healthHealOneButton", healButton);
        SetPrivate(controller, "resourceEmptyButton", resourceEmptyButton);
        SetPrivate(controller, "resourceAddButton", resourceAddButton);
        SetPrivate(controller, "resourcePartialButton", resourcePartialButton);
        SetPrivate(controller, "resourceFullButton", resourceFullButton);
        SetPrivate(controller, "resourceSpendButton", resourceSpendButton);
        SetPrivate(controller, "resourceClearButton", resourceClearButton);

        controller.ConfigureFixtureControls();
        controller.ApplyHealthFixture_Full();
        controller.ApplyResourceFixture_Partial();
    }

    [TearDown]
    public void TearDown()
    {
        SetPrivate(controller, "sandboxHealthState", null);
        SetPrivate(controller, "sandboxResourceState", null);

        Object.DestroyImmediate(healthState);
        Object.DestroyImmediate(resourceState);
        Object.DestroyImmediate(healthDisplay.gameObject);
        Object.DestroyImmediate(resourceDisplay.gameObject);
        Object.DestroyImmediate(resourceBar.gameObject);
        Object.DestroyImmediate(damageButton.gameObject);
        Object.DestroyImmediate(healButton.gameObject);
        Object.DestroyImmediate(resourceEmptyButton.gameObject);
        Object.DestroyImmediate(resourceAddButton.gameObject);
        Object.DestroyImmediate(resourcePartialButton.gameObject);
        Object.DestroyImmediate(resourceFullButton.gameObject);
        Object.DestroyImmediate(resourceSpendButton.gameObject);
        Object.DestroyImmediate(resourceClearButton.gameObject);
        Object.DestroyImmediate(controllerObject);
    }

    [Test]
    public void DamageAndHealButtonsMutateExactlyOneHealthPerClickAndClamp()
    {
        damageButton.onClick.Invoke();
        Assert.That(healthState.CurrentHealth, Is.EqualTo(4));
        Assert.That(healthDisplay.CurrentHealth, Is.EqualTo(4));

        damageButton.onClick.Invoke();
        Assert.That(healthState.CurrentHealth, Is.EqualTo(3));
        Assert.That(healthDisplay.CurrentHealth, Is.EqualTo(3));

        for (int i = 0; i < 10; i++) damageButton.onClick.Invoke();
        Assert.That(healthState.CurrentHealth, Is.Zero);
        Assert.That(healthDisplay.CurrentHealth, Is.Zero);

        healButton.onClick.Invoke();
        Assert.That(healthState.CurrentHealth, Is.EqualTo(1));
        Assert.That(healthDisplay.CurrentHealth, Is.EqualTo(1));

        for (int i = 0; i < 10; i++) healButton.onClick.Invoke();
        Assert.That(healthState.CurrentHealth, Is.EqualTo(healthState.MaximumHealth));
        Assert.That(healthDisplay.CurrentHealth, Is.EqualTo(healthState.MaximumHealth));
    }

    [Test]
    public void RepeatedConfigurationAndEnableCyclesDoNotDuplicateHealthListeners()
    {
        controller.ConfigureFixtureControls();
        controller.ConfigureFixtureControls();
        controllerObject.SetActive(false);
        controllerObject.SetActive(true);

        damageButton.onClick.Invoke();
        Assert.That(healthState.CurrentHealth, Is.EqualTo(4),
            "One click after repeated configuration/enable cycles must still apply one mutation.");
    }

    [Test]
    public void EveryResourceControlMutatesTheStateObservedByTheSharedDisplay()
    {
        resourceEmptyButton.onClick.Invoke();
        AssertResource(0, 10, 0f);

        resourceAddButton.onClick.Invoke();
        AssertResource(1, 10, 0.1f);

        resourcePartialButton.onClick.Invoke();
        AssertResource(6, 10, 0.6f);

        resourceFullButton.onClick.Invoke();
        AssertResource(10, 10, 1f);

        resourceSpendButton.onClick.Invoke();
        AssertResource(9, 10, 0.9f);

        resourceClearButton.onClick.Invoke();
        AssertResource(0, 10, 0f);
    }

    [Test]
    public void RepeatedConfigurationAndEnableCyclesDoNotDuplicateResourceListeners()
    {
        controller.ConfigureFixtureControls();
        controller.ConfigureFixtureControls();
        controllerObject.SetActive(false);
        controllerObject.SetActive(true);

        resourceEmptyButton.onClick.Invoke();
        resourceAddButton.onClick.Invoke();
        AssertResource(1, 10, 0.1f);
    }

    [Test]
    public void BonusHealthFixtureIsDeterministicAcrossRepeatedClicks()
    {
        controller.ApplyHealthFixture_Bonus();
        controller.ApplyHealthFixture_Bonus();
        controller.ApplyHealthFixture_Bonus();

        Assert.That(healthState.BonusHealth, Is.EqualTo(2));
    }

    [Test]
    public void ResourcePresetsAreDeterministicAcrossRepeatedClicks()
    {
        controller.ApplyResourceFixture_Full();
        controller.ApplyResourceFixture_Full();
        AssertResource(10, 10, 1f);

        controller.ApplyResourceFixture_Partial();
        controller.ApplyResourceFixture_Partial();
        AssertResource(6, 10, 0.6f);

        controller.ApplyResourceFixture_Empty();
        controller.ApplyResourceFixture_Empty();
        AssertResource(0, 10, 0f);
    }

    private void AssertResource(int current, int maximum, float fill)
    {
        Assert.That(resourceState.CurrentParts, Is.EqualTo(current));
        Assert.That(resourceState.MaximumParts, Is.EqualTo(maximum));
        Assert.That(resourceDisplay.CurrentParts, Is.EqualTo(current));
        Assert.That(resourceDisplay.MaximumParts, Is.EqualTo(maximum));
        Assert.That(resourceDisplay.FillAmount01, Is.EqualTo(fill).Within(0.001f));
        Assert.That(resourceBar.FillAmount01, Is.EqualTo(fill).Within(0.001f));
    }

    private static Button NewButton(string name)
    {
        return new GameObject(name).AddComponent<Button>();
    }

    private static void SetPrivate(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        field.SetValue(target, value);
    }
}
