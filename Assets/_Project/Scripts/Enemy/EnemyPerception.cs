using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyPerception : MonoBehaviour
{
    public event Action<Transform> HeroDetected;
    public event Action HeroLost;

    [SerializeField] private LayerMask heroLayers;
    [SerializeField] private LayerMask lineOfSightBlockers;
    [SerializeField] private Transform eyePoint;
    [SerializeField] private EnemyConfig config;

    public bool IsHeroDetected { get; private set; }
    public bool HasLineOfSight { get; private set; }
    public Transform CurrentTarget { get; private set; }
    public Vector2 LastKnownHeroPosition { get; private set; }

    // Cached once on Initialize so we don't re-resolve the mask every FixedUpdate.
    private LayerMask resolvedObstructionMask;

    public void Initialize(EnemyConfig enemyConfig)
    {
        config = enemyConfig;
        resolvedObstructionMask = ResolveObstructionMask();
        if (resolvedObstructionMask.value == 0)
        {
            Debug.LogWarning(
                $"EnemyPerception on '{name}': obstruction mask resolved to 0. " +
                "Assign 'lineOfSightBlockers' or ensure EnemyConfig.terrainLayers is set — " +
                "enemies will see through all terrain until this is fixed.", this);
        }
    }

    private void FixedUpdate()
    {
        if (config == null)
        {
            return;
        }

        Collider2D hit = Physics2D.OverlapCircle(transform.position, config.detectionRadius, heroLayers);
        if (hit != null)
        {
            Transform target = hit.transform;
            Vector2 startPoint = transform.position;
            if (eyePoint != null)
            {
                startPoint = eyePoint.position;
            }
            else
            {
                Collider2D col = GetComponent<Collider2D>();
                if (col != null)
                {
                    startPoint = col.bounds.center;
                }
            }

            Vector2 endPoint = target.position;
            Collider2D targetCol = target.GetComponent<Collider2D>();
            if (targetCol != null)
            {
                endPoint = targetCol.bounds.center;
            }

            HasLineOfSight = resolvedObstructionMask.value == 0 || CheckLineOfSight(startPoint, endPoint, target);

            if (HasLineOfSight)
            {
                CurrentTarget = target;
                LastKnownHeroPosition = endPoint;
                if (!IsHeroDetected)
                {
                    IsHeroDetected = true;
                    HeroDetected?.Invoke(CurrentTarget);
                }
            }
            else
            {
                ClearTarget();
            }
        }
        else
        {
            HasLineOfSight = false;
            ClearTarget();
        }
    }

    private bool CheckLineOfSight(Vector2 start, Vector2 end, Transform targetTransform)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(start, end, resolvedObstructionMask);
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider != null)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    continue;
                }
                if (hit.transform == targetTransform || hit.transform.IsChildOf(targetTransform))
                {
                    continue;
                }
                return false;
            }
        }
        return true;
    }

    // Called once during Initialize to bake the mask; never called per-frame.
    private LayerMask ResolveObstructionMask()
    {
        if (lineOfSightBlockers.value != 0)
        {
            return lineOfSightBlockers;
        }
        if (config != null && config.terrainLayers.value != 0)
        {
            return config.terrainLayers;
        }
        // Last resort: attempt to find the layer by name.
        return LayerMask.GetMask("Terrain");
    }

    private void ClearTarget()
    {
        CurrentTarget = null;
        if (IsHeroDetected)
        {
            IsHeroDetected = false;
            HeroLost?.Invoke();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (config != null)
        {
            Gizmos.color = IsHeroDetected ? Color.red : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, config.detectionRadius);
        }

        Vector2 startPoint = transform.position;
        if (eyePoint != null)
        {
            startPoint = eyePoint.position;
        }
        else
        {
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                startPoint = col.bounds.center;
            }
        }

        if (eyePoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(eyePoint.position, 0.05f);
        }

        if (CurrentTarget != null)
        {
            Gizmos.color = HasLineOfSight ? Color.red : Color.gray;
            Gizmos.DrawLine(startPoint, CurrentTarget.position);
        }
    }
}
