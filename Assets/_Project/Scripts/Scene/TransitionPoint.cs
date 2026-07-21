using System.Collections.Generic;
using UnityEngine;
using WorldGraphEditor;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class TransitionPoint : PassageBase, ITransitionComponent
{
    private enum TransitionActivationMode
    {
        AutoTrigger,
        DoorInteract
    }

    [Header("Identity")]
    [SerializeField] private GateSide gateSide = GateSide.Unknown;

    // ---------------------------------------------------------------------
    // Destination — incoming entry motion.
    //
    // The values below tune the SCRIPTED, ONE-SHOT motion that plays when the
    // hero arrives at this gate from another scene. They are NOT regular
    // movement, jump, dash, or gravity tuning — those live in HeroConfig /
    // HeroAbilityConfig and must not be touched from here. Each gate authors
    // its own entry feel independently.
    //
    // Defaults are based on the reference Hollow Knight values (RUN_SPEED 6.5,
    // SPEED_TO_ENTER_SCENE_HOR 6.5, SPEED_TO_ENTER_SCENE_UP 8.9,
    // SPEED_TO_ENTER_SCENE_DOWN -8.9, TIME_TO_ENTER_SCENE_HOR/BOT 0.3) so a
    // freshly-added gate reads as a controlled entry rather than a slingshot.
    // Override per gate when a room needs a stronger or softer arrival.
    // ---------------------------------------------------------------------
    [Header("Destination — incoming entry motion")]
    [Tooltip("World-space offset added to this gate's transform.position to produce the hero's spawn point. Independent from normal hero physics.")]
    [SerializeField] private Vector2 entryOffset;

    [Tooltip("Override the hero's facing on arrival. 'None' does NOT carry over the previous scene's facing — the destination hero uses its blackboard default (facingRight). For Bottom gates that need a specific launch direction, pick ForceRight / ForceLeft explicitly.")]
    [SerializeField] private EntryFacing entryFacingOverride = EntryFacing.None;

    [Tooltip("LEFT / RIGHT / DOOR gates: seconds of scripted entry motion before control returns. Transition-entry value only; does not affect normal run timing.")]
    [SerializeField] private float entryRunInDuration = 0.3f;

    [Tooltip("TOP gate: scripted downward speed applied at entry (positive magnitude; the motor receives -entryDropSpeed). Transition-entry value only; does not affect gravity, fall speed, or jump arcs.")]
    [SerializeField] private float entryDropSpeed = 8.9f;

    [Tooltip("BOTTOM gate: scripted horizontal speed during the diagonal throw and the subsequent X-locked drop. Transition-entry value only; does not affect runSpeed.")]
    [SerializeField] private float bottomThrowHorizontal = 6.5f;

    [Tooltip("BOTTOM gate: initial upward velocity for the diagonal throw. Must be > 0 so the hero rises above the +3 m spawn placement before gravity takes over. Transition-entry value only; does not affect jumpSpeed.")]
    [SerializeField] private float bottomThrowVertical = 8.9f;

    [Tooltip("BOTTOM gate: duration of the phase-1 full-velocity-lock throw before gravity is restored. Transition-entry value only.")]
    [SerializeField] private float bottomThrowDuration = 0.3f;

    [Tooltip("BOTTOM gate: meters the hero is lifted above the gate's spawn point before the diagonal throw begins. Prevents the hero from immediately re-overlapping the gate trigger and gives the throw arc clearance. Authored per-gate; the default works for most rooms. Transition-entry value only.")]
    [SerializeField] private float bottomGateSpawnLift = 1.5f;

    [Tooltip("Safety timeout for any entry. If the per-gate termination signal (timer or grounded) never fires within this window, the entry ends anyway so the hero cannot get stuck. Transition-entry value only.")]
    [SerializeField] private float entryMaxFallbackTime = 1.5f;

    [Header("Door behaviour")]
    [SerializeField] private bool isDoor;
    [SerializeField] private bool requireInteract = true;

    [Header("Linked respawn (reserved — not consumed at runtime in this pass)")]
    [Tooltip("Reserved field. Per the current design decision, TransitionPoint does NOT update GameManager's active RespawnMarker. Death after a gate crossing returns the hero to the last activated checkpoint. This field is here for a later policy pass.")]
    [SerializeField] private RespawnMarker linkedRespawnMarker;

    private static readonly List<TransitionPoint> Active = new List<TransitionPoint>();
    private bool localTransitionGuard;
    private Collider2D cachedCollider;

    public GateSide GateSide => gateSide;
    public Vector3 EntrySpawnPosition => transform.position + (Vector3)entryOffset;
    public EntryFacing FacingOverride => entryFacingOverride;
    public float EntryRunInDuration => entryRunInDuration;
    public float EntryDropSpeed => entryDropSpeed;
    public float BottomThrowHorizontal => bottomThrowHorizontal;
    public float BottomThrowVertical => bottomThrowVertical;
    public float BottomThrowDuration => bottomThrowDuration;
    public float BottomGateSpawnLift => bottomGateSpawnLift;
    public float EntryMaxFallbackTime => entryMaxFallbackTime;
    public bool IsDoor => isDoor;
    public bool RequireInteract => requireInteract;
    public RespawnMarker LinkedRespawnMarker => linkedRespawnMarker;

    // Shadows PassageBase.GetGuid() intentionally — PassageBase.GetGuid() is not virtual.
    // In builds: delegates to base, which returns PortsDropdown._selectedGuid directly.
    // In the Editor: reads _selectedGuid via SerializedObject rather than through
    // PortsDropdown.GetSelectedValue(). GetSelectedValue() can silently return _guidData[0]
    // (the first port available in the current scene) when _data is populated by a WGE Refresh
    // but the assigned port no longer exists in the graph (renamed or deleted after assignment).
    // Reading the serialized field directly ensures the value always matches what is baked into builds.
    // ITransitionComponent.GetGuid() below routes through this same method.
    public new string GetGuid()
    {
#if UNITY_EDITOR
        return GetSerializedAssignedGuid();
#else
        return base.GetGuid();
#endif
    }

    string ITransitionComponent.GetGuid()
    {
        return GetGuid();
    }

    public override Vector3 GetSpawnPosition()
    {
        return EntrySpawnPosition;
    }

    public static TransitionPoint FindByPassageGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid)) return null;

        TransitionPoint match = null;
        for (int i = 0; i < Active.Count; i++)
        {
            TransitionPoint candidate = Active[i];
            if (candidate == null || candidate.GetGuid() != guid) continue;

            if (match != null)
            {
                Debug.LogWarning($"[TransitionPoint] Multiple active gates with GUID '{guid}' - using first match.");
                break;
            }

            match = candidate;
        }

        return match;
    }

    private void Awake()
    {
        cachedCollider = GetComponent<Collider2D>();

        // Authoring convenience: if no RespawnMarker is wired in the Inspector,
        // pick up a child RespawnMarker so authors can just parent one under the
        // gate. linkedRespawnMarker is reserved (not consumed at runtime in this
        // pass) but populating it here prepares for the future policy pass.
        if (linkedRespawnMarker == null)
        {
            linkedRespawnMarker = GetComponentInChildren<RespawnMarker>();
        }
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
        TryActivateFromTrigger(other);
    }

    // Safety net: if the hero is already overlapping this gate's trigger when
    // conditions become valid (e.g., after a respawn drops the hero inside a
    // trigger, or after IsTransitionBlocked clears), Stay re-fires the check
    // each physics step until either the transition fires or the hero exits.
    // Guarded by localTransitionGuard so a successful transition won't loop.
    private void OnTriggerStay2D(Collider2D other)
    {
        if (localTransitionGuard) return;
        TryActivateFromTrigger(other);
    }

    public bool TryActivateFromInteract(HeroController hero)
    {
        return TryActivate(hero, null, TransitionActivationMode.DoorInteract);
    }

    private bool TryActivateFromTrigger(Collider2D other)
    {
        HeroController hero = other.GetComponentInParent<HeroController>();
        if (hero == null) return false;

        return TryActivate(hero, other, TransitionActivationMode.AutoTrigger);
    }

    private bool TryActivate(HeroController hero, Collider2D heroCollider, TransitionActivationMode mode)
    {
        if (localTransitionGuard) return false;
        if (hero == null) return false;
        if (GameManager.Instance == null) return false;

        if (mode == TransitionActivationMode.AutoTrigger && isDoor && requireInteract)
            return false;

        if (mode == TransitionActivationMode.DoorInteract && !isDoor)
        {
            Debug.LogWarning($"[TransitionPoint] '{name}' was activated as a door but isDoor is false.", this);
            return false;
        }

        // Bail before the push-back path while a scene transition is loading or placing/entering
        // the hero (Loading/EnteringLevel). Without this, a destination gate's own trigger firing
        // during placement or scripted entry motion would push the hero via HeroMotor.PushOut,
        // fighting the concurrently-running scripted entry velocity. See ApplyPushBack below.
        if (GameManager.Instance.State != GameState.Playing) return false;

        // Push-back path: hero is in a state where the transition would mis-fire.
        // Auto gates nudge the hero out of the trigger; door interact gates reject
        // without directional displacement.
        if (ShouldRejectActivation(hero))
        {
            if (mode == TransitionActivationMode.AutoTrigger && heroCollider != null)
                ApplyPushBack(hero, heroCollider);
            return false;
        }

        string graphGuid = GetGuid();
        if (string.IsNullOrEmpty(graphGuid))
        {
            Debug.LogWarning($"[TransitionPoint] '{name}' has no WGE port assigned; transition rejected.", this);
            return false;
        }

        if (!WorldGraphTransitionResolver.TryResolve(graphGuid, false, out WorldGraphTransitionRequest request))
        {
            Debug.LogWarning($"[TransitionPoint] WGE resolve failed for '{name}': {request.FailureReason}", this);

            if (mode == TransitionActivationMode.AutoTrigger && heroCollider != null)
                ApplyPushBack(hero, heroCollider);

            return false;
        }

        if (!GameManager.Instance.BeginSceneTransition(new SceneTransitionRequest(
            request.TargetSceneName,
            request.TargetPortGuid,
            kind: SceneTransitionKind.Gate,
            sourceDescription: name)))
            return false;

        localTransitionGuard = true;
        return true;
    }

    private bool ShouldRejectActivation(HeroController hero)
    {
        if (hero.IsRecoiling) return true;
        if (hero.IsControlLocked) return true;
        return false;
    }

    private void ApplyPushBack(HeroController hero, Collider2D heroCollider)
    {
        if (cachedCollider == null) return;

        Bounds gateBounds = cachedCollider.bounds;
        Bounds heroBounds = heroCollider.bounds;

        Vector2 offset = Vector2.zero;
        bool zeroX = false;
        bool zeroY = false;

        switch (gateSide)
        {
            case GateSide.Right:
                // Hero entered from the room side; push left so they sit just outside the gate.
                offset.x = gateBounds.min.x - heroBounds.max.x;
                zeroX = true;
                break;
            case GateSide.Left:
                offset.x = gateBounds.max.x - heroBounds.min.x;
                zeroX = true;
                break;
            case GateSide.Top:
                offset.y = gateBounds.min.y - heroBounds.max.y;
                zeroY = true;
                break;
            case GateSide.Bottom:
                offset.y = gateBounds.max.y - heroBounds.min.y;
                zeroY = true;
                break;
            case GateSide.Door:
            case GateSide.Unknown:
            default:
                // No directional push-back makes sense; just bail.
                return;
        }

        hero.PushOutOfGate(offset, zeroX, zeroY);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (gateSide == GateSide.Unknown)
            Debug.LogWarning($"[TransitionPoint] '{name}' has GateSide.Unknown. Pick a direction.", this);

        if (string.IsNullOrEmpty(GetGuid()))
            Debug.LogWarning($"[TransitionPoint] '{name}' has no WGE port assigned. Select a port in the Inspector.", this);

        if (gateSide == GateSide.Door && !isDoor)
            Debug.LogWarning($"[TransitionPoint] '{name}' has GateSide.Door but isDoor is false.", this);

        if (isDoor && gateSide != GateSide.Door && gateSide != GateSide.Unknown)
            Debug.LogWarning($"[TransitionPoint] '{name}' has isDoor=true but gateSide is not Door.", this);

        if (gateSide == GateSide.Bottom && bottomThrowVertical <= 0f)
            Debug.LogWarning($"[TransitionPoint] '{name}' is a Bottom gate but bottomThrowVertical is <= 0 — hero will not rise above the spawn placement and may terminate the throw arc immediately.", this);

        if (gateSide == GateSide.Bottom && bottomGateSpawnLift <= 0f)
            Debug.LogWarning($"[TransitionPoint] '{name}' is a Bottom gate but bottomGateSpawnLift is <= 0 — hero will spawn at gate position and may immediately re-overlap the gate trigger.", this);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
            Debug.LogWarning($"[TransitionPoint] '{name}' Collider2D is not set as trigger.", this);
    }

    private string GetSerializedAssignedGuid()
    {
        UnityEditor.SerializedObject serializedPoint = new UnityEditor.SerializedObject(this);
        UnityEditor.SerializedProperty assignedPort = serializedPoint.FindProperty("_assignedPort");
        UnityEditor.SerializedProperty selectedGuid = assignedPort?.FindPropertyRelative("_selectedGuid");
        return selectedGuid != null ? selectedGuid.stringValue : "";
    }
#endif
}
