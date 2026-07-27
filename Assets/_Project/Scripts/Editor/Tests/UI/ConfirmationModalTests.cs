using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Package A1 correction-pass coverage for <see cref="ConfirmationModal"/>'s selection-fallback
/// behavior (Finding #9): a disabled or inactive invoker must fall back to the parent root's
/// supplied fallback selection on close rather than leaving EventSystem selection null, while a
/// whole-root teardown close must not restore any selection at all.
/// </summary>
public sealed class ConfirmationModalTests
{
    private GameObject modalObject;
    private ConfirmationModal modal;
    private Button confirmButton;
    private Button cancelButton;
    private GameObject eventSystemObject;
    private EventSystem eventSystem;
    private Button invokerButton;
    private Button fallbackButton;

    [SetUp]
    public void SetUp()
    {
        eventSystemObject = new GameObject("EventSystem Test");
        eventSystem = eventSystemObject.AddComponent<EventSystem>();

        modalObject = new GameObject("ConfirmationModal Test");
        modal = modalObject.AddComponent<ConfirmationModal>();
        confirmButton = NewButton("Confirm");
        cancelButton = NewButton("Cancel");
        invokerButton = NewButton("Invoker");
        fallbackButton = NewButton("Fallback");

        SetPrivateField(modal, "visualRoot", modalObject);
        SetPrivateField(modal, "confirmButton", confirmButton);
        SetPrivateField(modal, "cancelButton", cancelButton);
        SetPrivateField(modal, "eventSystem", eventSystem);
        InvokePrivate(modal, "Awake");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(modalObject);
        Object.DestroyImmediate(confirmButton.gameObject);
        Object.DestroyImmediate(cancelButton.gameObject);
        Object.DestroyImmediate(invokerButton.gameObject);
        Object.DestroyImmediate(fallbackButton.gameObject);
        Object.DestroyImmediate(eventSystemObject);
    }

    private static Button NewButton(string name)
    {
        GameObject go = new GameObject(name);
        return go.AddComponent<Button>();
    }

    [Test]
    public void CloseRestoresValidInvokerSelection()
    {
        modal.Show(invokerButton, fallbackButton, null, null);

        modal.Close(restoreInvokerSelection: true);

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(invokerButton.gameObject));
    }

    [Test]
    public void CloseFallsBackWhenInvokerIsDisabled()
    {
        invokerButton.interactable = false;
        modal.Show(invokerButton, fallbackButton, null, null);

        modal.Close(restoreInvokerSelection: true);

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(fallbackButton.gameObject));
    }

    [Test]
    public void CloseFallsBackWhenInvokerIsInactive()
    {
        modal.Show(invokerButton, fallbackButton, null, null);
        invokerButton.gameObject.SetActive(false);

        modal.Close(restoreInvokerSelection: true);

        Assert.That(eventSystem.currentSelectedGameObject, Is.SameAs(fallbackButton.gameObject));
    }

    [Test]
    public void RootTeardownCloseClearsSelectionInsteadOfRestoring()
    {
        modal.Show(invokerButton, fallbackButton, null, null);
        eventSystem.SetSelectedGameObject(null);

        modal.Close(restoreInvokerSelection: false);

        Assert.That(eventSystem.currentSelectedGameObject, Is.Null);
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
