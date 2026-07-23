using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PersistentSwitchTests
{
    [Test]
    public void Awake_RoomRuntimeLifetime_NeverQueriesOrWritesRegistry()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Switch");
        try
        {
            PersistentSwitch sw = SetUpSwitch(go, "switch_1", registry, PersistenceLifetime.RoomRuntime);
            InvokePrivate(sw, "Awake");

            BoxCollider2D blocker = (BoxCollider2D)GetPrivateField(sw, "blockerCollider");
            Assert.That(blocker.enabled, Is.True, "RoomRuntime always initializes from the authored default (inactive).");

            sw.Interact();

            Assert.That(registry.TryGetUntilDeathState("switch_1", out _), Is.False, "RoomRuntime must never write until-death state.");
            Assert.That(registry.TryGetObjectState("switch_1", out _), Is.False, "RoomRuntime must never write permanent state.");
            Assert.That(blocker.enabled, Is.False, "The paired blocker must still open locally even though nothing is persisted.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Interact_UntilDeathLifetime_WritesOnlyUntilDeathState()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Switch");
        try
        {
            PersistentSwitch sw = SetUpSwitch(go, "switch_2", registry, PersistenceLifetime.UntilDeath);
            InvokePrivate(sw, "Awake");

            sw.Interact();

            Assert.That(registry.TryGetUntilDeathState("switch_2", out string state), Is.True);
            Assert.That(state, Is.EqualTo("active"));
            Assert.That(registry.TryGetObjectState("switch_2", out _), Is.False, "UntilDeath must never write permanent state.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Interact_PermanentLifetime_WritesPermanentState()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Switch");
        try
        {
            PersistentSwitch sw = SetUpSwitch(go, "switch_3", registry, PersistenceLifetime.Permanent);
            InvokePrivate(sw, "Awake");

            sw.Interact();

            Assert.That(registry.TryGetObjectState("switch_3", out string state), Is.True);
            Assert.That(state, Is.EqualTo("active"));
            Assert.That(registry.TryGetUntilDeathState("switch_3", out _), Is.False, "Permanent must never write until-death state.");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_RestoredUntilDeathActive_AppliesBeforeInteractionIsAvailable_WithoutReplayingFeedback()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Switch");
        try
        {
            registry.SetUntilDeathState("switch_4", "active");
            PersistentSwitch sw = SetUpSwitch(go, "switch_4", registry, PersistenceLifetime.UntilDeath);
            BoxCollider2D blocker = (BoxCollider2D)GetPrivateField(sw, "blockerCollider");

            Assert.DoesNotThrow(() => InvokePrivate(sw, "Awake"));
            LogAssert.NoUnexpectedReceived();

            // "State applies before interaction becomes available": by the time Awake returns, the
            // switch is already disabled (nothing left to interact with) and its blocker already open.
            Assert.That(sw.IsDisabled, Is.True);
            Assert.That(blocker.enabled, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Interact_CalledTwice_IsIdempotent()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Switch");
        try
        {
            PersistentSwitch sw = SetUpSwitch(go, "switch_5", registry, PersistenceLifetime.Permanent);
            InvokePrivate(sw, "Awake");

            sw.Interact();
            sw.Interact();

            Assert.That(registry.TryGetObjectState("switch_5", out string state), Is.True);
            Assert.That(state, Is.EqualTo("active"));
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_UnknownStoredValue_LogsWarningAndFallsBackToInactive()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("Switch");
        try
        {
            registry.SetObjectState("switch_6", "garbled");
            PersistentSwitch sw = SetUpSwitch(go, "switch_6", registry, PersistenceLifetime.Permanent);
            BoxCollider2D blocker = (BoxCollider2D)GetPrivateField(sw, "blockerCollider");

            LogAssert.Expect(LogType.Warning, new Regex("unrecognized stored state"));
            InvokePrivate(sw, "Awake");

            Assert.That(blocker.enabled, Is.True, "Unknown stored values must fall back to the authored default (inactive).");
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    private static PersistentSwitch SetUpSwitch(GameObject go, string worldObjectId, WorldStateRegistry registry, PersistenceLifetime lifetime)
    {
        go.SetActive(false); // prevent Unity's own Awake pass; we invoke the target method directly
        BoxCollider2D trigger = go.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;

        GameObject gateGo = new GameObject("Gate");
        gateGo.transform.SetParent(go.transform); // parented purely so DestroyImmediate(go) cleans it up too
        BoxCollider2D gateCollider = gateGo.AddComponent<BoxCollider2D>();
        gateCollider.isTrigger = false;

        PersistentSwitch sw = go.AddComponent<PersistentSwitch>();
        SetPrivateField(sw, "worldObjectId", worldObjectId);
        SetPrivateField(sw, "registry", registry);
        SetPrivateField(sw, "lifetime", lifetime);
        SetPrivateField(sw, "blockerCollider", gateCollider);
        SetPrivateField(sw, "blockerClosedVisualRoot", gateGo);

        return sw;
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
