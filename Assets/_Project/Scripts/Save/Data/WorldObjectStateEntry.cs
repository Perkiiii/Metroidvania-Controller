using System;

// JsonUtility cannot serialize Dictionary<,> directly, so permanent object states round-trip
// through a flat list of these entries.
[Serializable]
public class WorldObjectStateEntry
{
    public string id = "";
    public string state = "";
}
