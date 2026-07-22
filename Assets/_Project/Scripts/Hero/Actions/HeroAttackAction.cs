using System.Collections.Generic;
using UnityEngine;

public sealed class HeroAttackAction
{
    private const int DefaultMaxAttackHits = 16;

    private readonly Collider2D[] damageHitBuffer;
    private readonly Collider2D[] clashHitBuffer;
    private readonly HashSet<IHeroAttackReceiver> hitReceivers = new HashSet<IHeroAttackReceiver>();
    private readonly HashSet<IHeroAttackClashReceiver> clashReceivers = new HashSet<IHeroAttackClashReceiver>();
    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;
    private readonly HeroAudioController audio;
    private readonly GameObject owner;
    private readonly Transform ownerTransform;
    private readonly HeroAttackModule[] attackModules;
    private readonly float failSafeTimeout;
    private readonly PlayerResourceState resourceState;

    private readonly HeroAttackImpactFeedbackController impactFeedback;
    private readonly Collider2D[] terrainHitBuffer = new Collider2D[8];

    private float attackTimer;
    private float attackFallbackTimeout;
    private float cooldownTimer;
    private float recoveryTimer;
    private bool attackWindowActive;
    private bool downslashBounceConsumedThisAttack;
    private bool terrainImpactPlayedThisSwing;
    private bool connectFeedbackPlayedThisSwing;
    private bool resourceAwardedThisAttack;
    private int attackVersion;
    private HeroAttackDirection currentDirection;
    private HeroAttackModule currentModule;

    public int AttackVersion => attackVersion;

    public HeroAttackAction(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        HeroAudioController heroAudio,
        GameObject ownerObject,
        Transform ownerRoot,
        HeroAttackModule[] modules,
        float attackFailSafeTimeout,
        PlayerResourceState playerResourceState)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        audio = heroAudio;
        owner = ownerObject;
        ownerTransform = ownerRoot;
        attackModules = modules ?? new HeroAttackModule[0];
        failSafeTimeout = Mathf.Max(0.1f, attackFailSafeTimeout);
        resourceState = playerResourceState;

        int maxHits = Mathf.Max(1, config != null ? config.maxHitsPerSwing : DefaultMaxAttackHits);
        damageHitBuffer = new Collider2D[maxHits];
        clashHitBuffer = new Collider2D[maxHits];
        impactFeedback = ownerObject != null ? ownerObject.GetComponentInChildren<HeroAttackImpactFeedbackController>() : null;
        DeactivateAllModules();
    }

    public void Tick(float deltaTime)
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= deltaTime;
        }

        if (recoveryTimer > 0f)
        {
            recoveryTimer -= deltaTime;
        }

        blackboard.attackRecovering = recoveryTimer > 0f;

        if (input.HasBufferedAttack && CanStartAttack())
        {
            input.ConsumeAttackBuffer();
            StartAttack();
        }
    }

    public void FixedTick(float fixedDeltaTime)
    {
        if (!blackboard.attacking || currentModule == null)
        {
            return;
        }

        attackTimer += fixedDeltaTime;

        if (attackWindowActive)
        {
            currentModule.SetHitWindowActive(true);
            EvaluateDamageCollider();
            EvaluateClashCollider();
            EvaluateTerrainImpact();
        }

        if (attackTimer >= attackFallbackTimeout)
        {
            ForceStopAttackFromFailSafe();
        }
    }

    public void CancelAttack()
    {
        EndAttack(true);
    }

    public void BeginAttackWindow()
    {
        if (!blackboard.attacking || currentModule == null)
        {
            return;
        }

        attackWindowActive = true;
        currentModule.SetHitWindowActive(true);
        EvaluateDamageCollider();
        EvaluateClashCollider();
        EvaluateTerrainImpact();
    }

    public void EndAttackWindow()
    {
        attackWindowActive = false;

        if (currentModule != null)
        {
            currentModule.SetHitWindowActive(false);
        }
    }

    public void CompleteAttackFromAnimation()
    {
        if (!blackboard.attacking)
        {
            return;
        }

        StopAttack();
    }

    public void SetFallbackTimeout(float timeout)
    {
        if (blackboard.attacking && timeout > 0f)
        {
            attackFallbackTimeout = Mathf.Max(attackTimer, timeout);
        }
    }

    public void DrawGizmosSelected()
    {
        if (attackModules == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        for (int i = 0; i < attackModules.Length; i++)
        {
            DrawPolygonGizmo(attackModules[i] != null ? attackModules[i].damageCollider : null);
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < attackModules.Length; i++)
        {
            DrawPolygonGizmo(attackModules[i] != null ? attackModules[i].clashCollider : null);
        }
    }

    private bool CanStartAttack()
    {
        return cooldownTimer <= 0f
            && !blackboard.controlLocked
            && !blackboard.inputBlocked
            && !blackboard.attacking
            && !blackboard.dashing
            && !blackboard.wallJumping;
    }

    private void StartAttack()
    {
        currentDirection = DetermineAttackDirection();

        bool useAlt = false;
        if (currentDirection == HeroAttackDirection.Side)
        {
            if (Time.unscaledTime - blackboard.altAttackTime > config.altAttackResetTime)
                blackboard.altAttack = false;

            useAlt = blackboard.altAttack;
            blackboard.altAttack = !blackboard.altAttack;
            blackboard.altAttackTime = Time.unscaledTime;
        }

        currentModule = FindModule(currentDirection, useAlt);

        if (currentModule == null || !currentModule.HasDamageCollider)
        {
            Debug.LogWarning($"Hero attack skipped because no {currentDirection} attack module with a damage collider is configured.", owner);
            currentModule = null;
            return;
        }

        blackboard.attacking = true;
        blackboard.upAttacking = currentDirection == HeroAttackDirection.Up;
        blackboard.downAttacking = currentDirection == HeroAttackDirection.Down;
        blackboard.attackDirection = currentDirection;
        attackVersion++;

        attackTimer = 0f;
        attackFallbackTimeout = failSafeTimeout;
        attackWindowActive = false;
        downslashBounceConsumedThisAttack = false;
        terrainImpactPlayedThisSwing = false;
        connectFeedbackPlayedThisSwing = false;
        resourceAwardedThisAttack = false;
        cooldownTimer = config.attackCooldown;
        recoveryTimer = config.attackRecovery;
        blackboard.attackRecovering = true;
        hitReceivers.Clear();
        clashReceivers.Clear();
        DeactivateAllModules();
        currentModule.SetAttackWindowCallbacks(BeginAttackWindow, EndAttackWindow);
        currentModule.Activate(blackboard.FacingDirection);
    }

    private void StopAttack()
    {
        EndAttack(false);
    }

    private void ForceStopAttackFromFailSafe()
    {
        Debug.LogWarning("Attack fail-safe triggered. Attack was force-ended because animation completion did not arrive in time.", owner);
        EndAttack(false);
    }

    private void EndAttack(bool clearRecovery)
    {
        DeactivateAllModules();
        currentModule = null;
        attackTimer = 0f;
        attackFallbackTimeout = 0f;
        attackWindowActive = false;
        downslashBounceConsumedThisAttack = false;
        terrainImpactPlayedThisSwing = false;
        connectFeedbackPlayedThisSwing = false;
        resourceAwardedThisAttack = false;
        blackboard.attacking = false;
        blackboard.upAttacking = false;
        blackboard.downAttacking = false;
        hitReceivers.Clear();
        clashReceivers.Clear();

        if (clearRecovery)
        {
            recoveryTimer = 0f;
            blackboard.attackRecovering = false;
        }
    }

    private HeroAttackDirection DetermineAttackDirection()
    {
        if (input.MoveVector.y >= config.attackDirectionThreshold)
        {
            return HeroAttackDirection.Up;
        }

        if (input.MoveVector.y <= -config.attackDirectionThreshold && !blackboard.grounded)
        {
            return HeroAttackDirection.Down;
        }

        return HeroAttackDirection.Side;
    }

    private void EvaluateDamageCollider()
    {
        ContactFilter2D contactFilter = currentModule.CreateDamageFilter(config.attackHitLayers);
        int hitCount = currentModule.DamageCollider.Overlap(contactFilter, damageHitBuffer);
        Vector2 referencePoint = currentModule.GetDamageReferencePoint();

        for (int i = 0; i < hitCount && hitReceivers.Count < damageHitBuffer.Length; i++)
        {
            Collider2D hitCollider = damageHitBuffer[i];
            if (hitCollider == null || IsSelfCollider(hitCollider))
            {
                continue;
            }

            IHeroAttackReceiver receiver = FindComponentInParents<IHeroAttackReceiver>(hitCollider);
            if (receiver == null || !hitReceivers.Add(receiver))
            {
                continue;
            }

            HeroAttackHit hit = new HeroAttackHit(
                owner,
                currentDirection,
                config.attackDamage,
                hitCollider.ClosestPoint(referencePoint),
                GetForceDirection());

            HeroAttackResult result = receiver.ReceiveHeroAttack(hit);
            if (result.WasAccepted)
            {
                TriggerConnectFeel(hit, false);
            }
            TryAwardResource(result);
            IHeroDownslashResponder downslashResponder = FindDownslashResponder(hitCollider, receiver);
            NotifyDownslashResponder(downslashResponder, hit);
            TryApplyDownslashBounce(downslashResponder);
        }
    }

    private void EvaluateTerrainImpact()
    {
        if (terrainImpactPlayedThisSwing) return;
        if (hitReceivers.Count > 0) return;
        if (clashReceivers.Count > 0) return;
        if (downslashBounceConsumedThisAttack) return;
        if (currentModule == null || !currentModule.HasDamageCollider) return;

        int maskValue = config.attackTerrainLayers.value != 0
            ? config.attackTerrainLayers.value
            : config.terrainLayers.value;

        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(maskValue);
        filter.useTriggers = false;

        int hitCount = currentModule.DamageCollider.Overlap(filter, terrainHitBuffer);
        if (hitCount == 0) return;

        if (TryGetDirectionalTerrainSurface(maskValue, out Vector2 directionalContact))
        {
            PlayTerrainImpactAt(directionalContact);
            return;
        }

        Vector2 referencePoint = currentModule.GetDamageReferencePoint();
        float nearestSqDist = float.MaxValue;
        Vector2 bestContact = Vector2.zero;
        bool found = false;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = terrainHitBuffer[i];
            if (col == null || IsSelfCollider(col)) continue;

            Vector2 candidate = GetTerrainSurfacePoint(col, referencePoint);

            float sqDist = (candidate - referencePoint).sqrMagnitude;
            if (sqDist < nearestSqDist)
            {
                nearestSqDist = sqDist;
                bestContact = candidate;
                found = true;
            }
        }

        if (!found) return;

        PlayTerrainImpactAt(bestContact);
    }

    private void TryAwardResource(HeroAttackResult result)
    {
        if (resourceState == null
            || currentModule == null
            || !result.WasAccepted
            || !result.ResourceEligible
            || currentModule.resourceGenerationMode == HeroResourceGenerationMode.None
            || currentModule.resourceGainParts <= 0)
        {
            return;
        }

        if (currentModule.resourceGenerationMode == HeroResourceGenerationMode.FirstSuccessfulHitPerAttack
            && resourceAwardedThisAttack)
        {
            return;
        }

        int gained = resourceState.Gain(currentModule.resourceGainParts);
        if (gained > 0 && currentModule.resourceGenerationMode == HeroResourceGenerationMode.FirstSuccessfulHitPerAttack)
        {
            resourceAwardedThisAttack = true;
        }
    }

    private bool TryGetDirectionalTerrainSurface(int maskValue, out Vector2 contact)
    {
        contact = default;

        if (currentModule == null || currentModule.DamageCollider == null)
        {
            return false;
        }

        Vector2 direction = GetForceDirection();
        if (direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Bounds bounds = currentModule.DamageCollider.bounds;
        float directionExtent = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? bounds.extents.x
            : bounds.extents.y;

        float skin = 0.02f;
        float probeDistance = Mathf.Max(config.wallProbeDistance * 3f, 0.05f);
        Vector2 origin = (Vector2)bounds.center - direction * (directionExtent + skin);
        float distance = directionExtent * 2f + skin * 2f + probeDistance;

        RaycastHit2D rayHit = Physics2D.Raycast(origin, direction, distance, maskValue);
        if (rayHit.collider == null || IsSelfCollider(rayHit.collider))
        {
            return false;
        }

        if (rayHit.fraction <= 0f)
        {
            return TryGetDirectionalBoundsSurface(rayHit.collider, origin, direction, out contact);
        }

        contact = rayHit.point;
        return true;
    }

    private Vector2 GetTerrainSurfacePoint(Collider2D terrainCollider, Vector2 referencePoint)
    {
        Vector2 direction = GetForceDirection();
        if (TryGetDirectionalBoundsSurface(terrainCollider, referencePoint, direction, out Vector2 surfacePoint))
        {
            return surfacePoint;
        }

        ColliderDistance2D distance = currentModule.DamageCollider.Distance(terrainCollider);
        if (distance.isValid)
        {
            return distance.pointB;
        }

        return terrainCollider.ClosestPoint(referencePoint);
    }

    private static bool TryGetDirectionalBoundsSurface(
        Collider2D terrainCollider,
        Vector2 referencePoint,
        Vector2 direction,
        out Vector2 surfacePoint)
    {
        surfacePoint = default;

        if (terrainCollider == null || direction.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        Bounds bounds = terrainCollider.bounds;
        if (bounds.size.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        surfacePoint = referencePoint;
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            surfacePoint.x = direction.x > 0f ? bounds.min.x : bounds.max.x;
            surfacePoint.y = Mathf.Clamp(referencePoint.y, bounds.min.y, bounds.max.y);
        }
        else
        {
            surfacePoint.x = Mathf.Clamp(referencePoint.x, bounds.min.x, bounds.max.x);
            surfacePoint.y = direction.y > 0f ? bounds.min.y : bounds.max.y;
        }

        return true;
    }

    private void PlayTerrainImpactAt(Vector2 contact)
    {
        float z = impactFeedback != null ? impactFeedback.transform.position.z : ownerTransform.position.z;
        Vector3 worldContact = new Vector3(contact.x, contact.y, z);
        impactFeedback?.PlayTerrainImpact(currentDirection, worldContact);
        audio?.PlayTerrainImpact();
        terrainImpactPlayedThisSwing = true;
    }

    private void EvaluateClashCollider()
    {
        if (!currentModule.HasClashCollider)
        {
            return;
        }

        ContactFilter2D contactFilter = currentModule.CreateClashFilter(config.attackHitLayers);
        int hitCount = currentModule.ClashCollider.Overlap(contactFilter, clashHitBuffer);
        Vector2 referencePoint = currentModule.ClashCollider.bounds.center;

        for (int i = 0; i < hitCount && clashReceivers.Count < clashHitBuffer.Length; i++)
        {
            Collider2D hitCollider = clashHitBuffer[i];
            if (hitCollider == null || IsSelfCollider(hitCollider))
            {
                continue;
            }

            IHeroAttackClashReceiver receiver = FindComponentInParents<IHeroAttackClashReceiver>(hitCollider);
            if (receiver == null || !clashReceivers.Add(receiver))
            {
                continue;
            }

            HeroAttackHit clashHit = new HeroAttackHit(
                owner,
                currentDirection,
                0,
                hitCollider.ClosestPoint(referencePoint),
                GetForceDirection());

            receiver.ReceiveHeroAttackClash(clashHit);
            TriggerConnectFeel(clashHit, true);
        }
    }

    private IHeroDownslashResponder FindDownslashResponder(Collider2D hitCollider, IHeroAttackReceiver attackReceiver)
    {
        if (currentDirection != HeroAttackDirection.Down)
        {
            return null;
        }

        if (attackReceiver is IHeroDownslashResponder directResponder)
        {
            return directResponder;
        }

        return FindComponentInParents<IHeroDownslashResponder>(hitCollider);
    }

    private static void NotifyDownslashResponder(IHeroDownslashResponder responder, HeroAttackHit hit)
    {
        responder?.ReceiveHeroDownslash(hit);
    }

    private void TryApplyDownslashBounce(IHeroDownslashResponder responder)
    {
        if (responder == null
            || downslashBounceConsumedThisAttack
            || currentDirection != HeroAttackDirection.Down
            || blackboard.grounded)
        {
            return;
        }

        if (blackboard.controlLocked
            || blackboard.inputBlocked
            || blackboard.dashing
            || blackboard.recoiling)
        {
            return;
        }

        downslashBounceConsumedThisAttack = true;
        motor?.ApplyDownslashBounce();
    }

    private void TriggerConnectFeel(HeroAttackHit hit, bool isClash)
    {
        if (connectFeedbackPlayedThisSwing) return;
        connectFeedbackPlayedThisSwing = true;

        float stopDuration = isClash ? config.attackClashHitStopDuration : config.attackHitStopDuration;
        GameManager.Instance?.HitStop(stopDuration);
        CameraEventService.RequestShake(CameraShakeIntensity.Small, hit.Point, 1f, owner);
        impactFeedback?.PlayConnectFeedback(hit.Point);
    }

    private Vector2 GetForceDirection()
    {
        return currentDirection switch
        {
            HeroAttackDirection.Up => Vector2.up,
            HeroAttackDirection.Down => Vector2.down,
            _ => new Vector2(blackboard.FacingDirection, 0f)
        };
    }

    private HeroAttackModule FindModule(HeroAttackDirection direction, bool useAlt = false)
    {
        for (int i = 0; i < attackModules.Length; i++)
        {
            HeroAttackModule module = attackModules[i];
            if (module != null && module.direction == direction && module.isAlt == useAlt)
                return module;
        }

        if (useAlt)
        {
            for (int i = 0; i < attackModules.Length; i++)
            {
                HeroAttackModule module = attackModules[i];
                if (module != null && module.direction == direction)
                    return module;
            }
        }

        return null;
    }

    private bool IsSelfCollider(Collider2D hitCollider)
    {
        Transform hitTransform = hitCollider.transform;
        return hitTransform == ownerTransform || hitTransform.IsChildOf(ownerTransform);
    }

    private void DeactivateAllModules()
    {
        for (int i = 0; i < attackModules.Length; i++)
        {
            if (attackModules[i] != null)
            {
                attackModules[i].Deactivate();
            }
        }
    }

    private static T FindComponentInParents<T>(Collider2D hitCollider) where T : class
    {
        MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T receiver)
            {
                return receiver;
            }
        }

        return null;
    }

    private static void DrawPolygonGizmo(PolygonCollider2D polygon)
    {
        if (polygon == null)
        {
            return;
        }

        Transform polygonTransform = polygon.transform;
        for (int pathIndex = 0; pathIndex < polygon.pathCount; pathIndex++)
        {
            Vector2[] path = polygon.GetPath(pathIndex);
            for (int pointIndex = 0; pointIndex < path.Length; pointIndex++)
            {
                Vector3 from = polygonTransform.TransformPoint(path[pointIndex]);
                Vector3 to = polygonTransform.TransformPoint(path[(pointIndex + 1) % path.Length]);
                Gizmos.DrawLine(from, to);
            }
        }
    }
}
