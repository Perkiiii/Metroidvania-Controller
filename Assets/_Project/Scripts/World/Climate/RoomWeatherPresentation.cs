using System;
using UnityEngine;

/// <summary>
/// Presentation-only weather request for one loaded room. The simulation weather enum is kept
/// out of this value so a visual consumer cannot accidentally treat presentation as world state.
/// </summary>
public enum RoomWeatherPresentationMode
{
    Clear,
    Rain,
    Storm
}

/// <summary>
/// Scene-local consumer of the persistent regional weather state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomWeatherPresentation : MonoBehaviour
{
    [Header("Weather Presentation")]
    [SerializeField] private WorldWeatherState weatherState;
    [SerializeField] private RoomClimateContext roomClimateContext;

    /// <summary>
    /// Current presentation request. Concrete effects can read this value without owning any
    /// simulation state.
    /// </summary>
    public RoomWeatherPresentationMode CurrentPresentation { get; private set; }

    /// <summary>
    /// Raised when the scene-local presentation request changes. This is intentionally the only
    /// output seam; concrete particle, light, and audio consumers can subscribe later.
    /// </summary>
    public event Action<RoomWeatherPresentationMode> PresentationChanged;

    private WorldWeatherState subscribedWeatherState;

    private void OnEnable()
    {
        Subscribe();
        RefreshPresentation();
    }

    private void OnDisable()
    {
        Unsubscribe();
        SetPresentation(RoomWeatherPresentationMode.Clear);
    }

    private void OnDestroy()
    {
        // OnDestroy is also reached for an object that was already inactive, so keep this path
        // independent from OnDisable and make both operations idempotent.
        Unsubscribe();
        SetPresentation(RoomWeatherPresentationMode.Clear);
    }

    private void Subscribe()
    {
        if (weatherState == null)
            return;

        if (subscribedWeatherState == weatherState)
            return;

        Unsubscribe();
        subscribedWeatherState = weatherState;
        subscribedWeatherState.WeatherChanged += HandleWeatherChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedWeatherState == null)
        {
            subscribedWeatherState = null;
            return;
        }

        subscribedWeatherState.WeatherChanged -= HandleWeatherChanged;
        subscribedWeatherState = null;
    }

    private void HandleWeatherChanged(WeatherSnapshot snapshot)
    {
        if (roomClimateContext == null
            || !string.Equals(snapshot.RegionId, roomClimateContext.RegionId, StringComparison.Ordinal))
        {
            return;
        }

        // Resolve through the state again so the room always presents the current query result,
        // rather than making the event payload a second source of truth.
        RefreshPresentation();
    }

    private void RefreshPresentation()
    {
        if (weatherState == null || roomClimateContext == null
            || !weatherState.TryGetWeather(roomClimateContext.RegionId, out WeatherSnapshot snapshot))
        {
            SetPresentation(RoomWeatherPresentationMode.Clear);
            return;
        }

        SetPresentation(ResolvePresentation(snapshot.WeatherType, roomClimateContext.Exposure));
    }

    private void SetPresentation(RoomWeatherPresentationMode presentation)
    {
        if (CurrentPresentation == presentation)
            return;

        CurrentPresentation = presentation;
        PresentationChanged?.Invoke(presentation);
    }

    private static RoomWeatherPresentationMode ResolvePresentation(
        WeatherType weatherType,
        EnvironmentExposure exposure)
    {
        if (exposure != EnvironmentExposure.Outdoor)
            return RoomWeatherPresentationMode.Clear;

        switch (weatherType)
        {
            case WeatherType.Rain:
                return RoomWeatherPresentationMode.Rain;
            case WeatherType.Storm:
                return RoomWeatherPresentationMode.Storm;
            default:
                return RoomWeatherPresentationMode.Clear;
        }
    }
}
