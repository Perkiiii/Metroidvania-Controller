using System;
using UnityEngine;

/// <summary>
/// Samples real time and advances the persistent world clock while gameplay is active.
/// Calendar conversion and persistence remain owned by <see cref="WorldTimeState"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldClockDriver : MonoBehaviour
{
    [Header("World Time")]
    [Tooltip("Persistent clock state. The state owns CalendarConfig and exposes its pacing.")]
    [SerializeField] private WorldTimeState worldTimeState;

    [Header("Realtime Sampling")]
    [Tooltip("Maximum real seconds consumed by one frame. Excess hitch/alt-tab time is discarded.")]
    [SerializeField, Min(0f)] private float maxRealSecondsPerFrame = 0.25f;
    [Tooltip("Development-only multiplier for manually checking clock progression.")]
    [SerializeField, Min(0f)] private float debugMultiplier = 1f;

    // This is deliberately only a duplicate guard. The driver is not a public service singleton;
    // consumers read the persistent WorldTimeState asset instead.
    private static WorldClockDriver activeDriver;

    // Fractional real seconds must survive frame boundaries but are not save data. Keeping this as
    // double avoids losing sub-minute progress when a very slow clock is used for long sessions.
    private double fractionalRealSeconds;

    private void Awake()
    {
        if (activeDriver != null && activeDriver != this)
        {
            Destroy(gameObject);
            return;
        }

        activeDriver = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (activeDriver == this)
        {
            activeDriver = null;
        }
    }

    private void Update()
    {
        Tick(Time.unscaledDeltaTime);
    }

    private void Tick(float unscaledDeltaTime)
    {
        if (activeDriver != null && activeDriver != this)
        {
            return;
        }

        if (worldTimeState == null || !worldTimeState.IsLoaded)
        {
            return;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null || gameManager.State != GameState.Playing)
        {
            return;
        }

        long currentTotalGameMinutes = worldTimeState.TotalGameMinutes;
        if (currentTotalGameMinutes >= long.MaxValue)
        {
            // A loaded timestamp at the representable end is a valid, inert state. Discard
            // sampled wall time immediately so the accumulator cannot grow while saturated.
            fractionalRealSeconds = 0d;
            return;
        }

        // Time.unscaledDeltaTime should be finite and non-negative, but keeping the guard here
        // makes the sampler safe when a test or tooling harness supplies an invalid delta.
        if (float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime)
            || unscaledDeltaTime <= 0f)
        {
            return;
        }

        double hitchClamp = Math.Max(0d, maxRealSecondsPerFrame);
        if (hitchClamp <= 0d)
        {
            return;
        }

        double multiplier = Math.Max(0d, debugMultiplier);
        if (multiplier <= 0d)
        {
            return;
        }

        double pacing = worldTimeState.RealSecondsPerGameMinute;
        if (double.IsNaN(pacing) || double.IsInfinity(pacing) || pacing <= 0d)
        {
            return;
        }

        double sampledSeconds = Math.Min((double)unscaledDeltaTime, hitchClamp) * multiplier;
        if (!(sampledSeconds > 0d) || double.IsInfinity(sampledSeconds))
        {
            return;
        }

        fractionalRealSeconds += sampledSeconds;
        if (double.IsInfinity(fractionalRealSeconds))
        {
            // A malformed development multiplier must never turn into an unchecked long cast.
            fractionalRealSeconds = 0d;
            return;
        }

        double completeMinutesDouble = Math.Floor(fractionalRealSeconds / pacing);
        if (!(completeMinutesDouble >= 1d))
        {
            return;
        }

        long completeMinutes = completeMinutesDouble >= long.MaxValue
            ? long.MaxValue
            : (long)completeMinutesDouble;

        // Consume every complete real-time quantum, including any portion that cannot fit before
        // the canonical timestamp's end. This deliberately discards overflow-side wall time
        // instead of retrying an invalid AdvanceMinutes call on every later frame.
        fractionalRealSeconds -= completeMinutes * pacing;
        if (fractionalRealSeconds < 0d)
        {
            // Floating-point rounding can leave an infinitesimal negative remainder at an exact
            // boundary. It is not meaningful progress and must not reverse the accumulator.
            fractionalRealSeconds = 0d;
        }

        long availableMinutes = long.MaxValue - currentTotalGameMinutes;
        long minutesToAdvance = Math.Min(completeMinutes, availableMinutes);
        if (minutesToAdvance > 0L)
        {
            // availableMinutes bounds this call so WorldTimeState's checked target calculation
            // remains authoritative without receiving an overflow-inducing quantum.
            worldTimeState.AdvanceMinutes(minutesToAdvance);
        }
    }
}
