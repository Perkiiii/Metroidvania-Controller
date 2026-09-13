using System;
using UnityEngine;

/// <summary>
/// Scene-local rain presentation consumer. The component owns no weather state; it follows the
/// request exposed by <see cref="RoomWeatherPresentation"/> and keeps the particle/audio cleanup
/// path idempotent across room reloads and repeated weather cycles.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomRainPresentation : MonoBehaviour
{
    [Header("Request and authored effects")]
    [SerializeField] private RoomWeatherPresentation weatherPresentation;
    [SerializeField] private ParticleSystem rainLayer;
    [SerializeField] private ParticleSystem[] splashEmitters = Array.Empty<ParticleSystem>();
    [SerializeField] private AudioClip rainAmbienceClip;

    [Header("Rain tuning")]
    [SerializeField, Min(0f)] private float rainEmissionRate = 30f;
    [SerializeField, Min(0f)] private float splashEmissionRate = 1.25f;
    [SerializeField, Min(0f)] private float rainOpacity = 0.22f;
    [SerializeField, Min(0f)] private float rampSeconds = 0.3f;
    [SerializeField, Min(0f)] private float ambienceVolume = 0.28f;
    [SerializeField, Min(0f)] private float ambienceFadeSeconds = 0.35f;

    [Header("Camera coverage")]
    [SerializeField, Min(0f)] private float cameraPadding = 1.25f;
    [SerializeField, Min(0.01f)] private float spawnBandHeight = 0.2f;
    [SerializeField, Min(0.01f)] private float depthBand = 0.25f;
    [SerializeField, Min(0.01f)] private float depthFromCamera = 37.8f;

    private RoomWeatherPresentation subscribedPresentation;
    private float currentRainIntensity;
    private float targetRainIntensity;
    private bool rainRequested;
    private bool configured;

    /// <summary>Latest presentation request observed by this effect.</summary>
    public RoomWeatherPresentationMode CurrentRequest { get; private set; }

    /// <summary>True while Rain or Storm requests are active for this room.</summary>
    public bool IsRainRequested => rainRequested;

    /// <summary>Current short-ramp value, useful for diagnostics and focused tests.</summary>
    public float RainIntensity => currentRainIntensity;

    /// <summary>Authored splash count; scene setup intentionally uses a small fixed set.</summary>
    public int SplashEmitterCount => splashEmitters == null ? 0 : splashEmitters.Length;

    /// <summary>Read-only access for validation tooling and focused tests.</summary>
    public ParticleSystem RainLayer => rainLayer;

    private void Awake()
    {
        ConfigureParticleSystems();
    }

    private void OnEnable()
    {
        ConfigureParticleSystems();
        Subscribe();

        if (weatherPresentation != null)
        {
            ApplyPresentation(weatherPresentation.CurrentPresentation);
        }
        else
        {
            ApplyPresentation(RoomWeatherPresentationMode.Clear);
        }

        UpdateCameraCoverage();
    }

    private void Update()
    {
        UpdateRainRamp(Time.unscaledDeltaTime);
    }

    private void LateUpdate()
    {
        UpdateCameraCoverage();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ApplyClear();
    }

    private void OnDestroy()
    {
        // Unity normally calls OnDisable before OnDestroy, but both operations stay safe when a
        // test or an unload path invokes them independently.
        Unsubscribe();
        ApplyClear();
    }

    private void Subscribe()
    {
        if (weatherPresentation == null || subscribedPresentation == weatherPresentation)
        {
            return;
        }

        Unsubscribe();
        subscribedPresentation = weatherPresentation;
        subscribedPresentation.PresentationChanged += HandlePresentationChanged;
    }

    private void Unsubscribe()
    {
        if (subscribedPresentation == null)
        {
            subscribedPresentation = null;
            return;
        }

        subscribedPresentation.PresentationChanged -= HandlePresentationChanged;
        subscribedPresentation = null;
    }

    private void HandlePresentationChanged(RoomWeatherPresentationMode presentation)
    {
        ApplyPresentation(presentation);
    }

    private void ApplyPresentation(RoomWeatherPresentationMode presentation)
    {
        CurrentRequest = presentation;

        if (presentation == RoomWeatherPresentationMode.Rain
            || presentation == RoomWeatherPresentationMode.Storm)
        {
            ApplyRain();
        }
        else
        {
            ApplyClear();
        }
    }

    private void ApplyRain()
    {
        bool wasRequested = rainRequested;
        rainRequested = true;
        targetRainIntensity = 1f;
        if (rampSeconds <= 0f)
        {
            currentRainIntensity = targetRainIntensity;
        }

        if (rainLayer != null)
        {
            ParticleSystem.EmissionModule emission = rainLayer.emission;
            emission.enabled = true;
            emission.rateOverTime = rainEmissionRate * currentRainIntensity;

            if (!wasRequested || !rainLayer.isPlaying)
            {
                rainLayer.Play(false);
            }
        }

        if (splashEmitters != null)
        {
            for (int i = 0; i < splashEmitters.Length; i++)
            {
                ParticleSystem splash = splashEmitters[i];
                if (splash == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = splash.emission;
                emission.enabled = true;
                emission.rateOverTime = splashEmissionRate;
                if (!wasRequested || !splash.isPlaying)
                {
                    splash.Play(false);
                }
            }
        }

        if (rainAmbienceClip != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.StartAmbience(
                this,
                rainAmbienceClip,
                ambienceVolume,
                ambienceFadeSeconds);
        }
    }

    private void ApplyClear()
    {
        CurrentRequest = RoomWeatherPresentationMode.Clear;
        rainRequested = false;
        targetRainIntensity = 0f;
        currentRainIntensity = 0f;

        if (rainLayer != null)
        {
            ParticleSystem.EmissionModule emission = rainLayer.emission;
            emission.rateOverTime = 0f;
            rainLayer.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        if (splashEmitters != null)
        {
            for (int i = 0; i < splashEmitters.Length; i++)
            {
                ParticleSystem splash = splashEmitters[i];
                if (splash == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = splash.emission;
                emission.rateOverTime = 0f;
                splash.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopAmbience(this, ambienceFadeSeconds);
        }
    }

    private void UpdateRainRamp(float deltaTime)
    {
        if (!rainRequested || rainLayer == null)
        {
            return;
        }

        if (rampSeconds <= 0f)
        {
            currentRainIntensity = targetRainIntensity;
        }
        else
        {
            currentRainIntensity = Mathf.MoveTowards(
                currentRainIntensity,
                targetRainIntensity,
                Mathf.Max(0f, deltaTime) / rampSeconds);
        }

        ParticleSystem.EmissionModule emission = rainLayer.emission;
        emission.rateOverTime = rainEmissionRate * currentRainIntensity;
    }

    private void ConfigureParticleSystems()
    {
        if (configured)
        {
            return;
        }

        configured = true;
        currentRainIntensity = 0f;
        targetRainIntensity = 0f;

        if (rainLayer != null)
        {
            ParticleSystem.MainModule main = rainLayer.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.74f, 0.84f, 0.94f, Mathf.Clamp01(rainOpacity));

            ParticleSystem.EmissionModule emission = rainLayer.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.VelocityOverLifetimeModule velocity = rainLayer.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.75f, 0.75f);
            velocity.y = new ParticleSystem.MinMaxCurve(-12f, -10f);
            velocity.z = 0f;

            ParticleSystem.CollisionModule collision = rainLayer.collision;
            collision.enabled = false;

            rainLayer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (splashEmitters == null)
        {
            return;
        }

        for (int i = 0; i < splashEmitters.Length; i++)
        {
            ParticleSystem splash = splashEmitters[i];
            if (splash == null)
            {
                continue;
            }

            ParticleSystem.MainModule main = splash.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = splash.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void UpdateCameraCoverage()
    {
        if (rainLayer == null)
        {
            return;
        }

        Camera camera = CameraInfoCache.MainCamera;
        if (camera == null)
        {
            camera = Camera.main;
        }

        if (camera == null)
        {
            return;
        }

        if (CameraInfoCache.MainCamera != camera || CameraInfoCache.FrameStamp != Time.frameCount)
        {
            CameraInfoCache.UpdateCache(camera);
        }

        Rect worldRect = CameraInfoCache.WorldRect;
        if (worldRect.width <= 0f || worldRect.height <= 0f)
        {
            return;
        }

        Transform rainTransform = rainLayer.transform;
        Vector3 position = rainTransform.position;
        position.x = worldRect.center.x;
        position.y = worldRect.center.y;
        position.z = camera.transform.position.z + Mathf.Max(0.01f, depthFromCamera);
        rainTransform.position = position;

        ParticleSystem.ShapeModule shape = rainLayer.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(
            Mathf.Max(0.1f, worldRect.width + cameraPadding * 2f),
            Mathf.Max(0.1f, spawnBandHeight),
            Mathf.Max(0.01f, depthBand));
        shape.position = new Vector3(
            0f,
            worldRect.height * 0.5f + cameraPadding + spawnBandHeight * 0.5f,
            0f);
    }
}
