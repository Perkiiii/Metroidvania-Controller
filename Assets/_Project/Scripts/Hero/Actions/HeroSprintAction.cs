using System;
using UnityEngine;

public sealed class HeroSprintAction
{
    private enum WildstridePhase
    {
        None,
        PendingAirDashLanding,
        Grounded,
        LedgeJumpBuffered,
        AirborneCarry,
        AirborneAuthorised,
        LedgeClimbSuspended,
        DisarmedUntilRelease
    }

    private readonly HeroConfig config;
    private readonly HeroAbilityConfig abilityConfig;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;
    private readonly PlayerAbilityState abilityState;
    private readonly HeroDashAction dash;

    private WildstridePhase phase;
    private int lastHandledDashSequenceVersion;
    private int authorisedDashSequenceVersion;
    private int pendingAirDashSequenceVersion;
    private int lastDownslashBounceVersion;
    private int capturedJumpCarryDirection;
    private float ledgeJumpBufferRemaining;

    public bool IsSprinting => phase == WildstridePhase.Grounded;
    public bool IsJumpCarrying => phase == WildstridePhase.AirborneCarry;
    public bool HasAirborneLandingAuthorisation => phase == WildstridePhase.AirborneCarry
        || phase == WildstridePhase.AirborneAuthorised;
    public bool HasPendingAirDashLanding => phase == WildstridePhase.PendingAirDashLanding;
    public bool HasLedgeJumpBuffer => phase == WildstridePhase.LedgeJumpBuffered;
    public bool IsLedgeClimbSuspended => phase == WildstridePhase.LedgeClimbSuspended;
    public int CapturedJumpCarryDirection => capturedJumpCarryDirection;
    public HeroLocomotionSpeed RequestedSpeed => IsSprinting
        ? HeroLocomotionSpeed.Wildstride
        : HeroLocomotionSpeed.Walk;

    public float ResolveGroundedMoveInput(float moveX)
    {
        if (!IsSprinting)
        {
            return moveX;
        }

        float deadZone = config != null ? config.horizontalInputDeadZone : 0.1f;
        return Mathf.Abs(moveX) > deadZone
            ? moveX
            : blackboard.sprintDirection;
    }

    public HeroSprintAction(
        HeroConfig heroConfig,
        HeroAbilityConfig heroAbilityConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        PlayerAbilityState heroAbilityState,
        HeroDashAction dashAction)
    {
        config = heroConfig;
        abilityConfig = heroAbilityConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        abilityState = heroAbilityState;
        dash = dashAction;
        lastDownslashBounceVersion = motor != null ? motor.DownslashBounceVersion : 0;
        SyncBlackboard();
    }

    public void Tick()
    {
        EvaluateState(false, 0f);
    }

    public void FixedTick(float fixedDeltaTime)
    {
        EvaluateState(true, fixedDeltaTime);
    }

    public bool TryBeginJumpCarry()
    {
        bool hasLaunchAuthorisation = IsSprinting
            || (HasLedgeJumpBuffer && ledgeJumpBufferRemaining > 0f);
        if (!hasLaunchAuthorisation || !CanContinueWithAuthorisation() || abilityConfig == null)
        {
            return false;
        }

        int launchDirection = GetInputDirection();
        if (launchDirection == 0)
        {
            launchDirection = blackboard.sprintDirection;
        }

        if (launchDirection == 0)
        {
            return false;
        }

        phase = WildstridePhase.AirborneCarry;
        capturedJumpCarryDirection = launchDirection;
        ledgeJumpBufferRemaining = 0f;
        blackboard.sprintDirection = launchDirection;
        blackboard.lastSprintCancelReason = HeroSprintCancelReason.None;
        motor?.BeginWildstrideCarry(launchDirection);
        SyncBlackboard();
        return true;
    }

    public void NotifyLanded()
    {
        if (HasPendingAirDashLanding)
        {
            ConsumePendingAirDashLanding();
            return;
        }

        if (HasLedgeJumpBuffer)
        {
            ConsumeAuthorisedLanding();
            return;
        }

        if (!HasAirborneLandingAuthorisation)
        {
            return;
        }

        ConsumeAuthorisedLanding();
    }

    public void NotifyDoubleJump()
    {
        if (!HasAuthorisation)
        {
            return;
        }

        // Double Jump is a confirmed Wildstride hard interruption. It does not touch the
        // motor-owned vertical jump path, but it consumes every continuation right and requires
        // a physical Dash release before a new sequence can qualify.
        Cancel(HeroSprintCancelReason.DoubleJump, true);
    }

    public void NotifyLedgeClimbStarted()
    {
        if (!HasAuthorisation)
        {
            // Preserve the established Dash-to-ledge contract for non-Wildstride climbs without
            // manufacturing a Wildstride authorization.
            input?.DisarmDashUntilRelease();
            ConsumeLatestDashCompletion();
            return;
        }

        phase = WildstridePhase.LedgeClimbSuspended;
        ledgeJumpBufferRemaining = 0f;
        motor?.EndWildstrideCarry();
        SyncBlackboard();
    }

    public void NotifyLedgeClimbEnded(bool completed, HeroLedgeClimbCancelReason reason)
    {
        if (!IsLedgeClimbSuspended)
        {
            return;
        }

        if (!completed)
        {
            bool dashReleased = !input.DashHeld || input.DashReleasedThisFrame;
            Cancel(
                dashReleased ? HeroSprintCancelReason.InputReleased : MapLedgeClimbCancelReason(reason),
                !dashReleased);
            return;
        }

        if (!input.DashCommandArmed)
        {
            Cancel(HeroSprintCancelReason.LedgeClimb, true);
            return;
        }

        if (!input.DashHeld || input.DashReleasedThisFrame)
        {
            Cancel(HeroSprintCancelReason.InputReleased, false);
            return;
        }

        if (!IsUnlocked())
        {
            Cancel(HeroSprintCancelReason.AbilityLocked, true);
            return;
        }

        int direction = GetInputDirection();
        if (direction == 0)
        {
            direction = blackboard.sprintDirection;
        }

        if (direction == 0 || HasIncompatibleState())
        {
            Cancel(HeroSprintCancelReason.LedgeClimb, true);
            return;
        }

        BeginGroundedWildstride(authorisedDashSequenceVersion, direction);
    }

    public void Cancel(HeroSprintCancelReason reason, bool disarmUntilRelease)
    {
        bool hadAuthorisation = HasAuthorisation;
        phase = disarmUntilRelease
            ? WildstridePhase.DisarmedUntilRelease
            : WildstridePhase.None;
        authorisedDashSequenceVersion = 0;
        pendingAirDashSequenceVersion = 0;
        capturedJumpCarryDirection = 0;
        ledgeJumpBufferRemaining = 0f;
        motor?.EndWildstrideCarry();
        ConsumeLatestDashCompletion();

        if (hadAuthorisation)
        {
            blackboard.lastSprintCancelReason = reason;
        }

        if (disarmUntilRelease)
        {
            input?.DisarmDashUntilRelease();
        }

        SyncBlackboard();
    }

    private bool HasAuthorisation => phase == WildstridePhase.PendingAirDashLanding
        || phase == WildstridePhase.Grounded
        || phase == WildstridePhase.LedgeJumpBuffered
        || phase == WildstridePhase.AirborneCarry
        || phase == WildstridePhase.AirborneAuthorised
        || phase == WildstridePhase.LedgeClimbSuspended;

    private void EvaluateState(bool advanceLedgeBuffer, float deltaTime)
    {
        if (blackboard == null || input == null)
        {
            return;
        }

        if (phase == WildstridePhase.DisarmedUntilRelease)
        {
            ConsumeLatestDashCompletion();
            if (input.DashCommandArmed && !input.DashHeld)
            {
                phase = WildstridePhase.None;
            }

            SyncBlackboard();
            return;
        }

        if (!IsUnlocked())
        {
            Cancel(HeroSprintCancelReason.AbilityLocked, HasAuthorisation);
            ConsumeLatestDashCompletion();
            return;
        }

        if (blackboard.controlLocked)
        {
            Cancel(HeroSprintCancelReason.ControlLock, true);
            return;
        }

        if (blackboard.inputBlocked || input.IsSuspended)
        {
            Cancel(HeroSprintCancelReason.InputSuspended, true);
            return;
        }

        if (blackboard.actorState == HeroActorState.Hurt || blackboard.recoiling)
        {
            Cancel(HeroSprintCancelReason.Hurt, true);
            return;
        }

        if (blackboard.actorState == HeroActorState.Dead)
        {
            Cancel(HeroSprintCancelReason.Death, true);
            return;
        }

        if (blackboard.ledgeClimbing)
        {
            if (HasAuthorisation && (!input.DashHeld || input.DashReleasedThisFrame))
            {
                Cancel(HeroSprintCancelReason.InputReleased, false);
            }

            return;
        }

        if (blackboard.binding)
        {
            Cancel(HeroSprintCancelReason.Bind, true);
            return;
        }

        if (blackboard.attacking || blackboard.attackRecovering)
        {
            Cancel(HeroSprintCancelReason.Attack, true);
            return;
        }

        if (blackboard.wallSliding || blackboard.wallJumping)
        {
            Cancel(HeroSprintCancelReason.WallState, true);
            return;
        }

        if (motor != null && motor.DownslashBounceVersion != lastDownslashBounceVersion)
        {
            lastDownslashBounceVersion = motor.DownslashBounceVersion;
            Cancel(HeroSprintCancelReason.DownslashBounce, true);
            return;
        }

        if (HasAuthorisation && (!input.DashHeld || input.DashReleasedThisFrame))
        {
            Cancel(HeroSprintCancelReason.InputReleased, false);
            return;
        }

        if (IsJumpCarrying)
        {
            int inputDirection = GetInputDirection();
            if (inputDirection == 0)
            {
                EndAirborneCarry(HeroSprintCancelReason.NeutralInput, true);
            }
            else if (inputDirection != capturedJumpCarryDirection)
            {
                EndAirborneCarry(HeroSprintCancelReason.DirectionReversed, true);
            }

        }

        if (IsJumpCarrying && blackboard.falling)
        {
            EndAirborneCarry(HeroSprintCancelReason.JumpCarryEnded, true);
        }

        if (IsSprinting)
        {
            UpdateGroundedDirection(GetInputDirection());
        }

        bool beganLedgeJumpBuffer = false;
        if (IsSprinting && !blackboard.grounded)
        {
            BeginLedgeJumpBuffer();
            beganLedgeJumpBuffer = HasLedgeJumpBuffer;
        }

        if (HasLedgeJumpBuffer)
        {
            if (blackboard.grounded && !blackboard.wasGrounded)
            {
                NotifyLanded();
                return;
            }

            if (advanceLedgeBuffer && !beganLedgeJumpBuffer)
            {
                ledgeJumpBufferRemaining -= Mathf.Max(0f, deltaTime);
                if (ledgeJumpBufferRemaining <= 0f)
                {
                    ExpireLedgeJumpBuffer();
                }
            }
        }

        if (HasPendingAirDashLanding && blackboard.grounded && !blackboard.wasGrounded)
        {
            ConsumePendingAirDashLanding();
            return;
        }

        if (HasAirborneLandingAuthorisation && blackboard.grounded && !blackboard.wasGrounded)
        {
            NotifyLanded();
            return;
        }

        TryConsumeDashCompletion();
    }

    private void BeginLedgeJumpBuffer()
    {
        float duration = abilityConfig != null ? abilityConfig.sprintLedgeJumpBufferTime : 0f;
        if (duration <= 0f)
        {
            ExpireLedgeJumpBuffer();
            return;
        }

        phase = WildstridePhase.LedgeJumpBuffered;
        ledgeJumpBufferRemaining = duration;
        blackboard.lastSprintCancelReason = HeroSprintCancelReason.None;
        SyncBlackboard();
    }

    private void ExpireLedgeJumpBuffer()
    {
        if (!HasLedgeJumpBuffer)
        {
            return;
        }

        // The short buffer is only the specialised Jump opportunity. The Dash sequence remains
        // authorised for ordinary airborne steering and its first valid landing.
        phase = WildstridePhase.AirborneAuthorised;
        ledgeJumpBufferRemaining = 0f;
        blackboard.lastSprintCancelReason = HeroSprintCancelReason.LedgeJumpBufferExpired;
        SyncBlackboard();
    }

    private void TryConsumeDashCompletion()
    {
        if (dash == null)
        {
            return;
        }

        HeroDashCompletion completion = dash.LastCompletion;
        if (completion.SequenceVersion <= lastHandledDashSequenceVersion)
        {
            return;
        }

        lastHandledDashSequenceVersion = completion.SequenceVersion;
        if (!completion.CompletedNaturally
            || !input.DashHeld
            || !input.DashCommandArmed
            || HasIncompatibleState())
        {
            return;
        }

        if (completion.StartedGrounded)
        {
            TryBeginGroundedWildstride(completion.SequenceVersion, GetInputDirection());
            return;
        }

        pendingAirDashSequenceVersion = completion.SequenceVersion;
        authorisedDashSequenceVersion = completion.SequenceVersion;
        phase = WildstridePhase.PendingAirDashLanding;
        blackboard.lastSprintCancelReason = HeroSprintCancelReason.None;
        SyncBlackboard();

        if (blackboard.grounded)
        {
            ConsumePendingAirDashLanding();
        }
    }

    private void ConsumePendingAirDashLanding()
    {
        if (!HasPendingAirDashLanding)
        {
            return;
        }

        int landingSequenceVersion = pendingAirDashSequenceVersion;
        pendingAirDashSequenceVersion = 0;
        phase = WildstridePhase.None;
        SyncBlackboard();

        // The first landing consumes this exact Dash sequence regardless of entry success.
        TryBeginGroundedWildstride(landingSequenceVersion, GetInputDirection());
    }

    private void ConsumeAuthorisedLanding()
    {
        int landingSequenceVersion = authorisedDashSequenceVersion;
        int landingDirection = GetInputDirection();
        if (landingDirection == 0)
        {
            landingDirection = blackboard.sprintDirection;
        }

        // The first landing consumes this sequence before entry is validated. A later landing
        // cannot replay it even if the current input or ability state is no longer eligible.
        phase = WildstridePhase.None;
        authorisedDashSequenceVersion = 0;
        capturedJumpCarryDirection = 0;
        ledgeJumpBufferRemaining = 0f;
        motor?.EndWildstrideCarry();
        SyncBlackboard();

        if (CanEnterGroundedWildstride(landingDirection))
        {
            BeginGroundedWildstride(landingSequenceVersion, landingDirection);
            return;
        }

        blackboard.lastSprintCancelReason = HeroSprintCancelReason.JumpCarryEnded;
        SyncBlackboard();
    }

    private bool TryBeginGroundedWildstride(int dashSequenceVersion, int direction)
    {
        if (!CanEnterGroundedWildstride(direction))
        {
            authorisedDashSequenceVersion = 0;
            SyncBlackboard();
            return false;
        }

        BeginGroundedWildstride(dashSequenceVersion, direction);
        return true;
    }

    private void BeginGroundedWildstride(int dashSequenceVersion, int direction)
    {
        authorisedDashSequenceVersion = dashSequenceVersion;
        capturedJumpCarryDirection = 0;
        ledgeJumpBufferRemaining = 0f;
        phase = WildstridePhase.Grounded;
        UpdateGroundedDirection(direction);
        blackboard.lastSprintCancelReason = HeroSprintCancelReason.None;
        SyncBlackboard();
    }

    private bool CanEnterGroundedWildstride(int direction)
    {
        return blackboard.grounded
            && IsUnlocked()
            && input.DashHeld
            && input.DashCommandArmed
            && direction != 0
            && !HasIncompatibleState();
    }

    private bool CanContinueWithAuthorisation()
    {
        return authorisedDashSequenceVersion > 0
            && IsUnlocked()
            && input.DashHeld
            && input.DashCommandArmed
            && !HasIncompatibleState();
    }

    private bool HasIncompatibleState()
    {
        return blackboard.controlLocked
            || blackboard.inputBlocked
            || blackboard.dashing
            || blackboard.attacking
            || blackboard.attackRecovering
            || blackboard.binding
            || blackboard.recoiling
            || blackboard.wallSliding
            || blackboard.wallJumping
            || blackboard.ledgeClimbing
            || blackboard.actorState == HeroActorState.Hurt
            || blackboard.actorState == HeroActorState.Dead;
    }

    private void UpdateGroundedDirection(int direction)
    {
        if (direction != 0)
        {
            blackboard.sprintDirection = direction;
        }
    }

    private void EndAirborneCarry(HeroSprintCancelReason reason, bool preserveLandingAuthorisation)
    {
        if (!IsJumpCarrying)
        {
            return;
        }

        motor?.EndWildstrideCarry();
        phase = preserveLandingAuthorisation
            ? WildstridePhase.AirborneAuthorised
            : WildstridePhase.None;
        if (!preserveLandingAuthorisation)
        {
            authorisedDashSequenceVersion = 0;
            capturedJumpCarryDirection = 0;
        }

        blackboard.lastSprintCancelReason = reason;
        SyncBlackboard();
    }

    private int GetInputDirection()
    {
        float moveX = input.MoveVector.x;
        float deadZone = config != null ? config.horizontalInputDeadZone : 0.1f;
        if (Mathf.Abs(moveX) <= deadZone)
        {
            return 0;
        }

        return moveX > 0f ? 1 : -1;
    }

    private static HeroSprintCancelReason MapLedgeClimbCancelReason(HeroLedgeClimbCancelReason reason)
    {
        switch (reason)
        {
            case HeroLedgeClimbCancelReason.ControlLock:
                return HeroSprintCancelReason.ControlLock;
            case HeroLedgeClimbCancelReason.ComponentDisabled:
                return HeroSprintCancelReason.ComponentDisabled;
            default:
                return HeroSprintCancelReason.LedgeClimb;
        }
    }

    private bool IsUnlocked()
    {
        return abilityState != null && abilityState.sprintUnlocked;
    }

    private void ConsumeLatestDashCompletion()
    {
        if (dash != null)
        {
            lastHandledDashSequenceVersion = Math.Max(
                lastHandledDashSequenceVersion,
                dash.LastCompletion.SequenceVersion);
        }
    }

    private void SyncBlackboard()
    {
        if (blackboard == null)
        {
            return;
        }

        blackboard.sprinting = IsSprinting;
        blackboard.sprintJumpCarrying = IsJumpCarrying;
        if (!HasAuthorisation)
        {
            blackboard.sprintDirection = 0;
        }
    }
}
