public enum EnemyPersistenceMode
{
    // Genuine room runtime — no persistence, always initializes active. Dynamically pooled,
    // summoned, or disposable enemies use this and do not query WorldStateRegistry.
    RoomRuntime,

    // Ordinary placed enemy. Suppressed on scene initialization only while its timed death
    // record remains valid; the timer never respawns or reactivates it mid-scene.
    RespawnableTimed,

    // One-time enemy, miniboss, or boss. Suppressed on scene initialization once its encounter
    // is recorded as permanently defeated in WorldStateRegistry.
    PermanentEncounter
}
