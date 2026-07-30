using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[DisallowMultipleComponent]
public sealed class HeroInputReader : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference jumpAction;
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private InputActionReference dashAction;
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
    private bool interactDisarmed;
    private bool bindDisarmed;
    private int lastHorizontalKeyboardDirection;

    public Vector2 MoveVector { get; private set; }
    public bool JumpPressedThisFrame { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool JumpReleasedThisFrame => jumpReleaseQueued;
    public bool AttackPressedThisFrame { get; private set; }
    public bool AttackHeld { get; private set; }
    public bool DashPressedThisFrame { get; private set; }
    public bool DashHeld { get; private set; }
    public bool DashReleasedThisFrame { get; private set; }
    public bool DashCommandArmed => !dashDisarmed;
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
        bool dashReleasedRaw = ReadReleasedThisFrame(GetAction(dashAction, "Dash"), false) || ReadDashFallbackReleased();
        if (dashDisarmed && !dashHeldRaw) dashDisarmed = false;
        DashPressedThisFrame = !dashDisarmed && dashPressedRaw;
        DashHeld = !dashDisarmed && dashHeldRaw;
        DashReleasedThisFrame = !dashDisarmed && dashReleasedRaw;

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
        DashHeld = false;
        DashReleasedThisFrame = false;
        InteractPressedThisFrame = false;
        BindPressedThisFrame = false;
        BindHeld = false;
        BindReleasedThisFrame = false;
        lastHorizontalKeyboardDirection = 0;
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
        interactDisarmed = ReadHeld(GetAction(interactAction, "Interact"), false);
        bindDisarmed = ReadHeld(GetAction(bindAction, "Bind"), false);

        suspended = false;
    }

    public void DisarmDashUntilRelease()
    {
        dashDisarmed = true;
        DashPressedThisFrame = false;
        DashHeld = false;
        DashReleasedThisFrame = false;
    }

    private Vector2 ReadMove()
    {
        InputAction action = GetAction(moveAction, "Move");
        Vector2 value = action != null ? action.ReadValue<Vector2>() : Vector2.zero;

        if (action != null)
        {
            // Dpad/2DVector composites intentionally collapse opposing digital parts to
            // neutral. Resolve that one ambiguity from the action's bound controls so custom
            // keyboard and D-pad rebindings receive the same newest-press behaviour.
            if (TryResolveOppositeDigitalHorizontal(action, out float resolvedHorizontal)
                && Mathf.Abs(value.x) <= Mathf.Epsilon)
            {
                value.x = resolvedHorizontal;
            }

            return value;
        }

        Vector2 keyboardValue = ReadKeyboardMove();

        if (Mathf.Abs(keyboardValue.x) > Mathf.Epsilon)
        {
            value.x = keyboardValue.x;
        }

        if (Mathf.Abs(value.y) <= Mathf.Epsilon && Mathf.Abs(keyboardValue.y) > Mathf.Epsilon)
        {
            value.y = keyboardValue.y;
        }

        return value;
    }

    private bool TryResolveOppositeDigitalHorizontal(InputAction action, out float resolvedHorizontal)
    {
        resolvedHorizontal = 0f;
        bool foundDigitalPart = false;
        bool leftHeld = false;
        bool rightHeld = false;
        int newestDirection = 0;

        foreach (InputControl control in action.controls)
        {
            if (control is DpadControl dpad)
            {
                foundDigitalPart = true;
                if (dpad.left.isPressed)
                {
                    leftHeld = true;
                    if (dpad.left.wasPressedThisFrame) newestDirection = -1;
                }

                if (dpad.right.isPressed)
                {
                    rightHeld = true;
                    if (dpad.right.wasPressedThisFrame) newestDirection = 1;
                }

                continue;
            }

            if (!(control is ButtonControl button))
            {
                // Analog sticks and axes remain entirely under the Input System's normal
                // composite/value resolution and never participate in keyboard press memory.
                continue;
            }

            int bindingIndex = action.GetBindingIndexForControl(control);
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count)
            {
                continue;
            }

            InputBinding binding = action.bindings[bindingIndex];
            if (!binding.isPartOfComposite)
            {
                continue;
            }

            int direction = string.Equals(binding.name, "left", StringComparison.OrdinalIgnoreCase)
                ? -1
                : string.Equals(binding.name, "right", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            if (direction == 0)
            {
                continue;
            }

            foundDigitalPart = true;
            if (button.isPressed)
            {
                if (direction < 0) leftHeld = true;
                else rightHeld = true;

                if (button.wasPressedThisFrame)
                {
                    newestDirection = direction;
                }
            }
        }

        if (!foundDigitalPart || (!leftHeld && !rightHeld))
        {
            lastHorizontalKeyboardDirection = 0;
            return false;
        }

        if (leftHeld && rightHeld)
        {
            if (newestDirection != 0)
            {
                lastHorizontalKeyboardDirection = newestDirection;
            }
            else if (lastHorizontalKeyboardDirection == 0)
            {
                // Both controls becoming actuated in one input update has no representable
                // press ordering; choose a stable direction until one control is released.
                lastHorizontalKeyboardDirection = 1;
            }

            resolvedHorizontal = lastHorizontalKeyboardDirection;
            return true;
        }

        lastHorizontalKeyboardDirection = leftHeld ? -1 : 1;
        resolvedHorizontal = lastHorizontalKeyboardDirection;
        return true;
    }

    private Vector2 ReadKeyboardMove()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            lastHorizontalKeyboardDirection = 0;
            return Vector2.zero;
        }

        bool leftHeld = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        bool rightHeld = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
        bool leftPressed = keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame;
        bool rightPressed = keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame;

        float horizontal = 0f;
        if (leftHeld != rightHeld)
        {
            horizontal = leftHeld ? -1f : 1f;
            lastHorizontalKeyboardDirection = horizontal > 0f ? 1 : -1;
        }
        else if (leftHeld)
        {
            // The digital D-pad composite collapses opposite keys to zero. Treat the newest
            // physical press as intent so a held-key reversal cannot masquerade as neutral.
            if (leftPressed != rightPressed)
            {
                lastHorizontalKeyboardDirection = leftPressed ? -1 : 1;
            }

            horizontal = lastHorizontalKeyboardDirection;
        }
        else
        {
            lastHorizontalKeyboardDirection = 0;
        }

        float vertical = 0f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            vertical += 1f;
        }

        return new Vector2(horizontal, vertical);
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

    private static bool ReadDashFallbackReleased()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null
            && (keyboard.leftCtrlKey.wasReleasedThisFrame
                || keyboard.rightCtrlKey.wasReleasedThisFrame
                || keyboard.xKey.wasReleasedThisFrame);
    }

    private void SetActionsEnabled(bool enabled)
    {
        SetActionEnabled(GetAction(moveAction, "Move"), enabled);
        SetActionEnabled(GetAction(jumpAction, "Jump"), enabled);
        SetActionEnabled(GetAction(attackAction, "Attack"), enabled);
        SetActionEnabled(GetAction(dashAction, "Dash"), enabled);
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
