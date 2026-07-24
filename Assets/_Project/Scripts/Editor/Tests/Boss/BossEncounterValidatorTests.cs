using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BossEncounterValidatorTests
{
    [Test]
    public void InvalidParticipantReportsConcreteHierarchyAndPersistenceIssues()
    {
        GameObject wrapper = new GameObject("Invalid Participant");
        GameObject actor = new GameObject("ActorRoot");
        GameObject external = new GameObject("External");
        try
        {
            BossEncounterParticipant participant = wrapper.AddComponent<BossEncounterParticipant>();
            actor.SetActive(true);
            EnemyHealthComponent health = external.AddComponent<EnemyHealthComponent>();
            InvalidBossBehaviour behaviour = external.AddComponent<InvalidBossBehaviour>();
            actor.AddComponent<EnemyPersistence>();
            SetPrivateField(participant, "actorRoot", actor);
            SetPrivateField(participant, "health", health);
            SetPrivateField(participant, "behaviourSource", behaviour);

            LogAssert.ignoreFailingMessages = true;
            int issues = BossEncounterValidator.ValidateParticipant(participant);
            Assert.That(issues, Is.GreaterThanOrEqualTo(4));
        }
        finally
        {
            LogAssert.ignoreFailingMessages = false;
            Object.DestroyImmediate(wrapper);
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(external);
        }
    }

    [Test]
    public void PermanentEnemyIdCollisionIsDetectedAgainstDefinitions()
    {
        GameObject enemy = new GameObject("Legacy Permanent Boss");
        try
        {
            EnemyPersistence persistence = enemy.AddComponent<EnemyPersistence>();
            SetPrivateField(persistence, "mode", EnemyPersistenceMode.PermanentEncounter);
            SetPrivateField(persistence, "worldObjectId", "shared_boss_id");
            LogAssert.Expect(
                LogType.Error,
                "[BossEncounterValidator] 'Legacy Permanent Boss' PermanentEncounter ID 'shared_boss_id' collides with a BossEncounterDefinition.");

            int issues = BossEncounterValidator.ValidatePermanentEncounterCollision(
                persistence,
                new HashSet<string> { "shared_boss_id" });

            Assert.That(issues, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(enemy);
        }
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }
}

public sealed class InvalidBossBehaviour : MonoBehaviour
{
}
