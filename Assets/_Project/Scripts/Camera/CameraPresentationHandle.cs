using System;
using UnityEngine;

// Source-owned lease over one camera presentation request, mirroring the freeze-handle contract:
// releasing or disposing removes only this request, repeated release is safe, and one owner can
// never release another owner's request.
public readonly struct CameraPresentationHandle : IDisposable
{
    private readonly GameCameras owner;
    private readonly long requestId;

    internal CameraPresentationHandle(GameCameras owner, long requestId)
    {
        this.owner = owner;
        this.requestId = requestId;
    }

    public bool IsValid => owner != null && requestId != 0L;

    // True while this exact request is still registered (not necessarily the selected one).
    public bool IsRegistered => IsValid && owner.IsPresentationRegistered(requestId);

    // True while this exact request is the one currently driving the camera.
    public bool IsSelected => IsValid && owner.SelectedPresentationId == requestId;

    internal long Id => requestId;

    // Re-authors this request in place. Preferred over release/re-acquire for per-frame updates
    // (Timeline weight, moving focus points) because it never changes registration order.
    public bool Update(in CameraPresentationSettings settings)
    {
        return IsValid && owner.UpdatePresentation(requestId, settings, null, false, 0, false);
    }

    public bool Update(in CameraPresentationSettings settings, Transform[] targets)
    {
        return IsValid && owner.UpdatePresentation(requestId, settings, targets, true, 0, false);
    }

    public bool Update(in CameraPresentationSettings settings, Transform[] targets, int priority)
    {
        return IsValid && owner.UpdatePresentation(requestId, settings, targets, true, priority, true);
    }

    public void Release()
    {
        owner?.ReleasePresentation(requestId);
    }

    public void Dispose()
    {
        Release();
    }
}
