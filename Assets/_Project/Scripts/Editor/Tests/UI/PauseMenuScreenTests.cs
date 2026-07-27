using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Package A1 correction-pass coverage for <see cref="PauseMenuScreen"/>: production Options/Quit
/// gating (Findings #4/#5), dynamic navigation rebuilding that skips disabled controls
/// (Finding #6), and the modal-first Pause behavior's selection restoration (Finding #3). Builds a
/// minimal flat UGUI hierarchy rather than the full production prefab, since the screen's own
/// Awake/RefreshAvailabilityAndNavigation logic is what is under test here.
/// </summary>
public sealed class PauseMenuScreenTests
{
    private GameObject screenObject;
    private PauseMenuScreen screen;
    private GameObject eventSystemObject;
    private EventSystem eventSystem;
    private Button continueButton;
    private Button optionsButton;
    private Button quitButton;
    private GameObject modalObject;
    private ConfirmationModal modal;
    private Button confirmButton;
    private Button cancelButton;

    [SetUp]
    public void SetUp()
    {
        eventSystemObject = new GameObject("EventSystem Test");
        eventSystem = eventSystemObject.AddComponent<EventSystem>();

        continueButton = NewButton("Continue");
        optionsButton = NewButton("Options");
        quitButton = NewButton("Quit");
        confirmButton = NewButton("Confirm");
        cancelButton = NewButton("Cancel");

        modalObject = new GameObject("ConfirmationModal Test");
        modal = modalObject.AddComponent<ConfirmationModal>();
        SetPrivateField(modal, "visualRoot", modalObject);
        SetPrivateField(modal, "confirmButton", confirmButton);
        SetPrivateField(modal, "cancelButton", cancelButton);
        SetPrivateField(modal, "eventSystem", eventSystem);
        InvokePrivate(modal, "Awake");
        modalObject.SetActive(false); // Modal panels default closed, matching the production prefab.

        screenObject = new GameObject("PauseMenuScreen Test");
        screen = screenObject.AddComponent<PauseMenuScreen>();
        SetPrivateField(screen, "visualRoot", screenObject);
        SetPrivateField(screen, "continueButton", continueButton);
        SetPrivateField(screen, "optionsButton", optionsButton);
        SetPrivateField(screen, "quitButton", quitButton);
        SetPrivateField(screen, "quitConfirmationModal", modal);
        SetPrivateField(screen, "eventSystem", eventSystem);
        InvokePrivate(screen, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(screenObject);
        Object.DestroyImmediate(modalObject);
        Object.DestroyImmediate(continueButton.gameObject);
        Object.DestroyImmediate(optionsButton.gameObject);
        Object.DestroyImmediate(quitButton.gameObject);
        Object.DestroyImmediate(confirmButton.gameObject);
        Object.DestroyImmediate(cancelButton.gameObject);
        Object.DestroyImmediate(eventSystemObject);
    }

    private static Button NewButton(string name)
    {
        GameObject go = new GameObject(name);
        return go.AddComponent<Button>();
    }

    [Test]
    public void ProductionDefaultsGateOptionsAndQuitNonInteractableContinueOnly()
    {
        Assert.That(continueButton.interactable, Is.True);
        Assert.That(optionsButton.interactable, Is.False);
        Assert.That(quitButton.interactable, Is.False);

        Assert.That(continueButton.navigation.selectOnDown, Is.Null);
        Assert.That(optionsButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));
        Assert.That(quitButton.navigation.mode, Is.EqualTo(Navigation.Mode.None));
    }

    [Test]
    public void OptionsEnabledQuitDisabled_NavigationChainsContinueToOptionsOnly()
    {
        screen.SetOptionsPreviewEnabled(true);

        Assert.That(optionsButton.interactable, Is.True);
        Assert.That(quitButton.interactable, Is.False);
        Assert.That(continueButton.navigation.selectOnDown, Is.SameAs(optionsButton));
        Assert.That(optionsButton.navigation.selectOnUp, Is.SameAs(continueButton));
        Assert.That(optionsButton.navigation.selectOnDown, Is.Null);
    }

    [Test]
    public void QuitEnabledOptionsDisabled_NavigationChainsContinueToQuitOnly()
    {
        screen.SetQuitRequestSeamEnabled(true);

        Assert.That(quitButton.interactable, Is.True);
        Assert.That(optionsButton.interactable, Is.False);
        Assert.That(continueButton.navigation.selectOnDown, Is.SameAs(quitButton));
        Assert.That(quitButton.navigation.selectOnUp, Is.SameAs(continueButton));
    }

    [Test]
    public void BothEnabled_NavigationChainsContinueOptionsQuit()
    {
        screen.SetOptionsPreviewEnabled(true);
        screen.SetQuitRequestSeamEnabled(true);

        Assert.That(continueButton.navigation.selectOnDown, Is.SameAs(optionsButton));
        Assert.That(optionsButton.navigation.selectOnUp, Is.SameAs(continueButton));
        Assert.That(optionsButton.navigation.selectOnDown, Is.SameAs(quitButton));
        Assert.That(quitButton.navigation.selectOnUp, Is.SameAs(optionsButton));
        Assert.That(quitButton.navigation.selectOnDown, Is.Null);
    }

    [Test]
    public void DisablingCurrentlySelectedOptionsRestoresSelectionToContinue()
    {
        screen.SetOptionsPreviewEnabled(true);
        eventSystem.SetSelectedGameObject(optionsButton.gameObject);

        screen.SetOptionsPreviewEnabled(false);

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(continueButton.gameObject));
    }

    [Test]
    public void DisablingCurrentlySelectedQuitRestoresSelectionToContinue()
    {
        screen.SetQuitRequestSeamEnabled(true);
        eventSystem.SetSelectedGameObject(quitButton.gameObject);

        screen.SetQuitRequestSeamEnabled(false);

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(continueButton.gameObject));
    }

    [Test]
    public void NavigationNeverPointsToANonInteractableControlAcrossAllCombinations()
    {
        bool[] states = { false, true };
        foreach (bool optionsEnabled in states)
        {
            foreach (bool quitEnabled in states)
            {
                screen.SetOptionsPreviewEnabled(optionsEnabled);
                screen.SetQuitRequestSeamEnabled(quitEnabled);

                AssertNavigationTargetsAreInteractable(continueButton);
                AssertNavigationTargetsAreInteractable(optionsButton);
                AssertNavigationTargetsAreInteractable(quitButton);
            }
        }
    }

    private static void AssertNavigationTargetsAreInteractable(Button button)
    {
        Navigation nav = button.navigation;
        if (nav.selectOnUp != null)
        {
            Assert.That(nav.selectOnUp.interactable, Is.True, button.name + ".selectOnUp must never target a non-interactable control.");
        }

        if (nav.selectOnDown != null)
        {
            Assert.That(nav.selectOnDown.interactable, Is.True, button.name + ".selectOnDown must never target a non-interactable control.");
        }
    }

    [Test]
    public void QuitClickedWhileSeamDisabledDoesNotOpenModal()
    {
        InvokePrivate(screen, "HandleQuitClicked");

        Assert.That(modal.IsOpen, Is.False);
    }

    [Test]
    public void QuitClickedWhileSeamEnabledOpensModal()
    {
        screen.SetQuitRequestSeamEnabled(true);

        InvokePrivate(screen, "HandleQuitClicked");

        Assert.That(modal.IsOpen, Is.True);
    }

    [Test]
    public void OptionsClickedWhilePreviewDisabledDoesNotFireEvent()
    {
        int firedCount = 0;
        screen.OptionsRequested += () => firedCount++;

        InvokePrivate(screen, "HandleOptionsClicked");

        Assert.That(firedCount, Is.EqualTo(0));
    }

    [Test]
    public void OptionsClickedWhilePreviewEnabledFiresEventExactlyOnce()
    {
        int firedCount = 0;
        screen.OptionsRequested += () => firedCount++;
        screen.SetOptionsPreviewEnabled(true);

        InvokePrivate(screen, "HandleOptionsClicked");

        Assert.That(firedCount, Is.EqualTo(1));
    }

    [Test]
    public void RestoreOptionsSelectionSelectsOptionsWhenInteractable()
    {
        screen.SetOptionsPreviewEnabled(true);
        eventSystem.SetSelectedGameObject(null);

        screen.RestoreOptionsSelection();

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(optionsButton.gameObject));
    }

    [Test]
    public void RestoreOptionsSelectionFallsBackToContinueWhenOptionsDisabled()
    {
        eventSystem.SetSelectedGameObject(null);

        screen.RestoreOptionsSelection();

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(continueButton.gameObject));
    }

    [Test]
    public void HandleBackInternallyWhileModalOpenClosesOnlyModalKeepsRootOpenAndRestoresQuitSelection()
    {
        screen.SetQuitRequestSeamEnabled(true);
        screen.Show();
        InvokePrivate(screen, "HandleQuitClicked");
        Assert.That(modal.IsOpen, Is.True);

        bool handled = screen.HandleBackInternally();

        Assert.That(handled, Is.True, "PauseMenuScreen must report it handled Back internally by closing only the modal.");
        Assert.That(modal.IsOpen, Is.False);
        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(quitButton.gameObject));
        Assert.That(screenObject.activeSelf, Is.True, "The root itself must remain open/visible; only the modal closes.");
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo fi = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(fi, Is.Not.Null, "Field not found: " + name);
        fi.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string name)
    {
        MethodInfo mi = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(mi, Is.Not.Null, "Method not found: " + name);
        mi.Invoke(target, null);
    }
}
