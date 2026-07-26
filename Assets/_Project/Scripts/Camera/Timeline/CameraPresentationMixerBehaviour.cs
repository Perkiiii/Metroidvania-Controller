using UnityEngine;
using UnityEngine.Playables;

// Owns exactly one camera presentation request for its track, for the whole life of the graph.
//
// Overlapping clips resolve deterministically: numeric framing values are weighted-averaged, while
// discrete choices (mode, targets, zoom flags, priority) come from the highest-weighted clip, with
// ties resolved in favour of the later track input. The aggregated clip weight becomes the request
// weight, so Timeline ease-in/ease-out blends the presentation against the underlying framing
// rather than repeatedly acquiring and releasing handles.
public sealed class CameraPresentationMixerBehaviour : PlayableBehaviour
{
    private const float WeightEpsilon = 0.0001f;

    public CameraPresentationReceiver receiver;

    private bool holdsRequest;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        CameraPresentationReceiver resolved = playerData as CameraPresentationReceiver ?? receiver;
        if (resolved == null)
        {
            Release();
            return;
        }

        receiver = resolved;

        int inputCount = playable.GetInputCount();
        float totalWeight = 0f;
        float dominantWeight = 0f;
        int dominantIndex = -1;

        Vector2 worldPoint = Vector2.zero;
        Vector2 framingOffset = Vector2.zero;
        float paddingX = 0f;
        float paddingY = 0f;
        float authoredZoom = 0f;
        float minZoom = 0f;
        float maxZoom = 0f;
        float authoredWeight = 0f;

        for (int i = 0; i < inputCount; i++)
        {
            float weight = playable.GetInputWeight(i);
            if (weight <= WeightEpsilon)
            {
                continue;
            }

            ScriptPlayable<CameraPresentationBehaviour> input =
                (ScriptPlayable<CameraPresentationBehaviour>)playable.GetInput(i);
            CameraPresentationBehaviour behaviour = input.GetBehaviour();
            if (behaviour == null)
            {
                continue;
            }

            CameraPresentationSettings clip = behaviour.settings;
            totalWeight += weight;
            worldPoint += clip.worldPoint * weight;
            framingOffset += clip.framingOffset * weight;
            paddingX += clip.paddingX * weight;
            paddingY += clip.paddingY * weight;
            authoredZoom += Mathf.Max(0.0001f, clip.authoredZoom) * weight;
            minZoom += clip.minZoom * weight;
            maxZoom += clip.maxZoom * weight;
            authoredWeight += clip.ResolvedWeight * weight;

            if (weight >= dominantWeight)
            {
                dominantWeight = weight;
                dominantIndex = i;
            }
        }

        if (dominantIndex < 0 || totalWeight <= WeightEpsilon)
        {
            Release();
            return;
        }

        ScriptPlayable<CameraPresentationBehaviour> dominantInput =
            (ScriptPlayable<CameraPresentationBehaviour>)playable.GetInput(dominantIndex);
        CameraPresentationBehaviour dominant = dominantInput.GetBehaviour();

        CameraPresentationSettings blended = dominant.settings;
        float inverse = 1f / totalWeight;
        blended.worldPoint = worldPoint * inverse;
        blended.framingOffset = framingOffset * inverse;
        blended.paddingX = paddingX * inverse;
        blended.paddingY = paddingY * inverse;
        blended.authoredZoom = authoredZoom * inverse;
        blended.minZoom = minZoom * inverse;
        blended.maxZoom = maxZoom * inverse;

        // Clip weight scales the authored weight so a clip authored at partial strength still
        // eases correctly at its own boundaries.
        blended.weight = Mathf.Clamp01(authoredWeight * inverse * Mathf.Clamp01(totalWeight));

        holdsRequest = receiver.SetPresentation(this, blended, dominant.targets, dominant.priority);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        // Fires when the director stops or the graph is paused at the end of the timeline.
        Release();
    }

    public override void OnGraphStop(Playable playable)
    {
        Release();
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        Release();
        receiver = null;
    }

    private void Release()
    {
        if (!holdsRequest && receiver == null)
        {
            return;
        }

        holdsRequest = false;
        receiver?.ReleasePresentation(this);
    }
}
