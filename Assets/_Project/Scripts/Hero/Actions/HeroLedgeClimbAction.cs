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

    public bool IsActive => active;
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
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        sensors = heroSensors;
        dash = dashAction;
        stopAnimation = stopLedgeAnimation;
        onBegin = onLedgeBegin;
    }

    public bool FixedTick(float fixedDeltaTime)
    {
        if (active)
        {
            TickActive(fixedDeltaTime);
            return true;
        }

        return TryStart();
    }

    public void CompleteFromAnimation()
    {
        animationCompleted = true;
    }

    public void Cancel(HeroLedgeClimbCancelReason reason = HeroLedgeClimbCancelReason.ExternalState)
    {
        if (!active && cleanedUp)
        {
            return;
        }

        LastCancelReason = reason;
        Cleanup(false);
    }

    private bool TryStart()
    {
        if (!CanEvaluateEntry())
        {
            return false;
        }

        int direction = blackboard.FacingDirection;
        bool inputIntent = Mathf.Abs(input.MoveVector.x) > config.horizontalInputDeadZone
            && Mathf.Sign(input.MoveVector.x) == direction;
        bool dashIntent = dash != null && dash.IsApproachingWall(direction);
        if (!inputIntent && !dashIntent)
        {
            return false;
        }

        if (!sensors.TryFindLedge(direction, out LedgeProbeResult result))
        {
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
            && blackboard.touchingWallFront
            && motor.Velocity.y < config.ledgeMaxUpwardSpeed;
    }

    private void Begin(in LedgeProbeResult result)
    {
        dash?.Cancel(HeroDashEndReason.LedgeClimb);
        onBegin?.Invoke();

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
        Vector2 finalPosition = target.TargetFrame != null
            ? target.ResolveStandingPosition()
            : motor.Position;
        motor.EndLedgeClimb(completed, finalPosition);
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
    }
}
