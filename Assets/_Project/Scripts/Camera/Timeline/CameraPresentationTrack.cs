using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[TrackColor(0.85f, 0.35f, 0.8f)]
[TrackClipType(typeof(CameraPresentationClip))]
[TrackBindingType(typeof(CameraPresentationReceiver))]
public sealed class CameraPresentationTrack : TrackAsset
{
    public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
    {
        ScriptPlayable<CameraPresentationMixerBehaviour> mixer =
            ScriptPlayable<CameraPresentationMixerBehaviour>.Create(graph, inputCount);

        PlayableDirector director = go != null ? go.GetComponent<PlayableDirector>() : null;
        if (director != null)
        {
            mixer.GetBehaviour().receiver = director.GetGenericBinding(this) as CameraPresentationReceiver;
        }

        return mixer;
    }
}
