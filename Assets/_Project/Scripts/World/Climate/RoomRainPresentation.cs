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
    [SerializeField] private ParticleSystem backRainLayer;
    [SerializeField] private ParticleSystem midRainLayer;
    [SerializeField] private ParticleSystem frontRainLayer;
    [SerializeField] private ParticleSystem splashSystem;
    [SerializeField] private RainImpactSplashHandler impactSplashHandler;
    [SerializeField] private AudioClip rainAmbienceClip;

    [Header("Rain tuning")]
    [SerializeField, Min(0f)] private float rainEmissionRate = 42f;
    [SerializeField, Min(0f)] private float backEmissionMultiplier = 0.7f;
    [SerializeField, Min(0f)] private float frontEmissionMultiplier = 0.18f;
    [SerializeField, Min(0f)] private float rainOpacity = 0.46f;
    [SerializeField, Range(0f, 1f)] private float backOpacityMultiplier = 0.38f;
    [SerializeField, Range(0f, 1f)] private float frontOpacityMultiplier = 0.34f;
    [SerializeField, Min(0f)] private float rampSeconds = 0.3f;
    [SerializeField, Min(0f)] private float ambienceVolume = 0.28f;
    [SerializeField, Min(0f)] private float ambienceFadeSeconds = 0.35f;

    [Header("Back camera coverage")]
    [SerializeField, Min(0f)] private float backCameraPadding = 3.5f;
    [SerializeField, Min(0.01f)] private float backSpawnBandHeight = 0.45f;
    [SerializeField, Min(0.01f)] private float backDepthBand = 0.35f;
    [SerializeField, Min(0.01f)] private float backDepthFromCamera = 44f;

    [Header("Mid camera coverage")]
    [SerializeField, Min(0f)] private float midCameraPadding = 3f;
    [SerializeField, Min(0.01f)] private float midSpawnBandHeight = 0.35f;
    [SerializeField, Min(0.01f)] private float midDepthBand = 0.4f;
    [SerializeField, Min(0.01f)] private float midDepthFromCamera = 37.8f;

    [Header("Front camera coverage")]
    [SerializeField, Min(0f)] private float frontCameraPadding = 4f;
    [SerializeField, Min(0.01f)] private float frontSpawnBandHeight = 0.5f;
    [SerializeField, Min(0.01f)] private float frontDepthBand = 0.3f;
    [SerializeField, Min(0.01f)] private float frontDepthFromCamera = 29.5f;

    [Header("Splash depth")]
    [SerializeField, Min(0.01f)] private float splashDepthFromCamera = 37.65f;

    [Header("Mid terrain collision")]
    [SerializeField] private LayerMask terrainCollisionLayers = 1 << 7;

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

    public int VisualLayerCount => (backRainLayer != null ? 1 : 0)
        + (midRainLayer != null ? 1 : 0)
        + (frontRainLayer != null ? 1 : 0);

    public ParticleSystem BackRainLayer => backRainLayer;
    public ParticleSystem MidRainLayer => midRainLayer;
    public ParticleSystem FrontRainLayer => frontRainLayer;
    public ParticleSystem SplashSystem => splashSystem;
    public RainImpactSplashHandler ImpactSplashHandler => impactSplashHandler;
    public LayerMask TerrainCollisionLayers => terrainCollisionLayers;

    /// <summary>The approved V1 rain is retained as the Mid/gameplay layer.</summary>
    public ParticleSystem RainLayer => midRainLayer;

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

        StartRainLayer(backRainLayer, rainEmissionRate * backEmissionMultiplier, wasRequested);
        StartRainLayer(midRainLayer, rainEmissionRate, wasRequested);
        StartRainLayer(frontRainLayer, rainEmissionRate * frontEmissionMultiplier, wasRequested);

        if (splashSystem != null && (!wasRequested || !splashSystem.isPlaying))
        {
            splashSystem.Play(false);
        }

        if (impactSplashHandler != null)
        {
            impactSplashHandler.SetImpactsEnabled(true);
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

        if (impactSplashHandler != null)
        {
            impactSplashHandler.SetImpactsEnabled(false);
        }

        StopRainLayer(backRainLayer);
        StopRainLayer(midRainLayer);
        StopRainLayer(frontRainLayer);

        if (splashSystem != null)
        {
            splashSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopAmbience(this, ambienceFadeSeconds);
        }
    }

    private void UpdateRainRamp(float deltaTime)
    {
        if (!rainRequested)
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

        SetEmissionRate(
            backRainLayer,
            rainEmissionRate * backEmissionMultiplier * currentRainIntensity);
        SetEmissionRate(midRainLayer, rainEmissionRate * currentRainIntensity);
        SetEmissionRate(
            frontRainLayer,
            rainEmissionRate * frontEmissionMultiplier * currentRainIntensity);
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

        ConfigureRainLayer(backRainLayer, backOpacityMultiplier, false);
        ConfigureRainLayer(midRainLayer, 1f, true);
        ConfigureRainLayer(frontRainLayer, frontOpacityMultiplier, false);

        if (splashSystem != null)
        {
            ParticleSystem.MainModule main = splashSystem.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.EmissionModule emission = splashSystem.emission;
            emission.enabled = false;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = splashSystem.shape;
            shape.enabled = false;
            splashSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (impactSplashHandler != null)
        {
            impactSplashHandler.SetImpactsEnabled(false);
        }
    }

    private void UpdateCameraCoverage()
    {
        if (backRainLayer == null && midRainLayer == null && frontRainLayer == null)
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

        UpdateLayerCoverage(
            camera,
            backRainLayer,
            backDepthFromCamera,
            backCameraPadding,
            backSpawnBandHeight,
            backDepthBand);
        UpdateLayerCoverage(
            camera,
            midRainLayer,
            midDepthFromCamera,
            midCameraPadding,
            midSpawnBandHeight,
            midDepthBand);
        UpdateLayerCoverage(
            camera,
            frontRainLayer,
            frontDepthFromCamera,
            frontCameraPadding,
            frontSpawnBandHeight,
            frontDepthBand);

        if (splashSystem != null)
        {
            splashSystem.transform.position = camera.ViewportToWorldPoint(
                new Vector3(0.5f, 0.5f, Mathf.Max(0.01f, splashDepthFromCamera)));
        }
    }

    private void ConfigureRainLayer(
        ParticleSystem layer,
        float opacityMultiplier,
        bool collisionSource)
    {
        if (layer == null)
        {
            return;
        }

        ParticleSystem.MainModule main = layer.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.startColor = new Color(
            0.76f,
            0.86f,
            0.98f,
            Mathf.Clamp01(rainOpacity * Mathf.Clamp01(opacityMultiplier)));

        ParticleSystem.EmissionModule emission = layer.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        ParticleSystem.VelocityOverLifetimeModule velocity = layer.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        ParticleSystem.CollisionModule collision = layer.collision;
        collision.sendCollisionMessages = collisionSource;
        collision.enableDynamicColliders = false;
        if (collisionSource)
        {
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision2D;
            collision.collidesWith = terrainCollisionLayers;
            collision.quality = ParticleSystemCollisionQuality.High;
            collision.dampen = 0f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;
            collision.minKillSpeed = 0f;
            collision.maxKillSpeed = 100f;
            collision.radiusScale = 0.35f;
            collision.maxCollisionShapes = 64;
            collision.enabled = true;
        }
        else
        {
            collision.enabled = false;
        }

        layer.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private static void UpdateLayerCoverage(
        Camera camera,
        ParticleSystem layer,
        float depthFromCamera,
        float cameraPadding,
        float spawnBandHeight,
        float depthBand)
    {
        if (layer == null)
        {
            return;
        }

        float depth = Mathf.Max(0.01f, depthFromCamera);
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, depth));
        Vector3 center = camera.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth));
        float worldWidth = Mathf.Abs(topRight.x - bottomLeft.x);
        float worldHeight = Mathf.Abs(topRight.y - bottomLeft.y);

        layer.transform.position = center;

        ParticleSystem.ShapeModule shape = layer.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(
            Mathf.Max(0.1f, worldWidth + cameraPadding * 2f),
            Mathf.Max(0.1f, spawnBandHeight),
            Mathf.Max(0.01f, depthBand));
        shape.position = new Vector3(
            0f,
            worldHeight * 0.5f + cameraPadding + spawnBandHeight * 0.5f,
            0f);
    }

    private void StartRainLayer(ParticleSystem layer, float fullEmissionRate, bool wasRequested)
    {
        if (layer == null)
        {
            return;
        }

        SetEmissionRate(layer, fullEmissionRate * currentRainIntensity);
        if (!wasRequested || !layer.isPlaying)
        {
            layer.Play(false);
        }
    }

    private static void StopRainLayer(ParticleSystem layer)
    {
        if (layer == null)
        {
            return;
        }

        SetEmissionRate(layer, 0f);
        layer.Stop(false, ParticleSystemStopBehavior.StopEmitting);
    }

    private static void SetEmissionRate(ParticleSystem layer, float rate)
    {
        if (layer == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = layer.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Max(0f, rate);
    }
}
