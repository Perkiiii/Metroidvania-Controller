using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// RoomVisitReporter marks its authored roomId visited on initialization (World Persistence Phase
// 3.1) -- room identity is authored data on the component, not derived from the Unity scene name,
// so renaming a .unity file never changes saved room identity. GameManager.OnSceneLoaded no longer
// touches WorldStateRegistry for this at all; see GameManagerWorldPersistenceLifecycleTests for the
// normal-death lifecycle coverage that used to live alongside the old scene-name-based mechanism.
public sealed class RoomVisitationTests
{
    [Test]
    public void Awake_MarksAuthoredRoomIdAsVisited()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("RoomVisitReporter Test");
        try
        {
            RoomVisitReporter reporter = SetUpReporter(go, "room_sample_01", registry);
            Assert.That(registry.IsRoomVisited("room_sample_01"), Is.False, "Precondition: not yet visited.");

            InvokePrivate(reporter, "Awake");

            Assert.That(registry.IsRoomVisited("room_sample_01"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_OnSecondInstanceWithSameRoomId_IsIdempotent()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject firstGo = new GameObject("RoomVisitReporter Test A");
        GameObject secondGo = new GameObject("RoomVisitReporter Test B");
        try
        {
            int notifyCount = 0;
            registry.Subscribe(new WorldStateKey(WorldStateCategory.VisitedRoom, "room_sample_01"), _ => notifyCount++);

            RoomVisitReporter first = SetUpReporter(firstGo, "room_sample_01", registry);
            InvokePrivate(first, "Awake");

            // Re-entering the same room loads a fresh scene, and therefore a fresh RoomVisitReporter
            // instance with the same authored roomId -- this must not re-notify or duplicate state.
            RoomVisitReporter second = SetUpReporter(secondGo, "room_sample_01", registry);
            InvokePrivate(second, "Awake");

            Assert.That(registry.IsRoomVisited("room_sample_01"), Is.True);
            Assert.That(notifyCount, Is.EqualTo(1), "A room entered more than once must not re-notify or duplicate its save entry.");
        }
        finally
        {
            Object.DestroyImmediate(firstGo);
            Object.DestroyImmediate(secondGo);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void Awake_MissingRegistry_DoesNotThrow()
    {
        GameObject go = new GameObject("RoomVisitReporter Test");
        try
        {
            RoomVisitReporter reporter = SetUpReporter(go, "room_sample_01", null);

            LogAssert.Expect(LogType.Error, new Regex("no WorldStateRegistry assigned"));
            Assert.DoesNotThrow(() => InvokePrivate(reporter, "Awake"));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Awake_MissingRoomId_DoesNotThrowAndDoesNotMarkAnything()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("RoomVisitReporter Test");
        try
        {
            RoomVisitReporter reporter = SetUpReporter(go, "", registry);

            LogAssert.Expect(LogType.Error, new Regex("no roomId assigned"));
            Assert.DoesNotThrow(() => InvokePrivate(reporter, "Awake"));
            Assert.That(registry.IsRoomVisited(""), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void VisitedRoom_SurvivesNormalDeathResetAndSaveRoundTrip()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.MarkRoomVisited("room_sample_01");

            // Exactly the two resets GameManager.BeginRespawnSequence performs on every normal death.
            registry.ResetRespawnableEnemyDeaths();
            registry.ResetUntilDeathState();
            Assert.That(registry.IsRoomVisited("room_sample_01"), Is.True, "Normal death must preserve visited-room state.");

            SaveData data = new SaveData();
            registry.GatherSaveData(data);
            Assert.That(data.world.visitedRoomIds, Contains.Item("room_sample_01"));

            WorldStateRegistry destination = ScriptableObject.CreateInstance<WorldStateRegistry>();
            try
            {
                destination.ApplySaveData(data);
                Assert.That(destination.IsRoomVisited("room_sample_01"), Is.True, "Continue must restore visited-room state.");
            }
            finally
            {
                Object.DestroyImmediate(destination);
            }
        }
        finally
        {
            Object.DestroyImmediate(registry);
        }
    }

    private static RoomVisitReporter SetUpReporter(GameObject go, string roomId, WorldStateRegistry registry)
    {
        go.SetActive(false); // prevent Unity's own Awake pass; we invoke the target method directly
        RoomVisitReporter reporter = go.AddComponent<RoomVisitReporter>();
        SetPrivateField(reporter, "roomId", roomId);
        SetPrivateField(reporter, "registry", registry);
        return reporter;
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
