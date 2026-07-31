using System;
using UnityEngine;

public enum HeroLedgeClimbPhase
{
    None,
    Catch,
    PullUp,
    Settle
}

public enum HeroLedgeClimbCancelReason
{
    None,
    ExternalState,
    ControlLock,
    TargetInvalid,
    TargetMoved,
    PathBlocked,
    ComponentDisabled
}

public sealed class HeroLedgeClimbAction
{
    private const float TargetMotionTolerance = 0.001f;

    private readonly HeroConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;
    private readonly HeroSensors sensors;
    private readonly HeroDashAction dash;
    private readonly Action onBegin;
    private readonly Action stopAnimation;
    private readonly Action<bool, HeroLedgeClimbCancelReason> onEnded;

    private LedgeProbeResult target;
    private HeroLedgeClimbPhase phase;
    private float phaseElapsed;
    private Vector2 phaseStartPosition;
    private Vector3 initialTargetPosition;
    private Quaternion initialTargetRotation;
    private Vector3 initialTargetScale;
    private bool active;
    private bool cleanedUp = true;
    private bool animationCompleted;
    private bool preCatchReservationActive;
    private float preCatchReservationRemaining;

    public bool IsActive => active;
    public bool IsPreCatchReservingWallSlide => preCatchReservationActive;
    public HeroLedgeClimbPhase Phase => phase;
    public HeroLedgeClimbCancelReason LastCancelReason { get; private set; }
    public LedgeProbeFailure LastProbeFailure => sensors != null
        ? sensors.LastLedgeFailure
        : LedgeProbeFailure.NotInitialized;
    public bool AnimationCompleted => animationCompleted;

    public HeroLedgeClimbAction(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        HeroSensors heroSensors,
        HeroDashAction dashAction,
        Action stopLedgeAnimation)
        : this(
            heroConfig,
            stateBlackboard,
            inputReader,
            heroMotor,
            heroSensors,
            dashAction,
            stopLedgeAnimation,
            null,
            null)
    {
    }

    public HeroLedgeClimbAction(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        HeroSensors heroSensors,
        HeroDashAction dashAction,
        Action stopLedgeAnimation,
        Action onLedgeBegin)
        : this(
            heroConfig,
            stateBlackboard,
            inputReader,
            heroMotor,
            heroSensors,
            dashAction,
            stopLedgeAnimation,
            onLedgeBegin,
            null)
    {
    }

    public HeroLedgeClimbAction(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        HeroSensors heroSensors,
        HeroDashAction dashAction,
        Action stopLedgeAnimation,
        Action onLedgeBegin,
        Action<bool, HeroLedgeClimbCancelReason> onLedgeEnded)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        sensors = heroSensors;
        dash = dashAction;
        stopAnimation = stopLedgeAnimation;
        onBegin = onLedgeBegin;
        onEnded = onLedgeEnded;
    }

    public bool FixedTick(float fixedDeltaTime)
    {
        if (active)
        {
            TickActive(fixedDeltaTime);
            return true;
        }

        if (preCatchReservationActive)
        {
            return TickPreCatchReservation(fixedDeltaTime);
        }

        return TryStart(fixedDeltaTime);
    }

    public void CompleteFromAnimation()
    {
        animationCompleted = true;
    }

    public void Cancel(HeroLedgeClimbCancelReason reason = HeroLedgeClimbCancelReason.ExternalState)
    {
        if (!active && cleanedUp)
        {
            ClearPreCatchReservation();
            return;
        }

        LastCancelReason = reason;
        Cleanup(false);
    }

    private bool TryStart(float fixedDeltaTime)
    {
        if (!CanEvaluateEntry())
        {
            return false;
        }

        if (!TryGetApproachDirection(out int direction))
        {
            return false;
        }

        if (!sensors.TryFindLedge(direction, out LedgeProbeResult result))
        {
            if (IsValidPreCatchResult(result))
            {
                BeginPreCatchReservation(fixedDeltaTime);
            }

            return false;
        }

        Begin(result);
        return true;
    }

    private bool CanEvaluateEntry()
    {
        return config != null
            && blackboard != null
            && input != null
            && motor != null
            && sensors != null
            && !blackboard.grounded
            && !blackboard.wallSliding
            && !blackboard.wallJumping
            && !blackboard.attacking
            && !blackboard.attackRecovering
            && !blackboard.binding
            && !blackboard.recoiling
            && !blackboard.controlLocked
            && !blackboard.inputBlocked
            && motor.Velocity.y < config.ledgeMaxUpwardSpeed;
    }

    private bool TickPreCatchReservation(float fixedDeltaTime)
    {
        if (!CanEvaluateEntry()
            || !TryGetApproachDirection(out int direction)
            || preCatchReservationRemaining <= 0f)
        {
            ClearPreCatchReservation();
            return false;
        }

        if (!sensors.TryFindLedge(direction, out LedgeProbeResult result))
        {
            if (!IsValidPreCatchResult(result))
            {
                ClearPreCatchReservation();
                return false;
            }

            preCatchReservationRemaining -= Mathf.Max(0f, fixedDeltaTime);
            if (preCatchReservationRemaining <= 0f)
            {
                ClearPreCatchReservation();
            }

            return false;
        }

        ClearPreCatchReservation();
        Begin(result);
        return true;
    }

    private bool TryGetApproachDirection(out int direction)
    {
        if (dash != null && dash.IsApproachingWall())
        {
            direction = dash.Direction;
            return true;
        }

        float moveX = input.MoveVector.x;
        if (Mathf.Abs(moveX) <= config.horizontalInputDeadZone)
        {
            direction = 0;
            return false;
        }

        direction = moveX > 0f ? 1 : -1;
        return true;
    }

    private bool IsValidPreCatchResult(in LedgeProbeResult result)
    {
        return sensors.LastLedgeFailure == LedgeProbeFailure.PreCatchHeight
            && result.IsPreCatch
            && result.HasUsableTarget();
    }

    private void BeginPreCatchReservation(float fixedDeltaTime)
    {
        preCatchReservationRemaining = Mathf.Max(
            0f,
            config.ledgePreCatchGraceDuration - Mathf.Max(0f, fixedDeltaTime));
        preCatchReservationActive = preCatchReservationRemaining > 0f;
    }

    private void ClearPreCatchReservation()
    {
        preCatchReservationActive = false;
        preCatchReservationRemaining = 0f;
    }

    private void Begin(in LedgeProbeResult result)
    {
        ClearPreCatchReservation();
        dash?.Cancel(HeroDashEndReason.LedgeClimb);

        target = result;
        active = true;
        cleanedUp = false;
        animationCompleted = false;
        LastCancelReason = HeroLedgeClimbCancelReason.None;
        blackboard.ledgeClimbing = true;
        blackboard.wallSliding = false;
        blackboard.jumping = false;
        blackboard.jumpSustaining = false;
        blackboard.actorState = HeroActorState.Airborne;

        Transform frame = target.TargetFrame;
        initialTargetPosition = frame.position;
        initialTargetRotation = frame.rotation;
        initialTargetScale = frame.lossyScale;

        motor.BeginLedgeClimb();
        onBegin?.Invoke();
        BeginPhase(HeroLedgeClimbPhase.Catch);
    }

    private void TickActive(float fixedDeltaTime)
    {
        if (!CanContinue())
        {
            return;
        }

        phaseElapsed += Mathf.Max(0f, fixedDeltaTime);
        float duration = GetPhaseDuration(phase);
        float progress = duration <= 0f ? 1f : Mathf.Clamp01(phaseElapsed / duration);
        Vector2 phaseTarget = ResolvePhaseTarget(phase);
        motor.SetLedgeClimbPosition(Vector2.Lerp(phaseStartPosition, phaseTarget, progress));

        if (progress < 1f)
        {
            return;
        }

        switch (phase)
        {
            case HeroLedgeClimbPhase.Catch:
                BeginPhase(HeroLedgeClimbPhase.PullUp);
                break;
            case HeroLedgeClimbPhase.PullUp:
                BeginPhase(HeroLedgeClimbPhase.Settle);
                break;
            case HeroLedgeClimbPhase.Settle:
                Complete();
                break;
        }
    }

    private bool CanContinue()
    {
        if (blackboard.controlLocked || blackboard.inputBlocked)
        {
            Cancel(HeroLedgeClimbCancelReason.ControlLock);
            return false;
        }

        if (blackboard.actorState == HeroActorState.Hurt
            || blackboard.actorState == HeroActorState.Dead
            || blackboard.recoiling
            || blackboard.attacking
            || blackboard.binding)
        {
            Cancel(HeroLedgeClimbCancelReason.ExternalState);
            return false;
        }

        if (!target.HasUsableTarget())
        {
            Cancel(HeroLedgeClimbCancelReason.TargetInvalid);
            return false;
        }

        Transform frame = target.TargetFrame;
        if (Vector3.Distance(frame.position, initialTargetPosition) > TargetMotionTolerance
            || Quaternion.Angle(frame.rotation, initialTargetRotation) > TargetMotionTolerance
            || Vector3.Distance(frame.lossyScale, initialTargetScale) > TargetMotionTolerance)
        {
            Cancel(HeroLedgeClimbCancelReason.TargetMoved);
            return false;
        }

        if (!sensors.ValidateLedgePath(target))
        {
            Cancel(HeroLedgeClimbCancelReason.PathBlocked);
            return false;
        }

        return true;
    }

    private void BeginPhase(HeroLedgeClimbPhase nextPhase)
    {
        phase = nextPhase;
        phaseElapsed = 0f;
        phaseStartPosition = motor.Position;
    }

    private Vector2 ResolvePhaseTarget(HeroLedgeClimbPhase currentPhase)
    {
        return currentPhase switch
        {
            HeroLedgeClimbPhase.Catch => target.ResolveCatchPosition(),
            HeroLedgeClimbPhase.PullUp => target.ResolveCrestPosition(),
            HeroLedgeClimbPhase.Settle => target.ResolveStandingPosition(),
            _ => motor.Position
        };
    }

    private float GetPhaseDuration(HeroLedgeClimbPhase currentPhase)
    {
        return currentPhase switch
        {
            HeroLedgeClimbPhase.Catch => config.ledgeCatchDuration,
            HeroLedgeClimbPhase.PullUp => config.ledgePullUpDuration,
            HeroLedgeClimbPhase.Settle => config.ledgeSettleDuration,
            _ => 0f
        };
    }

    private void Complete()
    {
        if (!target.HasUsableTarget() || !sensors.ValidateLedgePath(target))
        {
            Cancel(HeroLedgeClimbCancelReason.PathBlocked);
            return;
        }

        Cleanup(true);
    }

    private void Cleanup(bool completed)
    {
        if (cleanedUp)
        {
            return;
        }

        cleanedUp = true;
        ClearPreCatchReservation();
        Vector2 finalPosition = target.TargetFrame != null
            ? target.ResolveStandingPosition()
            : motor.Position;
        motor.EndLedgeClimb(completed, finalPosition);
        if (completed)
        {
            // The final validated placement is the standing handoff; sensors will confirm it on
            // the next fixed step without inserting a transient airborne locomotion state.
            blackboard.grounded = true;
        }
        blackboard.ledgeClimbing = false;
        if (blackboard.actorState != HeroActorState.Hurt
            && blackboard.actorState != HeroActorState.Dead
            && blackboard.actorState != HeroActorState.Binding)
        {
            blackboard.actorState = blackboard.grounded
                ? HeroActorState.Grounded
                : HeroActorState.Airborne;
        }

        stopAnimation?.Invoke();
        active = false;
        phase = HeroLedgeClimbPhase.None;
        phaseElapsed = 0f;
        target = default;
        onEnded?.Invoke(completed, completed ? HeroLedgeClimbCancelReason.None : LastCancelReason);
    }
}
