using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Package A2.1 coverage for the five-tab Gameplay Menu root: fixed registration order, runtime tab
/// memory, selection resolution and per-tab memory, Previous/Next cycling, Back delegation, and
/// authoring diagnostics.
///
/// Uses the project's established EditMode convention (see UIFlowControllerTests): components are
/// added directly and driven through the public composition seam
/// (<see cref="GameplayMenuScreen.Configure"/>) rather than relying on Awake, which Unity does not
/// invoke for AddComponent in Edit Mode. Real Input System cycling and real click routing are
/// covered by GameplayMenuPlayModeTests instead.
/// </summary>
public sealed class GameplayMenuScreenTests
{
    /// <summary>Minimal tab presenter: a content child that activates/deactivates like a real tab.</summary>
    private sealed class FakeTab : MonoBehaviour, IGameplayMenuTab
    {
        public GameObject Content;
        public int ShowCount;
        public int HideCount;
        public bool BackResult;
        public bool ModalOpen;
        public Selectable First;

        public Selectable FirstSelection => First;
        public bool HasOpenModal => ModalOpen;

        public void Show()
        {
            ShowCount++;
            if (Content != null) Content.SetActive(true);
        }

        public void Hide()
        {
            HideCount++;
            if (Content != null) Content.SetActive(false);
        }

        public bool HandleBackInternally() => BackResult;
    }

    private GameObject screenObject;
    private GameplayMenuScreen screen;
    private GameObject eventSystemObject;
    private EventSystem eventSystem;
    private GameObject visualRoot;
    private Button closeButton;
    private readonly List<GameObject> spawned = new List<GameObject>();
    private GameplayMenuTabButton[] tabButtons;
    private FakeTab[] tabViews;

    [SetUp]
    public void SetUp()
    {
        eventSystemObject = NewObject("EventSystem Test");
        eventSystem = eventSystemObject.AddComponent<EventSystem>();

        visualRoot = NewObject("VisualRoot Test");
        closeButton = NewObject("CloseButton Test").AddComponent<Button>();

        tabButtons = new GameplayMenuTabButton[GameplayMenuScreen.FixedTabOrder.Length];
        tabViews = new FakeTab[GameplayMenuScreen.FixedTabOrder.Length];

        screenObject = NewObject("GameplayMenuScreen Test");
        screen = screenObject.AddComponent<GameplayMenuScreen>();

        List<GameplayMenuTabRegistration> registrations = new List<GameplayMenuTabRegistration>();
        for (int i = 0; i < GameplayMenuScreen.FixedTabOrder.Length; i++)
        {
            GameplayMenuTabId id = GameplayMenuScreen.FixedTabOrder[i];
            tabButtons[i] = CreateTabButton(id.ToString());
            tabViews[i] = CreateTabView(id.ToString());
            registrations.Add(new GameplayMenuTabRegistration(id, tabButtons[i], tabViews[i], null, id.ToString()));
        }

        screen.Configure(eventSystem, visualRoot, closeButton, registrations);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
        }

        spawned.Clear();
    }

    private GameObject NewObject(string name)
    {
        GameObject go = new GameObject(name);
        spawned.Add(go);
        return go;
    }

    private GameplayMenuTabButton CreateTabButton(string name)
    {
        GameObject go = NewObject("Tab_" + name);
        Button button = go.AddComponent<Button>();
        GameplayMenuTabButton tabButton = go.AddComponent<GameplayMenuTabButton>();
        SetPrivate(tabButton, "button", button);
        return tabButton;
    }

    private FakeTab CreateTabView(string name)
    {
        GameObject go = NewObject(name + "Tab");
        FakeTab view = go.AddComponent<FakeTab>();

        GameObject content = new GameObject("Content");
        content.transform.SetParent(go.transform, false);
        content.SetActive(false);
        view.Content = content;
        return view;
    }

    /// <summary>Adds a selectable inside a tab's content so per-tab selection memory can be exercised.</summary>
    private Selectable AddContentSelectable(int tabIndex, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(tabViews[tabIndex].Content.transform, false);
        Selectable selectable = go.AddComponent<Button>();
        tabViews[tabIndex].First = selectable;
        return selectable;
    }

    private static void SetPrivate(object target, string field, object value)
    {
        FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, "Field not found: " + field);
        info.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string method)
    {
        MethodInfo info = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, "Method not found: " + method);
        info.Invoke(target, null);
    }

    private static int IndexOf(GameplayMenuTabId id)
    {
        for (int i = 0; i < GameplayMenuScreen.FixedTabOrder.Length; i++)
        {
            if (GameplayMenuScreen.FixedTabOrder[i] == id) return i;
        }

        return -1;
    }

    // -------------------------------------------------------------------------
    // Fixed order and visibility
    // -------------------------------------------------------------------------

    [Test]
    public void FixedTabOrderIsTheFiveConfirmedTabs()
    {
        Assert.That(GameplayMenuScreen.FixedTabOrder, Is.EqualTo(new[]
        {
            GameplayMenuTabId.Gear,
            GameplayMenuTabId.CombatLoadout,
            GameplayMenuTabId.Satchel,
            GameplayMenuTabId.FieldNotes,
            GameplayMenuTabId.Map
        }));
    }

    [Test]
    public void GameplayMenuTabIdsRemainStable()
    {
        Assert.That((int)GameplayMenuTabId.Gear, Is.EqualTo(0));
        Assert.That((int)GameplayMenuTabId.CombatLoadout, Is.EqualTo(1));
        Assert.That((int)GameplayMenuTabId.Satchel, Is.EqualTo(2));
        Assert.That((int)GameplayMenuTabId.FieldNotes, Is.EqualTo(3));
        Assert.That((int)GameplayMenuTabId.Map, Is.EqualTo(4));
    }

    [Test]
    public void EveryTabButtonRemainsActiveAndInteractableRegardlessOfContent()
    {
        screen.Show();

        for (int i = 0; i < tabButtons.Length; i++)
        {
            Assert.That(tabButtons[i].Button.gameObject.activeInHierarchy, Is.True,
                $"Tab '{GameplayMenuScreen.FixedTabOrder[i]}' must stay visible; confirmed tabs are never hidden.");
            Assert.That(tabButtons[i].Button.IsInteractable(), Is.True,
                $"Tab '{GameplayMenuScreen.FixedTabOrder[i]}' must stay reachable; empty content never disables a tab.");
        }
    }

    [Test]
    public void EveryTabIsDirectlySelectableAndShowsExactlyOneContentAtATime()
    {
        screen.Show();

        foreach (GameplayMenuTabId id in GameplayMenuScreen.FixedTabOrder)
        {
            Assert.That(screen.SelectTab(id), Is.True, $"'{id}' must be directly selectable.");
            Assert.That(screen.ActiveTabId, Is.EqualTo(id));

            int visible = 0;
            for (int i = 0; i < tabViews.Length; i++)
            {
                if (tabViews[i].Content.activeSelf) visible++;
            }

            Assert.That(visible, Is.EqualTo(1), $"Exactly one tab's content may be visible; '{id}' left {visible}.");
        }
    }

    // -------------------------------------------------------------------------
    // Default tab and runtime memory
    // -------------------------------------------------------------------------

    [Test]
    public void FirstOpenSelectsGear()
    {
        screen.Show();
        Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuTabId.Gear));
    }

    [Test]
    public void LastTabIsRetainedAcrossHideAndShow()
    {
        screen.Show();
        screen.SelectTab(GameplayMenuTabId.FieldNotes);
        screen.Hide();

        screen.Show();

        Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuTabId.FieldNotes),
            "Last-viewed tab is runtime memory on the persistent screen and must survive a close/reopen.");
    }

    [Test]
    public void TabMemoryIsRuntimeOnlyAndNotSerialized()
    {
        FieldInfo lastValidTab = typeof(GameplayMenuScreen)
            .GetField("lastValidTab", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(lastValidTab, Is.Not.Null);
        Assert.That(lastValidTab.GetCustomAttribute<SerializeField>(), Is.Null,
            "Tab memory must never be serialized to disk, PlayerPrefs, or SaveData.");
    }

    [Test]
    public void RemeberedTabThatIsNoLongerRegisteredFallsBackToGear()
    {
        screen.Show();
        screen.SelectTab(GameplayMenuTabId.Map);
        screen.Hide();

        // Re-register without Map, mimicking a misauthored screen; the root must still open usably.
        List<GameplayMenuTabRegistration> partial = new List<GameplayMenuTabRegistration>();
        for (int i = 0; i < GameplayMenuScreen.FixedTabOrder.Length - 1; i++)
        {
            partial.Add(new GameplayMenuTabRegistration(GameplayMenuScreen.FixedTabOrder[i], tabButtons[i], tabViews[i]));
        }

        LogAssert.ignoreFailingMessages = true;
        screen.Configure(eventSystem, visualRoot, closeButton, partial);
        LogAssert.ignoreFailingMessages = false;

        screen.Show();

        Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuTabId.Gear));
    }

    // -------------------------------------------------------------------------
    // Cycling
    // -------------------------------------------------------------------------

    [Test]
    public void NextTabAdvancesThroughTheFixedOrder()
    {
        screen.Show();

        for (int i = 1; i < GameplayMenuScreen.FixedTabOrder.Length; i++)
        {
            Assert.That(screen.SelectNextTab(), Is.True);
            Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuScreen.FixedTabOrder[i]));
        }
    }

    [Test]
    public void NextFromMapWrapsToGearAndPreviousFromGearWrapsToMap()
    {
        screen.Show();

        screen.SelectTab(GameplayMenuTabId.Map);
        screen.SelectNextTab();
        Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuTabId.Gear));

        screen.SelectPreviousTab();
        Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuTabId.Map));
    }

    [Test]
    public void CyclingIsIgnoredWhileHidden()
    {
        Assert.That(screen.SelectNextTab(), Is.False);
        Assert.That(screen.SelectPreviousTab(), Is.False);
    }

    [Test]
    public void CyclingIsIgnoredWhileTheActiveTabOwnsAModal()
    {
        screen.Show();
        tabViews[0].ModalOpen = true;

        Assert.That(screen.SelectNextTab(), Is.False);
        Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuTabId.Gear));
    }

    [Test]
    public void ClickingATabButtonSelectsThatTab()
    {
        screen.Show();

        // GameplayMenuTabButton.Awake does not run in Edit Mode, so drive the same handler its
        // Button.onClick listener would. Real click routing is covered in Play Mode.
        InvokePrivate(tabButtons[IndexOf(GameplayMenuTabId.FieldNotes)], "HandleClicked");

        Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuTabId.FieldNotes));
    }

    // -------------------------------------------------------------------------
    // Selection
    // -------------------------------------------------------------------------

    [Test]
    public void EmptyTabWithNoContentSelectionFallsBackToItsTabButton()
    {
        screen.Show();
        screen.SelectTab(GameplayMenuTabId.CombatLoadout);

        Assert.That(screen.FirstSelection, Is.SameAs(tabButtons[IndexOf(GameplayMenuTabId.CombatLoadout)].Selectable),
            "An empty tab keeps focus on the tab strip rather than leaving selection null.");
    }

    [Test]
    public void EveryTabResolvesANonNullFirstSelection()
    {
        screen.Show();

        foreach (GameplayMenuTabId id in GameplayMenuScreen.FixedTabOrder)
        {
            screen.SelectTab(id);
            Assert.That(screen.FirstSelection, Is.Not.Null, $"'{id}' resolved no valid first selection.");
        }
    }

    [Test]
    public void FirstSelectionFallsBackToCloseWhenTheTabButtonIsUnusable()
    {
        screen.Show();
        tabButtons[0].Button.interactable = false;

        Assert.That(screen.FirstSelection, Is.SameAs(closeButton));
    }

    [Test]
    public void TabWithContentPrefersItsOwnFirstSelection()
    {
        Selectable entry = AddContentSelectable(IndexOf(GameplayMenuTabId.Gear), "GearEntry");
        screen.Show();

        Assert.That(screen.FirstSelection, Is.SameAs(entry));
    }

    [Test]
    public void PerTabContentSelectionIsRememberedAndRestored()
    {
        int gear = IndexOf(GameplayMenuTabId.Gear);
        AddContentSelectable(gear, "GearEntryA");
        Selectable second = AddContentSelectable(gear, "GearEntryB");
        AddContentSelectable(IndexOf(GameplayMenuTabId.FieldNotes), "FieldNotesEntry");

        screen.Show();
        eventSystem.SetSelectedGameObject(second.gameObject);

        // Both tabs have content, so focus stays in the content zone across the switch and the
        // remembered Gear selection — not merely the tab's own first entry — must come back.
        screen.SelectTab(GameplayMenuTabId.FieldNotes);
        screen.SelectTab(GameplayMenuTabId.Gear);

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(second.gameObject),
            "Returning to a tab restores the content selection the player left it on.");
    }

    [Test]
    public void LeavingContentForAnEmptyTabParksFocusOnTheStripAndKeepsItThere()
    {
        int gear = IndexOf(GameplayMenuTabId.Gear);
        Selectable entry = AddContentSelectable(gear, "GearEntry");

        screen.Show();
        eventSystem.SetSelectedGameObject(entry.gameObject);

        // Combat Loadout has no content, so the only valid target is its strip button...
        screen.SelectTab(GameplayMenuTabId.CombatLoadout);
        Assert.That(eventSystem.currentSelectedGameObject,
            Is.SameAs(tabButtons[IndexOf(GameplayMenuTabId.CombatLoadout)].gameObject));

        // ...and focus therefore stays in the strip on the way back, rather than silently diving
        // into Gear's content behind the player.
        screen.SelectTab(GameplayMenuTabId.Gear);
        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(tabButtons[gear].gameObject));
    }

    [Test]
    public void SwitchingTabsWhileFocusIsOnTheStripKeepsFocusOnTheStrip()
    {
        AddContentSelectable(IndexOf(GameplayMenuTabId.FieldNotes), "FieldNotesEntry");
        screen.Show();

        eventSystem.SetSelectedGameObject(tabButtons[0].gameObject);
        screen.SelectTab(GameplayMenuTabId.FieldNotes);

        Assert.That(eventSystem.currentSelectedGameObject,
            Is.SameAs(tabButtons[IndexOf(GameplayMenuTabId.FieldNotes)].gameObject),
            "Cycling the strip must not throw focus into the new tab's content.");
    }

    [Test]
    public void SwitchingTabsWhileFocusIsInContentEntersTheNewTabsContent()
    {
        AddContentSelectable(IndexOf(GameplayMenuTabId.Gear), "GearEntry");
        Selectable fieldNotesEntry = AddContentSelectable(IndexOf(GameplayMenuTabId.FieldNotes), "FieldNotesEntry");
        screen.Show();

        eventSystem.SetSelectedGameObject(tabViews[IndexOf(GameplayMenuTabId.Gear)].First.gameObject);
        screen.SelectTab(GameplayMenuTabId.FieldNotes);

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(fieldNotesEntry.gameObject));
    }

    [Test]
    public void TabSwitchingNeverLeavesSelectionNull()
    {
        screen.Show();
        eventSystem.SetSelectedGameObject(tabButtons[0].gameObject);

        foreach (GameplayMenuTabId id in GameplayMenuScreen.FixedTabOrder)
        {
            screen.SelectTab(id);
            Assert.That(eventSystem.currentSelectedGameObject, Is.Not.Null, $"Selection became null on '{id}'.");
        }
    }

    [Test]
    public void StripNavigationChainsAllFiveTabsHorizontallyWithWraparound()
    {
        Selectable first = tabButtons[0].Selectable;
        Selectable last = tabButtons[tabButtons.Length - 1].Selectable;

        Assert.That(first.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
        Assert.That(first.navigation.selectOnRight, Is.SameAs(tabButtons[1].Selectable));
        Assert.That(first.navigation.selectOnLeft, Is.SameAs(last), "Left from Gear wraps to Map.");
        Assert.That(last.navigation.selectOnRight, Is.SameAs(first), "Right from Map wraps to Gear.");
        Assert.That(first.navigation.selectOnUp, Is.SameAs(closeButton));
        Assert.That(closeButton.navigation.selectOnDown, Is.SameAs(first));
    }

    [Test]
    public void ActiveTabButtonPointsDownAtItsContentEntryPoint()
    {
        Selectable entry = AddContentSelectable(IndexOf(GameplayMenuTabId.Gear), "GearEntry");
        screen.Show();

        Assert.That(tabButtons[0].Selectable.navigation.selectOnDown, Is.SameAs(entry));
    }

    [Test]
    public void EmptyTabButtonHasNoContentEntryPoint()
    {
        screen.Show();
        screen.SelectTab(GameplayMenuTabId.Satchel);

        Assert.That(tabButtons[IndexOf(GameplayMenuTabId.Satchel)].Selectable.navigation.selectOnDown, Is.Null,
            "Down from an empty tab must do nothing rather than jump into another tab's content.");
    }

    // -------------------------------------------------------------------------
    // Lifecycle, Back, and close
    // -------------------------------------------------------------------------

    [Test]
    public void ShowActivatesOnlyTheActiveTabAndHideDeactivatesEverything()
    {
        screen.Show();
        Assert.That(visualRoot.activeSelf, Is.True);
        Assert.That(tabViews[0].Content.activeSelf, Is.True);

        screen.Hide();

        Assert.That(visualRoot.activeSelf, Is.False);
        for (int i = 0; i < tabViews.Length; i++)
        {
            Assert.That(tabViews[i].Content.activeSelf, Is.False, "A hidden root must leave no tab content active.");
        }
    }

    [Test]
    public void HideIsIdempotent()
    {
        screen.Show();
        screen.Hide();
        Assert.DoesNotThrow(() => screen.Hide());
        Assert.That(visualRoot.activeSelf, Is.False);
    }

    [Test]
    public void RepeatedShowHideCyclesDoNotDuplicateTabShowCalls()
    {
        for (int i = 0; i < 3; i++)
        {
            screen.Show();
            screen.Hide();
        }

        Assert.That(tabViews[0].ShowCount, Is.EqualTo(3),
            "Each open must show the active tab exactly once — no accumulated subscriptions.");
    }

    [Test]
    public void RepeatedShowHideCyclesDoNotDuplicateTabButtonSubscriptions()
    {
        screen.Show();
        screen.Hide();
        screen.Show();

        int selections = 0;
        screen.SelectTab(GameplayMenuTabId.CombatLoadout);
        selections++;

        // A duplicate subscription would re-enter SelectTab; the active tab must simply be Combat Loadout.
        Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuTabId.CombatLoadout));
        Assert.That(tabViews[IndexOf(GameplayMenuTabId.CombatLoadout)].ShowCount, Is.EqualTo(selections));
    }

    [Test]
    public void BackHandledByTheActiveTabKeepsTheRootOpen()
    {
        screen.Show();
        tabViews[0].BackResult = true;

        Assert.That(screen.HandleBackInternally(), Is.True);
    }

    [Test]
    public void BackAtABareTabReturnsFalseSoTheFlowControllerClosesTheRoot()
    {
        screen.Show();
        tabViews[0].BackResult = false;

        Assert.That(screen.HandleBackInternally(), Is.False);
    }

    [Test]
    public void HasOpenModalReflectsOnlyTheActiveTab()
    {
        screen.Show();
        tabViews[IndexOf(GameplayMenuTabId.Map)].ModalOpen = true;

        Assert.That(screen.HasOpenModal, Is.False, "An inactive tab's nested layer must not block the root.");

        screen.SelectTab(GameplayMenuTabId.Map);
        Assert.That(screen.HasOpenModal, Is.True);
    }

    [Test]
    public void CloseControlRaisesCloseRequestedRatherThanUnpausingDirectly()
    {
        int raised = 0;
        screen.CloseRequested += () => raised++;

        screen.Show();
        screen.RequestClose();

        Assert.That(raised, Is.EqualTo(1));
        Assert.That(visualRoot.activeSelf, Is.True,
            "The screen must not close itself; UIFlowController owns the close sequence.");
    }

    [Test]
    public void ScreenSatisfiesTheSharedRootScreenContract()
    {
        Assert.That(screen, Is.InstanceOf<IUIFlowRootScreen>());
    }

    // -------------------------------------------------------------------------
    // Authoring diagnostics
    // -------------------------------------------------------------------------

    [Test]
    public void WrongTabCountIsReported()
    {
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("exactly 5 are required"));
        LogAssert.ignoreFailingMessages = true;

        screen.Configure(eventSystem, visualRoot, closeButton, new List<GameplayMenuTabRegistration>
        {
            new GameplayMenuTabRegistration(GameplayMenuTabId.Gear, tabButtons[0], tabViews[0])
        });

        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void OutOfOrderRegistrationIsReported()
    {
        List<GameplayMenuTabRegistration> swapped = new List<GameplayMenuTabRegistration>();
        for (int i = 0; i < GameplayMenuScreen.FixedTabOrder.Length; i++)
        {
            swapped.Add(new GameplayMenuTabRegistration(GameplayMenuScreen.FixedTabOrder[i], tabButtons[i], tabViews[i]));
        }

        GameplayMenuTabRegistration head = swapped[0];
        swapped[0] = swapped[1];
        swapped[1] = head;

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("the fixed order requires 'Gear'"));
        LogAssert.ignoreFailingMessages = true;
        screen.Configure(eventSystem, visualRoot, closeButton, swapped);
        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void DuplicateTabIdIsReported()
    {
        List<GameplayMenuTabRegistration> duplicated = new List<GameplayMenuTabRegistration>();
        for (int i = 0; i < GameplayMenuScreen.FixedTabOrder.Length; i++)
        {
            GameplayMenuTabId id = i == 1 ? GameplayMenuTabId.Gear : GameplayMenuScreen.FixedTabOrder[i];
            duplicated.Add(new GameplayMenuTabRegistration(id, tabButtons[i], tabViews[i]));
        }

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("registers tab 'Gear' more than once"));
        LogAssert.ignoreFailingMessages = true;
        screen.Configure(eventSystem, visualRoot, closeButton, duplicated);
        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void NonCastableTabViewIsReported()
    {
        List<GameplayMenuTabRegistration> broken = new List<GameplayMenuTabRegistration>();
        MonoBehaviour notATab = NewObject("NotATab").AddComponent<EventSystem>();
        for (int i = 0; i < GameplayMenuScreen.FixedTabOrder.Length; i++)
        {
            MonoBehaviour view = i == 0 ? notATab : tabViews[i];
            broken.Add(new GameplayMenuTabRegistration(GameplayMenuScreen.FixedTabOrder[i], tabButtons[i], view));
        }

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("does not implement IGameplayMenuTab"));
        LogAssert.ignoreFailingMessages = true;
        screen.Configure(eventSystem, visualRoot, closeButton, broken);
        LogAssert.ignoreFailingMessages = false;
    }

    [Test]
    public void SelectingAnUnregisteredTabFailsClosedWithoutChangingTheActiveTab()
    {
        List<GameplayMenuTabRegistration> partial = new List<GameplayMenuTabRegistration>();
        for (int i = 0; i < GameplayMenuScreen.FixedTabOrder.Length - 1; i++)
        {
            partial.Add(new GameplayMenuTabRegistration(GameplayMenuScreen.FixedTabOrder[i], tabButtons[i], tabViews[i]));
        }

        LogAssert.ignoreFailingMessages = true;
        screen.Configure(eventSystem, visualRoot, closeButton, partial);
        screen.Show();
        bool selected = screen.SelectTab(GameplayMenuTabId.Map);
        LogAssert.ignoreFailingMessages = false;

        Assert.That(selected, Is.False);
        Assert.That(screen.ActiveTabId, Is.EqualTo(GameplayMenuTabId.Gear));
    }
}
