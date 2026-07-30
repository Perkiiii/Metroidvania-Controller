using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class HeroLedgeClimbPlayModeTests
{
    private GameObject hero;
    private GameObject ledge;
    private ScriptableObject config;
    private ScriptableObject abilityConfig;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (ledge != null) UnityEngine.Object.Destroy(ledge);
        if (hero != null) UnityEngine.Object.Destroy(hero);
        if (config != null) UnityEngine.Object.Destroy(config);
        if (abilityConfig != null) UnityEngine.Object.Destroy(abilityConfig);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TimerOwnedClimbCompletesWithoutAnimationCallbackAndRestoresGravity()
    {
        object action = CreateRuntime(out Component blackboard, out Component motor, out Rigidbody2D body);
        bool started = (bool)Invoke(action, "FixedTick", Time.fixedDeltaTime);
        Assert.That(started, Is.True);

        float deadline = Time.realtimeSinceStartup + 1f;
        while ((bool)GetProperty(action, "IsActive"))
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline));
            Invoke(action, "FixedTick", Time.fixedDeltaTime);
            Invoke(motor, "FixedTick", Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        Assert.That((bool)GetField(blackboard, "ledgeClimbing"), Is.False);
        Assert.That(body.gravityScale, Is.Not.Zero);
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
    }

    [UnityTest]
    public IEnumerator ControlLockDuringCatchCancelsWithoutLeakingMotorState()
    {
        object action = CreateRuntime(out Component blackboard, out _, out Rigidbody2D body);
        Assert.That((bool)Invoke(action, "FixedTick", Time.fixedDeltaTime), Is.True);

        SetField(blackboard, "controlLocked", true);
        Invoke(action, "FixedTick", Time.fixedDeltaTime);
        yield return new WaitForFixedUpdate();

        Assert.That((bool)GetProperty(action, "IsActive"), Is.False);
        Assert.That((bool)GetField(blackboard, "ledgeClimbing"), Is.False);
        Assert.That(body.gravityScale, Is.Not.Zero);
    }

    private object CreateRuntime(out Component blackboard, out Component motor, out Rigidbody2D body)
    {
        Type configType = RuntimeType("HeroConfig");
        Type abilityConfigType = RuntimeType("HeroAbilityConfig");
        Type blackboardType = RuntimeType("HeroStateBlackboard");
        Type inputType = RuntimeType("HeroInputReader");
        Type sensorsType = RuntimeType("HeroSensors");
        Type motorType = RuntimeType("HeroMotor");
        Type dashType = RuntimeType("HeroDashAction");
        Type actionType = RuntimeType("HeroLedgeClimbAction");

        config = ScriptableObject.CreateInstance(configType);
        abilityConfig = ScriptableObject.CreateInstance(abilityConfigType);
        SetField(config, "terrainLayers", (LayerMask)1);
        SetField(config, "ledgeSurfaceLayers", (LayerMask)1);
        SetField(config, "wallProbeDistance", 0.1f);
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

        hero = new GameObject("PlayMode Ledge Hero");
        hero.layer = LayerMask.NameToLayer("Player");
        hero.transform.position = new Vector3(0f, 1f, 0f);
        body = hero.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        BoxCollider2D heroCollider = hero.AddComponent<BoxCollider2D>();
        heroCollider.size = new Vector2(0.36f, 0.9f);
        blackboard = hero.AddComponent(blackboardType);
        Component input = hero.AddComponent(inputType);
        Component sensors = hero.AddComponent(sensorsType);
        motor = hero.AddComponent(motorType);
        Invoke(sensors, "Initialize", config, blackboard, body, heroCollider);
        Invoke(motor, "Initialize", config, abilityConfig, blackboard, body, heroCollider, null, hero.transform);

        PropertyInfo moveVector = inputType.GetProperty("MoveVector", BindingFlags.Instance | BindingFlags.Public);
        moveVector.SetValue(input, Vector2.right);
        SetField(blackboard, "grounded", false);
        SetField(blackboard, "touchingWallFront", true);

        ledge = new GameObject("PlayMode Static Terrain");
        ledge.transform.position = new Vector3(1.135f, 0.75f, 0f);
        BoxCollider2D ledgeCollider = ledge.AddComponent<BoxCollider2D>();
        ledgeCollider.size = new Vector2(1.73f, 1.5f);
        Physics2D.SyncTransforms();

        object dash = Activator.CreateInstance(
            dashType,
            config,
            abilityConfig,
            blackboard,
            input,
            motor,
            null,
            null);
        return Activator.CreateInstance(
            actionType,
            config,
            blackboard,
            input,
            motor,
            sensors,
            dash,
            (Action)(() => { }));
    }

    private static Type RuntimeType(string name)
    {
        Type type = Type.GetType($"{name}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Runtime type '{name}' was not found.");
        return type;
    }

    private static object GetProperty(object instance, string name)
    {
        return instance.GetType()
            .GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(instance);
    }

    private static object GetField(object instance, string name)
    {
        return instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .GetValue(instance);
    }

    private static void SetField(object instance, string name, object value)
    {
        instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SetValue(instance, value);
    }

    private static object Invoke(object instance, string name, params object[] arguments)
    {
        foreach (MethodInfo method in instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (method.Name != name || parameters.Length != arguments.Length)
            {
                continue;
            }

            bool compatible = true;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (arguments[i] != null && !parameters[i].ParameterType.IsInstanceOfType(arguments[i]))
                {
                    compatible = false;
                    break;
                }
            }

            if (compatible)
            {
                return method.Invoke(instance, arguments);
            }
        }

        Assert.Fail($"Method '{name}' was not found on {instance.GetType().Name}.");
        return null;
    }
}
