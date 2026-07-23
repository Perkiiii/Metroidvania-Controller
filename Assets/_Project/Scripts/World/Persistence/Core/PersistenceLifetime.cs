// Shared physical-object persistence lifetime for switches and breakables (World Persistence
// Phase 3). Doors/shortcuts do not use this enum -- a door that "un-opens" itself has no design use
// case in this milestone, so PersistentDoor always writes permanent state directly.
public enum PersistenceLifetime
{
    RoomRuntime,
    UntilDeath,
    Permanent
}
