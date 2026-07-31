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
    private Component actions;
    private Component animations;

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
        body = hero.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        BoxCollider2D collider = hero.AddComponent<BoxCollider2D>();
        SpriteRenderer renderer = hero.AddComponent<SpriteRenderer>();
        Animator animator = hero.AddComponent<Animator>();

        blackboard = hero.AddComponent(GameType("HeroStateBlackboard"));
        input = hero.AddComponent(GameType("HeroInputReader"));
        motor = hero.AddComponent(GameType("HeroMotor"));
        Component audio = hero.AddComponent(GameType("HeroAudioController"));
        Component sensors = hero.AddComponent(GameType("HeroSensors"));
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
    public IEnumerator OrdinaryIdle_HoldingDashWithoutQualifyingDirection_DoesNotBeginWildstride()
    {
        yield return AdvanceHero(0.11f, Key.X);
        Assert.That((bool)GetField(blackboard, "sprinting"), Is.False);
        for (int i = 1; i < 30; i++)
        {
            SimulateHeroStep(0.02f);
            Assert.That((bool)GetField(blackboard, "sprinting"), Is.False);
        }

        Assert.That((int)GetField(blackboard, "sprintDirection"), Is.Zero);
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.Not.EqualTo("Wildstride"));
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
    public IEnumerator WildstrideDoubleJump_PreservesLandingContinuityWithoutWalkFrame()
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
        Assert.That((bool)GetProperty(input, "DashCommandArmed"), Is.True, "Double Jump must keep Dash armed");
        Assert.That((float)GetField(motor, "desiredMoveX"), Is.EqualTo(1f), "Ordinary air steering must resume after Double Jump.");

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
            Is.True,
            $"Landing must resume Wildstride after Double Jump (authorised={GetProperty(sprintAction, "HasAirborneLandingAuthorisation")}, phaseCarry={GetProperty(sprintAction, "IsJumpCarrying")}, reason={GetField(blackboard, "lastSprintCancelReason")}, grounded={GetField(blackboard, "grounded")}, dash={GetProperty(input, "DashHeld")})");
        Assert.That(GetField(animations, "currentVisualState").ToString(), Is.EqualTo("Wildstride"));
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
