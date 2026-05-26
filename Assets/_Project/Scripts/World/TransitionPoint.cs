using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TransitionPoint : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string gateKey;
    [SerializeField] private GateSide gateSide = GateSide.Unknown;

    [Header("Source — outgoing transition")]
    [SerializeField] private string targetScene;
    [SerializeField] private string entryGateKey;

    [Header("Destination — incoming entry motion")]
    [SerializeField] private Vector2 entryOffset;
    [SerializeField] private EntryFacing entryFacingOverride = EntryFacing.None;
    [SerializeField] private float entryRunInDuration = 0.33f;
    [SerializeField] private float entryDropSpeed = 12f;
    [SerializeField] private float bottomThrowHorizontal = 8f;
    [SerializeField] private float bottomThrowVertical = 14f;
    [SerializeField] private float bottomThrowDuration = 0.25f;
    [SerializeField] private float entryMaxFallbackTime = 1.5f;

    [Header("Door behaviour")]
    [SerializeField] private bool isDoor;
    [SerializeField] private bool requireInteract = true;

    [Header("Linked respawn (reserved — not consumed at runtime in this pass)")]
    [SerializeField] private RespawnMarker linkedRespawnMarker;

    private static readonly List<TransitionPoint> Active = new List<TransitionPoint>();
    private bool localTransitionGuard;

    public string GateKey => gateKey;
    public GateSide GateSide => gateSide;
    public string TargetScene => targetScene;
    public string EntryGateKey => entryGateKey;
    public Vector3 EntrySpawnPosition => transform.position + (Vector3)entryOffset;
    public EntryFacing FacingOverride => entryFacingOverride;
    public float EntryRunInDuration => entryRunInDuration;
    public float EntryDropSpeed => entryDropSpeed;
    public float BottomThrowHorizontal => bottomThrowHorizontal;
    public float BottomThrowVertical => bottomThrowVertical;
    public float BottomThrowDuration => bottomThrowDuration;
    public float EntryMaxFallbackTime => entryMaxFallbackTime;
    public bool IsDoor => isDoor;
    public bool RequireInteract => requireInteract;
    public RespawnMarker LinkedRespawnMarker => linkedRespawnMarker;

    public static TransitionPoint FindByGateKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        TransitionPoint match = null;
        for (int i = 0; i < Active.Count; i++)
        {
            TransitionPoint candidate = Active[i];
            if (candidate == null || candidate.gateKey != key) continue;

            if (match != null)
            {
                Debug.LogWarning($"[TransitionPoint] Multiple active gates with key '{key}' — using first match.");
                break;
            }

            match = candidate;
        }

        return match;
    }

    private void OnEnable()
    {
        if (!Active.Contains(this))
            Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
        localTransitionGuard = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (localTransitionGuard) return;
        if (string.IsNullOrEmpty(targetScene)) return;
        if (isDoor && requireInteract) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.State != GameState.Playing) return;
        if (other.GetComponentInParent<HeroController>() == null) return;

        localTransitionGuard = true;
        GameManager.Instance.BeginSceneTransition(targetScene, entryGateKey);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gateSide == GateSide.Unknown)
            Debug.LogWarning($"[TransitionPoint] '{name}' has GateSide.Unknown. Pick a direction.", this);

        if (string.IsNullOrWhiteSpace(gateKey))
            Debug.LogWarning($"[TransitionPoint] '{name}' has no gateKey set.", this);

        if (!string.IsNullOrEmpty(targetScene) && string.IsNullOrWhiteSpace(entryGateKey))
            Debug.LogWarning($"[TransitionPoint] '{name}' is a source gate (targetScene set) but entryGateKey is empty.", this);

        if (gateSide == GateSide.Door && !isDoor)
            Debug.LogWarning($"[TransitionPoint] '{name}' has GateSide.Door but isDoor is false.", this);

        if (isDoor && gateSide != GateSide.Door && gateSide != GateSide.Unknown)
            Debug.LogWarning($"[TransitionPoint] '{name}' has isDoor=true but gateSide is not Door.", this);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[TransitionPoint] '{name}' Collider2D is not set as trigger.", this);
    }
#endif
}
