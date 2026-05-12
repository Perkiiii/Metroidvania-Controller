using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class HeroActionController : MonoBehaviour
{
    [Header("Attack Modules")]
    [SerializeField] private Transform attackRoot;
    [SerializeField] private HeroAttackModule[] attackModules;

    [Header("Attack Safety")]
    [SerializeField, Min(0.1f)] private float attackFailSafeTimeout = 1f;

    private HeroConfig config;
    private HeroStateBlackboard blackboard;
    private HeroInputReader input;
    private HeroMotor motor;

    private HeroJumpAction jump;
    private HeroDashAction dash;
    private HeroAttackAction attack;
    private HeroWallSlideAction wallSlide;

    public int AttackVersion => attack != null ? attack.AttackVersion : 0;

    public void Initialize(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;

        jump = new HeroJumpAction(config, blackboard, input, heroMotor);
        dash = new HeroDashAction(config, blackboard, input, heroMotor);
        attack = new HeroAttackAction(config, blackboard, input, gameObject, transform, ResolveAttackModules(), attackFailSafeTimeout);
        wallSlide = new HeroWallSlideAction(config, blackboard, input, heroMotor);
    }

    public void Tick()
    {
        if (config == null || blackboard == null || input == null || motor == null)
        {
            return;
        }

        dash.Tick(Time.deltaTime);
        attack.Tick(Time.deltaTime);
        ApplyLocomotionIntent();
    }

    public void FixedTick(float fixedDeltaTime)
    {
        if (config == null || blackboard == null || input == null || motor == null)
        {
            return;
        }

        wallSlide.FixedTick();
        jump.FixedTick(fixedDeltaTime);
        dash.FixedTick(fixedDeltaTime);
        attack.FixedTick(fixedDeltaTime);
    }

    public void CancelAttack()
    {
        attack?.CancelAttack();
    }

    public void BeginAttackWindow()
    {
        attack?.BeginAttackWindow();
    }

    public void EndAttackWindow()
    {
        attack?.EndAttackWindow();
    }

    public void CompleteAttackFromAnimation()
    {
        attack?.CompleteAttackFromAnimation();
    }

    public void SetAttackFallbackTimeout(float timeout)
    {
        attack?.SetFallbackTimeout(timeout);
    }

    private void ApplyLocomotionIntent()
    {
        float moveX = blackboard.controlLocked || blackboard.inputBlocked || blackboard.dashing ? 0f : input.MoveVector.x;
        bool wantsRun = input.SprintHeld || !config.requireSprintForRun;
        motor.SetDesiredMove(moveX, wantsRun);

        if (Mathf.Abs(moveX) > config.horizontalInputDeadZone && !blackboard.controlLocked)
        {
            motor.SetFacingDirection(moveX > 0f ? 1 : -1);
        }
    }

    private HeroAttackModule[] ResolveAttackModules()
    {
        if (attackModules == null || attackModules.Length == 0)
        {
            CacheAttackModules();
        }

        return attackModules;
    }

    [ContextMenu("Cache Attack Modules")]
    private void CacheAttackModules()
    {
        Transform searchRoot = attackRoot != null ? attackRoot : transform;
        attackModules = searchRoot.GetComponentsInChildren<HeroAttackModule>(true);
    }

    [ContextMenu("Create Default Attack Modules")]
    private void CreateDefaultAttackModules()
    {
#if UNITY_EDITOR
        if (attackRoot == null)
        {
            GameObject rootObject = new GameObject("Attacks");
            Undo.RegisterCreatedObjectUndo(rootObject, "Create Attack Root");
            Undo.SetTransformParent(rootObject.transform, transform, "Parent Attack Root");
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            attackRoot = rootObject.transform;
        }

        HeroConfig sourceConfig = config;
        if (sourceConfig == null)
        {
            HeroController hero = GetComponent<HeroController>();
            SerializedObject serializedHero = hero != null ? new SerializedObject(hero) : null;
            sourceConfig = serializedHero?.FindProperty("config")?.objectReferenceValue as HeroConfig;
        }

        CreateOrUpdateDefaultModule(
            "SlashSide",
            HeroAttackDirection.Side,
            true,
            sourceConfig != null ? sourceConfig.attackSideOffset : new Vector2(0.75f, 0f),
            sourceConfig != null ? sourceConfig.attackSideSize : new Vector2(1.2f, 0.5f),
            sourceConfig);

        CreateOrUpdateDefaultModule(
            "SlashUp",
            HeroAttackDirection.Up,
            false,
            sourceConfig != null ? sourceConfig.attackUpOffset : new Vector2(0f, 0.75f),
            sourceConfig != null ? sourceConfig.attackUpSize : new Vector2(0.75f, 1f),
            sourceConfig);

        CreateOrUpdateDefaultModule(
            "SlashDown",
            HeroAttackDirection.Down,
            false,
            sourceConfig != null ? sourceConfig.attackDownOffset : new Vector2(0f, -0.75f),
            sourceConfig != null ? sourceConfig.attackDownSize : new Vector2(0.75f, 1f),
            sourceConfig);

        CacheAttackModules();
        EditorUtility.SetDirty(this);
#endif
    }

#if UNITY_EDITOR
    private void CreateOrUpdateDefaultModule(
        string moduleName,
        HeroAttackDirection direction,
        bool mirrorWithFacing,
        Vector2 localOffset,
        Vector2 size,
        HeroConfig sourceConfig)
    {
        Transform moduleTransform = attackRoot.Find(moduleName);
        GameObject moduleObject;
        if (moduleTransform == null)
        {
            moduleObject = new GameObject(moduleName);
            Undo.RegisterCreatedObjectUndo(moduleObject, $"Create {moduleName}");
            Undo.SetTransformParent(moduleObject.transform, attackRoot, $"Parent {moduleName}");
        }
        else
        {
            moduleObject = moduleTransform.gameObject;
        }

        moduleObject.transform.localPosition = localOffset;
        moduleObject.transform.localRotation = Quaternion.identity;
        moduleObject.transform.localScale = Vector3.one;

        PolygonCollider2D polygon = moduleObject.GetComponent<PolygonCollider2D>();
        if (polygon == null)
        {
            polygon = Undo.AddComponent<PolygonCollider2D>(moduleObject);
        }

        polygon.isTrigger = true;
        polygon.enabled = false;
        polygon.pathCount = 1;
        polygon.SetPath(0, CreateBoxPath(size));

        HeroAttackModule module = moduleObject.GetComponent<HeroAttackModule>();
        if (module == null)
        {
            module = Undo.AddComponent<HeroAttackModule>(moduleObject);
        }

        module.direction = direction;
        module.mirrorWithFacing = mirrorWithFacing;
        module.damageCollider = polygon;
        module.damageLayers = sourceConfig != null ? sourceConfig.attackHitLayers : default(LayerMask);

        EditorUtility.SetDirty(moduleObject);
        EditorUtility.SetDirty(module);
    }

    private static Vector2[] CreateBoxPath(Vector2 size)
    {
        Vector2 halfSize = size * 0.5f;
        return new[]
        {
            new Vector2(-halfSize.x, -halfSize.y),
            new Vector2(-halfSize.x, halfSize.y),
            new Vector2(halfSize.x, halfSize.y),
            new Vector2(halfSize.x, -halfSize.y)
        };
    }
#endif

    private void Reset()
    {
        CacheAttackModules();
    }

    private void OnValidate()
    {
        if (attackModules == null || attackModules.Length == 0)
        {
            CacheAttackModules();
        }
    }

    private void OnDrawGizmosSelected()
    {
        attack?.DrawGizmosSelected();
    }
}
