using System.Reflection;
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
