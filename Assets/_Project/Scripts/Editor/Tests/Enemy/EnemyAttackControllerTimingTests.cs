using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class EnemyAttackControllerTimingTests
{
    private GameObject root;
    private EnemyConfig config;

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        if (config != null) Object.DestroyImmediate(config);
    }

    [Test]
    public void TryConfigureTimingsUpdatesIdleControllerWithoutPrefabMigration()
    {
        EnemyAttackController controller = CreateController();

        Assert.That(controller.TryConfigureTimings(0.2f, 0.3f, 0.4f, 0.5f), Is.True);
        Assert.That(GetFloat(controller, "startupDuration"), Is.EqualTo(0.2f));
        Assert.That(GetFloat(controller, "activeDuration"), Is.EqualTo(0.3f));
        Assert.That(GetFloat(controller, "recoveryDuration"), Is.EqualTo(0.4f));
        Assert.That(GetFloat(controller, "cooldownDuration"), Is.EqualTo(0.5f));
    }

    [Test]
    public void TryConfigureTimingsRejectsInvalidValuesAndActiveAttack()
    {
        EnemyAttackController controller = CreateController();

        Assert.That(controller.TryConfigureTimings(-0.01f, 0.2f, 0.3f, 0f), Is.False);
        Assert.That(controller.TryConfigureTimings(float.NaN, 0.2f, 0.3f, 0f), Is.False);
        Assert.That(controller.BeginAttack(), Is.True);
        Assert.That(controller.TryConfigureTimings(0.1f, 0.1f, 0.1f, 0f), Is.False);
    }

    private EnemyAttackController CreateController()
    {
        root = new GameObject("Attack Timing Test");
        Rigidbody2D body = root.AddComponent<Rigidbody2D>();
        EnemyMotor motor = root.AddComponent<EnemyMotor>();
        EnemyStateBlackboard blackboard = root.AddComponent<EnemyStateBlackboard>();
        config = ScriptableObject.CreateInstance<EnemyConfig>();
        motor.Initialize(config, body);

        GameObject child = new GameObject("Attack");
        child.transform.SetParent(root.transform);
        EnemyAttackController controller = child.AddComponent<EnemyAttackController>();
        controller.Initialize(config, blackboard, motor);
        return controller;
    }

    private static float GetFloat(object target, string fieldName)
    {
        return (float)target.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }
}
