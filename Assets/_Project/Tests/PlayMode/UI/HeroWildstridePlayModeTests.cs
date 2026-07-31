using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

public sealed class HeroWildstridePlayModeTests
{
    private static Assembly gameAssembly;

    private Keyboard keyboard;
    private GameObject hero;
    private Rigidbody2D body;
    private ScriptableObject config;
    private ScriptableObject abilityConfig;
    private ScriptableObject abilityState;
    private ScriptableObject resourceState;
    private ScriptableObject healthState;
    private ScriptableObject resourceConfig;
    private Component blackboard;
    private Component input;
    private Component motor;
    private Component sensors;
    private Component actions;
    private Component animations;
    private GameObject ledge;

    private static Type GameType(string name)
    {
        if (gameAssembly == null)
        {
            gameAssembly = Assembly.Load("Assembly-CSharp");
        }

        Type type = gameAssembly.GetType(name);
        Assert.That(type, Is.Not.Null, "Type not found in Assembly-CSharp: " + name);
        return type;
    }

    private static Type LoadedType(string fullName)
    {
        Type type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName))
            .FirstOrDefault(candidate => candidate != null);
        Assert.That(type, Is.Not.Null, "Loaded type not found: " + fullName);
        return type;
    }

    private static object Invoke(object target, string method, params object[] arguments)
    {
        MethodInfo match = target.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == method
                && candidate.GetParameters().Length == arguments.Length);
        return match.Invoke(target, arguments);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        field.SetValue(target, value);
    }

    private static object GetField(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, "Field not found: " + name);
        return field.GetValue(target);
    }

    private static object GetProperty(object target, string name)
    {
        PropertyInfo property = target.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(property, Is.Not.Null, "Property not found: " + name);
        return property.GetValue(target);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();

        config = ScriptableObject.CreateInstance(GameType("HeroConfig"));
        SetField(config, "horizontalInputDeadZone", 0.3f);
        SetField(config, "walkSpeed", 4f);
        SetField(config, "runSpeed", 6f);
        SetField(config, "groundAcceleration", 20f);
        SetField(config, "groundDeceleration", 25f);
        SetField(config, "airAcceleration", 8f);
        SetField(config, "airDeceleration", 6f);
        SetField(config, "baseGravityScale", 0f);
        SetField(config, "terrainLayers", (LayerMask)1);
        SetField(config, "ledgeSurfaceLayers", (LayerMask)1);
        SetField(config, "wallProbeDistance", 0.1f);
        SetField(config, "sensorInset", 0.02f);
        SetField(config, "ledgeMinimumHeightFromFeet", 0.25f);
        SetField(config, "ledgeMaximumHeightFromFeet", 1.35f);
        SetField(config, "ledgeTopProbeExtraHeight", 0.3f);
        SetField(config, "ledgeTopSampleInset", 0.04f);
        SetField(config, "ledgeSurfaceHeightTolerance", 0.08f);
        SetField(config, "ledgeMinimumUpNormal", 0.85f);
        SetField(config, "ledgeSupportGapTolerance", 0.08f);
        SetField(config, "ledgePlacementSkin", 0.02f);
        SetField(config, "ledgeCatchDrop", 0.1f);
        SetField(config, "ledgeCatchDuration", 0.08f);
        SetField(config, "ledgePullUpDuration", 0.28f);
        SetField(config, "ledgeSettleDuration", 0.05f);

        abilityConfig = ScriptableObject.CreateInstance(GameType("HeroAbilityConfig"));
        SetField(abilityConfig, "dashSpeed", 18f);
        SetField(abilityConfig, "dashDuration", 0.1f);
        SetField(abilityConfig, "dashCooldown", 0.45f);
        SetField(abilityConfig, "sprintSpeed", 10f);
        SetField(abilityConfig, "sprintJumpSpeed", 10f);
        SetField(abilityConfig, "sprintLedgeJumpBufferTime", 0.08f);

        abilityState = ScriptableObject.CreateInstance(GameType("PlayerAbilityState"));
        SetField(abilityState, "dashUnlocked", true);
        SetField(abilityState, "sprintUnlocked", true);
        resourceState = ScriptableObject.CreateInstance(GameType("PlayerResourceState"));
        healthState = ScriptableObject.CreateInstance(GameType("PlayerHealthState"));
        resourceConfig = ScriptableObject.CreateInstance(GameType("PlayerResourceConfig"));

        hero = new GameObject("Wildstride PlayMode Integration Hero");
        hero.layer = LayerMask.NameToLayer("Player");
        body = hero.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        BoxCollider2D collider = hero.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.36f, 0.9f);
        SpriteRenderer renderer = hero.AddComponent<SpriteRenderer>();
        Animator animator = hero.AddComponent<Animator>();

        blackboard = hero.AddComponent(GameType("HeroStateBlackboard"));
        input = hero.AddComponent(GameType("HeroInputReader"));
        motor = hero.AddComponent(GameType("HeroMotor"));
        Component audio = hero.AddComponent(GameType("HeroAudioController"));
        sensors = hero.AddComponent(GameType("HeroSensors"));
        actions = hero.AddComponent(GameType("HeroActionController"));
        animations = hero.AddComponent(GameType("HeroAnimationController"));

        Type animancerType = LoadedType("Animancer.AnimancerComponent");
        Component animancer = hero.AddComponent(animancerType);
        animancerType.GetProperty("Animator")?.SetValue(animancer, animator);

        Invoke(input, "Initialize", config);
        Invoke(audio, "Initialize", config, blackboard);
        Invoke(motor, "Initialize", config, abilityConfig, blackboard, body, collider, renderer, hero.transform);
        Invoke(
            actions,
            "Initialize",
            config,
            abilityConfig,
            blackboard,
            input,
            motor,
            sensors,
            null,
            abilityState,
            resourceState,
            healthState,
            resourceConfig);

        UnityEngine.Object library = UnityEditor.AssetDatabase.LoadAssetAtPath(
            "Assets/_Project/ScriptableObjects/Hero/HeroAnimationLibrary.asset",
            GameType("HeroAnimationLibrary"));
        Invoke(animations, "Initialize", config, blackboard, motor, animancer, actions, library);
        Invoke(actions, "SetAnimationController", animations);

        SetField(blackboard, "grounded", true);
        SetField(blackboard, "wasGrounded", true);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        UnityEngine.Object.Destroy(ledge);
        UnityEngine.Object.Destroy(hero);
        UnityEngine.Object.Destroy(config);
        UnityEngine.Object.Destroy(abilityConfig);
        UnityEngine.Object.Destroy(abilityState);
        UnityEngine.Object.Destroy(resourceState);
        UnityEngine.Object.Destroy(healthState);
        UnityEngine.Object.Destroy(resourceConfig);
        if (keyboard != null)
        {
            InputSystem.RemoveDevice(keyboard);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator GroundedKeyboardReversal_PreservesRealActionMotorAndAnimationOrdering()
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D, Key.X));
        yield return null;
        Invoke(input, "Tick");
        Invoke(actions, "Tick");
        Invoke(actions, "FixedTick", 0.11f);
        Invoke(motor, "FixedTick", 0.02f);
        Invoke(animations, "TickVisuals");

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
        float startingVelocity = body.linearVelocity.x;
        Assert.That(startingVelocity, Is.GreaterThan(0f));

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.D, Key.X));
        yield return null;
        Invoke(input, "Tick");
        Assert.That(((Vector2)GetProperty(input, "MoveVector")).x, Is.LessThan(0f));

        Invoke(actions, "Tick");
        Invoke(actions, "FixedTick", 0.02f);
        Invoke(motor, "FixedTick", 0.02f);
        Invoke(animations, "TickVisuals");

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.EqualTo(-1));
        Assert.That((float)GetField(blackboard, "desiredMoveX"), Is.LessThan(0f));
        Assert.That((bool)GetProperty(input, "DashCommandArmed"), Is.True);
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
        Assert.That(body.linearVelocity.x, Is.LessThan(startingVelocity));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.X));
        yield return null;
        for (int i = 0; i < 80; i++)
        {
            Invoke(input, "Tick");
            Invoke(actions, "Tick");
            Invoke(actions, "FixedTick", 0.02f);
            Invoke(motor, "FixedTick", 0.02f);
            Invoke(animations, "TickVisuals");
        }

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That(body.linearVelocity.x, Is.EqualTo(-10f).Within(0.01f));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
    }

    [UnityTest]
    public IEnumerator GroundedWildstride_ReleaseLeftWhileDashHeld_PreservesLeftwardSprint()
    {
        yield return BeginGroundedWildstride(Key.A);
        float startingVelocity = body.linearVelocity.x;

        for (int i = 0; i < 6; i++)
        {
            yield return AdvanceHero(0.02f, Key.X);
        }

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.EqualTo(-1));
        Assert.That((float)GetField(motor, "desiredMoveX"), Is.EqualTo(-1f));
        Assert.That(body.linearVelocity.x, Is.LessThan(0f));
        Assert.That(Mathf.Abs(body.linearVelocity.x), Is.LessThan(Mathf.Abs(startingVelocity)));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
        Assert.That((bool)GetProperty(input, "DashCommandArmed"), Is.True);
    }

    [UnityTest]
    public IEnumerator GroundedWildstride_NeutralGapThenRight_PreservesAuthorisationAndTurns()
    {
        yield return BeginGroundedWildstride(Key.A);

        for (int i = 0; i < 8; i++)
        {
            yield return AdvanceHero(0.02f, Key.X);
            Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        }

        float leftwardVelocity = body.linearVelocity.x;
        yield return AdvanceHero(0.02f, Key.D, Key.X);

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.EqualTo(1));
        Assert.That(body.linearVelocity.x, Is.GreaterThan(leftwardVelocity));
        Assert.That(body.linearVelocity.x, Is.LessThanOrEqualTo(0f));

        for (int i = 0; i < 80; i++)
        {
            SimulateHeroStep(0.02f);
        }

        Assert.That(body.linearVelocity.x, Is.EqualTo(10f).Within(0.01f));
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
    }

    [UnityTest]
    public IEnumerator GroundedWildstride_RightPressedBeforeLeftRelease_MostRecentDirectionWins()
    {
        yield return BeginGroundedWildstride(Key.A);
        yield return AdvanceHero(0.02f, Key.A, Key.D, Key.X);

        Assert.That(((Vector2)GetProperty(input, "MoveVector")).x, Is.GreaterThan(0f));
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.EqualTo(1));
        Assert.That((float)GetField(motor, "desiredMoveX"), Is.GreaterThan(0f));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
    }

    [UnityTest]
    public IEnumerator GroundedWildstride_AfterRightTurnReleasingRightWhileLeftHeld_TurnsBackLeft()
    {
        yield return BeginGroundedWildstride(Key.A);
        yield return AdvanceHero(0.02f, Key.A, Key.D, Key.X);
        for (int i = 0; i < 80; i++)
        {
            SimulateHeroStep(0.02f);
        }

        Assert.That(body.linearVelocity.x, Is.EqualTo(10f).Within(0.01f));

        yield return AdvanceHero(0.02f, Key.A, Key.X);

        Assert.That(((Vector2)GetProperty(input, "MoveVector")).x, Is.LessThan(0f));
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.EqualTo(-1));
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
        Assert.That(body.linearVelocity.x, Is.LessThan(10f));

        for (int i = 0; i < 80; i++)
        {
            SimulateHeroStep(0.02f);
        }

        Assert.That(body.linearVelocity.x, Is.EqualTo(-10f).Within(0.01f));
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
    }

    [UnityTest]
    public IEnumerator GroundedWildstride_DashReleaseWhileMovementNeutral_EndsImmediately()
    {
        yield return BeginGroundedWildstride(Key.A);
        yield return AdvanceHero(0.02f, Key.X);
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);

        yield return AdvanceHero(0.02f);

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.False);
        Assert.That(GetField(blackboard, "lastSprintCancelReason").ToString(), Is.EqualTo("InputReleased"));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.Not.EqualTo("Wildstride"));
    }

    [UnityTest]
    public IEnumerator NeutralGroundedDash_UsesFacingDirectionWhenFacingRight()
    {
        SetField(blackboard, "facingRight", true);
        yield return AdvanceHero(0.11f, Key.X);

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.EqualTo(1));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
    }

    [UnityTest]
    public IEnumerator NeutralGroundedDash_UsesFacingDirectionWhenFacingLeft()
    {
        SetField(blackboard, "facingRight", false);
        yield return AdvanceHero(0.11f, Key.X);

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.EqualTo(-1));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
    }

    [UnityTest]
    public IEnumerator HeldDashAfterUnsuccessfulCompletionCannotCreateWildstride()
    {
        yield return AdvanceHero(0.02f, Key.X);
        yield return AdvanceHero(0.02f);
        yield return AdvanceHero(0.11f);

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.False);

        // This is a new held input while the previous Dash is still on cooldown, not a new
        // qualifying completion. It must not replay the stale completion.
        yield return AdvanceHero(0.02f, Key.X);
        for (int i = 0; i < 30; i++)
        {
            SimulateHeroStep(0.02f);
        }

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.False);
        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator NeutralWildstrideJump_DashOnlyPreservesCarryThroughActionAndMotor()
    {
        yield return BeginGroundedWildstride(Key.D);
        yield return AdvanceHero(0.02f, Key.X, Key.Space);

        Assert.That((bool)GetField(blackboard, "sprintJumpCarrying"), Is.True);
        Assert.That((float)GetField(motor, "desiredMoveX"), Is.EqualTo(1f));

        for (int i = 0; i < 80; i++)
        {
            SimulateHeroStep(0.02f);
        }

        Assert.That((bool)GetField(blackboard, "sprintJumpCarrying"), Is.True);
        Assert.That(body.linearVelocity.x, Is.EqualTo(10f).Within(0.01f));
    }

    [UnityTest]
    public IEnumerator WildstrideNaturalFall_LongAirtime_ReconcilesLandingWithoutWalkFrame()
    {
        yield return BeginGroundedWildstride(Key.D);
        yield return AdvanceHero(0.02f, Key.D, Key.X, Key.Space);
        yield return AdvanceHero(0.02f, Key.D, Key.X);

        SetField(blackboard, "grounded", false);
        SetField(blackboard, "falling", true);
        SimulateHeroStep(0.02f);
        Assert.That((bool)GetField(blackboard, "sprintJumpCarrying"), Is.False);

        for (int i = 0; i < 240; i++)
        {
            SetField(blackboard, "grounded", false);
            SetField(blackboard, "falling", true);
            SimulateHeroStep(0.02f);
        }

        SetField(blackboard, "wasGrounded", false);
        SetField(blackboard, "grounded", true);
        SimulateHeroStep(0.02f);

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
        Assert.That((float)GetField(motor, "desiredMoveX"), Is.EqualTo(1f));
    }

    [UnityTest]
    public IEnumerator WildstrideOppositeAirInput_LandingRestoresCurrentDirectionWithoutWalkFrame()
    {
        yield return BeginGroundedWildstride(Key.D);
        yield return AdvanceHero(0.02f, Key.D, Key.X, Key.Space);

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.X));
        yield return null;
        body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
        SetField(blackboard, "grounded", true);
        SetField(blackboard, "wasGrounded", false);
        SetField(blackboard, "falling", true);
        SimulateHeroStep(0.02f);

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.EqualTo(-1));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
        Assert.That((float)GetField(motor, "desiredMoveX"), Is.EqualTo(-1f));
    }

    [UnityTest]
    public IEnumerator OrdinaryWalkOff_ExpiresSpecialJumpWindowButResumesOnFirstLanding()
    {
        yield return BeginGroundedWildstride(Key.D);

        SetField(blackboard, "wasGrounded", true);
        SetField(blackboard, "grounded", false);
        body.linearVelocity = Vector2.zero;
        yield return AdvanceHero(0.02f, Key.X);

        for (int i = 0; i < 8; i++)
        {
            SetField(blackboard, "wasGrounded", false);
            SetField(blackboard, "grounded", false);
            SimulateHeroStep(0.02f);
        }

        object sprintAction = GetField(actions, "sprint");
        Assert.That((bool)GetProperty(sprintAction, "HasAirborneLandingAuthorisation"), Is.True);
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.False);

        SetField(blackboard, "wasGrounded", false);
        SetField(blackboard, "grounded", true);
        SimulateHeroStep(0.02f);

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((float)GetField(motor, "desiredMoveX"), Is.EqualTo(1f));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
    }

    [UnityTest]
    public IEnumerator NeutralWildstrideLedgeClimb_SuspendsAndResumesWildstride()
    {
        yield return BeginGroundedWildstride(Key.D);

        ledge = new GameObject("Wildstride PlayMode Ledge");
        ledge.layer = 0;
        ledge.transform.position = new Vector3(body.position.x + 1.135f, body.position.y - 0.25f, 0f);
        BoxCollider2D ledgeCollider = ledge.AddComponent<BoxCollider2D>();
        ledgeCollider.size = new Vector2(1.73f, 1.5f);
        Physics2D.SyncTransforms();
        Invoke(sensors, "Initialize", config, blackboard, body, hero.GetComponent<Collider2D>());

        SetField(blackboard, "wasGrounded", true);
        SetField(blackboard, "grounded", false);
        SetField(blackboard, "actorState", Enum.Parse(GameType("HeroActorState"), "Airborne"));
        body.linearVelocity = new Vector2(0f, -0.5f);
        yield return AdvanceHero(0.02f, Key.X);

        Assert.That(
            (bool)GetField(blackboard, "ledgeClimbing"),
            Is.True,
            $"Ledge did not start: failure={GetProperty(sensors, "LastLedgeFailure")}, position={body.position}, move={GetProperty(input, "MoveVector")}");
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.False);
        Assert.That((bool)GetField(blackboard, "sprintJumpCarrying"), Is.False);
        object sprintAction = GetField(actions, "sprint");
        Assert.That((bool)GetProperty(sprintAction, "IsLedgeClimbSuspended"), Is.True);

        float deadline = Time.realtimeSinceStartup + 1f;
        while ((bool)GetField(blackboard, "ledgeClimbing"))
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            SimulateHeroStep(0.02f);
            yield return new WaitForFixedUpdate();
        }

        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That((bool)GetProperty(sprintAction, "IsLedgeClimbSuspended"), Is.False);
        Assert.That((float)GetField(motor, "desiredMoveX"), Is.EqualTo(1f));
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
    }

    [UnityTest]
    public IEnumerator NeutralWildstrideWallContact_StartsWallSlideAndDisarmsWildstride()
    {
        SetField(abilityState, "wallClingUnlocked", true);
        yield return BeginGroundedWildstride(Key.D);

        SetField(blackboard, "wasGrounded", true);
        SetField(blackboard, "grounded", false);
        SetField(blackboard, "falling", true);
        SetField(blackboard, "touchingWallFront", true);
        SetField(blackboard, "facingRight", true);
        body.linearVelocity = new Vector2(0f, -1f);

        yield return AdvanceHero(0.02f, Key.X);

        Assert.That((bool)GetField(blackboard, "wallSliding"), Is.True);
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.False);
        Assert.That((bool)GetProperty(input, "DashCommandArmed"), Is.False);
    }

    [UnityTest]
    public IEnumerator NeutralAirborneWallContactWithoutWildstride_DoesNotStartWallSlide()
    {
        SetField(abilityState, "wallClingUnlocked", true);
        SetField(blackboard, "grounded", false);
        SetField(blackboard, "falling", true);
        SetField(blackboard, "touchingWallFront", true);
        SetField(blackboard, "facingRight", true);
        body.linearVelocity = new Vector2(0f, -1f);

        yield return AdvanceHero(0.02f);

        Assert.That((bool)GetField(blackboard, "wallSliding"), Is.False);
    }

    [UnityTest]
    public IEnumerator WildstrideDoubleJump_CancelsSequenceAndDoesNotResumeOnLanding()
    {
        SetField(abilityState, "doubleJumpUnlocked", true);
        yield return BeginGroundedWildstride(Key.D);
        yield return AdvanceHero(0.02f, Key.D, Key.X, Key.Space);
        yield return AdvanceHero(0.02f, Key.D, Key.X);
        yield return AdvanceHero(0.02f, Key.D, Key.X, Key.Space);
        Assert.That(
            body.linearVelocity.y,
            Is.EqualTo((float)GetField(abilityConfig, "doubleJumpSpeed")).Within(0.001f),
            "Wildstride continuity must not alter the normal Double Jump vertical path.");
        yield return AdvanceHero(0.02f, Key.D, Key.X);

        Assert.That((bool)GetField(blackboard, "sprintJumpCarrying"), Is.False, "Double Jump must end forced carry");
        Assert.That((bool)GetProperty(input, "DashCommandArmed"), Is.False, "Double Jump must disarm Dash until release");

        for (int i = 0; i < 240; i++)
        {
            SetField(blackboard, "grounded", false);
            SetField(blackboard, "falling", true);
            SimulateHeroStep(0.02f);
        }

        SetField(blackboard, "wasGrounded", false);
        SetField(blackboard, "grounded", true);
        body.linearVelocity = new Vector2(body.linearVelocity.x, 0f);
        SimulateHeroStep(0.02f);

        object sprintAction = GetField(actions, "sprint");
        Assert.That(
            (bool)GetField(blackboard, "sprinting"),
            Is.False,
            $"Landing must not resume Wildstride after Double Jump (authorised={GetProperty(sprintAction, "HasAirborneLandingAuthorisation")}, phaseCarry={GetProperty(sprintAction, "IsJumpCarrying")}, reason={GetField(blackboard, "lastSprintCancelReason")}, grounded={GetField(blackboard, "grounded")}, dash={GetProperty(input, "DashHeld")})");
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.Not.EqualTo("Wildstride"));
    }

    private IEnumerator BeginGroundedWildstride(Key direction)
    {
        yield return AdvanceHero(0.11f, direction, Key.X);
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.True);
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
    }

    private IEnumerator AdvanceHero(float actionFixedDeltaTime, params Key[] keys)
    {
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        yield return null;
        SimulateHeroStep(actionFixedDeltaTime);
    }

    private void SimulateHeroStep(float actionFixedDeltaTime)
    {
        Invoke(input, "Tick");
        Invoke(actions, "Tick");
        Invoke(actions, "FixedTick", actionFixedDeltaTime);
        Invoke(motor, "FixedTick", 0.02f);
        Invoke(animations, "TickVisuals");
    }
}
