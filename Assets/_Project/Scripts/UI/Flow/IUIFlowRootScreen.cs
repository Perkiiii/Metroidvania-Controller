using System;
using UnityEngine.UI;

/// <summary>
/// Minimal shared contract a pausing root screen exposes to <see cref="UIFlowController"/>.
/// Deliberately narrow — Package A1 does not build a generic window/screen framework. Tests may
/// implement this directly with a lightweight fake root to verify the shared open/close flow
/// without a full UGUI hierarchy.
/// </summary>
public interface IUIFlowRootScreen
{
    /// <summary>Fired when the screen itself requests a full close (e.g. Continue clicked).</summary>
    event Action CloseRequested;

    void Show();
    void Hide();
    Selectable FirstSelection { get; }

    /// <summary>
    /// Back/Cancel input while this root is the active, stably-open root and no modal is open.
    /// Returning true means the screen handled it internally (e.g. closed its own child) and the
    /// flow controller should NOT proceed to close the whole root; false means the flow
    /// controller should run the normal root-close sequence.
    /// </summary>
    bool HandleBackInternally();

    /// <summary>True while this screen owns an open modal (blocks root-level Back/Pause-toggle).</summary>
    bool HasOpenModal { get; }
}
