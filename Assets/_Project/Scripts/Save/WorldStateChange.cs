/// Payload delivered to a WorldStateRegistry keyed subscriber. For flag-like categories
/// (VisitedRoom/CollectedPickup/DefeatedEncounter) only Value is meaningful. For ObjectState and
/// UntilDeathState, StateValue carries the generic string payload.
public readonly struct WorldStateChange
{
    public readonly WorldStateKey Key;
    public readonly bool Value;
    public readonly string StateValue;

    public WorldStateChange(WorldStateKey key, bool value, string stateValue = null)
    {
        Key = key;
        Value = value;
        StateValue = stateValue;
    }
}
