using System;
using System.Collections.Generic;

[Serializable]
public class WorldSaveData
{
    public List<string> collectedPickupIds = new List<string>();
    public List<string> visitedRoomIds = new List<string>();
    public List<string> defeatedEncounterIds = new List<string>();
    public List<WorldObjectStateEntry> objectStates = new List<WorldObjectStateEntry>();
}
