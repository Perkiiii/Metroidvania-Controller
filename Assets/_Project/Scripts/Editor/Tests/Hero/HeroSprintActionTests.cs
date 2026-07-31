using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class HeroSprintActionTests
{
    private GameObject hero;
    private Rigidbody2D body;
    private BoxCollider2D collider;
    private HeroStateBlackboard blackboard;
    private HeroInputReader input;
    private HeroMotor motor;
    private HeroConfig config;
    private HeroAbilityConfig abilityConfig;
    private PlayerAbilityState abilityState;
    private PlayerResourceState resourceState;
    private HeroDashAction dash;
    private HeroSprintAction sprint;

    [SetUp]
    public void SetUp()
    {
        hero = new GameObject("Wildstride Test Hero");
        body = hero.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        collider = hero.AddComponent<BoxCollider2D>();
        blackboard = hero.AddComponent<HeroStateBlackboard>();
        input = hero.AddComponent<HeroInputReader>();
        motor = hero.AddComponent<HeroMotor>();

        config = ScriptableObject.CreateInstance<HeroConfig>();
        config.horizontalInputDeadZone = 0.3f;
        config.requireSprintForRun = false;
        config.walkSpeed = 4f;
        config.runSpeed = 6f;
        config.jumpSpeed = 14f;
        config.groundAcceleration = 20f;
        config.groundDeceleration = 25f;
        config.airAcceleration = 8f;
        config.airDeceleration = 6f;

        abilityConfig = ScriptableObject.CreateInstance<HeroAbilityConfig>();
        abilityConfig.dashSpeed = 18f;
        abilityConfig.dashDuration = 0.1f;
        abilityConfig.dashCooldown = 0.45f;
        abilityConfig.sprintSpeed = 10f;
        abilityConfig.sprintJumpSpeed = 10f;
        abilityConfig.sprintLedgeJumpBufferTime = 0.08f;

        abilityState = ScriptableObject.CreateInstance<PlayerAbilityState>();
        abilityState.dashUnlocked = true;
        abilityState.sprintUnlocked = true;
        resourceState = ScriptableObject.CreateInstance<PlayerResourceState>();
        resourceState.SetMaximumParts(100);
        resourceState.Gain(100);

        motor.Initialize(config, abilityConfig, blackboard, body, collider, null, null);
        dash = new HeroDashAction(config, abilityConfig, blackboard, input, motor, null, abilityState);
        sprint = new HeroSprintAction(config, abilityConfig, blackboard, input, motor, abilityState, dash);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(config);
        UnityEngine.Object.DestroyImmediate(abilityConfig);
        UnityEngine.Object.DestroyImmediate(abilityState);
        UnityEngine.Object.DestroyImmediate(resourceState);
        UnityEngine.Object.DestroyImmediate(hero);
    }

    [Test]
    public void SprintLocked_PreservesDashButPreventsWildstride()
    {
        abilityState.sprintUnlocked = false;
        BeginDash(true);

        Assert.That(blackboard.dashing, Is.True);
        CompleteDash();

        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(dash.LastEndReason, Is.EqualTo(HeroDashEndReason.Completed));
    }

    [Test]
    public void NaturalGroundDashHeld_EntersWildstrideOnce()
    {
        BeginDash(true);
        CompleteDash();

        Assert.That(blackboard.sprinting, Is.True);
        int completion = blackboard.completedGroundDashVersion;

        sprint.Cancel(HeroSprintCancelReason.InputReleased, false);
        sprint.Tick();

        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(blackboard.completedGroundDashVersion, Is.EqualTo(completion));
    }

    [TestCase(true, 1)]
    [TestCase(false, -1)]
    public void NaturalGroundDashNeutral_UsesDashCompletionDirection(bool facingRight, int expectedDirection)
    {
        blackboard.facingRight = facingRight;
        BeginDash(true, Vector2.zero);
        CompleteDash();

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(expectedDirection));
    }

    [Test]
    public void GroundDashCompletion_CurrentSteeringOverridesDashDirection()
    {
        BeginDash(true, Vector2.right);
        SetInput("MoveVector", Vector2.left);
        CompleteDash();

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(-1));
    }

    [Test]
    public void ReleaseBeforeCompletion_PreventsWildstride()
    {
        BeginDash(true);
        SetInput("DashHeld", false);
        CompleteDash();

        Assert.That(blackboard.sprinting, Is.False);
    }

    [Test]
    public void AirDashCreatesPendingButIdleCooldownHoldCannotStartWildstride()
    {
        BeginDash(false);
        CompleteDash();
        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(sprint.HasPendingAirDashLanding, Is.True);

        SetInput("DashHeld", false);
        sprint.Tick();
        blackboard.grounded = true;
        SetInput("DashHeld", true);
        SetInput("DashPressedThisFrame", true);
        dash.Tick(0f);
        sprint.Tick();

        Assert.That(blackboard.dashing, Is.False, "Dash cooldown should still be active.");
        Assert.That(blackboard.sprinting, Is.False);
    }

    [Test]
    public void ActiveWildstride_DashReleaseEndsWithoutVelocitySnap()
    {
        BeginAndEnterSprint();
        body.linearVelocity = new Vector2(abilityConfig.sprintSpeed, 3f);

        SetInput("DashHeld", false);
        sprint.Tick();

        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(body.linearVelocity, Is.EqualTo(new Vector2(abilityConfig.sprintSpeed, 3f)));
    }

    [Test]
    public void ActiveGroundedWildstride_NeutralInputPreservesRememberedDirection()
    {
        BeginAndEnterSprint();
        SetInput("MoveVector", Vector2.left);
        sprint.Tick();
        SetInput("MoveVector", Vector2.zero);

        for (int i = 0; i < 5; i++)
        {
            sprint.Tick();
        }

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(-1));
        Assert.That(sprint.ResolveGroundedMoveInput(0f), Is.EqualTo(-1f));
        Assert.That(sprint.RequestedSpeed, Is.EqualTo(HeroLocomotionSpeed.Wildstride));
        Assert.That(input.DashCommandArmed, Is.True);
        Assert.That(blackboard.lastSprintCancelReason, Is.EqualTo(HeroSprintCancelReason.None));
    }

    [Test]
    public void OrdinaryIdle_NeutralInputIsNotResolvedIntoWildstrideMovement()
    {
        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(sprint.ResolveGroundedMoveInput(0f), Is.Zero);
        Assert.That(sprint.RequestedSpeed, Is.EqualTo(HeroLocomotionSpeed.Walk));
    }

    [Test]
    public void OrdinaryMovement_ExplicitlyRequestsWalkAndTargetsWalkSpeed()
    {
        Assert.That(sprint.RequestedSpeed, Is.EqualTo(HeroLocomotionSpeed.Walk));

        motor.SetDesiredMove(1f, sprint.RequestedSpeed);
        for (int i = 0; i < 30; i++)
        {
            motor.FixedTick(0.02f);
        }

        Assert.That(body.linearVelocity.x, Is.EqualTo(config.walkSpeed).Within(0.001f));
        Assert.That(blackboard.sprinting, Is.False);
    }

    [TestCase(1f, -1f)]
    [TestCase(-1f, 1f)]
    public void GroundedDirectionChange_PreservesAuthorisationAndTurnsThroughMotor(float from, float to)
    {
        BeginAndEnterSprint();
        SetInput("MoveVector", new Vector2(from, 0f));
        sprint.Tick();
        body.linearVelocity = new Vector2(from * abilityConfig.sprintSpeed, 0f);
        int dashSequence = dash.SequenceVersion;

        SetInput("MoveVector", new Vector2(to, 0f));
        sprint.Tick();

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo((int)Mathf.Sign(to)));
        Assert.That(sprint.RequestedSpeed, Is.EqualTo(HeroLocomotionSpeed.Wildstride));
        Assert.That(input.DashCommandArmed, Is.True);
        Assert.That(dash.SequenceVersion, Is.EqualTo(dashSequence));
        Assert.That(blackboard.lastSprintCancelReason, Is.EqualTo(HeroSprintCancelReason.None));

        motor.SetDesiredMove(to, sprint.RequestedSpeed);
        motor.FixedTick(0.02f);

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(Mathf.Sign(body.linearVelocity.x), Is.EqualTo(Mathf.Sign(from)));
        Assert.That(Mathf.Abs(body.linearVelocity.x), Is.LessThan(abilityConfig.sprintSpeed));
        Assert.That(Mathf.Abs(body.linearVelocity.x), Is.GreaterThan(0f));

        bool crossedZero = false;
        for (int i = 0; i < 80; i++)
        {
            sprint.Tick();
            Assert.That(sprint.RequestedSpeed, Is.EqualTo(HeroLocomotionSpeed.Wildstride));
            Assert.That(blackboard.sprinting, Is.True);
            motor.SetDesiredMove(to, sprint.RequestedSpeed);
            motor.FixedTick(0.02f);
            crossedZero |= Mathf.Sign(body.linearVelocity.x) == Mathf.Sign(to);
        }

        Assert.That(crossedZero, Is.True);
        Assert.That(body.linearVelocity.x, Is.EqualTo(to * abilityConfig.sprintSpeed).Within(0.001f));
        Assert.That(blackboard.FacingDirection, Is.EqualTo((int)Mathf.Sign(to)));
        Assert.That(input.DashCommandArmed, Is.True);
        Assert.That(dash.SequenceVersion, Is.EqualTo(dashSequence));
    }

    [Test]
    public void WildstrideJumpCarry_UsesNormalVerticalJumpAndLongerHorizontalTarget()
    {
        motor.StartJump();
        float normalY = body.linearVelocity.y;
        motor.ResetMotion();
        blackboard.grounded = true;

        BeginAndEnterSprint();
        Assert.That(sprint.TryBeginJumpCarry(), Is.True);
        motor.StartJump();
        float wildstrideY = body.linearVelocity.y;
        motor.SetDesiredMove(1f, HeroLocomotionSpeed.Walk);
        for (int i = 0; i < 80; i++)
        {
            motor.FixedTick(0.02f);
        }

        Assert.That(wildstrideY, Is.EqualTo(normalY).Within(0.0001f));
        Assert.That(blackboard.sprintJumpCarrying, Is.True);
        Assert.That(body.linearVelocity.x, Is.EqualTo(abilityConfig.sprintJumpSpeed).Within(0.01f));
        Assert.That(body.linearVelocity.x, Is.GreaterThan(config.runSpeed));
    }

    [Test]
    public void JumpCarry_CapturesLaunchDirectionAndSameDirectionMaintainsCarry()
    {
        BeginAndEnterSprint();
        Assert.That(sprint.TryBeginJumpCarry(), Is.True);
        blackboard.grounded = false;
        SetInput("MoveVector", Vector2.right);

        sprint.Tick();
        motor.SetDesiredMove(1f, HeroLocomotionSpeed.Walk);
        for (int i = 0; i < 80; i++)
        {
            motor.FixedTick(0.02f);
        }

        Assert.That(sprint.CapturedJumpCarryDirection, Is.EqualTo(1));
        Assert.That(blackboard.sprintJumpCarrying, Is.True);
        Assert.That(body.linearVelocity.x, Is.EqualTo(abilityConfig.sprintJumpSpeed).Within(0.001f));
    }

    [Test]
    public void JumpCarry_OppositeInputCancelsLockedCarryAndMotorDecaysIntoNormalAirSteering()
    {
        BeginCarry();
        body.linearVelocity = new Vector2(abilityConfig.sprintJumpSpeed, 0f);

        SetInput("MoveVector", Vector2.left);
        sprint.Tick();

        Assert.That(blackboard.sprintJumpCarrying, Is.False);
        Assert.That(blackboard.lastSprintCancelReason, Is.EqualTo(HeroSprintCancelReason.DirectionReversed));
        Assert.That(input.DashCommandArmed, Is.True);

        motor.SetDesiredMove(-1f, sprint.RequestedSpeed);
        motor.FixedTick(0.02f);
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
        Assert.That(body.linearVelocity.x, Is.LessThan(abilityConfig.sprintJumpSpeed));

        for (int i = 0; i < 160; i++)
        {
            motor.FixedTick(0.02f);
        }

        Assert.That(body.linearVelocity.x, Is.EqualTo(-config.walkSpeed).Within(0.001f));
        Assert.That(body.linearVelocity.x, Is.GreaterThan(-abilityConfig.sprintJumpSpeed));
    }

    [Test]
    public void JumpCarry_OppositeCancellationPreservesAuthorisationForLanding()
    {
        BeginCarry();
        SetInput("MoveVector", Vector2.left);
        sprint.Tick();
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.True);

        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.NotifyLanded();

        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintJumpCarrying, Is.False);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(-1));
    }

    [Test]
    public void JumpCarry_SameDirectionLandingResumesWildstride()
    {
        BeginCarry();
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.Tick();

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(sprint.RequestedSpeed, Is.EqualTo(HeroLocomotionSpeed.Wildstride));
    }

    [TestCase(HeroSprintCancelReason.DownslashBounce)]
    [TestCase(HeroSprintCancelReason.WallState)]
    public void CarryTraversalInterruptions_CancelAndDisarm(HeroSprintCancelReason reason)
    {
        BeginCarry();

        switch (reason)
        {
            case HeroSprintCancelReason.DownslashBounce:
                motor.ApplyDownslashBounce();
                sprint.Tick();
                break;
            case HeroSprintCancelReason.WallState:
                blackboard.wallSliding = true;
                sprint.Tick();
                break;
        }

        Assert.That(blackboard.sprintJumpCarrying, Is.False);
        Assert.That(input.DashCommandArmed, Is.False);
    }

    [Test]
    public void DoubleJumpCancelsCarryLandingAuthorisationAndDashArming()
    {
        BeginCarry();
        sprint.NotifyDoubleJump();

        Assert.That(blackboard.sprintJumpCarrying, Is.False);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
        Assert.That(sprint.HasLedgeJumpBuffer, Is.False);
        Assert.That(input.DashCommandArmed, Is.False);
        Assert.That(blackboard.sprintDirection, Is.Zero);

        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);

        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
    }

    [TestCase(1)]
    [TestCase(50)]
    [TestCase(600)]
    public void FallingEndsForcedCarryWithoutExpiringLandingAuthorisation(int airborneFixedSteps)
    {
        BeginCarry();
        body.linearVelocity = new Vector2(abilityConfig.sprintJumpSpeed, -1f);
        SetInput("MoveVector", Vector2.zero);
        blackboard.falling = true;
        sprint.FixedTick(0.02f);

        Assert.That(blackboard.sprintJumpCarrying, Is.False);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.True);
        Assert.That(sprint.RequestedSpeed, Is.EqualTo(HeroLocomotionSpeed.Walk));
        Assert.That(sprint.ResolveGroundedMoveInput(0f), Is.EqualTo(1f));

        for (int i = 0; i < airborneFixedSteps; i++)
        {
            sprint.FixedTick(0.02f);
        }

        SetInput("MoveVector", Vector2.zero);
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);
        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(1));
    }

    [Test]
    public void DashReleaseDuringLongFallConsumesLandingAuthorisation()
    {
        BeginCarry();
        blackboard.falling = true;
        sprint.FixedTick(0.02f);
        for (int i = 0; i < 300; i++)
        {
            sprint.FixedTick(0.02f);
        }

        SetInput("DashHeld", false);
        SetInput("DashReleasedThisFrame", true);
        sprint.Tick();

        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);
        Assert.That(blackboard.sprinting, Is.False);
    }

    [Test]
    public void HardInterruptionAfterDoubleJumpConsumesLandingAuthorisationAndDisarms()
    {
        BeginCarry();
        sprint.NotifyDoubleJump();
        blackboard.attacking = true;
        sprint.Tick();

        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
        Assert.That(input.DashCommandArmed, Is.False);

        blackboard.attacking = false;
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);
        Assert.That(blackboard.sprinting, Is.False);
    }

    [Test]
    public void DashReleaseAfterDoubleJumpPreventsLandingResumption()
    {
        BeginCarry();
        sprint.NotifyDoubleJump();
        SetInput("DashHeld", false);
        SetInput("DashReleasedThisFrame", true);
        sprint.Tick();

        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);

        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
    }

    [Test]
    public void UnauthorisedDoubleJumpCannotCreateLandingAuthorisation()
    {
        blackboard.grounded = false;
        blackboard.wasGrounded = true;
        SetInput("DashHeld", true);
        SetInput("MoveVector", Vector2.right);

        sprint.NotifyDoubleJump();
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);

        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);
        Assert.That(blackboard.sprinting, Is.False);
    }

    [Test]
    public void NeutralGroundedWildstrideJumpUsesRememberedDirection()
    {
        BeginAndEnterSprint();
        SetInput("MoveVector", Vector2.zero);

        Assert.That(sprint.TryBeginJumpCarry(), Is.True);
        Assert.That(sprint.CapturedJumpCarryDirection, Is.EqualTo(1));

        motor.StartJump();
        sprint.Tick();
        Assert.That(blackboard.sprintJumpCarrying, Is.True);
        Assert.That(sprint.ResolveGroundedMoveInput(0f), Is.EqualTo(1f));

        motor.SetDesiredMove(sprint.ResolveGroundedMoveInput(0f), sprint.RequestedSpeed);
        motor.FixedTick(0.02f);
        Assert.That(blackboard.sprintJumpCarrying, Is.True);
    }

    [Test]
    public void NeutralLedgeBufferDoesNotEraseSpecialisedJumpLaunch()
    {
        BeginLedgeJumpBuffer();
        SetInput("MoveVector", Vector2.zero);
        sprint.Tick();

        Assert.That(sprint.HasLedgeJumpBuffer, Is.True);
        Assert.That(sprint.TryBeginJumpCarry(), Is.True);
        Assert.That(sprint.CapturedJumpCarryDirection, Is.EqualTo(1));
    }

    [Test]
    public void UnrelatedJumpCannotReuseCancelledWildstrideAuthorisation()
    {
        BeginCarry();
        sprint.Cancel(HeroSprintCancelReason.InputReleased, false);
        SetInput("DashHeld", true);
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);

        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
    }

    [TestCase("attack")]
    [TestCase("bind")]
    public void CarryIncompatibleOwners_CancelAndDisarm(string owner)
    {
        BeginCarry();

        if (owner == "attack") blackboard.attacking = true;
        if (owner == "bind") blackboard.binding = true;
        sprint.Tick();

        Assert.That(blackboard.sprintJumpCarrying, Is.False);
        Assert.That(input.DashCommandArmed, Is.False);
    }

    [Test]
    public void BlockingWallContact_DoesNotByItselfDisarmCarry()
    {
        BeginCarry();
        blackboard.facingRight = true;
        blackboard.sprintDirection = 1;
        blackboard.touchingWallFront = true;

        sprint.Tick();

        Assert.That(blackboard.sprintJumpCarrying, Is.True);
        Assert.That(input.DashCommandArmed, Is.True);
    }

    [Test]
    public void NaturalAirDash_FirstLandingConsumesExactSequenceAndUsesCurrentDirection()
    {
        BeginDash(false);
        CompleteDash();
        Assert.That(sprint.HasPendingAirDashLanding, Is.True);

        SetInput("MoveVector", Vector2.left);
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.Tick();

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(-1));
        Assert.That(sprint.HasPendingAirDashLanding, Is.False);

        sprint.Cancel(HeroSprintCancelReason.InputReleased, false);
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.Tick();
        Assert.That(blackboard.sprinting, Is.False, "The consumed air-Dash sequence cannot authorise a later landing.");
    }

    [TestCase(true, 1)]
    [TestCase(false, -1)]
    public void NaturalAirDash_NeutralFirstLandingUsesCompletionDirection(bool facingRight, int expectedDirection)
    {
        blackboard.facingRight = facingRight;
        BeginDash(false, Vector2.zero);
        CompleteDash();
        Assert.That(sprint.HasPendingAirDashLanding, Is.True);

        SetInput("MoveVector", Vector2.zero);
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(expectedDirection));
        Assert.That(sprint.HasPendingAirDashLanding, Is.False);
    }

    [Test]
    public void AirDashLandingAuthorisation_IsConsumedWhenFirstLandingFails()
    {
        BeginDash(false);
        CompleteDash();
        Assert.That(sprint.HasPendingAirDashLanding, Is.True);

        SetInput("MoveVector", Vector2.zero);
        blackboard.wasGrounded = false;
        blackboard.grounded = false;
        sprint.NotifyLanded();
        Assert.That(sprint.HasPendingAirDashLanding, Is.False);
        Assert.That(blackboard.sprinting, Is.False);

        SetInput("MoveVector", Vector2.right);
        blackboard.wasGrounded = false;
        sprint.Tick();
        Assert.That(blackboard.sprinting, Is.False);
    }

    [TestCase("release")]
    [TestCase("attack")]
    [TestCase("bind")]
    [TestCase("doubleJump")]
    [TestCase("wall")]
    [TestCase("control")]
    public void PendingAirDash_HardInterruptionsCancelAuthorisation(string interruption)
    {
        BeginDash(false);
        CompleteDash();
        Assert.That(sprint.HasPendingAirDashLanding, Is.True);

        if (interruption == "release") SetInput("DashHeld", false);
        if (interruption == "attack") blackboard.attacking = true;
        if (interruption == "bind") blackboard.binding = true;
        if (interruption == "doubleJump") sprint.NotifyDoubleJump();
        if (interruption == "wall") blackboard.wallSliding = true;
        if (interruption == "control") blackboard.controlLocked = true;
        sprint.Tick();

        Assert.That(sprint.HasPendingAirDashLanding, Is.False);
        Assert.That(blackboard.sprinting, Is.False);
    }

    [Test]
    public void ZeroResource_DoesNotBlockOrMutateWildstrideCarryAndLanding()
    {
        resourceState.Clear();
        int initial = resourceState.CurrentParts;

        BeginAndEnterSprint();
        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(resourceState.CurrentParts, Is.EqualTo(initial));

        for (int i = 0; i < 120; i++)
        {
            sprint.Tick();
        }

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(resourceState.CurrentParts, Is.EqualTo(initial));
        Assert.That(sprint.TryBeginJumpCarry(), Is.True);
        Assert.That(resourceState.CurrentParts, Is.EqualTo(initial));

        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.Tick();

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(resourceState.CurrentParts, Is.EqualTo(initial));
    }

    [Test]
    public void HeroSprintAction_HasNoResourceDependencyOrTrySpendReference()
    {
        Assert.That(
            typeof(HeroSprintAction).GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Any(parameter => parameter.ParameterType == typeof(PlayerResourceState)),
            Is.False);
        Assert.That(
            typeof(HeroSprintAction).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(PlayerResourceState)),
            Is.False);

        string source = File.ReadAllText("Assets/_Project/Scripts/Hero/Actions/HeroSprintAction.cs");
        StringAssert.DoesNotContain("PlayerResourceState", source);
        StringAssert.DoesNotContain("TrySpend", source);
    }

    [Test]
    public void LeavingGroundFromWildstride_StartsShortLedgeJumpBuffer()
    {
        BeginAndEnterSprint();
        blackboard.wasGrounded = true;
        blackboard.grounded = false;

        sprint.FixedTick(0.02f);

        Assert.That(sprint.HasLedgeJumpBuffer, Is.True);
        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(blackboard.sprintJumpCarrying, Is.False);
        Assert.That(input.DashCommandArmed, Is.True);
    }

    [Test]
    public void JumpDuringLedgeBuffer_BeginsCarryAndConsumesBufferExactlyOnce()
    {
        BeginLedgeJumpBuffer();

        Assert.That(sprint.TryBeginJumpCarry(), Is.True);
        Assert.That(sprint.HasLedgeJumpBuffer, Is.False);
        Assert.That(blackboard.sprintJumpCarrying, Is.True);
        Assert.That(sprint.TryBeginJumpCarry(), Is.False);
    }

    [Test]
    public void LedgeJumpBufferExpiry_PreventsSpecialisedCarry()
    {
        BeginLedgeJumpBuffer();

        sprint.FixedTick(abilityConfig.sprintLedgeJumpBufferTime);

        Assert.That(sprint.HasLedgeJumpBuffer, Is.False);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.True);
        Assert.That(sprint.TryBeginJumpCarry(), Is.False);
        Assert.That(blackboard.lastSprintCancelReason, Is.EqualTo(HeroSprintCancelReason.LedgeJumpBufferExpired));
    }

    [Test]
    public void ShortWalkOffLandingBeforeBufferExpiry_ResumesWildstrideAndConsumesSequence()
    {
        BeginLedgeJumpBuffer();
        int dashSequence = dash.SequenceVersion;

        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(sprint.HasLedgeJumpBuffer, Is.False);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
        Assert.That(dash.SequenceVersion, Is.EqualTo(dashSequence));

        sprint.Cancel(HeroSprintCancelReason.InputReleased, false);
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);
        Assert.That(blackboard.sprinting, Is.False);
    }

    [Test]
    public void LongWalkOffLandingAfterBufferExpiry_ResumesUsingRememberedDirection()
    {
        BeginLedgeJumpBuffer();
        sprint.FixedTick(abilityConfig.sprintLedgeJumpBufferTime);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.True);

        SetInput("MoveVector", Vector2.zero);
        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(1));
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
    }

    [Test]
    public void OppositeSteeringDuringOrdinaryFall_ChangesLandingDirectionWithoutCancelling()
    {
        BeginLedgeJumpBuffer();
        sprint.FixedTick(abilityConfig.sprintLedgeJumpBufferTime);
        SetInput("MoveVector", Vector2.left);

        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(-1));
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
    }

    [Test]
    public void DoubleJumpDuringLedgeJumpBuffer_ClearsBufferAndPreventsLandingResumption()
    {
        BeginLedgeJumpBuffer();
        sprint.NotifyDoubleJump();

        Assert.That(sprint.HasLedgeJumpBuffer, Is.False);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
        Assert.That(input.DashCommandArmed, Is.False);

        blackboard.wasGrounded = false;
        blackboard.grounded = true;
        sprint.FixedTick(0.02f);
        Assert.That(blackboard.sprinting, Is.False);
    }

    [Test]
    public void LedgeClimbSuspendsAuthorisedCarryAndResumesOnceWithCurrentDirection()
    {
        BeginCarry();
        int dashSequence = dash.SequenceVersion;
        sprint.NotifyLedgeClimbStarted();

        Assert.That(sprint.IsLedgeClimbSuspended, Is.True);
        Assert.That(blackboard.sprintJumpCarrying, Is.False);
        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(input.DashCommandArmed, Is.True);

        SetInput("MoveVector", Vector2.left);
        blackboard.ledgeClimbing = false;
        blackboard.grounded = true;
        sprint.NotifyLedgeClimbEnded(true, HeroLedgeClimbCancelReason.None);

        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(blackboard.sprintDirection, Is.EqualTo(-1));
        Assert.That(dash.SequenceVersion, Is.EqualTo(dashSequence));
        Assert.That(sprint.IsLedgeClimbSuspended, Is.False);

        sprint.NotifyLedgeClimbEnded(true, HeroLedgeClimbCancelReason.None);
        Assert.That(blackboard.sprinting, Is.True);
        Assert.That(dash.SequenceVersion, Is.EqualTo(dashSequence));
    }

    [Test]
    public void DashReleaseDuringLedgeClimbConsumesSuspensionWithoutDisarmingFreshInput()
    {
        BeginAndEnterSprint();
        sprint.NotifyLedgeClimbStarted();
        blackboard.ledgeClimbing = true;
        SetInput("DashHeld", false);
        SetInput("DashReleasedThisFrame", true);
        sprint.Tick();

        Assert.That(sprint.IsLedgeClimbSuspended, Is.False);
        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(sprint.HasAirborneLandingAuthorisation, Is.False);
        Assert.That(input.DashCommandArmed, Is.True);

        blackboard.ledgeClimbing = false;
        sprint.NotifyLedgeClimbEnded(true, HeroLedgeClimbCancelReason.None);
        Assert.That(blackboard.sprinting, Is.False);
    }

    [Test]
    public void UnauthorisedLedgeClimbNotificationsCannotCreateWildstride()
    {
        sprint.NotifyLedgeClimbStarted();
        sprint.NotifyLedgeClimbEnded(true, HeroLedgeClimbCancelReason.None);

        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(input.DashCommandArmed, Is.False);
    }

    [Test]
    public void UnrelatedFallAndOrdinaryWalk_DoNotCreateLedgeJumpBuffer()
    {
        blackboard.wasGrounded = true;
        blackboard.grounded = false;
        SetInput("MoveVector", Vector2.right);
        SetInput("DashHeld", true);

        sprint.FixedTick(0.02f);

        Assert.That(sprint.HasLedgeJumpBuffer, Is.False);
        Assert.That(sprint.TryBeginJumpCarry(), Is.False);
    }

    [Test]
    public void CancelledDash_DoesNotCreateLedgeJumpBuffer()
    {
        BeginDash(true);
        dash.Cancel(HeroDashEndReason.Attack);
        blackboard.wasGrounded = true;
        blackboard.grounded = false;

        sprint.FixedTick(0.02f);

        Assert.That(sprint.HasLedgeJumpBuffer, Is.False);
    }

    [TestCase("release")]
    [TestCase("attack")]
    [TestCase("bind")]
    [TestCase("control")]
    [TestCase("input")]
    [TestCase("hurt")]
    [TestCase("death")]
    [TestCase("wall")]
    [TestCase("pogo")]
    [TestCase("unlock")]
    public void LedgeJumpBuffer_HardInterruptionsClearAndCannotBeConsumed(string interruption)
    {
        BeginLedgeJumpBuffer();

        if (interruption == "release") SetInput("DashHeld", false);
        if (interruption == "attack") blackboard.attacking = true;
        if (interruption == "bind") blackboard.binding = true;
        if (interruption == "control") blackboard.controlLocked = true;
        if (interruption == "input") blackboard.inputBlocked = true;
        if (interruption == "hurt") blackboard.actorState = HeroActorState.Hurt;
        if (interruption == "death") blackboard.actorState = HeroActorState.Dead;
        if (interruption == "wall") blackboard.wallSliding = true;
        if (interruption == "pogo") motor.ApplyDownslashBounce();
        if (interruption == "unlock") abilityState.sprintUnlocked = false;
        sprint.Tick();

        Assert.That(sprint.HasLedgeJumpBuffer, Is.False);
        Assert.That(sprint.TryBeginJumpCarry(), Is.False);
    }

    [TestCase(HeroSprintCancelReason.SceneEntry)]
    [TestCase(HeroSprintCancelReason.Respawn)]
    [TestCase(HeroSprintCancelReason.ComponentDisabled)]
    [TestCase(HeroSprintCancelReason.InputSuspended)]
    public void LedgeJumpBuffer_CentralLifecycleCancellationClearsAndDisarms(HeroSprintCancelReason reason)
    {
        BeginLedgeJumpBuffer();

        sprint.Cancel(reason, true);

        Assert.That(sprint.HasLedgeJumpBuffer, Is.False);
        Assert.That(input.DashCommandArmed, Is.False);
        Assert.That(sprint.TryBeginJumpCarry(), Is.False);
    }

    [TestCase(HeroSprintCancelReason.ControlLock)]
    [TestCase(HeroSprintCancelReason.InputSuspended)]
    [TestCase(HeroSprintCancelReason.Hurt)]
    [TestCase(HeroSprintCancelReason.Death)]
    [TestCase(HeroSprintCancelReason.SceneEntry)]
    [TestCase(HeroSprintCancelReason.Respawn)]
    public void ExternalCancellation_CleansStateAndDisarms(HeroSprintCancelReason reason)
    {
        BeginCarry();
        sprint.Cancel(reason, true);

        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(blackboard.sprintJumpCarrying, Is.False);
        Assert.That(input.DashCommandArmed, Is.False);
    }

    [Test]
    public void RuntimeUnlockLoss_CancelsActiveWildstride()
    {
        BeginAndEnterSprint();
        abilityState.sprintUnlocked = false;
        sprint.Tick();

        Assert.That(blackboard.sprinting, Is.False);
        Assert.That(blackboard.lastSprintCancelReason, Is.EqualTo(HeroSprintCancelReason.AbilityLocked));
    }

    private void BeginCarry()
    {
        BeginAndEnterSprint();
        Assert.That(sprint.TryBeginJumpCarry(), Is.True);
        blackboard.grounded = false;
        blackboard.wasGrounded = true;
    }

    private void BeginLedgeJumpBuffer()
    {
        BeginAndEnterSprint();
        blackboard.wasGrounded = true;
        blackboard.grounded = false;
        sprint.FixedTick(0.02f);
        Assert.That(sprint.HasLedgeJumpBuffer, Is.True);
    }

    private void BeginAndEnterSprint()
    {
        BeginDash(true);
        CompleteDash();
        Assert.That(blackboard.sprinting, Is.True);
    }

    private void BeginDash(bool grounded)
    {
        BeginDash(grounded, Vector2.right);
    }

    private void BeginDash(bool grounded, Vector2 move)
    {
        blackboard.grounded = grounded;
        blackboard.wasGrounded = grounded;
        SetInput("MoveVector", move);
        SetInput("DashHeld", true);
        SetInput("DashPressedThisFrame", true);
        dash.Tick(0f);
        SetInput("DashPressedThisFrame", false);
    }

    private void CompleteDash()
    {
        dash.FixedTick(abilityConfig.dashDuration + 0.01f);
        sprint.Tick();
    }

    private void SetInput(string propertyName, object value)
    {
        PropertyInfo property = typeof(HeroInputReader).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.SetValue(input, value);
    }
}
