using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Real-player-loop coverage for System/Pause and UI/Cancel sharing Keyboard Escape.
/// Production types are reached by reflection because this custom PlayMode assembly cannot
/// reference the predefined Assembly-CSharp assembly directly.
/// </summary>
public sealed class UIFlowInputArbitrationPlayModeTests
{
    private const string InputActionsPath =
        "Assets/_Project/Input/InputSystem_Actions.inputactions";

    private static Assembly gameAssembly;

    private Keyboard keyboard;
    private InputActionAsset actions;
    private GameObject gameManagerObject;
    private Component gameManager;
    private GameObject eventSystemObject;
    private EventSystem eventSystem;
    private GameObject pauseSelectionObject;
    private Button pauseSelection;
    private GameObject gameplaySelectionObject;
    private Button gameplaySelection;
    private GameObject modalSelectionObject;
    private Button modalSelection;
    private GameObject flowObject;
    private Component flow;
    private GameObject pauseRootObject;
    private Component pauseRoot;
    private GameObject gameplayRootObject;
    private Component gameplayRoot;

    private static Type GameType(string name)
    {
        if (gameAssembly == null)
        {
            gameAssembly = Assembly.Load("Assembly-CSharp");
        }

        Type type = gameAssembly.GetType(name);
        Assert.That(type, Is.Not.Null, "Type not found in Assembly-CSharp: " + name);
        return type;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        field.SetValue(target, value);
    }

    private static object GetProperty(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, "Property not found: " + name);
        return property.GetValue(target);
    }

    private static void SetProperty(object target, string name, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, "Property not found: " + name);
        property.SetValue(target, value);
    }

    private static object Invoke(object target, string method, params object[] args)
    {
        MethodInfo candidate = null;
        foreach (MethodInfo methodInfo in target.GetType().GetMethods(
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
        {
            if (methodInfo.Name == method && methodInfo.GetParameters().Length == args.Length)
            {
                candidate = methodInfo;
                break;
            }
        }

        Assert.That(candidate, Is.Not.Null, "Method not found: " + method);
        return candidate.Invoke(target, args);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        Time.timeScale = 1f;
        keyboard = InputSystem.AddDevice<Keyboard>();

        InputActionAsset shippedActions =
            UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        Assert.That(
            shippedActions,
            Is.Not.Null,
            "Could not load " + InputActionsPath);
        actions = InputActionAsset.FromJson(shippedActions.ToJson());

        gameManagerObject = new GameObject("UI Flow Input Test GameManager");
        gameManager = gameManagerObject.AddComponent(GameType("GameManager"));

        eventSystemObject = new GameObject("UI Flow Input Test EventSystem");
        eventSystem = eventSystemObject.AddComponent<EventSystem>();

        pauseSelectionObject = new GameObject("Quit Selection");
        pauseSelection = pauseSelectionObject.AddComponent<Button>();
        gameplaySelectionObject = new GameObject("Gameplay Back Selection");
        gameplaySelection = gameplaySelectionObject.AddComponent<Button>();
        modalSelectionObject = new GameObject("Modal Cancel Selection");
        modalSelection = modalSelectionObject.AddComponent<Button>();

        pauseRootObject = new GameObject("Pause Root Fixture");
        pauseRoot = pauseRootObject.AddComponent(GameType("UIFlowTestRootScreen"));
        Invoke(pauseRoot, "Configure", eventSystem, pauseSelection);
        gameplayRootObject = new GameObject("Gameplay Root Fixture");
        gameplayRoot = gameplayRootObject.AddComponent(GameType("UIFlowTestRootScreen"));
        Invoke(gameplayRoot, "Configure", eventSystem, gameplaySelection);

        flowObject = new GameObject("UI Flow Input Test Controller");
        flowObject.SetActive(false);
        flow = flowObject.AddComponent(GameType("UIFlowController"));
        SetField(flow, "inputActions", actions);
        SetField(flow, "eventSystem", eventSystem);
        Invoke(flow, "ConfigureRoots", pauseRoot, gameplayRoot);
        flowObject.SetActive(true);
        SetField(flow, "lockoutRemaining", 0f);

        yield return ReleaseAllKeys();

        InputActionMap systemMap = actions.FindActionMap("System", true);
        InputActionMap uiMap = actions.FindActionMap("UI", true);

        Assert.That(systemMap.FindAction("Pause", true), Is.Not.Null);
        Assert.That(systemMap.FindAction("GameplayMenu", true), Is.Not.Null);
        Assert.That(uiMap.FindAction("Cancel", true), Is.Not.Null);
        Assert.That(systemMap.enabled, Is.True);
        Assert.That(uiMap.enabled, Is.False);
#else
        yield return null;
        Assert.Ignore("UI flow arbitration coverage loads the shipped action asset through the AssetDatabase.");
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        Time.timeScale = 1f;

        if (flowObject != null) UnityEngine.Object.Destroy(flowObject);
        if (gameManagerObject != null) UnityEngine.Object.Destroy(gameManagerObject);
        if (eventSystemObject != null) UnityEngine.Object.Destroy(eventSystemObject);
        if (pauseSelectionObject != null) UnityEngine.Object.Destroy(pauseSelectionObject);
        if (gameplaySelectionObject != null) UnityEngine.Object.Destroy(gameplaySelectionObject);
        if (modalSelectionObject != null) UnityEngine.Object.Destroy(modalSelectionObject);
        if (pauseRootObject != null) UnityEngine.Object.Destroy(pauseRootObject);
        if (gameplayRootObject != null) UnityEngine.Object.Destroy(gameplayRootObject);
        if (actions != null)
        {
            actions.Disable();
            UnityEngine.Object.Destroy(actions);
        }
        if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);

        yield return null;
    }

    [UnityTest]
    public IEnumerator EscapeFromClosedOpensPauseOnceAndFreshEscapeClosesOnce()
    {
        yield return Press(Key.Escape);

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.True);
        Assert.That(GetProperty(flow, "ActiveRootKind").ToString(), Does.Contain("Pause"));
        Assert.That(GetProperty(pauseRoot, "ShowCount"), Is.EqualTo(1));
        Assert.That(GetProperty(pauseRoot, "HideCount"), Is.Zero);
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Paused"));
        Assert.That(Time.timeScale, Is.EqualTo(0f));

        yield return null;
        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.True,
            "Holding the opening Escape press must not synthesize UI/Cancel and close Pause.");
        Assert.That(GetProperty(pauseRoot, "ShowCount"), Is.EqualTo(1));

        yield return ReleaseAllKeys();
        yield return Press(Key.Escape);

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False);
        Assert.That(GetProperty(pauseRoot, "HideCount"), Is.EqualTo(1));
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator EscapeWithQuitModalOpenClosesOnlyModalThenLaterFreshEscapeClosesRoot()
    {
        yield return Press(Key.Escape);
        yield return ReleaseAllKeys();

        SetProperty(pauseRoot, "HasOpenModal", true);
        eventSystem.SetSelectedGameObject(modalSelection.gameObject);

        yield return Press(Key.Escape);

        Assert.That(GetProperty(pauseRoot, "HasOpenModal"), Is.False);
        Assert.That(GetProperty(pauseRoot, "BackCount"), Is.EqualTo(1),
            "The same Escape edge must route to the modal exactly once.");
        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(pauseSelection.gameObject),
            "Closing the modal must restore its parent Quit selection.");
        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.True);
        Assert.That(GetProperty(pauseRoot, "HideCount"), Is.Zero);
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Paused"));
        Assert.That(Time.timeScale, Is.EqualTo(0f));

        yield return ReleaseAllKeys();
        yield return Press(Key.Escape);

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.False);
        Assert.That(GetProperty(pauseRoot, "HideCount"), Is.EqualTo(1));
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Playing"));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator EscapeWhileGameplayMenuIsActiveRoutesThroughCancelBack()
    {
        yield return Press(Key.I);
        yield return ReleaseAllKeys();

        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.True);
        Assert.That(GetProperty(flow, "ActiveRootKind").ToString(), Does.Contain("GameplayMenu"));
        Assert.That(GetProperty(gameplayRoot, "ShowCount"), Is.EqualTo(1));

        SetProperty(gameplayRoot, "HandleBackResult", true);
        yield return Press(Key.Escape);

        Assert.That(GetProperty(gameplayRoot, "BackCount"), Is.EqualTo(1),
            "Pause must remain rejected while Gameplay Menu owns the frame, leaving Escape for UI/Cancel.");
        Assert.That((bool)GetProperty(flow, "IsRootOpen"), Is.True);
        Assert.That(GetProperty(gameplayRoot, "HideCount"), Is.Zero);
        Assert.That(GetProperty(pauseRoot, "ShowCount"), Is.Zero);
        Assert.That(GetProperty(gameManager, "State").ToString(), Is.EqualTo("Paused"));
        Assert.That(Time.timeScale, Is.EqualTo(0f));
    }

    private IEnumerator Press(Key key)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        yield return null;
    }

    private IEnumerator ReleaseAllKeys()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return null;
    }
}
