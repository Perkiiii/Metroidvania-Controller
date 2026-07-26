using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Authored camera-presentation clip. Targets are ExposedReferences resolved through the playing
// PlayableDirector, because a Timeline asset is a project asset and cannot hold scene references.
[System.Serializable]
public sealed class CameraPresentationClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("Framing, padding, zoom, and blend authoring for this clip.")]
    public CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);

    [Tooltip("Scene transforms framed by this clip. Required by FocusTarget and FrameTargets.")]
    public ExposedReference<Transform>[] targets = new ExposedReference<Transform>[1];

    [Tooltip("Higher priority wins against other camera presentation requests; ties resolve to the newest request.")]
    public int priority;

    // Blending lets overlapping clips and clip ease-in/ease-out drive the request weight instead
    // of creating and destroying handles.
    public ClipCaps clipCaps => ClipCaps.Blending | ClipCaps.Extrapolation;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        ScriptPlayable<CameraPresentationBehaviour> playable =
            ScriptPlayable<CameraPresentationBehaviour>.Create(graph);

        CameraPresentationBehaviour behaviour = playable.GetBehaviour();
        behaviour.settings = settings;
        behaviour.priority = priority;
        behaviour.targets = ResolveTargets(graph.GetResolver());
        return playable;
    }

    private Transform[] ResolveTargets(IExposedPropertyTable resolver)
    {
        if (targets == null || targets.Length == 0)
        {
            return System.Array.Empty<Transform>();
        }

        List<Transform> resolved = new List<Transform>(targets.Length);
        for (int i = 0; i < targets.Length; i++)
        {
            Transform value = targets[i].Resolve(resolver);
            if (value != null)
            {
                resolved.Add(value);
            }
        }

        return resolved.ToArray();
    }
}
