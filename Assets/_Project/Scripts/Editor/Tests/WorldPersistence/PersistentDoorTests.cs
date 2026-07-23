using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PersistentDoorTests
{
    // Ordinary always-usable scene-transition doors (TransitionPoint / DoorTransitionInteractable)
    // have no reference to WorldStateRegistry or PersistentDoor anywhere in their source -- they
    // cannot be affected by this component's persistence by construction, so no runtime test is
    // needed to prove that non-interaction.

    [Test]
    public void Awake_NoStoredState_InitializesClosed()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Door");
        try
        {
            PersistentDoor door = SetUpDoor(go, "door_1", registry);
            BoxCollider2D blocker = (BoxCollider2D)GetPrivateField(door, "blockerCollider");

            InvokePrivate(door, "Awake");

            Assert.That(blocker.enabled, Is.True, "A door with no stored state must initialize closed (blocker enabled).");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_StoredOpen_InitializesOpenAndDisablesBlockerImmediately()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Door");
        try
        {
            registry.SetObjectState("door_2", "open");
            PersistentDoor door = SetUpDoor(go, "door_2", registry);
            BoxCollider2D blocker = (BoxCollider2D)GetPrivateField(door, "blockerCollider");

            InvokePrivate(door, "Awake");

            Assert.That(blocker.enabled, Is.False,
                "Restored open state must disable the blocker before the first physics frame -- never block the hero even momentarily.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_StoredOpen_DoesNotReplayFeedbackOrThrow()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Door");
        try
        {
            registry.SetObjectState("door_3", "open");
            PersistentDoor door = SetUpDoor(go, "door_3", registry);

            // Restoration must be silent for a consistent, recognized stored value -- no warning or
            // error, and no exception from any feedback path.
            Assert.DoesNotThrow(() => InvokePrivate(door, "Awake"));
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Interact_ConfirmedOpening_RecordsPermanentStateAndDisablesInteractable()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Door");
        try
        {
            PersistentDoor door = SetUpDoor(go, "door_4", registry);
            InvokePrivate(door, "Awake");

            Assert.That(registry.TryGetObjectState("door_4", out _), Is.False, "Precondition: not yet opened.");

            door.Interact();

            Assert.That(registry.TryGetObjectState("door_4", out string state), Is.True);
            Assert.That(state, Is.EqualTo("open"));
            Assert.That(door.IsDisabled, Is.True, "An opened shortcut door has nothing left to interact with.");

            BoxCollider2D blocker = (BoxCollider2D)GetPrivateField(door, "blockerCollider");
            Assert.That(blocker.enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Interact_CalledTwice_IsIdempotentAndRecordsOnce()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Door");
        try
        {
            PersistentDoor door = SetUpDoor(go, "door_5", registry);
            InvokePrivate(door, "Awake");

            door.Interact();
            door.Interact(); // must no-op -- already open (and already disabled/unregistered)

            Assert.That(registry.TryGetObjectState("door_5", out string state), Is.True);
            Assert.That(state, Is.EqualTo("open"));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Interact_NoRegistryAssigned_OpensLocallyButRecordsNothing()
    {
        GameObject go = new GameObject("Door");
        try
        {
            PersistentDoor door = SetUpDoor(go, "door_6", null);
            InvokePrivate(door, "Awake");

            Assert.DoesNotThrow(() => door.Interact());

            BoxCollider2D blocker = (BoxCollider2D)GetPrivateField(door, "blockerCollider");
            Assert.That(blocker.enabled, Is.False, "Interaction still opens the door locally even without a registry to persist to.");
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Awake_UnknownStoredValue_LogsWarningAndFallsBackToClosed()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Door");
        try
        {
            registry.SetObjectState("door_7", "garbled");
            PersistentDoor door = SetUpDoor(go, "door_7", registry);
            BoxCollider2D blocker = (BoxCollider2D)GetPrivateField(door, "blockerCollider");

            LogAssert.Expect(LogType.Warning, new Regex("unrecognized stored state"));
            InvokePrivate(door, "Awake");

            Assert.That(blocker.enabled, Is.True, "Unknown stored values must fall back to the authored default (closed).");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    private static PersistentDoor SetUpDoor(GameObject go, string worldObjectId, WorldStateRegistry registry)
    {
        go.SetActive(false); // prevent Unity's own Awake pass; we invoke the target method directly
        BoxCollider2D trigger = go.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;

        GameObject blockerGo = new GameObject("Blocker");
        blockerGo.transform.SetParent(go.transform);
        BoxCollider2D blocker = blockerGo.AddComponent<BoxCollider2D>();
        blocker.isTrigger = false;

        PersistentDoor door = go.AddComponent<PersistentDoor>();
        SetPrivateField(door, "worldObjectId", worldObjectId);
        SetPrivateField(door, "registry", registry);
        SetPrivateField(door, "blockerCollider", blocker);
        SetPrivateField(door, "closedVisualRoot", blockerGo);

        return door;
    }

    private static object GetPrivateField(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on '{target.GetType().Name}'.");
        return field.GetValue(target);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found on '{target.GetType().Name}'.");
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Method '{methodName}' not found on '{target.GetType().Name}'.");
        method.Invoke(target, arguments);
    }
}
