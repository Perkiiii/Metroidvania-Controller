public readonly struct HazardContact
{
    public HazardContact(int damage, HazardRecoveryMode recoveryMode, HazardRespawnMarker respawnMarker, HazardRecoveryProfile recoveryProfile, object source)
    {
        Damage = damage;
        RecoveryMode = recoveryMode;
        RespawnMarker = respawnMarker;
        RecoveryProfile = recoveryProfile;
        Source = source;
    }

    public int Damage { get; }
    public HazardRecoveryMode RecoveryMode { get; }
    public HazardRespawnMarker RespawnMarker { get; }
    public HazardRecoveryProfile RecoveryProfile { get; }
    public object Source { get; }
}
