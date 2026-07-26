using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public sealed class GameCameras : MonoBehaviour
{
    private sealed class FreezeRegistration
    {
        public long Id;
        public CameraFreezeKind Kind;
        public object Source;
        public CameraRequestLifetime Lifetime;
        public int SourceSceneHandle;
        public float ExpiresAt;
        public bool SuppressFinalReleaseTransition;
    }

    private sealed class PresentationRegistration
    {
        public long Id;
        public object Source;
        public CameraRequestLifetime Lifetime;
        public int SourceSceneHandle;
        public float ExpiresAt;
        public int Priority;
        public long Sequence;
        public CameraPresentationSettings Settings;
        public Transform[] Targets;
    }

    public static GameCameras Instance { get; private set; }

    [SerializeField] private CameraController cameraController;
    [SerializeField] private CameraTarget     cameraTarget;
    [FormerlySerializedAs("cameraFade")]
    [SerializeField] private CameraFade       fade;
    [SerializeField] private CameraShakeCueService shakeCues;
    [SerializeField] private Camera           hudCamera;

    public CameraController Controller => cameraController;
    public CameraTarget     Target     => cameraTarget;
    public CameraFade       Fade       => fade;
    public CameraShakeCueService ShakeCues => shakeCues;
    public ICameraShakeService ShakeService => shakeCues;
    public Camera           HudCamera  => hudCamera;

    private Coroutine fadeRoutine;
    private readonly List<FreezeRegistration> freezeRegistrations = new List<FreezeRegistration>();
    private readonly List<PresentationRegistration> presentationRegistrations = new List<PresentationRegistration>();
    private Collider2D trackedPlayerCollider;
    private long nextFreezeRequestId;
    private long nextPresentationRequestId;
    private long nextPresentationSequence;
    private long selectedPresentationId;

    public int ActiveFreezeCount => freezeRegistrations.Count;
    public int ActivePresentationCount => presentationRegistrations.Count;

    // 0 when no presentation request is currently driving the camera.
    public long SelectedPresentationId => selectedPresentationId;
    public CameraSceneEntryReadiness LastSceneEntryReadiness { get; private set; }

    // Screen-cover alpha of the persistent fade (1 = fully black, 0 = fully clear), or 1 when no
    // fade is assigned so callers gating on visibility default to "still hidden".
    public float FadeAlpha => fade != null ? fade.CurrentAlpha : 1f;

    // The single collider camera volumes recognise as "the player" for overlap purposes. Bound
    // once per scene entry, not any collider that merely shares the hero's Player tag/hierarchy.
    public Collider2D TrackedPlayerCollider => trackedPlayerCollider;

    // Camera volumes (lock/bounds/offset areas) must key off this exact collider rather than a
    // tag/hierarchy check, so a temporary trigger under the hero (e.g. an attack hitbox) enabling
    // or disabling can never itself cause the volume to register or drop registration.
    public static bool IsCanonicalPlayerCollider(Collider2D collider)
    {
        return collider != null && Instance != null && collider == Instance.trackedPlayerCollider;
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        DisableExternalAudioListeners();

        if (shakeCues == null && !TryGetComponent(out shakeCues))
            shakeCues = gameObject.AddComponent<CameraShakeCueService>();
    }

    private void OnEnable()
    {
        if (Instance != this)
        {
            return;
        }

        CameraEventService.LockEntered += OnCameraLockEntered;
        CameraEventService.LockExited += OnCameraLockExited;
        CameraEventService.OffsetEntered += OnCameraOffsetEntered;
        CameraEventService.OffsetExited += OnCameraOffsetExited;
        CameraEventService.FadeRequested += OnCameraFadeRequested;
        CameraEventService.ShakeRequested += OnCameraShakeRequested;
        CameraEventService.ShakeCancelRequested += OnCameraShakeCancelRequested;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        CameraEventService.LockEntered -= OnCameraLockEntered;
        CameraEventService.LockExited -= OnCameraLockExited;
        CameraEventService.OffsetEntered -= OnCameraOffsetEntered;
        CameraEventService.OffsetExited -= OnCameraOffsetExited;
        CameraEventService.FadeRequested -= OnCameraFadeRequested;
        CameraEventService.ShakeRequested -= OnCameraShakeRequested;
        CameraEventService.ShakeCancelRequested -= OnCameraShakeCancelRequested;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        ClearFreezeRegistrations();
        ClearPresentationRegistrations();
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SceneInit += OnSceneInit;
            if (GameObject.FindWithTag("Player") != null)
                OnSceneInit(SceneManager.GetActiveScene());

            return;
        }

        // Fallback: when playing directly in a scene without going through Bootstrap,
        // GameManager never fires SceneInit, so we initialise immediately here.
        OnSceneInit(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        ClearFreezeRegistrations();
        ClearPresentationRegistrations();

        if (Instance == this)
            Instance = null;

        if (GameManager.Instance != null)
            GameManager.Instance.SceneInit -= OnSceneInit;
    }

    private void OnSceneInit(Scene scene)
    {
        DisableExternalAudioListeners();
        ResetPresentationForSceneEntry();
        cameraTarget?.SceneInit();
        cameraController?.SceneInit();
        BindTrackedPlayerCollider();
        RefreshSceneOverlaps();
        ApplyFreezeAggregate();
    }

    public void RebindForSceneEntry()
    {
        ResetPresentationForSceneEntry();
        cameraTarget?.SceneInit();
        cameraController?.SceneInit();
        BindTrackedPlayerCollider();
        RefreshSceneOverlaps();
        ApplyFreezeAggregate();
        cameraTarget?.SnapToHero();
        cameraController?.SnapToTarget();
    }

    // Scene entry always frames the underlying hero/room/lock destination while the screen is
    // black. A deliberately persistent presentation request stays registered but is deselected, so
    // it re-enters through the ordinary PresentationEntered blend after the reveal instead of
    // silently overriding the hidden immediate positioning.
    private void ResetPresentationForSceneEntry()
    {
        selectedPresentationId = 0L;
        cameraController?.ClearPresentation();
    }

    public IEnumerator RebindAndPositionForSceneEntry(
        string destinationScene,
        float timeoutSeconds,
        System.Action<CameraSceneEntryReadiness> completed)
    {
        float deadline = Time.realtimeSinceStartup + Mathf.Max(0f, timeoutSeconds);
        string failureDetail;

        do
        {
            if (TryApplySceneEntryImmediate(out failureDetail))
            {
                LastSceneEntryReadiness = new CameraSceneEntryReadiness(
                    destinationScene,
                    true,
                    false,
                    "");
                completed?.Invoke(LastSceneEntryReadiness);
                yield break;
            }

            if (Time.realtimeSinceStartup >= deadline)
            {
                break;
            }

            // Scene activation normally creates every dependency synchronously. This yield only
            // permits late Awake/enable work to finish when an authored scene creates its hero
            // or collider one frame after activation; readiness is still dependency-driven.
            yield return null;
        }
        while (true);

        string fallbackDetail;
        bool fallbackApplied = TryApplySceneEntryFallback(out fallbackDetail);
        string detail = fallbackApplied
            ? $"{failureDetail} Applied direct hero-position fallback."
            : $"{failureDetail} Direct fallback also failed: {fallbackDetail}";

        Debug.LogError(
            $"[GameCameras] Camera readiness failed for destination scene '{destinationScene}': {detail}",
            this);

        LastSceneEntryReadiness = new CameraSceneEntryReadiness(
            destinationScene,
            false,
            fallbackApplied,
            detail);
        completed?.Invoke(LastSceneEntryReadiness);
    }

    public CameraRequestHandle FreezeForSceneTransition(object source)
    {
        return AcquireFreezeInternal(
            CameraFreezeKind.Hard,
            -1f,
            source,
            CameraRequestLifetime.Persistent,
            true);
    }

    public IEnumerator FadeOut(float duration = -1f)
    {
        if (fade == null)
        {
            Debug.LogWarning("[GameCameras] FadeOut requested, but no CameraFade is assigned. Continuing without a screen fade.", this);
            yield break;
        }

        yield return StartCoroutine(fade.FadeOut(duration));
    }

    public IEnumerator FadeOut(FadeProfile profile)
    {
        if (fade == null)
        {
            Debug.LogWarning("[GameCameras] FadeOut requested, but no CameraFade is assigned. Continuing without a screen fade.", this);
            yield break;
        }

        yield return StartCoroutine(fade.FadeOut(profile));
    }

    public IEnumerator FadeIn(float duration = -1f)
    {
        if (fade == null)
        {
            Debug.LogWarning("[GameCameras] FadeIn requested, but no CameraFade is assigned. Continuing without a screen fade.", this);
            yield break;
        }

        yield return StartCoroutine(fade.FadeIn(duration));
    }

    public IEnumerator FadeIn(FadeProfile profile)
    {
        if (fade == null)
        {
            Debug.LogWarning("[GameCameras] FadeIn requested, but no CameraFade is assigned. Continuing without a screen fade.", this);
            yield break;
        }

        yield return StartCoroutine(fade.FadeIn(profile));
    }

    public void SetBlack()
    {
        if (fade == null)
        {
            Debug.LogWarning("[GameCameras] SetBlack requested, but no CameraFade is assigned. Continuing without a screen fade.", this);
            return;
        }

        fade.SetBlack();
    }

    public void SetClear()
    {
        if (fade == null)
        {
            return;
        }

        fade.SetClear();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisableExternalAudioListeners();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        cameraController?.ClearSceneRegistrations(scene);
        freezeRegistrations.RemoveAll(registration =>
            registration.Lifetime == CameraRequestLifetime.Scene
            && registration.SourceSceneHandle == scene.handle);
        ApplyFreezeAggregate();

        int removedPresentations = presentationRegistrations.RemoveAll(registration =>
            registration.Lifetime == CameraRequestLifetime.Scene
            && registration.SourceSceneHandle == scene.handle);
        if (removedPresentations > 0)
        {
            ApplyPresentationAggregate();
        }
    }

    private void DisableExternalAudioListeners()
    {
        AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        bool hasOwnEnabledListener = false;

        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null && listener.enabled && IsListenerInOwnHierarchy(listener))
            {
                hasOwnEnabledListener = true;
                break;
            }
        }

        if (!hasOwnEnabledListener)
        {
            return;
        }

        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener != null && listener.enabled && !IsListenerInOwnHierarchy(listener))
            {
                listener.enabled = false;
            }
        }
    }

    private bool IsListenerInOwnHierarchy(AudioListener listener)
    {
        return listener != null && listener.transform.IsChildOf(transform);
    }

    private void OnCameraLockEntered(CameraLockArea area)
    {
        cameraController?.EnterLockArea(area);
    }

    private void OnCameraLockExited(CameraLockArea area)
    {
        cameraController?.ExitLockArea(area);
    }

    private void OnCameraOffsetEntered(CameraOffsetArea area)
    {
        cameraTarget?.AddOffsetArea(area);
    }

    private void OnCameraOffsetExited(CameraOffsetArea area)
    {
        cameraTarget?.RemoveOffsetArea(area);
    }

    private void OnCameraFadeRequested(CameraFadeRequest request)
    {
        if (fade == null)
        {
            Debug.LogWarning("[GameCameras] Camera fade request ignored because no CameraFade is assigned.", this);
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(request.Direction == CameraFadeDirection.Out
            ? fade.FadeOut(request.Duration)
            : fade.FadeIn(request.Duration));
    }

    private void OnCameraShakeRequested(CameraShakeRequest request)
    {
        if (shakeCues == null)
        {
            return;
        }

        if (request.Profile != null)
        {
            shakeCues.Shake(request.Profile, request.WorldPosition, request.IntensityMultiplier);
            return;
        }

        shakeCues.Shake(request.Intensity, request.WorldPosition, request.IntensityMultiplier);
    }

    private void OnCameraShakeCancelRequested(object source)
    {
        shakeCues?.Cancel(source);
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;

        if (freezeRegistrations.Count > 0)
        {
            int removed = freezeRegistrations.RemoveAll(registration =>
                IsDestroyedUnitySource(registration.Source)
                || (registration.ExpiresAt > 0f && now >= registration.ExpiresAt));

            if (removed > 0)
            {
                ApplyFreezeAggregate();
            }
        }

        if (presentationRegistrations.Count > 0)
        {
            // Requests are pruned rather than left unresolvable: a destroyed source, an expired
            // duration, or the loss of every valid target must hand the camera back to the next
            // best request (or the current underlying framing), never to a stale focus point.
            int removed = presentationRegistrations.RemoveAll(registration =>
                IsDestroyedUnitySource(registration.Source)
                || (registration.ExpiresAt > 0f && now >= registration.ExpiresAt)
                || !IsResolvable(registration));

            if (removed > 0)
            {
                ApplyPresentationAggregate();
            }
        }

        // Target transforms move every frame, so the selected request is always re-pushed.
        RefreshSelectedPresentation();
    }

    public CameraPresentationHandle AcquirePresentation(
        in CameraPresentationSettings settings,
        Transform[] targets,
        object source,
        CameraRequestLifetime lifetime = CameraRequestLifetime.Scene,
        int priority = 0,
        float duration = -1f)
    {
        // A request that cannot resolve a focus is never registered, so it can neither win
        // selection nor sit in the registry until the next prune.
        if (settings.RequiresTargets && CountValidTargets(targets) == 0)
        {
            return default;
        }

        if (nextPresentationRequestId == long.MaxValue)
        {
            if (presentationRegistrations.Count > 0)
            {
                Debug.LogError("[GameCameras] Camera presentation request ID space was exhausted while requests are still active.", this);
                return default;
            }

            nextPresentationRequestId = 0L;
        }

        EnsurePresentationSequenceCapacity();

        long id = ++nextPresentationRequestId;
        presentationRegistrations.Add(new PresentationRegistration
        {
            Id = id,
            Source = source,
            Lifetime = lifetime,
            SourceSceneHandle = ResolveSourceSceneHandle(source),
            ExpiresAt = duration > 0f ? Time.realtimeSinceStartup + duration : -1f,
            Priority = priority,
            Sequence = ++nextPresentationSequence,
            Settings = settings,
            Targets = CopyTargets(targets)
        });

        ApplyPresentationAggregate();
        return new CameraPresentationHandle(this, id);
    }

    internal bool UpdatePresentation(
        long requestId,
        in CameraPresentationSettings settings,
        Transform[] targets,
        bool replaceTargets,
        int priority,
        bool applyPriority)
    {
        PresentationRegistration registration = FindPresentation(requestId);
        if (registration == null)
        {
            return false;
        }

        registration.Settings = settings;
        if (replaceTargets)
        {
            registration.Targets = CopyTargets(targets);
        }

        bool priorityChanged = applyPriority && registration.Priority != priority;
        if (priorityChanged)
        {
            registration.Priority = priority;
        }

        // Re-authoring never changes registration order, so an in-place update cannot steal or
        // lose selection against an equal-priority request registered later.
        if (!IsResolvable(registration))
        {
            presentationRegistrations.Remove(registration);
            ApplyPresentationAggregate();
            return false;
        }

        if (priorityChanged)
        {
            // Priority participates in selection, so a changed priority has to re-resolve.
            ApplyPresentationAggregate();
            return true;
        }

        if (registration.Id == selectedPresentationId)
        {
            PushSelectedPresentation(registration, CameraTransitionCause.None);
        }

        return true;
    }

    internal void ReleasePresentation(long requestId)
    {
        int removed = presentationRegistrations.RemoveAll(registration => registration.Id == requestId);
        if (removed > 0)
        {
            ApplyPresentationAggregate();
        }
    }

    internal bool IsPresentationRegistered(long requestId)
    {
        return FindPresentation(requestId) != null;
    }

    public IReadOnlyList<CameraPresentationSnapshot> GetPresentationSnapshots()
    {
        List<CameraPresentationSnapshot> snapshots =
            new List<CameraPresentationSnapshot>(presentationRegistrations.Count);
        float now = Time.realtimeSinceStartup;

        for (int i = 0; i < presentationRegistrations.Count; i++)
        {
            PresentationRegistration registration = presentationRegistrations[i];
            float remaining = registration.ExpiresAt > 0f
                ? Mathf.Max(0f, registration.ExpiresAt - now)
                : -1f;

            snapshots.Add(new CameraPresentationSnapshot(
                registration.Id,
                registration.Priority,
                registration.Settings.mode,
                DescribeSource(registration.Source),
                registration.Lifetime,
                remaining,
                CountValidTargets(registration.Targets),
                DescribeTargets(registration.Targets),
                registration.Id == selectedPresentationId,
                registration.Settings.ResolvedWeight));
        }

        return snapshots;
    }

    private PresentationRegistration FindPresentation(long requestId)
    {
        for (int i = 0; i < presentationRegistrations.Count; i++)
        {
            if (presentationRegistrations[i].Id == requestId)
            {
                return presentationRegistrations[i];
            }
        }

        return null;
    }

    private PresentationRegistration GetSelectedPresentation()
    {
        PresentationRegistration best = null;
        for (int i = 0; i < presentationRegistrations.Count; i++)
        {
            PresentationRegistration candidate = presentationRegistrations[i];
            if (best == null
                || candidate.Priority > best.Priority
                || (candidate.Priority == best.Priority && candidate.Sequence > best.Sequence))
            {
                best = candidate;
            }
        }

        return best;
    }

    private void ApplyPresentationAggregate()
    {
        PresentationRegistration selected = GetSelectedPresentation();
        long previousId = selectedPresentationId;

        if (selected == null)
        {
            selectedPresentationId = 0L;
            if (previousId != 0L)
            {
                cameraController?.ClearPresentation();
            }

            return;
        }

        selectedPresentationId = selected.Id;
        CameraTransitionCause cause = previousId == 0L
            ? CameraTransitionCause.PresentationEntered
            : previousId == selected.Id
                ? CameraTransitionCause.None
                : CameraTransitionCause.PresentationChanged;

        PushSelectedPresentation(selected, cause);
    }

    private void RefreshSelectedPresentation()
    {
        if (selectedPresentationId == 0L)
        {
            return;
        }

        PresentationRegistration selected = FindPresentation(selectedPresentationId);
        if (selected == null)
        {
            ApplyPresentationAggregate();
            return;
        }

        PushSelectedPresentation(selected, CameraTransitionCause.None);
    }

    private void PushSelectedPresentation(PresentationRegistration registration, CameraTransitionCause cause)
    {
        cameraController?.ApplyPresentation(
            registration.Id,
            registration.Priority,
            registration.Settings,
            registration.Targets,
            DescribeSource(registration.Source),
            cause);
    }

    private void ClearPresentationRegistrations()
    {
        presentationRegistrations.Clear();
        selectedPresentationId = 0L;
        cameraController?.ClearPresentation();
    }

    private void EnsurePresentationSequenceCapacity()
    {
        if (nextPresentationSequence < long.MaxValue)
        {
            return;
        }

        presentationRegistrations.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
        for (int i = 0; i < presentationRegistrations.Count; i++)
        {
            presentationRegistrations[i].Sequence = i + 1L;
        }

        nextPresentationSequence = presentationRegistrations.Count;
    }

    private static Transform[] CopyTargets(Transform[] targets)
    {
        if (targets == null || targets.Length == 0)
        {
            return System.Array.Empty<Transform>();
        }

        Transform[] copy = new Transform[targets.Length];
        System.Array.Copy(targets, copy, targets.Length);
        return copy;
    }

    private static bool IsResolvable(PresentationRegistration registration)
    {
        return !registration.Settings.RequiresTargets || CountValidTargets(registration.Targets) > 0;
    }

    private static int CountValidTargets(Transform[] targets)
    {
        if (targets == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static string DescribeTargets(Transform[] targets)
    {
        if (targets == null || targets.Length == 0)
        {
            return "(none)";
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(targets[i].name);
        }

        return builder.Length > 0 ? builder.ToString() : "(all destroyed)";
    }

    public CameraRequestHandle AcquireFreeze(
        CameraFreezeKind kind,
        float duration,
        object source,
        CameraRequestLifetime lifetime)
    {
        return AcquireFreezeInternal(kind, duration, source, lifetime, false);
    }

    private CameraRequestHandle AcquireFreezeInternal(
        CameraFreezeKind kind,
        float duration,
        object source,
        CameraRequestLifetime lifetime,
        bool suppressFinalReleaseTransition)
    {
        if (nextFreezeRequestId == long.MaxValue)
        {
            if (freezeRegistrations.Count > 0)
            {
                Debug.LogError("[GameCameras] Camera freeze request ID space was exhausted while requests are still active.", this);
                return default;
            }

            nextFreezeRequestId = 0L;
        }

        long id = ++nextFreezeRequestId;
        freezeRegistrations.Add(new FreezeRegistration
        {
            Id = id,
            Kind = kind,
            Source = source,
            Lifetime = lifetime,
            SourceSceneHandle = ResolveSourceSceneHandle(source),
            ExpiresAt = duration > 0f ? Time.realtimeSinceStartup + duration : -1f,
            SuppressFinalReleaseTransition = suppressFinalReleaseTransition
        });
        ApplyFreezeAggregate();
        return new CameraRequestHandle(this, id);
    }

    internal void ReleaseFreeze(long requestId)
    {
        bool suppressFinalReleaseTransition = false;
        int removed = freezeRegistrations.RemoveAll(registration =>
        {
            if (registration.Id != requestId)
            {
                return false;
            }

            suppressFinalReleaseTransition |= registration.SuppressFinalReleaseTransition;
            return true;
        });
        if (removed > 0)
        {
            ApplyFreezeAggregate(suppressFinalReleaseTransition && freezeRegistrations.Count == 0);
        }
    }

    public IReadOnlyList<CameraFreezeSnapshot> GetFreezeSnapshots()
    {
        List<CameraFreezeSnapshot> snapshots = new List<CameraFreezeSnapshot>(freezeRegistrations.Count);
        float now = Time.realtimeSinceStartup;
        for (int i = 0; i < freezeRegistrations.Count; i++)
        {
            FreezeRegistration registration = freezeRegistrations[i];
            float remaining = registration.ExpiresAt > 0f ? Mathf.Max(0f, registration.ExpiresAt - now) : -1f;
            snapshots.Add(new CameraFreezeSnapshot(registration.Kind, DescribeSource(registration.Source), remaining));
        }

        return snapshots;
    }

    private static string DescribeSource(object source)
    {
        if (source == null)
        {
            return "(none)";
        }

        if (source is Object unityObject)
        {
            return unityObject == null ? "(destroyed)" : unityObject.name;
        }

        return source.ToString();
    }

    public void RefreshOverlapFor(CameraLockArea area)
    {
        if (area != null && trackedPlayerCollider != null && area.Overlaps(trackedPlayerCollider))
        {
            cameraController?.EnterLockArea(area);
        }
    }

    public void RefreshOverlapFor(CameraBoundsVolume volume)
    {
        if (volume != null && trackedPlayerCollider != null && volume.Overlaps(trackedPlayerCollider))
        {
            cameraController?.SetBoundsVolume(volume);
        }
    }

    public void RefreshSceneOverlaps()
    {
        if (trackedPlayerCollider == null)
        {
            return;
        }

        IReadOnlyList<CameraBoundsVolume> volumes = CameraBoundsVolume.ActiveVolumes;
        for (int i = 0; i < volumes.Count; i++)
        {
            RefreshOverlapFor(volumes[i]);
        }

        IReadOnlyList<CameraLockArea> areas = CameraLockArea.ActiveAreas;
        for (int i = 0; i < areas.Count; i++)
        {
            RefreshOverlapFor(areas[i]);
        }
    }

    // The canonical player collider is the Collider2D attached directly to the Player-tagged hero
    // root GameObject -- never a child hurtbox (Herobox) or a temporary child hitbox (attack
    // modules) -- so identity is a single deterministic lookup rather than a hierarchy traversal
    // that could resolve to whichever child collider GetComponentInChildren happens to visit
    // first. A hero root without its own collider has no canonical identity; camera volumes stay
    // unregistered until one is authored there rather than guessing a child collider.
    private void BindTrackedPlayerCollider()
    {
        trackedPlayerCollider = null;
        GameObject hero = GameObject.FindWithTag("Player");
        if (hero == null)
        {
            return;
        }

        trackedPlayerCollider = hero.GetComponent<Collider2D>();
        if (trackedPlayerCollider == null)
        {
            Debug.LogWarning(
                $"[GameCameras] Player-tagged hero '{hero.name}' has no Collider2D on its root GameObject. Camera volumes will not recognise this hero until one is added there.",
                this);
        }
    }

    private void ApplyFreezeAggregate()
    {
        ApplyFreezeAggregate(false);
    }

    private void ApplyFreezeAggregate(bool suppressReleaseTransition)
    {
        bool anyFreeze = freezeRegistrations.Count > 0;
        bool anyHardFreeze = false;
        for (int i = 0; i < freezeRegistrations.Count; i++)
        {
            if (freezeRegistrations[i].Kind == CameraFreezeKind.Hard)
            {
                anyHardFreeze = true;
                break;
            }
        }

        cameraController?.ApplyFreezeState(anyFreeze, anyHardFreeze, suppressReleaseTransition);
    }

    private void ClearFreezeRegistrations()
    {
        freezeRegistrations.Clear();
        cameraController?.ApplyFreezeState(false, false);
    }

    private bool TryApplySceneEntryImmediate(out string failureDetail)
    {
        failureDetail = "";
        if (cameraTarget == null)
        {
            failureDetail = "GameCameras has no CameraTarget reference.";
            return false;
        }

        if (cameraController == null)
        {
            failureDetail = "GameCameras has no CameraController reference.";
            return false;
        }

        GameObject hero = GameObject.FindWithTag("Player");
        if (hero == null)
        {
            failureDetail = "No active GameObject tagged 'Player' exists.";
            return false;
        }

        BindTrackedPlayerCollider();
        if (trackedPlayerCollider == null)
        {
            failureDetail = $"Positioned hero '{hero.name}' has no enabled Collider2D for camera-volume readiness.";
            return false;
        }

        // Teleport placement changes overlap geometry without waiting for a physics step.
        // Synchronising here lets the existing idempotent overlap registries resolve bounds and
        // ordinary locks in the same hidden frame, so no arbitrary FixedUpdate delay is needed.
        Physics2D.SyncTransforms();
        ResetPresentationForSceneEntry();
        cameraTarget.SceneInit(false);
        cameraController.SceneInit();
        BindTrackedPlayerCollider();
        RefreshSceneOverlaps();
        ApplyFreezeAggregate();
        cameraTarget.SnapToHero();

        if (!cameraController.ApplySceneEntryImmediate(out failureDetail))
        {
            return false;
        }

        return cameraTarget.HasHeroBinding
            && cameraTarget.BoundHero == hero.transform;
    }

    private bool TryApplySceneEntryFallback(out string failureDetail)
    {
        GameObject hero = GameObject.FindWithTag("Player");
        if (hero == null)
        {
            failureDetail = "No positioned hero was available for a direct camera fallback.";
            return false;
        }

        if (cameraTarget != null)
        {
            cameraTarget.SceneInit(false);
            cameraTarget.SnapToHero();
        }

        if (cameraController == null)
        {
            failureDetail = "No CameraController was available for a direct camera fallback.";
            return false;
        }

        return cameraController.ApplySceneEntryFallback(hero.transform.position, out failureDetail);
    }

    private static bool IsDestroyedUnitySource(object source)
    {
        return source is Object unityObject && unityObject == null;
    }

    private static int ResolveSourceSceneHandle(object source)
    {
        if (source is Component component)
        {
            return component.gameObject.scene.handle;
        }

        if (source is GameObject gameObject)
        {
            return gameObject.scene.handle;
        }

        return SceneManager.GetActiveScene().handle;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (fade == null)
            Debug.LogWarning("[GameCameras] No CameraFade assigned. Scene transitions will continue, but no screen fade will be shown.", this);
    }
#endif
}
