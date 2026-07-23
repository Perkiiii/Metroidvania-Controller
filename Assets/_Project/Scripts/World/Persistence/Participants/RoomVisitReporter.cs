using UnityEngine;

/// Marks its authored roomId visited exactly once on initialization (World Persistence Phase 3.1).
/// Room identity is authored data on this component, not derived from the Unity scene name -- so
/// renaming a .unity file never changes saved room identity. Place exactly one instance in each
/// gameplay scene; Boot and other non-gameplay scenes should have none.
[DisallowMultipleComponent]
public sealed class RoomVisitReporter : MonoBehaviour
{
    [Tooltip("Stable identity for this room. Must be unique across every RoomVisitReporter in the " +
        "project (a separate ID namespace from worldObjectId used by physical persistent objects).")]
    [SerializeField] private string roomId;

    [SerializeField] private WorldStateRegistry registry;

    private void Awake()
    {
        if (registry == null)
        {
            Debug.LogError($"[RoomVisitReporter] '{name}' has no WorldStateRegistry assigned. Room visitation cannot be recorded.", this);
            return;
        }

        if (string.IsNullOrEmpty(roomId))
        {
            Debug.LogError($"[RoomVisitReporter] '{name}' has no roomId assigned. Room visitation cannot be recorded.", this);
            return;
        }

        registry.MarkRoomVisited(roomId);
    }
}
