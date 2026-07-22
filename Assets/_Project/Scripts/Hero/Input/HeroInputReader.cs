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
        JumpPressedThisFrame = ReadPressedThisFrame(GetAction(jumpAction, "Jump"));
        if (ReadReleasedThisFrame(GetAction(jumpAction, "Jump")))
        {
            jumpReleaseQueued = true;
        }

        JumpHeld = ReadHeld(GetAction(jumpAction, "Jump"));
        AttackPressedThisFrame = ReadPressedThisFrame(GetAction(attackAction, "Attack"), false) || ReadAttackFallbackPressed();
        AttackHeld = ReadHeld(GetAction(attackAction, "Attack"), false) || ReadAttackFallbackHeld();
        DashPressedThisFrame = ReadPressedThisFrame(GetAction(dashAction, "Dash"), false) || ReadDashFallbackPressed();
        SprintHeld = ReadHeld(GetAction(sprintAction, "Sprint"), false) || ReadSprintFallback();
        InteractPressedThisFrame = ReadPressedThisFrame(GetAction(interactAction, "Interact"), false);
        BindPressedThisFrame = ReadPressedThisFrame(GetAction(bindAction, "Bind"), false);
        BindHeld = ReadHeld(GetAction(bindAction, "Bind"), false);
        BindReleasedThisFrame = ReadReleasedThisFrame(GetAction(bindAction, "Bind"), false);
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

    private Vector2 ReadMove()
    {
        InputAction action = GetAction(moveAction, "Move");
        if (action != null)
        {
            return action.ReadValue<Vector2>();
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
            return action.IsPressed();
        }

        Keyboard keyboard = Keyboard.current;
        return useJumpFallback && keyboard != null && keyboard.spaceKey.isPressed;
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
