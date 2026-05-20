public readonly struct HazardContact
{
    public HazardContact(int damage, HazardRecoveryMode recoveryMode, HazardRespawnMarker respawnMarker, object source)
    {
        Damage = damage;
        RecoveryMode = recoveryMode;
        RespawnMarker = respawnMarker;
        Source = source;
    }

    public int Damage { get; }
    public HazardRecoveryMode RecoveryMode { get; }
    public HazardRespawnMarker RespawnMarker { get; }
    public object Source { get; }
}
