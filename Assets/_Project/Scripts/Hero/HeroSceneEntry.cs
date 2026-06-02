using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroSceneEntry : MonoBehaviour
{
    private HeroController heroController;
    private HeroMotor motor;
    private HeroStateBlackboard blackboard;
    private HeroConfig config;
    private HeroHealthComponent health;

    private bool placementDone;
    private bool placementHadUnknownSide;
    private TransitionPoint activeDest;
    private Coroutine motionRoutine;
    private bool healthEventsSubscribed;

    public bool IsEnteringScene { get; private set; }
    public event Action EntryCompleted;

    public void Initialize(
        HeroController controller,
        HeroMotor heroMotor,
        HeroStateBlackboard heroBlackboard,
        HeroConfig heroConfig,
        HeroHealthComponent heroHealth)
    {
        heroController = controller;
        motor = heroMotor;
        blackboard = heroBlackboard;
        config = heroConfig;

        if (healthEventsSubscribed && health != null)
        {
            health.OnDeath -= CancelMotionDueToDeath;
            health.OnHazardDamaged -= CancelMotionDueToHazard;
            healthEventsSubscribed = false;
        }

        health = heroHealth;

        if (health != null)
        {
            health.OnDeath += CancelMotionDueToDeath;
            health.OnHazardDamaged += CancelMotionDueToHazard;
            healthEventsSubscribed = true;
        }
    }

    private void OnDestroy()
    {
        if (healthEventsSubscribed && health != null)
        {
            health.OnDeath -= CancelMotionDueToDeath;
            health.OnHazardDamaged -= CancelMotionDueToHazard;
            healthEventsSubscribed = false;
        }
    }

    private void CancelMotionDueToDeath()
    {
        CancelMotionForExternalInterrupt();
    }

    private void CancelMotionDueToHazard(DamageResult _)
    {
        CancelMotionForExternalInterrupt();
    }

    // Stops the in-progress motion coroutine so the respawn / hazard recovery sequence
    // can take over without fighting the scripted velocity. The coroutine's finally
    // block guarantees motor + control-lock cleanup.
    private void CancelMotionForExternalInterrupt()
    {
        if (motionRoutine == null) return;
        StopCoroutine(motionRoutine);
        motionRoutine = null;
        // finally inside MotionRoutine runs as part of the IEnumerator disposal triggered
        // by StopCoroutine, restoring motor + control lock state.
    }

    public void PrepareSceneEntry(TransitionPoint dest)
    {
        if (IsEnteringScene) return;
        if (dest == null) return;

        activeDest = dest;
        placementHadUnknownSide = dest.GateSide == GateSide.Unknown;

        ApplyFacing(dest);
        PlaceHeroAtGate(dest);

        if (placementHadUnknownSide)
        {
            Debug.LogError($"[HeroSceneEntry] Gate '{dest.name}' ({dest.GetGuid()}) has GateSide.Unknown - skipping entry motion.");
            placementDone = true;
            return;
        }

        heroController.AddControlLock(this);
        heroController.CancelAttack();
        motor.ResetMotion();
        motor.BeginScriptedEntry(zeroGravity: dest.GateSide == GateSide.Bottom);
        placementDone = true;
    }

    public void PlaySceneEntryMotion(TransitionPoint dest)
    {
        if (!placementDone)
        {
            EntryCompleted?.Invoke();
            return;
        }

        if (placementHadUnknownSide)
        {
            // Placement happened but no scripted motion will run. Clean up state.
            placementDone = false;
            placementHadUnknownSide = false;
            activeDest = null;
            EntryCompleted?.Invoke();
            return;
        }

        if (dest == null) dest = activeDest;
        if (dest == null)
        {
            // Defensive: clean up the scripted entry mode if we somehow lost the gate.
            motor.EndScriptedEntry();
            heroController.RemoveControlLock(this);
            placementDone = false;
            placementHadUnknownSide = false;
            activeDest = null;
            EntryCompleted?.Invoke();
            return;
        }

        motionRoutine = StartCoroutine(MotionRoutine(dest));
    }

    private IEnumerator MotionRoutine(TransitionPoint dest)
    {
        IsEnteringScene = true;
        try
        {
            switch (dest.GateSide)
            {
                case GateSide.Left:   yield return RunInEntry(+1, dest);  break;
                case GateSide.Right:  yield return RunInEntry(-1, dest);  break;
                case GateSide.Top:    yield return TopDropEntry(dest);    break;
                case GateSide.Bottom: yield return BottomEntry(dest);     break;
                case GateSide.Door:   yield return DoorEntry(dest);       break;
            }
        }
        finally
        {
            motor.EndScriptedEntry();
            heroController.RemoveControlLock(this);
            motionRoutine = null;
            IsEnteringScene = false;
            placementDone = false;
            placementHadUnknownSide = false;
            activeDest = null;
            EntryCompleted?.Invoke();
        }
    }

    private IEnumerator RunInEntry(int direction, TransitionPoint dest)
    {
        float speed = config != null ? config.runSpeed : 6.5f;
        motor.SetScriptedVelocity(new Vector2(direction * speed, 0f));

        float duration = dest.EntryRunInDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator TopDropEntry(TransitionPoint dest)
    {
        motor.SetScriptedVelocityX(0f, -dest.EntryDropSpeed);

        float elapsed = 0f;
        float maxTime = dest.EntryMaxFallbackTime;

        // Give sensors at least one fixed step to update grounded before testing it.
        yield return new WaitForFixedUpdate();

        // Require an ungrounded observation before grounded can complete the entry,
        // so a stale `grounded == true` (e.g. spawn position on geometry) cannot
        // short-circuit the drop. Max-time fallback below still guards against hang.
        bool sawUngrounded = false;
        while (elapsed < maxTime)
        {
            bool grounded = blackboard != null && blackboard.grounded;
            if (!grounded) sawUngrounded = true;
            else if (sawUngrounded) yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator BottomEntry(TransitionPoint dest)
    {
        int direction = blackboard != null && blackboard.facingRight ? 1 : -1;

        // Phase 1 — full velocity lock, gravity already suspended by BeginScriptedEntry.
        motor.SetScriptedVelocity(new Vector2(direction * dest.BottomThrowHorizontal, dest.BottomThrowVertical));

        float throwElapsed = 0f;
        float throwDuration = dest.BottomThrowDuration;
        while (throwElapsed < throwDuration)
        {
            throwElapsed += Time.deltaTime;
            yield return null;
        }

        // Phase 2 — release gravity, lock only X so the hero arcs down naturally.
        motor.SetGravitySuspended(false);
        motor.SetScriptedVelocityX(direction * dest.BottomThrowHorizontal);

        float elapsed = 0f;
        float maxTime = dest.EntryMaxFallbackTime;

        yield return new WaitForFixedUpdate();

        // Same sensor-safe guard as TopDropEntry: require an ungrounded observation
        // before grounded can complete the entry. The phase-1 throw normally lifts
        // the hero off the ground, but this protects against geometry that grounds
        // the hero mid-throw or sensors that haven't refreshed yet.
        bool sawUngrounded = false;
        while (elapsed < maxTime)
        {
            bool grounded = blackboard != null && blackboard.grounded;
            if (!grounded) sawUngrounded = true;
            else if (sawUngrounded) yield break;

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator DoorEntry(TransitionPoint dest)
    {
        motor.SetScriptedVelocity(Vector2.zero);

        float duration = dest.EntryRunInDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void ApplyFacing(TransitionPoint dest)
    {
        switch (dest.FacingOverride)
        {
            case EntryFacing.ForceRight: heroController.ForceFacingDirection(+1); break;
            case EntryFacing.ForceLeft:  heroController.ForceFacingDirection(-1); break;
        }
    }

    private void PlaceHeroAtGate(TransitionPoint dest)
    {
        Vector3 spawn = dest.EntrySpawnPosition;

        switch (dest.GateSide)
        {
            case GateSide.Left:
            case GateSide.Right:
            case GateSide.Door:
                if (TryFindGroundY(spawn.x, spawn.y, out float groundY))
                {
                    Vector2 groundedPosition = motor.GetPositionWithFeetAt(spawn, groundY);
                    spawn.x = groundedPosition.x;
                    spawn.y = groundedPosition.y;
                }
                break;
            case GateSide.Bottom:
                // Lift the spawn above the gate so (a) the hero doesn't immediately re-overlap
                // this gate's trigger on the way down and (b) the diagonal throw has clearance.
                // The lift amount is authored per-gate via TransitionPoint.bottomGateSpawnLift
                // (default 1.5 m). The +3 m hardcoded constant the reference project uses was
                // tuned for that game's scale; ours is per-gate-configurable.
                spawn.y += dest.BottomGateSpawnLift;
                break;
            case GateSide.Top:
            case GateSide.Unknown:
            default:
                break;
        }

        motor.TeleportTo(spawn);
    }

    private bool TryFindGroundY(float x, float startY, out float groundY)
    {
        groundY = startY;
        LayerMask mask = config != null ? config.terrainLayers : (LayerMask)~0;
        const float castFromAbove = 1f;
        const float castDistance = 8f;

        RaycastHit2D hit = Physics2D.Raycast(
            new Vector2(x, startY + castFromAbove),
            Vector2.down,
            castFromAbove + castDistance,
            mask);

        if (hit.collider == null)
        {
            Debug.LogWarning($"[HeroSceneEntry] FindGroundY: no terrain below ({x:F2}, {startY:F2}). Using start Y.");
            return false;
        }

        groundY = hit.point.y;
        return true;
    }
}
