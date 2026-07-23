using UnityEditor;
using UnityEngine;

public static class EnemyAIFoundationValidator
{
    private const string MushroomPrefabPath = "Assets/_Project/Prefabs/Enemies/Mushroom.prefab";
    private const string MushroomConfigPath = "Assets/_Project/ScriptableObjects/Enemy/MushroomConfig.asset";

    [MenuItem("Tools/Project/Validate Enemy AI Foundation")]
    public static void ValidateEnemyAIFoundation()
    {
        int issues = 0;

        EnemyConfig config = AssetDatabase.LoadAssetAtPath<EnemyConfig>(MushroomConfigPath);
        if (config == null)
        {
            Debug.LogError($"[EnemyAIFoundationValidator] Missing Mushroom config at '{MushroomConfigPath}'.");
            issues++;
        }
        else
        {
            issues += ValidateConfig(config);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[EnemyAIFoundationValidator] Missing Mushroom prefab at '{MushroomPrefabPath}'.");
            issues++;
        }
        else
        {
            issues += ValidateMushroomPrefab(prefab, config);
        }

        issues += ValidatePhysicsLayers();

        if (issues == 0)
            Debug.Log("[EnemyAIFoundationValidator] Enemy AI foundation validation passed.");
        else
            Debug.LogWarning($"[EnemyAIFoundationValidator] Enemy AI foundation validation found {issues} issue(s). See earlier logs.");
    }

    private static int ValidateConfig(EnemyConfig config)
    {
        int issues = 0;

        if (config.maxHealth <= 0)
            issues += Error(config, "MushroomConfig.maxHealth must be greater than 0.");

        if (config.detectionRadius <= 0f)
            issues += Error(config, "MushroomConfig.detectionRadius must be greater than 0.");

        if (config.attackRange <= 0f)
            issues += Error(config, "MushroomConfig.attackRange must be greater than 0.");

        if (config.attackCooldown < 0f)
            issues += Error(config, "MushroomConfig.attackCooldown must not be negative.");

        if (config.terrainLayers.value == 0)
            issues += Error(config, "MushroomConfig.terrainLayers must include Terrain.");

        return issues;
    }

    private static int ValidateMushroomPrefab(GameObject prefab, EnemyConfig expectedConfig)
    {
        int issues = 0;

        int enemiesLayer = LayerMask.NameToLayer("Enemies");
        int enemyAttackLayer = LayerMask.NameToLayer("Enemy Attack");
        int heroBoxLayer = LayerMask.NameToLayer("Hero Box");
        int terrainLayer = LayerMask.NameToLayer("Terrain");

        if (enemiesLayer < 0)
            issues += Error(prefab, "Layer 'Enemies' is missing.");
        else if (prefab.layer != enemiesLayer)
            issues += Error(prefab, "Mushroom root must be on the 'Enemies' layer.");

        EnemyController controller = prefab.GetComponent<EnemyController>();
        EnemyStateBlackboard blackboard = prefab.GetComponent<EnemyStateBlackboard>();
        EnemyMotor motor = prefab.GetComponent<EnemyMotor>();
        EnemyPerception perception = prefab.GetComponent<EnemyPerception>();
        EnemyHealthComponent health = prefab.GetComponent<EnemyHealthComponent>();
        EnemyRecoil recoil = prefab.GetComponent<EnemyRecoil>();
        EnemyAttackController attackController = prefab.GetComponent<EnemyAttackController>();
        MushroomEnemy behaviour = prefab.GetComponent<MushroomEnemy>();
        EnemyContactDamage contactDamage = prefab.GetComponent<EnemyContactDamage>();
        DamageHero contactDamageMetadata = prefab.GetComponent<DamageHero>();
        EnemyAttackHitbox rootAttackHitbox = prefab.GetComponent<EnemyAttackHitbox>();
        Rigidbody2D body = prefab.GetComponent<Rigidbody2D>();
        Collider2D bodyCollider = prefab.GetComponent<Collider2D>();

        if (controller == null) issues += Error(prefab, "Mushroom prefab requires EnemyController.");
        if (blackboard == null) issues += Error(prefab, "Mushroom prefab requires EnemyStateBlackboard.");
        if (motor == null) issues += Error(prefab, "Mushroom prefab requires EnemyMotor.");
        if (perception == null) issues += Error(prefab, "Mushroom prefab requires EnemyPerception.");
        if (health == null) issues += Error(prefab, "Mushroom prefab requires EnemyHealthComponent.");
        if (recoil == null) issues += Error(prefab, "Mushroom prefab requires EnemyRecoil.");
        if (attackController == null) issues += Error(prefab, "Mushroom prefab requires EnemyAttackController.");
        if (behaviour == null) issues += Error(prefab, "Mushroom prefab requires MushroomEnemy.");
        if (contactDamage == null) issues += Error(prefab, "Mushroom prefab requires EnemyContactDamage for retained body-contact damage.");
        if (contactDamageMetadata == null) issues += Error(prefab, "Mushroom contact damage requires root DamageHero metadata.");
        else if (contactDamageMetadata.DamageDealt <= 0) issues += Error(contactDamageMetadata, "Mushroom root DamageHero.damageDealt must be greater than 0.");
        if (rootAttackHitbox != null) issues += Error(rootAttackHitbox, "Mushroom root must not carry EnemyAttackHitbox; authored attack damage belongs on child hitboxes.");
        if (body == null) issues += Error(prefab, "Mushroom prefab requires Rigidbody2D.");
        if (bodyCollider == null) issues += Error(prefab, "Mushroom prefab requires a body Collider2D.");

        if (controller != null && expectedConfig != null && controller.Config != expectedConfig)
            issues += Error(controller, "EnemyController.config must reference MushroomConfig.");

        if (body != null && body.bodyType != RigidbodyType2D.Dynamic)
            issues += Error(body, "Mushroom Rigidbody2D should be Dynamic.");

        if (body != null && body.constraints != RigidbodyConstraints2D.FreezeRotation)
            issues += Error(body, "Mushroom Rigidbody2D should freeze rotation.");

        if (bodyCollider != null && bodyCollider.isTrigger)
            issues += Error(bodyCollider, "Mushroom body collider must not be a trigger.");

        if (attackController != null)
        {
            SerializedObject serializedAttack = new SerializedObject(attackController);
            SerializedProperty hitboxes = serializedAttack.FindProperty("hitboxes");
            if (hitboxes == null || hitboxes.arraySize == 0)
            {
                issues += Error(attackController, "EnemyAttackController.hitboxes must contain at least one EnemyAttackHitbox.");
            }
            else
            {
                for (int i = 0; i < hitboxes.arraySize; i++)
                {
                    if (hitboxes.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        issues += Error(attackController, $"EnemyAttackController.hitboxes[{i}] is missing.");
                }
            }

            issues += ValidateLayerMask(serializedAttack, "heroHurtboxLayers", heroBoxLayer, attackController, "EnemyAttackController.heroHurtboxLayers must include 'Hero Box'.");
        }

        if (perception != null)
        {
            SerializedObject serializedPerception = new SerializedObject(perception);
            issues += ValidateLayerMask(serializedPerception, "heroLayers", LayerMask.NameToLayer("Player"), perception, "EnemyPerception.heroLayers must include 'Player'.");
            issues += ValidateLayerMask(serializedPerception, "lineOfSightBlockers", terrainLayer, perception, "EnemyPerception.lineOfSightBlockers must include 'Terrain'.");
        }

        if (behaviour != null)
        {
            SerializedObject serializedBehaviour = new SerializedObject(behaviour);
            if (serializedBehaviour.FindProperty("wallCheck")?.objectReferenceValue == null)
                issues += Error(behaviour, "MushroomEnemy.wallCheck must be assigned.");

            if (serializedBehaviour.FindProperty("groundCheck")?.objectReferenceValue == null)
                issues += Error(behaviour, "MushroomEnemy.groundCheck must be assigned.");

            if (serializedBehaviour.FindProperty("attackClip")?.objectReferenceValue == null)
            {
                issues += Error(behaviour, "MushroomEnemy.attackClip must be assigned to the single Mushroom attack animation clip.");
            }
            else
            {
                AnimationClip attackClip = serializedBehaviour.FindProperty("attackClip").objectReferenceValue as AnimationClip;
                issues += ValidateRequiredAnimationEvents(attackClip, behaviour);
            }
        }

        EnemyAttackHitbox[] attackHitboxes = prefab.GetComponentsInChildren<EnemyAttackHitbox>(true);
        if (attackHitboxes.Length == 0)
        {
            issues += Error(prefab, "Mushroom prefab requires at least one EnemyAttackHitbox child.");
        }
        else
        {
            foreach (EnemyAttackHitbox attackHitbox in attackHitboxes)
            {
                if (enemyAttackLayer >= 0 && attackHitbox.gameObject.layer != enemyAttackLayer)
                    issues += Error(attackHitbox, "EnemyAttackHitbox GameObject must be on the 'Enemy Attack' layer.");

                Collider2D hitboxCollider = attackHitbox.GetComponent<Collider2D>();
                DamageHero damageHero = attackHitbox.GetComponent<DamageHero>();
                EnemyContactDamage hitboxContactDamage = attackHitbox.GetComponent<EnemyContactDamage>();

                if (hitboxCollider == null)
                    issues += Error(attackHitbox, "EnemyAttackHitbox requires a Collider2D.");
                else
                {
                    if (!hitboxCollider.isTrigger)
                        issues += Error(hitboxCollider, "EnemyAttackHitbox collider must be a trigger.");

                    if (hitboxCollider.enabled)
                        issues += Error(hitboxCollider, "EnemyAttackHitbox collider must be disabled by default.");
                }

                if (damageHero == null)
                    issues += Error(attackHitbox, "EnemyAttackHitbox requires DamageHero metadata.");
                else if (damageHero.DamageDealt <= 0)
                    issues += Error(damageHero, "EnemyAttackHitbox DamageHero.damageDealt must be greater than 0.");

                if (hitboxContactDamage != null)
                    issues += Error(hitboxContactDamage, "EnemyAttackHitbox children must not also carry EnemyContactDamage.");
            }
        }

        return issues;
    }

    private static int ValidateRequiredAnimationEvents(AnimationClip clip, Object context)
    {
        int issues = 0;

        if (clip == null)
        {
            return Error(context, "MushroomEnemy.attackClip must reference an AnimationClip.");
        }

        AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
        if (!HasAnimationEvent(events, "OpenAttackWindow"))
            issues += Error(clip, "Mushroom attack clip must contain an OpenAttackWindow animation event.");

        if (!HasAnimationEvent(events, "CloseAttackWindow"))
            issues += Error(clip, "Mushroom attack clip must contain a CloseAttackWindow animation event.");

        if (!HasAnimationEvent(events, "CompleteAttack"))
            issues += Error(clip, "Mushroom attack clip must contain a CompleteAttack animation event.");

        return issues;
    }

    private static bool HasAnimationEvent(AnimationEvent[] events, string functionName)
    {
        for (int i = 0; i < events.Length; i++)
        {
            if (events[i] != null && events[i].functionName == functionName)
            {
                return true;
            }
        }

        return false;
    }

    private static int ValidatePhysicsLayers()
    {
        int issues = 0;

        int enemiesLayer = LayerMask.NameToLayer("Enemies");
        int enemyAttackLayer = LayerMask.NameToLayer("Enemy Attack");
        int heroBoxLayer = LayerMask.NameToLayer("Hero Box");
        int terrainLayer = LayerMask.NameToLayer("Terrain");

        if (enemyAttackLayer < 0)
            issues += Error(null, "Layer 'Enemy Attack' is missing.");

        if (heroBoxLayer < 0)
            issues += Error(null, "Layer 'Hero Box' is missing.");

        if (enemiesLayer < 0)
            issues += Error(null, "Layer 'Enemies' is missing.");

        if (terrainLayer < 0)
            issues += Error(null, "Layer 'Terrain' is missing.");

        if (enemyAttackLayer >= 0 && heroBoxLayer >= 0 && Physics2D.GetIgnoreLayerCollision(enemyAttackLayer, heroBoxLayer))
            issues += Error(null, "Physics 2D must allow 'Enemy Attack' to overlap 'Hero Box'.");

        if (enemyAttackLayer >= 0 && enemyAttackLayer >= 0 && !Physics2D.GetIgnoreLayerCollision(enemyAttackLayer, enemyAttackLayer))
            issues += Error(null, "Physics 2D should disable 'Enemy Attack' vs 'Enemy Attack'.");

        if (enemyAttackLayer >= 0 && enemiesLayer >= 0 && !Physics2D.GetIgnoreLayerCollision(enemyAttackLayer, enemiesLayer))
            issues += Error(null, "Physics 2D should disable 'Enemy Attack' vs 'Enemies'.");

        return issues;
    }

    private static int ValidateLayerMask(SerializedObject serializedObject, string propertyName, int requiredLayer, Object context, string message)
    {
        if (requiredLayer < 0)
            return Error(context, message);

        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return Error(context, $"Missing serialized LayerMask '{propertyName}'.");

        int maskValue = property.FindPropertyRelative("m_Bits")?.intValue ?? property.intValue;
        if ((maskValue & (1 << requiredLayer)) == 0)
            return Error(context, message);

        return 0;
    }

    private static int Error(Object context, string message)
    {
        Debug.LogError($"[EnemyAIFoundationValidator] {message}", context);
        return 1;
    }
}
