using UnityEngine;

// Tuning data for a single recoverable hazard's feel. Referenced by HazardZone and
// carried in HazardContact so GameManager receives exactly the profile it should use.
[CreateAssetMenu(menuName = "World/Hazard Recovery Profile", fileName = "HazardRecoveryProfile")]
public sealed class HazardRecoveryProfile : ScriptableObject
{
    [Header("Timing")]
    [Tooltip("Delay between hazard contact and fade-out. Gives the hit flash and camera shake time to register before the screen goes black. Recommended: 0.18–0.20 s.")]
    [SerializeField, Min(0f)] private float impactDelay = 0.18f;
    [Tooltip("Realtime pause after camera snap while the screen is black, before fade-in begins.")]
    [SerializeField, Min(0f)] private float blackScreenHold = 0.1f;

    [Header("Protection")]
    [Tooltip("Duration of temporary invincibility granted when local recovery begins. Should cover the full impact delay + fade-out + reposition + fade-in window so enemy contact cannot deal damage mid-sequence.")]
    [SerializeField, Min(0f)] private float recoveryIFrameDuration = 0.75f;

    public float ImpactDelay => impactDelay;
    public float BlackScreenHold => blackScreenHold;
    public float RecoveryIFrameDuration => recoveryIFrameDuration;
}
