using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

/// <summary>
/// Package A1 input-leakage coverage: HeroInputReader suspension, buffer clearing, and
/// per-command fresh-press rearming. Runs in Play Mode because Unity's Input System only
/// re-evaluates InputAction phase/press-edge state during the player loop's own dynamic
/// update — simulated device state applies immediately in Edit Mode, but bound InputActions
/// (WasPressedThisFrame/IsPressed) do not advance without an actual running player loop.
///
/// This assembly (Underbrew.UI.PlayModeTests) cannot reference Assembly-CSharp directly (a
/// predefined assembly cannot be a dependency of a custom one — see
/// Underbrew.Camera.PlayModeTests/CameraLifecyclePlayModeTests for the same established
/// project convention), so HeroInputReader/HeroConfig are accessed via reflection.
/// </summary>
public sealed class HeroInputSuspensionPlayModeTests
{
    private static Assembly gameAssembly;

    private Keyboard keyboard;
    private Gamepad gamepad;
    private Mouse mouse;
    private ScriptableObject config;
    private GameObject readerObject;
    private Component reader;

    private static Type GameType(string name)
    {
        if (gameAssembly == null) gameAssembly = Assembly.Load("Assembly-CSharp");
        Type t = gameAssembly.GetType(name);
        Assert.That(t, Is.Not.Null, "Type not found in Assembly-CSharp: " + name);
        return t;
    }

    private static void Invoke(object target, string method)
    {
        MethodInfo mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(mi, Is.Not.Null, "Method not found: " + method);
        mi.Invoke(target, null);
    }

    private static object GetProp(object target, string name)
    {
        PropertyInfo pi = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(pi, Is.Not.Null, "Property not found: " + name);
        return pi.GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo fi = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(fi, Is.Not.Null, "Field not found: " + name);
        fi.SetValue(target, value);
    }

    private bool B(string name) => (bool)GetProp(reader, name);
    private Vector2 MoveVector => (Vector2)GetProp(reader, "MoveVector");

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        gamepad = InputSystem.AddDevice<Gamepad>();
        mouse = InputSystem.AddDevice<Mouse>();

        Type configType = GameType("HeroConfig");
        config = ScriptableObject.CreateInstance(configType);
        SetField(config, "jumpBufferTime", 0.1f);
        SetField(config, "attackBufferTime", 0.1f);

        readerObject = new GameObject("HeroInputReader PlayMode Suspension Test");
        reader = readerObject.AddComponent(GameType("HeroInputReader"));
        reader.GetType().GetMethod("Initialize").Invoke(reader, new object[] { config });
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        UnityEngine.Object.Destroy(readerObject);
        UnityEngine.Object.Destroy(config);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (gamepad != null) InputSystem.RemoveDevice(gamepad);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        yield return null;
    }

    private static IEnumerator PressKey(Keyboard kb, Key key)
    {
        InputSystem.QueueStateEvent(kb, new KeyboardState(key));
        yield return null;
    }

    private static IEnumerator PressKeys(Keyboard kb, Key a, Key b)
    {
        InputSystem.QueueStateEvent(kb, new KeyboardState(a, b));
        yield return null;
    }

    private static IEnumerator ReleaseAllKeys(Keyboard kb)
    {
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        yield return null;
    }

    private static IEnumerator PressMouseLeft(Mouse m)
    {
        InputSystem.QueueStateEvent(m, new MouseState().WithButton(MouseButton.Left));
        yield return null;
    }

    private static IEnumerator ReleaseMouse(Mouse m)
    {
        InputSystem.QueueStateEvent(m, new MouseState());
        yield return null;
    }

    private static IEnumerator PressGamepadEast(Gamepad gp)
    {
        GamepadState state = new GamepadState();
        state.buttons |= 1 << (int)GamepadButton.East;
        InputSystem.QueueStateEvent(gp, state);
        yield return null;
    }

    private static IEnumerator ReleaseGamepad(Gamepad gp)
    {
        InputSystem.QueueStateEvent(gp, new GamepadState());
        yield return null;
    }

    [UnityTest]
    public IEnumerator SuspendClearsBuffersAndFreezesSamplingEvenWhileHeld()
    {
        yield return PressKey(keyboard, Key.Space);
        Invoke(reader, "Tick");
        Assert.That(B("HasBufferedJump"), Is.True, "Precondition: jump buffer should be set by the press.");

        Invoke(reader, "SuspendGameplayInput");
        Assert.That(B("HasBufferedJump"), Is.False, "Suspend must clear the existing buffer immediately.");
        Assert.That(B("IsSuspended"), Is.True);

        Invoke(reader, "Tick");
        Assert.That(B("JumpPressedThisFrame"), Is.False);
        Assert.That(B("HasBufferedJump"), Is.False);
        Assert.That(MoveVector, Is.EqualTo(Vector2.zero));

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator JumpHeldThroughResumeIsDisarmedUntilReleasedThenFreshPressWorks()
    {
        yield return PressKey(keyboard, Key.Space);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "Tick");

        Invoke(reader, "BeginResumeGameplayInput");
        Assert.That(B("IsSuspended"), Is.False);

        Invoke(reader, "Tick");
        Assert.That(B("JumpPressedThisFrame"), Is.False, "Held-through-resume must not synthesize a fresh press.");

        yield return ReleaseAllKeys(keyboard);
        Invoke(reader, "Tick");
        Assert.That(B("JumpPressedThisFrame"), Is.False);

        yield return PressKey(keyboard, Key.Space);
        Invoke(reader, "Tick");
        Assert.That(B("JumpPressedThisFrame"), Is.True);

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator AttackHeldThroughResumeIsDisarmedUntilReleasedThenFreshPressWorks()
    {
        yield return PressKey(keyboard, Key.Enter);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.False);

        yield return ReleaseAllKeys(keyboard);
        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.False);

        yield return PressKey(keyboard, Key.Enter);
        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.True);

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator DashHeldThroughResumeIsDisarmedUntilReleasedThenFreshPressWorks()
    {
        yield return PressKey(keyboard, Key.LeftCtrl);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.False);

        yield return ReleaseAllKeys(keyboard);
        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.False);

        yield return PressKey(keyboard, Key.LeftCtrl);
        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.True);

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator DashHoldSignalHeldThroughResumeStaysDisarmedUntilReleaseThenFreshHoldWorks()
    {
        yield return PressKey(keyboard, Key.X);
        Invoke(reader, "Tick");
        Assert.That(B("DashHeld"), Is.True, "Precondition: shared Dash/Wildstride command reads held before suspension.");

        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("DashHeld"), Is.False, "Held Dash/Wildstride must remain disarmed while its control is still held.");

        yield return ReleaseAllKeys(keyboard);
        Invoke(reader, "Tick");
        Assert.That(B("DashHeld"), Is.False);

        yield return PressKey(keyboard, Key.X);
        Invoke(reader, "Tick");
        Assert.That(B("DashHeld"), Is.True, "A fresh hold after release must work normally.");

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator GamepadEastOverlapWithBindDoesNotLeakIntoGameplayOnClose()
    {
        // Reproduces the documented UI Cancel / Gamepad East vs Bind/Crouch overlap risk: closing
        // a root with East must never let the same press become a gameplay Bind action.
        yield return PressGamepadEast(gamepad);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("BindPressedThisFrame"), Is.False);

        yield return ReleaseGamepad(gamepad);
        Invoke(reader, "Tick");
        Assert.That(B("BindPressedThisFrame"), Is.False);

        yield return PressGamepadEast(gamepad);
        Invoke(reader, "Tick");
        Assert.That(B("BindPressedThisFrame"), Is.True);

        yield return ReleaseGamepad(gamepad);
    }

    [UnityTest]
    public IEnumerator ReleasingOneCommandDoesNotAffectAnotherStillHeldCommand()
    {
        yield return PressKeys(keyboard, Key.Space, Key.Enter);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");
        Invoke(reader, "Tick");

        // Release only Enter (Attack); Space (Jump) remains held.
        yield return PressKey(keyboard, Key.Space);
        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.False);
        Assert.That(B("JumpPressedThisFrame"), Is.False, "Jump is still held, so it must remain disarmed.");

        // A fresh Attack press now works even though Jump is still held.
        yield return PressKeys(keyboard, Key.Space, Key.Enter);
        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.True, "Attack rearms independently of Jump remaining held.");
        Assert.That(B("JumpPressedThisFrame"), Is.False, "Jump must still be disarmed since it was never released.");

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator ContinuousMovementResumesImmediatelyWithoutNeutralRelease()
    {
        yield return PressKey(keyboard, Key.D);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Assert.That(MoveVector, Is.EqualTo(Vector2.zero));

        Invoke(reader, "BeginResumeGameplayInput");
        Invoke(reader, "Tick");

        Assert.That(MoveVector.x, Is.GreaterThan(0f), "Movement must resume live immediately, with no neutral-release requirement.");

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator EnablingPlayerMapAfterResumeDoesNotSynthesizeABufferedJump()
    {
        yield return PressKey(keyboard, Key.Space);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("HasBufferedJump"), Is.False, "Re-enabling the Player map must not itself create a buffer.");

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator RightControlDashFallbackHeldThroughResumeIsDisarmedUntilReleasedThenFreshPressWorks()
    {
        // Right Control is a direct-fallback-only physical control (not bound in the Dash Input
        // Action itself, unlike Left Control/X) — this is exactly the leak Finding #7 fixed:
        // dashDisarmed must consult the fallback held-state too, not only the action's own state.
        yield return PressKey(keyboard, Key.RightCtrl);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.False, "Right Control held through resume must not synthesize a fresh Dash.");

        yield return ReleaseAllKeys(keyboard);
        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.False, "Releasing Right Control alone must not itself trigger a Dash.");

        yield return PressKey(keyboard, Key.RightCtrl);
        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.True, "A later fresh Right Control press must Dash normally.");

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator XKeyDashHeldThroughResumeIsDisarmedWithNoDoubleTriggerFromActionAndFallback()
    {
        // X is covered by both the real Dash Input Action binding and the fallback path — confirm
        // the two paths compose via OR without the disarm/rearm cycle behaving any differently.
        yield return PressKey(keyboard, Key.X);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.False, "X held through resume must not synthesize a fresh Dash.");

        yield return ReleaseAllKeys(keyboard);
        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.False);

        yield return PressKey(keyboard, Key.X);
        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.True, "A later fresh X press must Dash exactly once, not double-trigger.");

        yield return ReleaseAllKeys(keyboard);
        Invoke(reader, "Tick");
        Assert.That(B("DashPressedThisFrame"), Is.False, "The press-edge must not remain latched into a following tick.");
    }

    [UnityTest]
    public IEnumerator InteractHeldThroughResumeIsDisarmedUntilReleasedThenFreshPressWorks()
    {
        yield return PressKey(keyboard, Key.E);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("InteractPressedThisFrame"), Is.False, "Interact held through resume must not synthesize a fresh press.");

        yield return ReleaseAllKeys(keyboard);
        Invoke(reader, "Tick");
        Assert.That(B("InteractPressedThisFrame"), Is.False);

        yield return PressKey(keyboard, Key.E);
        Invoke(reader, "Tick");
        Assert.That(B("InteractPressedThisFrame"), Is.True, "A later fresh Interact press must work normally.");

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator AttackJKeyFallbackHeldThroughResumeIsDisarmedUntilReleasedThenFreshPressWorks()
    {
        yield return PressKey(keyboard, Key.J);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.False, "J held through resume must not synthesize a fresh Attack.");

        yield return ReleaseAllKeys(keyboard);
        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.False);

        yield return PressKey(keyboard, Key.J);
        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.True, "A later fresh J press must Attack normally.");

        yield return ReleaseAllKeys(keyboard);
    }

    [UnityTest]
    public IEnumerator AttackMouseLeftFallbackHeldThroughResumeIsDisarmedUntilReleasedThenFreshPressWorks()
    {
        yield return PressMouseLeft(mouse);
        Invoke(reader, "Tick");
        Invoke(reader, "SuspendGameplayInput");
        Invoke(reader, "BeginResumeGameplayInput");

        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.False, "Mouse-left held through resume must not synthesize a fresh Attack.");

        yield return ReleaseMouse(mouse);
        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.False);

        yield return PressMouseLeft(mouse);
        Invoke(reader, "Tick");
        Assert.That(B("AttackPressedThisFrame"), Is.True, "A later fresh mouse-left press must Attack normally.");

        yield return ReleaseMouse(mouse);
    }
}
