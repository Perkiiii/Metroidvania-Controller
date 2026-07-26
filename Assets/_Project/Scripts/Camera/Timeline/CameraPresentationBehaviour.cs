using UnityEngine;
using UnityEngine.Playables;

// Per-clip payload. Holds only resolved presentation data; it never reads or writes the camera.
public sealed class CameraPresentationBehaviour : PlayableBehaviour
{
    public CameraPresentationSettings settings = CameraPresentationSettings.Default(CameraPresentationMode.FocusTarget);
    public Transform[] targets = System.Array.Empty<Transform>();
    public int priority;
}
