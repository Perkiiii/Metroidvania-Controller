using System;
using System.Collections.Generic;
using UnityEngine;

/// Owns physical world facts: visited rooms, consumed pickups, permanent object states, and
/// permanent encounter completion (all serialized), plus non-serialized generic until-death
/// state and non-serialized timed death records for ordinary respawnable enemies. Ability,
/// quest, dialogue/NPC, inventory, and currency state remain in their own dedicated ISaveTarget
/// ScriptableObjects — this registry never reads or writes them.
[CreateAssetMenu(menuName = "Project/World/World State Registry", fileName = "WorldStateRegistry")]
public sealed class WorldStateRegistry : ScriptableObject, ISaveTarget
{
    private readonly HashSet<string> visitedRoomIds = new HashSet<string>();
    private readonly HashSet<string> collectedPickupIds = new HashSet<string>();
    private readonly HashSet<string> defeatedEncounterIds = new HashSet<string>();
    private readonly Dictionary<string, string> objectStates = new Dictionary<string, string>();

    // Never serialized. Cleared on ApplySaveData (fresh game / Continue / slot change) and on
    // normal death via the explicit reset calls below.
    private readonly Dictionary<string, string> untilDeathStates = new Dictionary<string, string>();
    private readonly Dictionary<string, EnemyDeathRecord> respawnableEnemyDeaths = new Dictionary<string, EnemyDeathRecord>();

    private readonly Dictionary<WorldStateKey, Action<WorldStateChange>> subscribers = new Dictionary<WorldStateKey, Action<WorldStateChange>>();

    private readonly struct EnemyDeathRecord
    {
        public readonly double ExpiryTime;

        public EnemyDeathRecord(double expiryTime)
        {
            ExpiryTime = expiryTime;
        }
    }

    // -------------------------------------------------------------------------
    // ISaveTarget
    // -------------------------------------------------------------------------

    public void GatherSaveData(SaveData data)
    {
        if (data?.world == null) return;

        data.world.collectedPickupIds = SortedList(collectedPickupIds);
        data.world.visitedRoomIds = SortedList(visitedRoomIds);
        data.world.defeatedEncounterIds = SortedList(defeatedEncounterIds);
        data.world.objectStates = SortedObjectStateEntries(objectStates);
    }

    public void ApplySaveData(SaveData data)
    {
        visitedRoomIds.Clear();
        collectedPickupIds.Clear();
        defeatedEncounterIds.Clear();
        objectStates.Clear();

        // Not serialized, but every ApplySaveData call represents a fresh game, a Continue, or a
        // slot change — all of which must present ordinary enemies alive and until-death state clear.
        untilDeathStates.Clear();
        respawnableEnemyDeaths.Clear();

        WorldSaveData world = data?.world;
        if (world == null) return;

        AddAllNonEmpty(visitedRoomIds, world.visitedRoomIds);
        AddAllNonEmpty(collectedPickupIds, world.collectedPickupIds);
        AddAllNonEmpty(defeatedEncounterIds, world.defeatedEncounterIds);

        if (world.objectStates == null) return;
        for (int i = 0; i < world.objectStates.Count; i++)
        {
            WorldObjectStateEntry entry = world.objectStates[i];
            if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
            objectStates[entry.id] = entry.state ?? "";
        }
    }

    // -------------------------------------------------------------------------
    // Visited rooms
    // -------------------------------------------------------------------------

    public bool IsRoomVisited(string roomId)
    {
        return !string.IsNullOrEmpty(roomId) && visitedRoomIds.Contains(roomId);
    }

    public void MarkRoomVisited(string roomId)
    {
        if (string.IsNullOrEmpty(roomId))
        {
            Debug.LogWarning("[WorldStateRegistry] MarkRoomVisited called with an empty roomId; ignoring.");
            return;
        }

        if (!visitedRoomIds.Add(roomId)) return;
        Notify(new WorldStateKey(WorldStateCategory.VisitedRoom, roomId), true);
    }

    // -------------------------------------------------------------------------
    // Pickup consumption
    // -------------------------------------------------------------------------

    public bool IsPickupCollected(string pickupId)
    {
        return !string.IsNullOrEmpty(pickupId) && collectedPickupIds.Contains(pickupId);
    }

    public void MarkPickupCollected(string pickupId)
    {
        if (string.IsNullOrEmpty(pickupId))
        {
            Debug.LogWarning("[WorldStateRegistry] MarkPickupCollected called with an empty pickupId; ignoring.");
            return;
        }

        if (!collectedPickupIds.Add(pickupId)) return;
        Notify(new WorldStateKey(WorldStateCategory.CollectedPickup, pickupId), true);
    }

    // Restricted: intended only for a physical-pickup persistence participant's own
    // initialization-time reconciliation, when its domain authority (e.g. PlayerAbilityState for
    // ability pickups) disagrees with a stale consumed record here. Removing the record makes a
    // later genuine collection behave identically to first-time collection, including its
    // notification -- this must never be used to implement general un-collection during gameplay.
    internal void ClearPickupCollectedRecord(string pickupId)
    {
        if (string.IsNullOrEmpty(pickupId)) return;
        collectedPickupIds.Remove(pickupId);
    }

    // -------------------------------------------------------------------------
    // Permanent encounter completion
    // -------------------------------------------------------------------------

    public bool IsEncounterDefeated(string encounterId)
    {
        return !string.IsNullOrEmpty(encounterId) && defeatedEncounterIds.Contains(encounterId);
    }

    public void MarkEncounterDefeated(string encounterId)
    {
        if (string.IsNullOrEmpty(encounterId))
        {
            Debug.LogWarning("[WorldStateRegistry] MarkEncounterDefeated called with an empty encounterId; ignoring.");
            return;
        }

        if (!defeatedEncounterIds.Add(encounterId)) return;
        Notify(new WorldStateKey(WorldStateCategory.DefeatedEncounter, encounterId), true);
    }

    // -------------------------------------------------------------------------
    // Permanent physical object state (serialized, arbitrary string payload)
    // -------------------------------------------------------------------------

    public bool TryGetObjectState(string objectId, out string state)
    {
        if (!string.IsNullOrEmpty(objectId) && objectStates.TryGetValue(objectId, out state)) return true;
        state = null;
        return false;
    }

    public void SetObjectState(string objectId, string state)
    {
        if (string.IsNullOrEmpty(objectId))
        {
            Debug.LogWarning("[WorldStateRegistry] SetObjectState called with an empty objectId; ignoring.");
            return;
        }

        string normalized = state ?? "";
        if (objectStates.TryGetValue(objectId, out string existing) && existing == normalized) return;

        objectStates[objectId] = normalized;
        Notify(new WorldStateKey(WorldStateCategory.ObjectState, objectId), true, normalized);
    }

    // -------------------------------------------------------------------------
    // Generic until-death physical state (never serialized)
    // -------------------------------------------------------------------------

    public bool TryGetUntilDeathState(string objectId, out string state)
    {
        if (!string.IsNullOrEmpty(objectId) && untilDeathStates.TryGetValue(objectId, out state)) return true;
        state = null;
        return false;
    }

    public void SetUntilDeathState(string objectId, string state)
    {
        if (string.IsNullOrEmpty(objectId))
        {
            Debug.LogWarning("[WorldStateRegistry] SetUntilDeathState called with an empty objectId; ignoring.");
            return;
        }

        string normalized = state ?? "";
        if (untilDeathStates.TryGetValue(objectId, out string existing) && existing == normalized) return;

        untilDeathStates[objectId] = normalized;
        Notify(new WorldStateKey(WorldStateCategory.UntilDeathState, objectId), true, normalized);
    }

    // Called on normal death, before the checkpoint scene loads. Never fires notifications —
    // this is a bulk lifecycle reset, not a per-object state change.
    public void ResetUntilDeathState()
    {
        untilDeathStates.Clear();
    }

    // -------------------------------------------------------------------------
    // Restricted enemy respawn-timer API
    // -------------------------------------------------------------------------

    public void RecordRespawnableEnemyDeath(string enemyId, float respawnDuration)
    {
        if (string.IsNullOrEmpty(enemyId))
        {
            Debug.LogWarning("[WorldStateRegistry] RecordRespawnableEnemyDeath called with an empty enemyId; ignoring.");
            return;
        }

        double expiry = Time.timeAsDouble + Math.Max(0f, respawnDuration);
        respawnableEnemyDeaths[enemyId] = new EnemyDeathRecord(expiry);
    }

    // Called on normal death, before the checkpoint scene loads. Never fires notifications and
    // never touches scene objects — only future EnemyController initializations observe this.
    public void ResetRespawnableEnemyDeaths()
    {
        respawnableEnemyDeaths.Clear();
    }

    // Internal: only the enemy persistence participant (EnemyPersistence, same assembly) may
    // call this, and only from EnemyController's one-time initialization path. This must never
    // be exposed as a general-purpose polling method — there is no live respawn scheduler.
    internal bool ShouldSuppressEnemyOnInitialization(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId)) return false;
        if (!respawnableEnemyDeaths.TryGetValue(enemyId, out EnemyDeathRecord record)) return false;

        if (Time.timeAsDouble >= record.ExpiryTime)
        {
            respawnableEnemyDeaths.Remove(enemyId);
            return false;
        }

        return true;
    }

    // -------------------------------------------------------------------------
    // Keyed subscriptions
    // -------------------------------------------------------------------------

    public void Subscribe(WorldStateKey key, Action<WorldStateChange> callback)
    {
        if (callback == null) return;
        subscribers.TryGetValue(key, out Action<WorldStateChange> existing);
        subscribers[key] = existing + callback;
    }

    public void Unsubscribe(WorldStateKey key, Action<WorldStateChange> callback)
    {
        if (callback == null) return;
        if (!subscribers.TryGetValue(key, out Action<WorldStateChange> existing)) return;

        existing -= callback;
        if (existing == null)
            subscribers.Remove(key);
        else
            subscribers[key] = existing;
    }

    private void Notify(WorldStateKey key, bool value, string stateValue = null)
    {
        if (subscribers.TryGetValue(key, out Action<WorldStateChange> callback))
            callback?.Invoke(new WorldStateChange(key, value, stateValue));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void AddAllNonEmpty(HashSet<string> target, List<string> source)
    {
        if (source == null) return;
        for (int i = 0; i < source.Count; i++)
        {
            if (!string.IsNullOrEmpty(source[i]))
                target.Add(source[i]);
        }
    }

    private static List<string> SortedList(HashSet<string> source)
    {
        List<string> result = new List<string>(source);
        result.Sort(StringComparer.Ordinal);
        return result;
    }

    private static List<WorldObjectStateEntry> SortedObjectStateEntries(Dictionary<string, string> source)
    {
        List<string> keys = new List<string>(source.Keys);
        keys.Sort(StringComparer.Ordinal);

        List<WorldObjectStateEntry> entries = new List<WorldObjectStateEntry>(keys.Count);
        for (int i = 0; i < keys.Count; i++)
        {
            entries.Add(new WorldObjectStateEntry { id = keys[i], state = source[keys[i]] });
        }
        return entries;
    }
}
