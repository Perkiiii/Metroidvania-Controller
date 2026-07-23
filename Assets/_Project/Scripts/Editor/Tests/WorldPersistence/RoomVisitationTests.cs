using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// GameManager.OnSceneLoaded marks the loaded scene's name as a visited room (World Persistence
// Phase 3) -- scene name is the room ID for this milestone, so marking requires no scene-wide
// search and no per-frame polling: it is a single direct call driven by the SceneManager.sceneLoaded
// event GameManager already subscribes to for every other purpose.
public sealed class RoomVisitationTests
{
    [Test]
    public void OnSceneLoaded_MarksLoadedSceneNameAsVisitedRoom()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("GameManager Test");
        string scenePath = null;
        try
        {
            go.SetActive(false);
            GameManager gm = go.AddComponent<GameManager>();
            SetPrivateField(gm, "worldStateRegistry", registry);

            // SceneManager.GetActiveScene().name is empty for the Test Runner's own unsaved scene,
            // which WorldStateRegistry.MarkRoomVisited correctly rejects as an empty ID. Scene.name
            // is only populated once a scene has a file path, so a real temporary scene asset (saved
            // then deleted within this test) is used here to exercise the real behavior with a real
            // room name, without touching any real project scene.
            Scene namedScene = CreateNamedTempScene("RoomVisitationTestScene_Marks", out scenePath);
            Assert.That(registry.IsRoomVisited(namedScene.name), Is.False, "Precondition: not yet visited.");

            InvokePrivate(gm, "OnSceneLoaded", namedScene, LoadSceneMode.Single);

            Assert.That(registry.IsRoomVisited(namedScene.name), Is.True);
        }
        finally
        {
            DeleteTempScene(scenePath);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void OnSceneLoaded_CalledTwiceForSameScene_IsIdempotent()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        GameObject go = new GameObject("GameManager Test");
        string scenePath = null;
        try
        {
            go.SetActive(false);
            GameManager gm = go.AddComponent<GameManager>();
            SetPrivateField(gm, "worldStateRegistry", registry);

            Scene namedScene = CreateNamedTempScene("RoomVisitationTestScene_Idempotent", out scenePath);

            int notifyCount = 0;
            registry.Subscribe(new WorldStateKey(WorldStateCategory.VisitedRoom, namedScene.name), _ => notifyCount++);

            InvokePrivate(gm, "OnSceneLoaded", namedScene, LoadSceneMode.Single);
            InvokePrivate(gm, "OnSceneLoaded", namedScene, LoadSceneMode.Single);

            Assert.That(registry.IsRoomVisited(namedScene.name), Is.True);
            Assert.That(notifyCount, Is.EqualTo(1), "A room entered more than once must not re-notify or duplicate its save entry.");
        }
        finally
        {
            DeleteTempScene(scenePath);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(registry);
        }
    }

    [Test]
    public void OnSceneLoaded_MissingRegistry_DoesNotThrow()
    {
        GameObject go = new GameObject("GameManager Test");
        string scenePath = null;
        try
        {
            go.SetActive(false);
            GameManager gm = go.AddComponent<GameManager>();
            // worldStateRegistry intentionally left unassigned.

            Scene namedScene = CreateNamedTempScene("RoomVisitationTestScene_MissingRegistry", out scenePath);
            Assert.DoesNotThrow(() => InvokePrivate(gm, "OnSceneLoaded", namedScene, LoadSceneMode.Single));
        }
        finally
        {
            DeleteTempScene(scenePath);
            Object.DestroyImmediate(go);
        }
    }

    // Scene.name is only populated once a scene has a file path (a brand-new in-memory scene's name
    // is always ""), so a real temporary scene asset is the only reliable way to exercise
    // scene-name-dependent behavior in an Edit Mode test. Callers must delete it via DeleteTempScene.
    private static Scene CreateNamedTempScene(string sceneName, out string scenePath)
    {
        // NewSceneMode.Additive requires the current base scene to already be saved, which the Test
        // Runner's own ambient scene never is -- NewSceneMode.Single has no such restriction and
        // replacing that ambient (always-empty-of-test-relevant-content) scene is harmless, since
        // every test in this suite creates its own GameObjects fresh rather than relying on
        // whatever scene happens to be loaded.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scenePath = $"Assets/_Project/Scenes/{sceneName}.unity";
        bool saved = EditorSceneManager.SaveScene(scene, scenePath);
        Assert.That(saved, Is.True, $"Failed to save temporary test scene at '{scenePath}'.");
        return scene;
    }

    private static void DeleteTempScene(string scenePath)
    {
        if (string.IsNullOrEmpty(scenePath))
            return;

        // The temp scene was loaded in Single mode, so it is very likely the only scene currently
        // loaded -- Unity refuses to close the last remaining scene. Replacing it with a fresh blank
        // scene first guarantees there is always at least one scene loaded before the asset is
        // deleted out from under it.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.DeleteAsset(scenePath);
    }

    [Test]
    public void VisitedRoom_SurvivesNormalDeathResetAndSaveRoundTrip()
    {
        WorldStateRegistry registry = ScriptableObject.CreateInstance<WorldStateRegistry>();
        try
        {
            registry.MarkRoomVisited("SampleScene");

            // Exactly the two resets GameManager.BeginRespawnSequence performs on every normal death.
            registry.ResetRespawnableEnemyDeaths();
            registry.ResetUntilDeathState();
            Assert.That(registry.IsRoomVisited("SampleScene"), Is.True, "Normal death must preserve visited-room state.");

            SaveData data = new SaveData();
            registry.GatherSaveData(data);
            Assert.That(data.world.visitedRoomIds, Contains.Item("SampleScene"));

            WorldStateRegistry destination = ScriptableObject.CreateInstance<WorldStateRegistry>();
            try
            {
                destination.ApplySaveData(data);
                Assert.That(destination.IsRoomVisited("SampleScene"), Is.True, "Continue must restore visited-room state.");
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
