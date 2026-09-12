using UnityEngine;

/// <summary>
/// Scene-local climate metadata for a room. Runtime simulation remains owned by the world time
/// and weather state assets; consumers use this component only to resolve the room's context.
/// </summary>
[DisallowMultipleComponent]
public sealed class RoomClimateContext : MonoBehaviour
{
    [Tooltip("Stable authored climate region ID used by this room. Many rooms may share one region.")]
    [SerializeField] private string regionId;

    [Tooltip("How much the room is exposed to the outside environment.")]
    [SerializeField] private EnvironmentExposure exposure;

    public string RegionId => regionId;

    public EnvironmentExposure Exposure => exposure;
}
