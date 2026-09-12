using System;
using UnityEngine;

// Canonical game-minute owner. The clock driver supplies elapsed real time; all calendar
// derivation and boundary notification stays here so skips and normal ticks share one path.
[CreateAssetMenu(menuName = "Project/World/World Time State", fileName = "WorldTimeState")]
public sealed class WorldTimeState : ScriptableObject, ISaveTarget
{
    [SerializeField] private CalendarConfig calendarConfig;

    [NonSerialized] private long totalGameMinutes;
    [NonSerialized] private bool runtimeTimestampInitialized;
    [NonSerialized] private bool isLoaded;
    [NonSerialized] private bool isAdvancing;

    public event Action<WorldTimeSnapshot> HourChanged;
    public event Action<WorldTimeSnapshot> DayChanged;
    public event Action<WorldTimeSnapshot> SeasonChanged;
    public event Action<WorldTimeSnapshot> YearChanged;
    public event Action<WorldTimeSnapshot> DayPhaseChanged;
    public event Action<WorldTimeSnapshot, WorldTimeSnapshot> TimeAdvanced;
    public event Action<WorldTimeSnapshot> StateApplied;

    public long TotalGameMinutes
    {
        get
        {
            EnsureRuntimeTimestamp();
            return totalGameMinutes;
        }
    }

    public WorldTimeSnapshot Current
    {
        get
        {
            EnsureRuntimeTimestamp();
            return WorldTimeSnapshot.From(totalGameMinutes, calendarConfig);
        }
    }

    public bool IsLoaded => isLoaded;

    // Narrow read-only calendar-shape seam for climate definitions. WorldTimeState still owns the
    // CalendarConfig reference and climate assets do not retain a duplicate calendar reference.
    public int DaysPerSeason => calendarConfig != null ? calendarConfig.DaysPerSeason : 0;
    public int SeasonsPerYear => calendarConfig != null ? calendarConfig.SeasonsPerYear : 0;

    // Deliberately narrow driver-facing seam: the driver need not retain CalendarConfig.
    public float RealSecondsPerGameMinute => calendarConfig != null
        ? calendarConfig.RealSecondsPerGameMinute
        : 0f;

    private void OnEnable()
    {
        totalGameMinutes = 0L;
        runtimeTimestampInitialized = false;
        isLoaded = false;
        isAdvancing = false;
    }

    public void AdvanceMinutes(long minutes)
    {
        if (isAdvancing)
            throw new InvalidOperationException("WorldTimeState cannot be advanced from a time callback.");
        if (minutes < 0)
            throw new ArgumentOutOfRangeException(nameof(minutes), minutes,
                "World time can only advance forward.");
        if (minutes == 0)
            return;

        EnsureRuntimeTimestamp();
        WorldTimeSnapshot before = Current;
        long target = checked(before.TotalGameMinutes + minutes);

        isAdvancing = true;
        try
        {
            bool hasNextBoundary = TryGetNextHourBoundary(before.TotalGameMinutes, out long nextBoundary);
            WorldTimeSnapshot previousBoundary = before;

            while (hasNextBoundary && nextBoundary <= target)
            {
                totalGameMinutes = nextBoundary;
                WorldTimeSnapshot boundary = Current;
                EmitBoundaryEvents(previousBoundary, boundary);
                previousBoundary = boundary;

                // Avoid overflowing while looking for a boundary beyond a near-max timestamp.
                if (nextBoundary > long.MaxValue - CalendarConfig.MinutesPerHour)
                    break;
                nextBoundary += CalendarConfig.MinutesPerHour;
            }

            totalGameMinutes = target;
            WorldTimeSnapshot after = Current;
            InvokeTimeAdvanced(before, after);
        }
        finally
        {
            isAdvancing = false;
        }
    }

    public void GatherSaveData(SaveData data)
    {
        if (data == null)
            return;

        EnsureRuntimeTimestamp();
        data.worldTime ??= new WorldTimeSaveData();
        data.worldTime.initialized = true;
        data.worldTime.totalGameMinutes = totalGameMinutes;
    }

    public void ApplySaveData(SaveData data)
    {
        if (isAdvancing)
            throw new InvalidOperationException("WorldTimeState cannot apply save data from a time callback.");

        EnsureConfig();

        WorldTimeSaveData saved = data?.worldTime;
        long nextTotalGameMinutes = saved != null && saved.initialized
            ? saved.totalGameMinutes
            : calendarConfig.ComputeInitialTotalGameMinutes();

        if (nextTotalGameMinutes < 0)
            throw new ArgumentOutOfRangeException(nameof(data), nextTotalGameMinutes,
                "Saved world time cannot be negative.");

        // Derive once before publishing the state so malformed shape/timestamp cannot leak a
        // partially applied value to StateApplied listeners.
        WorldTimeSnapshot next = WorldTimeSnapshot.From(nextTotalGameMinutes, calendarConfig);
        totalGameMinutes = next.TotalGameMinutes;
        runtimeTimestampInitialized = true;
        isLoaded = true;
        InvokeStateApplied(next);
    }

    private void EmitBoundaryEvents(WorldTimeSnapshot previous, WorldTimeSnapshot boundary)
    {
        // Subscribers at midnight must observe the new date from HourChanged onward, so the
        // fixed order intentionally puts HourChanged before date/season/year notifications.
        InvokeBoundarySubscribers(HourChanged, boundary);

        if (boundary.TotalDays != previous.TotalDays)
            InvokeBoundarySubscribers(DayChanged, boundary);
        if (boundary.SeasonInstanceIndex != previous.SeasonInstanceIndex)
            InvokeBoundarySubscribers(SeasonChanged, boundary);
        if (boundary.Year != previous.Year)
            InvokeBoundarySubscribers(YearChanged, boundary);
        if (boundary.Phase != previous.Phase)
            InvokeBoundarySubscribers(DayPhaseChanged, boundary);
    }

    private void InvokeBoundarySubscribers(Action<WorldTimeSnapshot> subscribers, WorldTimeSnapshot snapshot)
    {
        if (subscribers == null)
            return;

        Delegate[] invocationList = subscribers.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<WorldTimeSnapshot>)invocationList[i]).Invoke(snapshot);
            }
            catch (Exception exception)
            {
                // One presentation/gameplay observer must not strand canonical time or prevent
                // later observers and crossed boundaries from being delivered.
                Debug.LogException(exception, this);
            }
        }
    }

    private void InvokeTimeAdvanced(WorldTimeSnapshot before, WorldTimeSnapshot after)
    {
        if (TimeAdvanced == null)
            return;

        Delegate[] invocationList = TimeAdvanced.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<WorldTimeSnapshot, WorldTimeSnapshot>)invocationList[i]).Invoke(before, after);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private void InvokeStateApplied(WorldTimeSnapshot snapshot)
    {
        if (StateApplied == null)
            return;

        Delegate[] invocationList = StateApplied.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            try
            {
                ((Action<WorldTimeSnapshot>)invocationList[i]).Invoke(snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }

    private bool TryGetNextHourBoundary(long currentMinute, out long boundary)
    {
        long remainder = currentMinute % CalendarConfig.MinutesPerHour;
        long delta = CalendarConfig.MinutesPerHour - remainder;
        if (delta > long.MaxValue - currentMinute)
        {
            boundary = 0L;
            return false;
        }

        boundary = currentMinute + delta;
        return true;
    }

    private void EnsureRuntimeTimestamp()
    {
        if (runtimeTimestampInitialized)
            return;

        EnsureConfig();
        totalGameMinutes = calendarConfig.ComputeInitialTotalGameMinutes();
        runtimeTimestampInitialized = true;
    }

    private void EnsureConfig()
    {
        if (calendarConfig == null)
            throw new InvalidOperationException("WorldTimeState requires a CalendarConfig reference.");
    }
}
