using System;

public enum WorldStateCategory
{
    VisitedRoom,
    CollectedPickup,
    DefeatedEncounter,
    ObjectState,
    UntilDeathState
}

public readonly struct WorldStateKey : IEquatable<WorldStateKey>
{
    public readonly WorldStateCategory Category;
    public readonly string Id;

    public WorldStateKey(WorldStateCategory category, string id)
    {
        Category = category;
        Id = id ?? "";
    }

    public bool Equals(WorldStateKey other)
    {
        return Category == other.Category && Id == other.Id;
    }

    public override bool Equals(object obj)
    {
        return obj is WorldStateKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (Category, Id).GetHashCode();
    }

    public override string ToString()
    {
        return $"{Category}:{Id}";
    }
}
