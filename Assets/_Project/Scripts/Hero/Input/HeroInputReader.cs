using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class HeroInputReader : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private InputActionReference dashAction;
    [SerializeField] private InputActionReference sprintAction;
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference bindAction;

    private HeroConfig config;
    private float jumpBufferTimer;
    private float attackBufferTimer;
    private bool jumpReleaseQueued;

    // Package A1 gameplay-input suspension. Suspending freezes Tick() sampling entirely (so
    // buffers created at Time.timeScale == 0 cannot linger — see Docs/ImplementationPlan.md
    // "Known Technical Debt"). Resuming rearms fresh-press-only commands independently: a
    // command still physically held at the moment of resume stays disarmed until it is released,
    // then accepts a later fresh press. Continuous movement is exempt and always reflects live
    // input immediately.
    private bool suspended;
    private bool jumpDisarmed;
    private bool attackDisarmed;
    private bool dashDisarmed;
    private bool sprintDisarmed;
    private bool interactDisarmed;
    private bool bindDisarmed;

    public Vector2 MoveVector { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool JumpReleasedThisFrame => jumpReleaseQueued;
    public bool AttackPressedThisFrame { get; private set; }
    public bool AttackHeld { get; private set; }
    public bool DashPressedThisFrame { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool InteractPressedThisFrame { get; private set; }
    public bool BindPressedThisFrame { get; private set; }
    public bool BindHeld { get; private set; }
    public bool BindReleasedThisFrame { get; private set; }
    public bool HasBufferedJump => jumpBufferTimer > 0f;
    public bool HasBufferedAttack => attackBufferTimer > 0f;
    public bool IsSuspended => suspended;

    public void Initialize(HeroConfig heroConfig)
    {
        config = heroConfig;

#if UNITY_EDITOR
        if (inputActions == null)
        {
            inputActions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Project/Input/InputSystem_Actions.inputactions");
        }
#endif

        if (isActiveAndEnabled)
        {
            SetActionsEnabled(true);
        }
    }

    private void OnEnable()
    {
        SetActionsEnabled(true);
    }

    private void OnDisable()
    {
        SetActionsEnabled(false);
    }

    public void Tick()
    {
        if (suspended)
        {
            // Fully frozen while a pausing UI root owns input. ClearTransientInput() already
            // zeroed every output; do not resample or let buffers decay/accumulate here — this
            // is what prevents a buffer created at Time.timeScale == 0 from lingering forever.
            return;
        }

        bool jumpPressedRaw = ReadPressedThisFrame(GetAction(jumpAction, "Jump"));
        bool jumpReleasedRaw = ReadReleasedThisFrame(GetAction(jumpAction, "Jump"));
        bool jumpHeldRaw = ReadHeld(GetAction(jumpAction, "Jump"));
        if (jumpDisarmed && !jumpHeldRaw) jumpDisarmed = false;
        JumpPressedThisFrame = !jumpDisarmed && jumpPressedRaw;
        if (jumpReleasedRaw) jumpReleaseQueued = true;
        JumpHeld = jumpHeldRaw;

        bool attackPressedRaw = ReadPressedThisFrame(GetAction(attackAction, "Attack"), false) || ReadAttackFallbackPressed();
        bool attackHeldRaw = ReadHeld(GetAction(attackAction, "Attack"), false) || ReadAttackFallbackHeld();
        if (attackDisarmed && !attackHeldRaw) attackDisarmed = false;
        AttackPressedThisFrame = !attackDisarmed && attackPressedRaw;
        AttackHeld = attackHeldRaw;

        bool dashPressedRaw = ReadPressedThisFrame(GetAction(dashAction, "Dash"), false) || ReadDashFallbackPressed();
        bool dashHeldRaw = ReadHeld(GetAction(dashAction, "Dash"), false) || ReadDashFallbackHeld();
        if (dashDisarmed && !dashHeldRaw) dashDisarmed = false;
        DashPressedThisFrame = !dashDisarmed && dashPressedRaw;

        bool sprintHeldRaw = ReadHeld(GetAction(sprintAction, "Sprint"), false) || ReadSprintFallback();
        if (sprintDisarmed && !sprintHeldRaw) sprintDisarmed = false;
        // Sprint has no discrete "start" signal — SprintHeld is its only gameplay entry point —
        // so unlike Jump/Attack/Dash it must itself be suppressed while disarmed.
        SprintHeld = !sprintDisarmed && sprintHeldRaw;

        bool interactPressedRaw = ReadPressedThisFrame(GetAction(interactAction, "Interact"), false);
        bool interactHeldRaw = ReadHeld(GetAction(interactAction, "Interact"), false);
        if (interactDisarmed && !interactHeldRaw) interactDisarmed = false;
        InteractPressedThisFrame = !interactDisarmed && interactPressedRaw;

        bool bindPressedRaw = ReadPressedThisFrame(GetAction(bindAction, "Bind"), false);
        bool bindHeldRaw = ReadHeld(GetAction(bindAction, "Bind"), false);
        if (bindDisarmed && !bindHeldRaw) bindDisarmed = false;
        BindPressedThisFrame = !bindDisarmed && bindPressedRaw;
        BindHeld = bindHeldRaw;
        BindReleasedThisFrame = ReadReleasedThisFrame(GetAction(bindAction, "Bind"), false);

        // Continuous movement is exempt from disarm/rearm gating — it may resume immediately
        // without requiring the stick/keys to pass through neutral.
        MoveVector = ReadMove();

        if (JumpPressedThisFrame)
        {
            jumpBufferTimer = config != null ? config.jumpBufferTime : 0.1f;
        }
        else if (jumpBufferTimer > 0f)
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        if (AttackPressedThisFrame)
        {
            attackBufferTimer = config != null ? config.attackBufferTime : 0.1f;
        }
        else if (attackBufferTimer > 0f)
        {
            attackBufferTimer -= Time.deltaTime;
        }
    }

    public void ConsumeJumpBuffer()
    {
        jumpBufferTimer = 0f;
        JumpPressedThisFrame = false;
    }

    public void ConsumeAttackBuffer()
    {
        attackBufferTimer = 0f;
        AttackPressedThisFrame = false;
    }

    public void ConsumeJumpRelease()
    {
        jumpReleaseQueued = false;
    }

    /// <summary>
    /// Disables Player-map sampling and clears every transient snapshot/buffer. Called only by
    /// the persistent UI-flow coordinator through the thin <see cref="HeroController"/> facade —
    /// never directly by gameplay code.
    /// </summary>
    public void SuspendGameplayInput()
    {
        if (suspended) return;
        suspended = true;
        SetActionsEnabled(false);
        ClearTransientInput();
    }

    /// <summary>
    /// Zeroes every buffered/transient/pressed-this-frame signal. Safe to call independently of
    /// suspension (e.g. during teardown) — does not touch the suspended flag or action enablement.
    /// </summary>
    public void ClearTransientInput()
    {
        jumpBufferTimer = 0f;
        attackBufferTimer = 0f;
        jumpReleaseQueued = false;
        MoveVector = Vector2.zero;
        JumpPressedThisFrame = false;
        JumpHeld = false;
        AttackPressedThisFrame = false;
        AttackHeld = false;
        DashPressedThisFrame = false;
        SprintHeld = false;
        InteractPressedThisFrame = false;
        BindPressedThisFrame = false;
        BindHeld = false;
        BindReleasedThisFrame = false;
    }

    /// <summary>
    /// Re-enables Player-map sampling and snapshots which one-shot commands are still physically
    /// held right now — those remain disarmed (ignored) until released, then accept a later fresh
    /// press. Continuous movement is exempt and resumes live on the next <see cref="Tick"/>.
    /// </summary>
    public void BeginResumeGameplayInput()
    {
        SetActionsEnabled(true);

        jumpDisarmed = ReadHeld(GetAction(jumpAction, "Jump"));
        attackDisarmed = ReadHeld(GetAction(attackAction, "Attack"), false) || ReadAttackFallbackHeld();
        dashDisarmed = ReadHeld(GetAction(dashAction, "Dash"), false) || ReadDashFallbackHeld();
        sprintDisarmed = ReadHeld(GetAction(sprintAction, "Sprint"), false) || ReadSprintFallback();
        interactDisarmed = ReadHeld(GetAction(interactAction, "Interact"), false);
        bindDisarmed = ReadHeld(GetAction(bindAction, "Bind"), false);

        suspended = false;
    }

    private Vector2 ReadMove()
    {
        InputAction action = GetAction(moveAction, "Move");
        if (action != null)
        {
            Vector2 value = action.ReadValue<Vector2>();
            if (value != Vector2.zero)
            {
                return value;
            }

            // A composite Value action's cached ReadValue() can still read zero for one frame
            // immediately after Enable() even while its bound keys remain physically held (same
            // resync gap as IsPressed() above) — fall back to the raw keyboard read so continuous
            // movement genuinely resumes live on the very tick input suspension ends, with no
            // neutral-release requirement. Harmless when input is genuinely absent (both are zero).
        }

        Vector2 fallback = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return fallback;
        }

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            fallback.x -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            fallback.x += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            fallback.y -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            fallback.y += 1f;
        }

        return fallback;
    }

    private static bool ReadPressedThisFrame(InputAction action)
    {
        return ReadPressedThisFrame(action, true);
    }

    private static bool ReadPressedThisFrame(InputAction action, bool useJumpFallback)
    {
        if (action != null)
        {
            return action.WasPressedThisFrame();
        }

        Keyboard keyboard = Keyboard.current;
        return useJumpFallback && keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    private static bool ReadReleasedThisFrame(InputAction action)
    {
        return ReadReleasedThisFrame(action, true);
    }

    private static bool ReadReleasedThisFrame(InputAction action, bool useJumpFallback)
    {
        if (action != null)
        {
            return action.WasReleasedThisFrame();
        }

        Keyboard keyboard = Keyboard.current;
        return useJumpFallback && keyboard != null && keyboard.spaceKey.wasReleasedThisFrame;
    }

    private static bool ReadHeld(InputAction action)
    {
        return ReadHeld(action, true);
    }

    private static bool ReadHeld(InputAction action, bool useJumpFallback)
    {
        if (action != null)
        {
            return IsPhysicallyActuated(action);
        }

        Keyboard keyboard = Keyboard.current;
        return useJumpFallback && keyboard != null && keyboard.spaceKey.isPressed;
    }

    /// <summary>
    /// Reads an action's bound controls' raw actuation directly, bypassing
    /// <see cref="InputAction.IsPressed"/>'s own phase tracking. Disabling then re-enabling an
    /// action while its control is still physically held — exactly what suspend/resume does —
    /// does not resynchronize <c>IsPressed()</c> within the same frame; it only reports true again
    /// after the Input System processes another update, one frame too late for held-through-resume
    /// disarm gating (and the stale read is exactly what let physically-held commands like Space,
    /// Enter, E, and Gamepad East leak a synthetic fresh press through immediately on resume).
    /// </summary>
    private static bool IsPhysicallyActuated(InputAction action)
    {
        foreach (InputControl control in action.controls)
        {
            if (control.IsActuated())
            {
                return true;
            }
        }

        return false;
    }

    private static bool ReadSprintFallback()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    private static bool ReadAttackFallbackPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        return (keyboard != null && keyboard.jKey.wasPressedThisFrame)
            || (mouse != null && mouse.leftButton.wasPressedThisFrame);
    }

    private static bool ReadAttackFallbackHeld()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        return (keyboard != null && keyboard.jKey.isPressed)
            || (mouse != null && mouse.leftButton.isPressed);
    }

    private static bool ReadDashFallbackPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.leftCtrlKey.wasPressedThisFrame
                || keyboard.rightCtrlKey.wasPressedThisFrame
                || keyboard.xKey.wasPressedThisFrame);
    }

    private static bool ReadDashFallbackHeld()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.leftCtrlKey.isPressed
                || keyboard.rightCtrlKey.isPressed
                || keyboard.xKey.isPressed);
    }

    private void SetActionsEnabled(bool enabled)
    {
        SetActionEnabled(GetAction(moveAction, "Move"), enabled);
        SetActionEnabled(GetAction(jumpAction, "Jump"), enabled);
        SetActionEnabled(GetAction(attackAction, "Attack"), enabled);
        SetActionEnabled(GetAction(dashAction, "Dash"), enabled);
        SetActionEnabled(GetAction(sprintAction, "Sprint"), enabled);
        SetActionEnabled(GetAction(interactAction, "Interact"), enabled);
        SetActionEnabled(GetAction(bindAction, "Bind"), enabled);
    }

    private InputAction GetAction(InputActionReference reference, string actionName)
    {
        if (reference != null && reference.action != null)
        {
            return reference.action;
        }

        return inputActions != null ? inputActions.FindActionMap("Player", false)?.FindAction(actionName, false) : null;
    }

    private static void SetActionEnabled(InputAction action, bool enabled)
    {
        if (action == null)
        {
            return;
        }

        if (enabled)
        {
            action.Enable();
        }
        else
        {
            action.Disable();
        }
    }
}
