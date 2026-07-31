using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class HeroActionController : MonoBehaviour
{
    private static readonly Vector2 DefaultSideAttackOffset = new Vector2(0.75f, 0f);
    private static readonly Vector2 DefaultSideAttackSize = new Vector2(1.2f, 0.5f);
    private static readonly Vector2 DefaultUpAttackOffset = new Vector2(0f, 0.75f);
    private static readonly Vector2 DefaultUpAttackSize = new Vector2(0.75f, 1f);
    private static readonly Vector2 DefaultDownAttackOffset = new Vector2(0f, -0.75f);
    private static readonly Vector2 DefaultDownAttackSize = new Vector2(0.75f, 1f);

    [Header("Attack Modules")]
    [SerializeField] private Transform attackRoot;
    [SerializeField] private HeroAttackModule[] attackModules;

    [Header("Attack Safety")]
    [SerializeField, Min(0.1f)] private float attackFailSafeTimeout = 1f;

    private HeroConfig config;
    private HeroAbilityConfig abilityConfig;
    private HeroStateBlackboard blackboard;
    private HeroInputReader input;
    private HeroMotor motor;
    private HeroSensors sensors;
    private HeroAudioController heroAudio;
    private PlayerAbilityState abilityState;
    private PlayerResourceConfig resourceConfig;
    private PlayerHealthState healthState;
    private HeroAnimationController animations;

    private HeroJumpAction jump;
    private HeroDashAction dash;
    private HeroAttackAction attack;
    private HeroWallSlideAction wallSlide;
    private HeroWallJumpAction wallJump;
    private HeroBindAction bind;
    private HeroLedgeClimbAction ledgeClimb;
    private HeroSprintAction sprint;

    public int AttackVersion => attack != null ? attack.AttackVersion : 0;
    public bool IsSprinting => sprint != null && sprint.IsSprinting;
    public bool IsSprintJumpCarrying => sprint != null && sprint.IsJumpCarrying;

    public void Initialize(
        HeroConfig heroConfig,
        HeroAbilityConfig heroAbilityConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        HeroSensors heroSensors,
        HeroAudioController heroAudio,
        PlayerAbilityState abilityState,
        PlayerResourceState resourceState,
        PlayerHealthState healthState,
        PlayerResourceConfig resourceConfig)
    {
        config = heroConfig;
        abilityConfig = heroAbilityConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        sensors = heroSensors;
        this.heroAudio = heroAudio;
        this.abilityState = abilityState;
        this.healthState = healthState;
        this.resourceConfig = resourceConfig;

        dash = new HeroDashAction(config, abilityConfig, blackboard, input, heroMotor, this.heroAudio, abilityState);
        sprint = new HeroSprintAction(
            config,
            abilityConfig,
            blackboard,
            input,
            heroMotor,
            abilityState,
            dash);
        jump = new HeroJumpAction(
            config,
            abilityConfig,
            blackboard,
            input,
            heroMotor,
            this.heroAudio,
            abilityState,
            sprint.TryBeginJumpCarry,
            sprint.NotifyDoubleJump);
        attack = new HeroAttackAction(config, blackboard, input, heroMotor, this.heroAudio, gameObject, transform, ResolveAttackModules(), attackFailSafeTimeout, resourceState);
        wallSlide = new HeroWallSlideAction(config, abilityConfig, blackboard, input, heroMotor, this.heroAudio, abilityState);
        wallJump = new HeroWallJumpAction(config, abilityConfig, blackboard, input, heroMotor, this.heroAudio, abilityState);
        bind = new HeroBindAction(
            resourceConfig,
            blackboard,
            input,
            heroMotor,
            healthState,
            resourceState,
            abilityState,
            () => animations != null && animations.CanPlayBindAnimation,
            () => animations?.StopBindAnimation());
        ledgeClimb = new HeroLedgeClimbAction(
            config,
            blackboard,
            input,
            motor,
            sensors,
            dash,
            () => animations?.StopLedgeClimbAnimation(),
            () => sprint?.NotifyLedgeClimbStarted(),
            (completed, reason) => sprint?.NotifyLedgeClimbEnded(completed, reason));
    }

    public void SetAnimationController(HeroAnimationController animationController)
    {
        animations = animationController;
    }

    public void Tick()
    {
        if (config == null || blackboard == null || input == null || motor == null)
        {
            return;
        }

        if (ledgeClimb != null && ledgeClimb.IsActive)
        {
            sprint?.Tick();
            dash?.TickCooldown(Time.deltaTime);
            ApplyLocomotionIntent();
            return;
        }

        bind?.Tick(Time.deltaTime);
        if (bind != null && bind.IsBinding)
        {
            sprint?.Cancel(HeroSprintCancelReason.Bind, true);
            input.ConsumeAttackBuffer();
            input.ConsumeJumpBuffer();
            ApplyLocomotionIntent();
            return;
        }

        dash.Tick(Time.deltaTime);
        attack.Tick(Time.deltaTime);
        sprint.Tick();
        ApplyLocomotionIntent();
    }

    public void FixedTick(float fixedDeltaTime)
    {
        if (config == null || blackboard == null || input == null || motor == null)
        {
            return;
        }

        bind?.FixedTick();
        if (bind != null && bind.IsBinding)
        {
            return;
        }

        float effectiveMoveX = ResolveEffectiveMoveInput();
        if (ledgeClimb != null && ledgeClimb.FixedTick(fixedDeltaTime, effectiveMoveX))
        {
            if (!ledgeClimb.IsActive)
            {
                sprint?.Tick();
                ApplyLocomotionIntent();
            }

            return;
        }

        wallSlide.FixedTick(
            ledgeClimb != null && ledgeClimb.IsPreCatchReservingWallSlide,
            effectiveMoveX);
        wallJump.FixedTick(fixedDeltaTime);
        sprint.FixedTick(fixedDeltaTime);
        jump.FixedTick(fixedDeltaTime);
        dash.FixedTick(fixedDeltaTime);
        attack.FixedTick(fixedDeltaTime);
        sprint.Tick();
        // Landing and natural Dash completion happen in FixedUpdate. Re-issuing locomotion here
        // lets HeroMotor observe a resumed/entered Wildstride request in this same simulation step.
        ApplyLocomotionIntent();
    }

    public void CancelAttack()
    {
        attack?.CancelAttack();
    }

    public void CancelBind()
    {
        bind?.Cancel();
    }

    public void CancelLedgeClimb()
    {
        ledgeClimb?.Cancel();
    }

    public void CancelActions(HeroActionCancelReason reason)
    {
        dash?.Cancel(MapDashReason(reason));
        attack?.CancelAttack();
        bind?.Cancel();
        ledgeClimb?.Cancel(reason == HeroActionCancelReason.ComponentDisabled
            ? HeroLedgeClimbCancelReason.ComponentDisabled
            : HeroLedgeClimbCancelReason.ExternalState);
        sprint?.Cancel(MapSprintReason(reason), true);
    }

    public void CompleteLedgeClimbFromAnimation()
    {
        ledgeClimb?.CompleteFromAnimation();
    }

    public void CompleteBindFromAnimation()
    {
        bind?.CompleteFromAnimation();
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
        float moveX = ResolveEffectiveMoveInput();

        HeroLocomotionSpeed speedMode = sprint != null
            ? sprint.RequestedSpeed
            : HeroLocomotionSpeed.Walk;
        motor.SetDesiredMove(moveX, speedMode);

        if (Mathf.Abs(moveX) > config.horizontalInputDeadZone
            && !blackboard.controlLocked
            && (sprint == null || (!sprint.IsSprinting && !sprint.IsJumpCarrying)))
        {
            motor.SetFacingDirection(moveX > 0f ? 1 : -1);
        }
    }

    private float ResolveEffectiveMoveInput()
    {
        float moveX = blackboard.controlLocked
            || blackboard.inputBlocked
            || blackboard.dashing
            || blackboard.ledgeClimbing
            || blackboard.binding
            ? 0f
            : input.MoveVector.x;
        if (sprint != null)
        {
            moveX = sprint.ResolveGroundedMoveInput(moveX);
        }

        return moveX;
    }

    private void OnDisable()
    {
        CancelActions(HeroActionCancelReason.ComponentDisabled);
    }

    private static HeroDashEndReason MapDashReason(HeroActionCancelReason reason)
    {
        switch (reason)
        {
            case HeroActionCancelReason.ControlLock: return HeroDashEndReason.ControlLock;
            case HeroActionCancelReason.InputSuspension: return HeroDashEndReason.InputSuspension;
            case HeroActionCancelReason.Hurt: return HeroDashEndReason.Hurt;
            case HeroActionCancelReason.Death: return HeroDashEndReason.Death;
            case HeroActionCancelReason.SceneEntry: return HeroDashEndReason.SceneEntry;
            case HeroActionCancelReason.Respawn: return HeroDashEndReason.Respawn;
            default: return HeroDashEndReason.ComponentDisabled;
        }
    }

    private static HeroSprintCancelReason MapSprintReason(HeroActionCancelReason reason)
    {
        switch (reason)
        {
            case HeroActionCancelReason.ControlLock: return HeroSprintCancelReason.ControlLock;
            case HeroActionCancelReason.InputSuspension: return HeroSprintCancelReason.InputSuspended;
            case HeroActionCancelReason.Hurt: return HeroSprintCancelReason.Hurt;
            case HeroActionCancelReason.Death: return HeroSprintCancelReason.Death;
            case HeroActionCancelReason.SceneEntry: return HeroSprintCancelReason.SceneEntry;
            case HeroActionCancelReason.Respawn: return HeroSprintCancelReason.Respawn;
            default: return HeroSprintCancelReason.ComponentDisabled;
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
            DefaultSideAttackOffset,
            DefaultSideAttackSize,
            sourceConfig);

        CreateOrUpdateDefaultModule(
            "SlashUp",
            HeroAttackDirection.Up,
            false,
            DefaultUpAttackOffset,
            DefaultUpAttackSize,
            sourceConfig);

        CreateOrUpdateDefaultModule(
            "SlashDown",
            HeroAttackDirection.Down,
            false,
            DefaultDownAttackOffset,
            DefaultDownAttackSize,
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
