using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldClockDriverTests
{
    private GameObject driverObject;
    private WorldClockDriver driver;
    private CalendarConfig calendarConfig;
    private WorldTimeState worldTimeState;
    private GameObject gameManagerObject;
    private GameManager gameManager;
    private float previousTimeScale;

    [SetUp]
    public void SetUp()
    {
        previousTimeScale = Time.timeScale;
        ResetGameManagerSingleton();
        SetStaticField(typeof(WorldClockDriver), "activeDriver", null);

        calendarConfig = ScriptableObject.CreateInstance<CalendarConfig>();
        calendarConfig.daysPerSeason = 28;
        calendarConfig.seasonsPerYear = 4;
        calendarConfig.epochWeekDay = WeekDay.Monday;
        calendarConfig.dawnHour = 5;
        calendarConfig.dayHour = 8;
        calendarConfig.duskHour = 18;
        calendarConfig.nightHour = 21;
        calendarConfig.initialYear = 1;
        calendarConfig.initialSeasonOrdinal = 0;
        calendarConfig.initialDayInSeason = 1;
        calendarConfig.initialHour = 8;
        calendarConfig.initialMinute = 0;
        calendarConfig.realSecondsPerGameMinute = 1f;

        worldTimeState = ScriptableObject.CreateInstance<WorldTimeState>();
        SetField(worldTimeState, "calendarConfig", calendarConfig);

        // Keep the component inactive so EditMode setup never invokes its persistent Awake path.
        driverObject = new GameObject("WorldClockDriver Test");
        driverObject.SetActive(false);
        driver = driverObject.AddComponent<WorldClockDriver>();
        SetField(driver, "worldTimeState", worldTimeState);
        SetField(driver, "maxRealSecondsPerFrame", 10f);
        SetField(driver, "debugMultiplier", 1f);
        SetStaticField(typeof(WorldClockDriver), "activeDriver", null);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (driverObject != null)
                Object.DestroyImmediate(driverObject);
            if (gameManagerObject != null)
                Object.DestroyImmediate(gameManagerObject);
            if (worldTimeState != null)
                Object.DestroyImmediate(worldTimeState);
            if (calendarConfig != null)
                Object.DestroyImmediate(calendarConfig);
        }
        finally
        {
            SetStaticField(typeof(WorldClockDriver), "activeDriver", null);
            ResetGameManagerSingleton();
            Time.timeScale = previousTimeScale;
        }
    }

    [Test]
    public void TickBeforeSaveLoadDoesNotAdvanceOrMarkStateLoaded()
    {
        WorldTimeSnapshot before = worldTimeState.Current;

        Tick(5f);

        Assert.That(worldTimeState.IsLoaded, Is.False);
        Assert.That(worldTimeState.Current.TotalGameMinutes, Is.EqualTo(before.TotalGameMinutes));
    }

    [Test]
    public void TickWithoutGameManagerDoesNotAdvanceLoadedState()
    {
        ApplyLoadedTime();
        Assert.That(GameManager.Instance, Is.Null);

        Tick(5f);

        Assert.That(worldTimeState.TotalGameMinutes, Is.Zero);
    }

    [Test]
    public void PlayingTickRetainsFractionalSecondsAndAdvancesCompleteMinutes()
    {
        CreateGameManager();
        ApplyLoadedTime();

        Tick(0.4f);
        Assert.That(worldTimeState.TotalGameMinutes, Is.Zero);
        Assert.That(GetAccumulator(), Is.EqualTo(0.4d).Within(1e-6d));

        Tick(0.6f);
        Assert.That(worldTimeState.TotalGameMinutes, Is.EqualTo(1L));
        Assert.That(GetAccumulator(), Is.EqualTo(0d).Within(1e-6d));

        Tick(1.5f);
        Assert.That(worldTimeState.TotalGameMinutes, Is.EqualTo(2L));
        Assert.That(GetAccumulator(), Is.EqualTo(0.5d).Within(1e-6d));
    }

    [Test]
    public void PauseAndSceneTransitionStatesBlockTheClock()
    {
        CreateGameManager();
        ApplyLoadedTime();

        gameManager.Pause();
        Assert.That(gameManager.State, Is.EqualTo(GameState.Paused));
        Assert.That(Time.timeScale, Is.Zero);
        Tick(5f);
        Assert.That(worldTimeState.TotalGameMinutes, Is.Zero);

        gameManager.Unpause();
        // SceneTransitionManager publishes Loading/EnteringLevel/ExitingLevel through the
        // GameManager state during its transition. The driver intentionally gates on that
        // state, rather than adding a second transition-manager dependency.
        GameState[] transitionStates =
        {
            GameState.Loading,
            GameState.EnteringLevel,
            GameState.ExitingLevel
        };

        for (int i = 0; i < transitionStates.Length; i++)
        {
            SetField(gameManager, "<State>k__BackingField", transitionStates[i]);
            Assert.That(gameManager.State, Is.EqualTo(transitionStates[i]));
            Tick(5f);
            Assert.That(worldTimeState.TotalGameMinutes, Is.Zero);
        }

        SetField(gameManager, "<State>k__BackingField", GameState.Playing);
    }

    [Test]
    public void PlayingClockUsesUnscaledDeltaDuringHitStop()
    {
        CreateGameManager();
        ApplyLoadedTime();
        Time.timeScale = 0f;

        Assert.That(gameManager.State, Is.EqualTo(GameState.Playing));
        Tick(1f);

        Assert.That(worldTimeState.TotalGameMinutes, Is.EqualTo(1L));
        Assert.That(Time.timeScale, Is.Zero, "The driver must not write Time.timeScale.");
    }

    [Test]
    public void HitchClampDiscardsExcessRealTime()
    {
        CreateGameManager();
        ApplyLoadedTime();
        SetField(driver, "maxRealSecondsPerFrame", 0.25f);

        Tick(10f);

        Assert.That(worldTimeState.TotalGameMinutes, Is.Zero,
            "A ten-second hitch must contribute only the configured quarter-second clamp.");
        Assert.That(GetAccumulator(), Is.EqualTo(0.25d).Within(1e-6d));

        // Four individually clamped frames make one real second; the discarded hitch tail must
        // not be replayed by the driver on later frames.
        Tick(10f);
        Tick(10f);
        Tick(10f);
        Assert.That(worldTimeState.TotalGameMinutes, Is.EqualTo(1L));
        Assert.That(GetAccumulator(), Is.EqualTo(0d).Within(1e-6d));
    }

    [Test]
    public void DebugMultiplierScalesSampledRealTimeWithoutChangingTimeScale()
    {
        CreateGameManager();
        ApplyLoadedTime();
        SetField(driver, "debugMultiplier", 2f);
        Time.timeScale = 0.35f;

        Tick(0.5f);

        Assert.That(worldTimeState.TotalGameMinutes, Is.EqualTo(1L));
        Assert.That(Time.timeScale, Is.EqualTo(0.35f).Within(1e-6f));

        SetField(driver, "debugMultiplier", 0f);
        Tick(5f);
        Assert.That(worldTimeState.TotalGameMinutes, Is.EqualTo(1L));
    }

    [Test]
    public void RepeatedTicksAtMaximumTimestampAreHarmless()
    {
        CreateGameManager();
        ApplyLoadedTime(long.MaxValue);

        Assert.DoesNotThrow(() => Tick(10f));
        Assert.DoesNotThrow(() => Tick(10f));

        Assert.That(worldTimeState.TotalGameMinutes, Is.EqualTo(long.MaxValue));
        Assert.That(GetAccumulator(), Is.EqualTo(0d).Within(1e-6d));
    }

    [Test]
    public void NearMaximumTimestampAdvancesOnlyAvailableMinutesAndSaturates()
    {
        CreateGameManager();
        ApplyLoadedTime(long.MaxValue - 2L);

        Assert.DoesNotThrow(() => Tick(5f));

        Assert.That(worldTimeState.TotalGameMinutes, Is.EqualTo(long.MaxValue));
        Assert.That(GetAccumulator(), Is.EqualTo(0d).Within(1e-6d),
            "Real-time quanta beyond the representable end must be discarded.");
        Assert.DoesNotThrow(() => Tick(1f));
        Assert.That(worldTimeState.TotalGameMinutes, Is.EqualTo(long.MaxValue));
    }

    private void ApplyLoadedTime(long totalGameMinutes = 0L)
    {
        worldTimeState.ApplySaveData(new SaveData
        {
            worldTime = new WorldTimeSaveData
            {
                initialized = true,
                totalGameMinutes = totalGameMinutes
            }
        });
    }

    private GameManager CreateGameManager()
    {
        gameManagerObject = new GameObject("GameManager Test");
        gameManagerObject.SetActive(false);
        gameManager = gameManagerObject.AddComponent<GameManager>();

        SceneLoader sceneLoader = new SceneLoader();
        SetField(gameManager, "_sceneLoader", sceneLoader);
        SetField(gameManager, "_sceneTransitionManager", new SceneTransitionManager(gameManager, sceneLoader));
        SetField(gameManager, "<State>k__BackingField", GameState.Playing);
        SetStaticField(typeof(GameManager), "<Instance>k__BackingField", gameManager);
        return gameManager;
    }

    private void Tick(float unscaledDeltaTime)
    {
        MethodInfo method = typeof(WorldClockDriver).GetMethod(
            "Tick",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(float) },
            null);
        Assert.That(method, Is.Not.Null, "WorldClockDriver.Tick(float) was not found.");
        method.Invoke(driver, new object[] { unscaledDeltaTime });
    }

    private double GetAccumulator()
    {
        return (double)GetField(driver, "fractionalRealSeconds");
    }

    private static void ResetGameManagerSingleton()
    {
        FieldInfo backing = typeof(GameManager).GetField(
            "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(backing, Is.Not.Null, "GameManager.Instance backing field not found.");

        GameManager stale = backing.GetValue(null) as GameManager;
        if (stale != null)
            Object.DestroyImmediate(stale.gameObject);

        backing.SetValue(null, null);
    }

    private static void SetStaticField(System.Type type, string name, object value)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Static field not found: " + name);
        field.SetValue(null, value);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        field.SetValue(target, value);
    }

    private static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        return field.GetValue(target);
    }
}
