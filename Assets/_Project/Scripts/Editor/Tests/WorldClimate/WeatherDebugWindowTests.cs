using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

public sealed class WeatherDebugWindowTests
{
    private const string CalendarAssetPath =
        "Assets/_Project/ScriptableObjects/World/CalendarConfig.asset";

    [Test]
    public void ActiveSlotUsesLatestSlotAtOrBeforeCurrentHour()
    {
        WorldTimeSnapshot snapshot = SnapshotAt(day: 3, hour: 10);

        bool resolved = WeatherDebugWindow.TryResolveActiveSlot(
            snapshot,
            new List<int> { 6, 18 },
            out WeatherDebugWindow.ActiveSlotResolution slot);

        Assert.That(resolved, Is.True);
        Assert.That(slot.AbsoluteDayIndex, Is.EqualTo(3L));
        Assert.That(slot.SlotIndex, Is.EqualTo(0));
        Assert.That(slot.SlotHour, Is.EqualTo(6));
        Assert.That(slot.IsPreviousDay, Is.False);
    }

    [Test]
    public void ActiveSlotBeforeFirstDailySlotUsesPriorDayFinalSlot()
    {
        WorldTimeSnapshot snapshot = SnapshotAt(day: 3, hour: 2);

        bool resolved = WeatherDebugWindow.TryResolveActiveSlot(
            snapshot,
            new List<int> { 6, 18 },
            out WeatherDebugWindow.ActiveSlotResolution slot);

        Assert.That(resolved, Is.True);
        Assert.That(slot.AbsoluteDayIndex, Is.EqualTo(2L));
        Assert.That(slot.SlotIndex, Is.EqualTo(1));
        Assert.That(slot.SlotHour, Is.EqualTo(18));
        Assert.That(slot.IsPreviousDay, Is.True);
    }

    [Test]
    public void DayZeroBeforeFirstSlotHasNoOverrideKey()
    {
        WorldTimeSnapshot snapshot = SnapshotAt(day: 0, hour: 2);

        bool resolved = WeatherDebugWindow.TryResolveActiveSlot(
            snapshot,
            new List<int> { 6, 18 },
            out _);

        Assert.That(resolved, Is.False);
    }

    [Test]
    public void ActionsRequireEveryRuntimeGate()
    {
        Assert.That(
            WeatherDebugWindow.CanUseActions(
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                true),
            Is.True);

        for (int disabledGate = 0; disabledGate < 8; disabledGate++)
        {
            bool[] gates =
            {
                true,
                true,
                true,
                true,
                true,
                true,
                true,
                true
            };
            gates[disabledGate] = false;

            Assert.That(
                WeatherDebugWindow.CanUseActions(
                    gates[0],
                    gates[1],
                    gates[2],
                    gates[3],
                    gates[4],
                    gates[5],
                    gates[6],
                    gates[7]),
                Is.False,
                $"Gate {disabledGate} must disable weather controls.");
        }
    }

    private static WorldTimeSnapshot SnapshotAt(long day, int hour)
    {
        CalendarConfig calendar = AssetDatabase.LoadAssetAtPath<CalendarConfig>(CalendarAssetPath);
        Assert.That(calendar, Is.Not.Null, "The authored CalendarConfig asset is required.");
        return WorldTimeSnapshot.From(
            day * CalendarConfig.MinutesPerDay + hour * CalendarConfig.MinutesPerHour,
            calendar);
    }
}
