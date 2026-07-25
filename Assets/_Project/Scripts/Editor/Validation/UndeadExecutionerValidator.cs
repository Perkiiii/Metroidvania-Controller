using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class UndeadExecutionerValidator
{
    private const string ParticipantPrefabPath =
        "Assets/_Project/Prefabs/Bosses/UndeadExecutioner/UndeadExecutionerParticipant.prefab";

    [MenuItem("Tools/Project/Validate Undead Executioner")]
    public static void ValidateUndeadExecutioner()
    {
        int issues = ValidateCurrent();
        if (issues == 0)
        {
            Debug.Log("[UndeadExecutionerValidator] Validation passed.");
        }
        else
        {
            Debug.LogWarning($"[UndeadExecutionerValidator] Found {issues} issue(s). See earlier logs.");
        }
    }

    public static int ValidateCurrent()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ParticipantPrefabPath);
        int issues = prefab == null
            ? Error(null, $"Missing participant prefab at '{ParticipantPrefabPath}'.")
            : ValidateParticipantPrefab(prefab);

        foreach (UndeadExecutionerBehaviour behaviour in Object.FindObjectsByType<UndeadExecutionerBehaviour>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(behaviour))
            {
                continue;
            }

            issues += ValidateSceneInstance(behaviour);
        }

        return issues;
    }

    internal static int ValidateParticipantPrefab(GameObject prefab)
    {
        int issues = 0;
        BossEncounterParticipant participant = prefab.GetComponent<BossEncounterParticipant>();
        if (participant == null)
        {
            return Error(prefab, "Root requires BossEncounterParticipant.");
        }

        if (!prefab.activeSelf)
        {
            issues += Error(prefab, "Participant wrapper must be authored active.");
        }

        GameObject actorRoot = participant.ActorRoot;
        if (actorRoot == null)
        {
            return issues + Error(participant, "Participant is missing ActorRoot.");
        }

        if (actorRoot.activeSelf)
        {
            issues += Error(actorRoot, "ActorRoot must be authored inactive.");
        }

        UndeadExecutionerBehaviour behaviour = actorRoot.GetComponent<UndeadExecutionerBehaviour>();
        if (behaviour == null)
        {
            return issues + Error(actorRoot, "ActorRoot requires UndeadExecutionerBehaviour.");
        }

        issues += ValidateBehaviour(behaviour, false);
        return issues;
    }

    internal static int ValidateBehaviour(UndeadExecutionerBehaviour behaviour, bool requireArenaLimits)
    {
        int issues = 0;
        if (behaviour.Config == null)
        {
            issues += Error(behaviour, "is missing UndeadExecutionerConfig.");
        }
        else
        {
            issues += ValidateClips(behaviour.Config, behaviour);
        }

        if (behaviour.Motor == null)
        {
            issues += Error(behaviour, "requires EnemyMotor.");
        }

        if (behaviour.Health == null)
        {
            issues += Error(behaviour, "requires EnemyHealthComponent.");
        }
        else if (behaviour.Health.DeathCleanupMode != EnemyDeathCleanupMode.RetainRoot)
        {
            issues += Error(behaviour.Health, "must use RetainRoot death cleanup.");
        }

        Rigidbody2D body = behaviour.GetComponent<Rigidbody2D>();
        if (body == null)
        {
            issues += Error(behaviour, "requires Rigidbody2D.");
        }
        else
        {
            if (body.bodyType != RigidbodyType2D.Dynamic)
                issues += Error(body, "Rigidbody2D must be Dynamic.");
            if (!Mathf.Approximately(body.gravityScale, 0f))
                issues += Error(body, "Rigidbody2D gravity scale must be zero.");
            if ((body.constraints & RigidbodyConstraints2D.FreezePositionY) == 0)
                issues += Error(body, "Rigidbody2D must freeze Y position for stable hover.");
            if ((body.constraints & RigidbodyConstraints2D.FreezeRotation) == 0)
                issues += Error(body, "Rigidbody2D must freeze rotation.");
        }

        Collider2D bodyCollider = behaviour.BodyCollider != null
            ? behaviour.BodyCollider
            : behaviour.GetComponent<Collider2D>();
        if (bodyCollider == null)
        {
            issues += Error(behaviour, "requires a stable body/hurt collider.");
        }
        else
        {
            if (bodyCollider.isTrigger)
                issues += Error(bodyCollider, "Body collider must be solid so the hero cannot remain merged inside the boss.");
            if (!bodyCollider.enabled)
                issues += Error(bodyCollider, "Body collider must be authored enabled for active combat.");

            int enemiesLayer = LayerMask.NameToLayer("Enemies");
            if (enemiesLayer < 0 || bodyCollider.gameObject.layer != enemiesLayer)
                issues += Error(bodyCollider, "Damageable body collider must be on the Enemies layer.");

            if (behaviour.Health != null
                && !ReferenceEquals(ResolveHeroAttackReceiver(bodyCollider), behaviour.Health))
            {
                issues += Error(
                    bodyCollider,
                    "Body collider parent chain must resolve the configured EnemyHealthComponent as IHeroAttackReceiver.");
            }
        }

        if (behaviour.PresentationRoot == null || behaviour.SpriteRenderer == null || behaviour.Animancer == null)
        {
            issues += Error(behaviour, "requires PresentationRoot, SpriteRenderer, and Animancer references.");
        }

        if (behaviour.ComboFirst == null || behaviour.ComboSecond == null || behaviour.ShadowBurst == null)
        {
            issues += Error(behaviour, "requires ComboFirst, ComboSecond, and ShadowBurst attack controllers.");
        }

        if (behaviour.SpiritPressure == null)
        {
            issues += Error(behaviour, "requires one boss-owned spirit pressure helper.");
        }
        else
        {
            if (behaviour.SpiritPressure.GetComponentInParent<BossEncounterParticipant>() != null
                && behaviour.SpiritPressure.GetComponent<BossEncounterParticipant>() != null)
            {
                issues += Error(behaviour.SpiritPressure, "Spirit must not be an encounter participant.");
            }

            if (behaviour.SpiritPressure.GetComponentInChildren<EnemyPersistence>(true) != null)
            {
                issues += Error(behaviour.SpiritPressure, "Spirit must not carry EnemyPersistence.");
            }
        }

        if (behaviour.GetComponentInChildren<EnemyPersistence>(true) != null)
        {
            issues += Error(behaviour, "Coordinated boss actor must not carry EnemyPersistence.");
        }

        if (behaviour.GetComponentInChildren<EnemyContactDamage>(true) != null)
        {
            issues += Error(behaviour, "Undead Executioner uses explicit attack windows and must not carry EnemyContactDamage.");
        }

        if (requireArenaLimits && (behaviour.ArenaLeftLimit == null || behaviour.ArenaRightLimit == null))
        {
            issues += Error(behaviour, "Scene instance requires authored left and right arena limits.");
        }
        else if (behaviour.ArenaLeftLimit != null
                 && behaviour.ArenaRightLimit != null
                 && behaviour.ArenaLeftLimit.position.x >= behaviour.ArenaRightLimit.position.x)
        {
            issues += Error(behaviour, "Arena left limit must be left of the right limit.");
        }

        foreach (EnemyAttackHitbox hitbox in behaviour.GetComponentsInChildren<EnemyAttackHitbox>(true))
        {
            Collider2D collider = hitbox.GetComponent<Collider2D>();
            DamageHero damage = hitbox.GetComponent<DamageHero>();
            if (collider == null || !collider.isTrigger)
                issues += Error(hitbox, "Attack hitbox requires a trigger Collider2D.");
            if (collider != null && collider.enabled)
                issues += Error(collider, "Attack hitbox must be authored disabled.");
            int enemyAttackLayer = LayerMask.NameToLayer("Enemy Attack");
            if (collider != null && (enemyAttackLayer < 0 || collider.gameObject.layer != enemyAttackLayer))
                issues += Error(collider, "Attack hitbox must be on the Enemy Attack layer.");
            if (damage == null || damage.DamageDealt <= 0)
                issues += Error(hitbox, "Attack hitbox requires positive DamageHero metadata.");
        }

        return issues;
    }

    private static int ValidateSceneInstance(UndeadExecutionerBehaviour behaviour)
    {
        return ValidateBehaviour(behaviour, true);
    }

    internal static IHeroAttackReceiver ResolveHeroAttackReceiver(Collider2D collider)
    {
        if (collider == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = collider.GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IHeroAttackReceiver receiver)
            {
                return receiver;
            }
        }

        return null;
    }

    private static int ValidateClips(UndeadExecutionerConfig config, Object context)
    {
        int issues = 0;
        issues += RequireClip(config.idleClip, true, "Idle", context);
        issues += RequireClip(config.phaseTwoIdleClip, true, "Idle2", context);
        issues += RequireClip(config.executionerComboClip, false, "ExecutionerCombo", context);
        issues += RequireClip(config.shadowBurstClip, false, "ShadowBurst", context);
        issues += RequireClip(config.summonClip, false, "Summon", context);
        issues += RequireClip(config.deathClip, false, "Death", context);

        issues += RequireEvents(
            config.executionerComboClip,
            context,
            "ComboFirstOpen",
            "ComboFirstClose",
            "ComboSecondBegin",
            "ComboSecondOpen",
            "ComboSecondClose",
            "ComboComplete");
        issues += RequireEvents(
            config.shadowBurstClip,
            context,
            "ShadowBurstOpen",
            "ShadowBurstClose",
            "ShadowBurstComplete");
        issues += RequireEvents(config.summonClip, context, "SummonSpirit");
        return issues;
    }

    private static int RequireClip(AnimationClip clip, bool shouldLoop, string label, Object context)
    {
        if (clip == null)
        {
            return Error(context, $"is missing {label} clip.");
        }

        bool loops = AnimationUtility.GetAnimationClipSettings(clip).loopTime;
        return loops == shouldLoop
            ? 0
            : Error(clip, $"{label} loop setting must be {shouldLoop}.");
    }

    private static int RequireEvents(AnimationClip clip, Object context, params string[] requiredNames)
    {
        if (clip == null)
        {
            return 0;
        }

        List<string> names = AnimationUtility.GetAnimationEvents(clip)
            .Select(animationEvent => animationEvent.functionName)
            .ToList();
        int issues = 0;
        for (int i = 0; i < requiredNames.Length; i++)
        {
            int count = names.Count(name => name == requiredNames[i]);
            if (count != 1)
            {
                issues += Error(context, $"Clip '{clip.name}' requires exactly one '{requiredNames[i]}' event; found {count}.");
            }
        }

        return issues;
    }

    private static int Error(Object context, string message)
    {
        Debug.LogError($"[UndeadExecutionerValidator] {message}", context);
        return 1;
    }
}
