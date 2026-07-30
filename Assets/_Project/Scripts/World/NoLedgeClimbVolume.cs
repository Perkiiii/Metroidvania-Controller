using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class NoLedgeClimbVolume : MonoBehaviour
{
    private void Reset()
    {
        if (TryGetComponent(out Collider2D volume))
        {
            volume.isTrigger = true;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (TryGetComponent(out Collider2D volume) && !volume.isTrigger)
        {
            Debug.LogWarning("[NoLedgeClimbVolume] The attached Collider2D should be a trigger.", this);
        }
    }
#endif
}
