/// <summary>
/// Narrow seam for the two environment-dependent decisions <see cref="UIFlowController"/> cannot
/// answer on its own. Production leaves it unset, and the controller then behaves exactly as
/// Package A1 authored it: <see cref="GameManager"/> plus the post-transition lockout is the sole
/// root-availability authority, and UI navigation exists only while a pausing root is open.
///
/// It exists because the UI Sandbox deliberately has no <see cref="GameManager"/>, no scene Hero,
/// and no scene transitions, yet must still exercise the real open/close sequence rather than
/// bypassing it with direct <c>Show</c>/<c>Hide</c> calls. Injection is runtime-only — there is no
/// serialized field for it, so a Sandbox host can never leak into <c>_GameCameras.prefab</c>.
///
/// This is not a general policy framework. Adding a third member is a signal that the real
/// requirement belongs on <see cref="GameManager"/> or on the screen itself instead. See
/// Docs/FeatureSpecs/UIArchitecture.md.
/// </summary>
public interface IUIFlowHost
{
    /// <summary>
    /// Replaces only the gameplay-availability half of the open predicate: Playing state, scene
    /// transition, post-transition lockout, and respawn/recovery. The controller still enforces
    /// its own concerns on top (no root already open/opening/closing, health not depleted), so a
    /// host cannot force a second root open or open one over death.
    /// </summary>
    bool CanOpenPausingRoot { get; }

    /// <summary>
    /// True when this host owns UI navigation itself and the controller must not disable the UI
    /// action map on close. The UI Sandbox returns true because its developer and fixture panels
    /// need pointer/navigation input with no root open. Production returns false: outside a
    /// pausing root there is nothing to navigate.
    /// </summary>
    bool KeepUiNavigationEnabledWhileClosed { get; }
}
