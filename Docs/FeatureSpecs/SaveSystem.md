# Feature Spec — Save System

## Responsibilities

Persist and restore game state across play sessions: player progress, unlocked abilities, visited rooms, and relevant world state (opened doors, defeated bosses, etc.).

---

## Current State

No save system exists. This spec describes the intended design.

---

## Design Goals

- Fully decoupled from all gameplay MonoBehaviours — save/load must never call into `HeroController` or any subsystem directly
- Data-driven: what gets saved is defined by the data types, not by scattered field references
- Fail-safe: a missing or corrupt save file starts a fresh game without crashing
- Extensible: adding a new saveable field should not require modifying the serializer

---

## Intended Architecture

```
SaveManager (MonoBehaviour — persistent singleton or service locator)
├── SaveData (serializable plain C# class)
│   ├── PlayerSaveData   (health, position, facing)
│   ├── AbilitySaveData  (unlocked ability flags)
│   ├── WorldSaveData    (room flags, defeated enemies, open doors)
│   └── MetaSaveData     (timestamp, version)
└── ISaveTarget (interface)
    implemented by ScriptableObjects that own live state
```

`SaveManager` does not know about the hero at all. At save time it calls `ISaveTarget.CollectSaveData()` on each registered SO. At load time it calls `ISaveTarget.ApplySaveData(data)`.

### Save Triggers

- Entering a designated save point / save room
- On application quit (auto-save)
- TODO: decide whether to support manual save slots or a single-slot checkpoint model

### Serialization

- JSON via `JsonUtility` or `Newtonsoft.Json`
- Written to `Application.persistentDataPath`
- Include a version field in `MetaSaveData` for future migration

---

## Dependencies

- `PlayerAbilityState` SO (unlock flags — TODO, see `Abilities.md`)
- `PlayerHealthState` SO (TODO)
- `WorldStateRegistry` SO or equivalent (TODO)

---

## Extension Points

- Any new SO that carries persistent state implements `ISaveTarget` and registers with `SaveManager`.
- `WorldSaveData` can grow new flag arrays without touching the serializer.

---

## Rules

- `SaveManager` must not call any method on `HeroController`, `HeroMotor`, `HeroStateBlackboard`, or any other hero subsystem.
- All save data types must be plain C# classes with no Unity Object references (use GUIDs or string keys for scene-object identity).
- Loading must be possible before any hero scene object exists (e.g. loading from a main menu).
