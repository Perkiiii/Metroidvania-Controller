using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Converts sampled Mid rain collision events into bounded, reused splash particles. This is a
/// presentation helper only; <see cref="RoomRainPresentation"/> controls when impacts are accepted.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class RainImpactSplashHandler : MonoBehaviour
{
    private const int CollisionEventCapacity = 128;

    [Header("Owned particle systems")]
    [SerializeField] private ParticleSystem collisionSource;
    [SerializeField] private ParticleSystem splashSystem;

    [Header("Terrain impact filter")]
    [SerializeField] private LayerMask terrainLayers;
    [SerializeField, Range(-1f, 1f)] private float minimumUpwardNormalY = 0.65f;
    [SerializeField, Min(0f)] private float surfaceOffset = 0.03f;

    [Header("Bounded splash sampling")]
    [SerializeField, Min(0f)] private float minimumSecondsBetweenSplashes = 0.065f;
    [SerializeField, Range(1, CollisionEventCapacity)] private int maxEventsToInspect = 48;

    private readonly List<ParticleCollisionEvent> collisionEvents =
        new List<ParticleCollisionEvent>(CollisionEventCapacity);

    private bool acceptingImpacts;
    private float nextAllowedSplashTime;
    private int emittedSplashCount;

    public ParticleSystem CollisionSource => collisionSource;
    public ParticleSystem SplashSystem => splashSystem;
    public LayerMask TerrainLayers => terrainLayers;
    public float MinimumUpwardNormalY => minimumUpwardNormalY;
    public float SurfaceOffset => surfaceOffset;
    public float MinimumSecondsBetweenSplashes => minimumSecondsBetweenSplashes;
    public bool IsAcceptingImpacts => acceptingImpacts;
    public int EmittedSplashCount => emittedSplashCount;

    private void Awake()
    {
        if (collisionSource == null)
        {
            collisionSource = GetComponent<ParticleSystem>();
        }
    }

    private void OnDisable()
    {
        SetImpactsEnabled(false);
    }

    public void SetImpactsEnabled(bool enabled)
    {
        if (enabled && !acceptingImpacts)
        {
            emittedSplashCount = 0;
        }

        acceptingImpacts = enabled;
        nextAllowedSplashTime = 0f;
        collisionEvents.Clear();
    }

    private void OnParticleCollision(GameObject other)
    {
        if (!acceptingImpacts || collisionSource == null || splashSystem == null
            || Time.unscaledTime < nextAllowedSplashTime)
        {
            return;
        }

        collisionEvents.Clear();
        int eventCount = collisionSource.GetCollisionEvents(other, collisionEvents);
        int inspectedCount = Mathf.Min(eventCount, Mathf.Max(1, maxEventsToInspect));
        for (int i = 0; i < inspectedCount; i++)
        {
            ParticleCollisionEvent collisionEvent = collisionEvents[i];
            Collider2D collider = collisionEvent.colliderComponent as Collider2D;
            if (TryEmitSplash(
                    collider,
                    collisionEvent.intersection,
                    collisionEvent.normal,
                    Time.unscaledTime))
            {
                // One reused splash particle per accepted interval keeps the result readable and
                // bounds work even when several drops strike the same platform in one callback.
                return;
            }
        }
    }

    private bool TryEmitSplash(
        Collider2D collider,
        Vector3 intersection,
        Vector3 normal,
        float eventTime)
    {
        if (!acceptingImpacts || splashSystem == null
            || eventTime < nextAllowedSplashTime
            || !IsIntendedTerrainSurface(collider, normal))
        {
            return false;
        }

        Vector3 normalizedNormal = normal.sqrMagnitude > 0.0001f ? normal.normalized : Vector3.up;
        Vector3 position = intersection + normalizedNormal * Mathf.Max(0f, surfaceOffset);
        position.z = splashSystem.transform.position.z;

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = position,
            applyShapeToPosition = false
        };
        splashSystem.Emit(emitParams, 1);

        emittedSplashCount++;
        nextAllowedSplashTime = eventTime + Mathf.Max(0f, minimumSecondsBetweenSplashes);
        return true;
    }

    private bool IsIntendedTerrainSurface(Collider2D collider, Vector3 normal)
    {
        if (collider == null || !collider.enabled || collider.isTrigger
            || !collider.gameObject.activeInHierarchy)
        {
            return false;
        }

        int colliderLayerBit = 1 << collider.gameObject.layer;
        if ((terrainLayers.value & colliderLayerBit) == 0)
        {
            return false;
        }

        Rigidbody2D attachedBody = collider.attachedRigidbody;
        if (attachedBody != null && attachedBody.bodyType != RigidbodyType2D.Static)
        {
            return false;
        }

        float threshold = Mathf.Clamp(minimumUpwardNormalY, -1f, 1f);
        return normal.sqrMagnitude > 0.0001f && normal.normalized.y >= threshold;
    }
}
