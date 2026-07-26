using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// Scene-side adapter that owns the camera presentation handles a Timeline track drives.
//
// Timeline never touches the gameplay camera transform, the controller, or GameCameras directly:
// the mixer only pushes high-level presentation data here, and this component owns exactly one
// handle per driving track. Disable, destroy, director stop, graph destruction, and scene unload
// all release through the same path, so scrubbing or re-evaluating a graph can never leak
// duplicate requests.
[DisallowMultipleComponent]
public sealed class CameraPresentationReceiver : MonoBehaviour
{
    [Tooltip("Lifetime of the requests this receiver acquires. Scene is correct for authored room content.")]
    [SerializeField] private CameraRequestLifetime lifetime = CameraRequestLifetime.Scene;

    [Tooltip("Added to each clip's authored priority, so one receiver can outrank another without re-authoring clips.")]
    [SerializeField] private int basePriority;

    [Tooltip("The director driving this receiver. Auto-resolved from this GameObject when empty. "
        + "Used as the authoritative stop signal, because PlayableBehaviour destroy callbacks are not "
        + "guaranteed on every teardown path (notably Editor Evaluate/scrub).")]
    [SerializeField] private PlayableDirector director;

    private readonly Dictionary<object, CameraPresentationHandle> handles =
        new Dictionary<object, CameraPresentationHandle>();

    public int ActiveHandleCount => handles.Count;
    public int BasePriority => basePriority;
    public CameraRequestLifetime Lifetime => lifetime;
    public PlayableDirector Director => ResolveDirector();

    private void Reset()
    {
        director = GetComponent<PlayableDirector>();
    }

    private void OnDisable()
    {
        ReleaseAll();
    }

    // Backstop for teardown paths the playable callbacks do not cover: a director that has
    // stopped, finished, or had its graph destroyed can no longer refresh these requests, so they
    // must not outlive it. A game pause leaves the director Playing, so pausing never drops the
    // presentation.
    private void LateUpdate()
    {
        if (handles.Count == 0)
        {
            return;
        }

        PlayableDirector resolved = ResolveDirector();
        if (resolved != null && resolved.state != PlayState.Playing)
        {
            ReleaseAll();
        }
    }

    private PlayableDirector ResolveDirector()
    {
        if (director == null)
        {
            director = GetComponent<PlayableDirector>();
        }

        return director;
    }

    private void OnDestroy()
    {
        ReleaseAll();
    }

    // Acquires on first call for this owner and re-authors in place afterwards. Returns false when
    // no persistent camera exists (Editor preview) or the request could not be resolved, in which
    // case a later frame simply tries again.
    public bool SetPresentation(object owner, in CameraPresentationSettings settings, Transform[] targets, int priority)
    {
        if (owner == null)
        {
            return false;
        }

        int resolvedPriority = basePriority + priority;

        if (handles.TryGetValue(owner, out CameraPresentationHandle existing))
        {
            // Priority is re-applied every frame: overlapping clips can hand dominance to a clip
            // with a different authored priority without churning the handle.
            if (existing.Update(settings, targets, resolvedPriority))
            {
                return true;
            }

            // The request was pruned underneath us (all targets destroyed, source gone, expired).
            handles.Remove(owner);
        }

        if (GameCameras.Instance == null)
        {
            return false;
        }

        CameraPresentationHandle handle = GameCameras.Instance.AcquirePresentation(
            settings,
            targets,
            this,
            lifetime,
            resolvedPriority);

        if (!handle.IsValid)
        {
            return false;
        }

        handles[owner] = handle;
        return true;
    }

    public void ReleasePresentation(object owner)
    {
        if (owner == null || !handles.TryGetValue(owner, out CameraPresentationHandle handle))
        {
            return;
        }

        handles.Remove(owner);
        handle.Release();
    }

    public void ReleaseAll()
    {
        if (handles.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<object, CameraPresentationHandle> entry in handles)
        {
            entry.Value.Release();
        }

        handles.Clear();
    }
}
