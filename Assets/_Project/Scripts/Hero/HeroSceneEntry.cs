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

    private bool placementDone;
    private bool placementHadUnknownSide;
    private TransitionPoint activeDest;

    public bool IsEnteringScene { get; private set; }
    public event Action EntryCompleted;

    public void Initialize(
        HeroController controller,
        HeroMotor heroMotor,
        HeroStateBlackboard heroBlackboard,
        HeroConfig heroConfig)
    {
        heroController = controller;
        motor = heroMotor;
        blackboard = heroBlackboard;
        config = heroConfig;
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
            Debug.LogError($"[HeroSceneEntry] Gate '{dest.GateKey}' has GateSide.Unknown — skipping entry motion.");
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
            EntryCompleted?.Invoke();
            return;
        }

        StartCoroutine(MotionRoutine(dest));
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
        motor.SetScriptedVelocity(new Vector2(0f, -dest.EntryDropSpeed));

        float elapsed = 0f;
        float maxTime = dest.EntryMaxFallbackTime;

        // Give sensors at least one fixed step to update grounded before testing it,
        // so a stale `grounded == true` from the previous scene cannot short-circuit.
        yield return new WaitForFixedUpdate();

        while (elapsed < maxTime)
        {
            if (blackboard != null && blackboard.grounded)
            {
                yield break;
            }
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

        while (elapsed < maxTime)
        {
            if (blackboard != null && blackboard.grounded)
            {
                yield break;
            }
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
                spawn.y = FindGroundY(spawn.x, spawn.y);
                break;
            case GateSide.Bottom:
                // Match study: launch from 3 m above the gate so the diagonal throw has room.
                spawn.y += 3f;
                break;
            case GateSide.Top:
            case GateSide.Unknown:
            default:
                break;
        }

        heroController.transform.position = spawn;
    }

    private float FindGroundY(float x, float startY)
    {
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
            return startY;
        }

        return hit.point.y;
    }
}
