using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; }

    // Fired after every scene load, before anything scene-local has run its Start.
    public event Action<Scene> SceneInit;

    private Coroutine _hitStopCoroutine;
    private HeroController _hero;
    private HeroHealthComponent _heroHealth;

    private RespawnMarker _activeRespawnMarker;
    private bool _placeHeroAtSavedRespawnOnNextSceneLoad;
    private bool _respawnOrRecoveryInProgress;
    private bool _isTransitioning;
    private bool _hasPendingNormalDeathRespawn;
    private string _pendingNormalDeathRespawnScene = "";
    private string _pendingNormalDeathRespawnMarkerKey = "";
    // Cached on scene load after any initial hero placement. Used as emergency fallback when
    // no RespawnMarker or HazardRespawnMarker exists, to avoid leaving the hero in the hazard.
    private Vector3 _sceneFallbackPosition;

    public bool IsRespawnOrRecoveryInProgress => _respawnOrRecoveryInProgress;

    public void RequestSavedRespawnPlacementOnNextSceneLoad()
    {
        _placeHeroAtSavedRespawnOnNextSceneLoad = true;
    }

    public void SetActiveRespawnMarker(RespawnMarker marker)
    {
        _activeRespawnMarker = marker;
        string sceneName = marker != null ? marker.gameObject.scene.name : "";
        SaveManager.Instance?.SetActiveRespawnPoint(sceneName, marker?.Key);
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
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // -------------------------------------------------------------------------
    // Scene transitions
    // -------------------------------------------------------------------------

    public bool BeginSceneTransition(string targetScene, string destinationPassageGuid = "")
    {
        if (_isTransitioning)
        {
            Debug.LogWarning($"[GameManager] Ignoring BeginSceneTransition('{targetScene}') — a transition is already in progress.");
            return false;
        }

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError("[GameManager] BeginSceneTransition called with empty targetScene; rejected.");
            return false;
        }

        if (!IsSceneLoadable(targetScene))
        {
            Debug.LogError($"[GameManager] Scene '{targetScene}' is not in Build Settings; rejected.");
            return false;
        }

        _isTransitioning = true;
        StartCoroutine(TransitionRoutine(targetScene, destinationPassageGuid));
        return true;
    }

    private IEnumerator TransitionRoutine(string targetScene, string destinationPassageGuid)
    {
        HeroController lockedHero = null;
        bool controlLockAdded = false;
        HeroController fallbackLockedHero = null;
        bool fallbackControlLockAdded = false;
        bool completed = false;

        try
        {
            State = GameState.Loading;
            CameraShakeRequester.ShakeStop();

            // Lock the outgoing hero so input is ignored during fade-out, even though
            // the hero will be destroyed when the scene unloads.
            if (_hero != null)
            {
                lockedHero = _hero;
                lockedHero.AddControlLock(this);
                controlLockAdded = true;
            }

            // Grant i-frames covering the fade-out + load + entry-motion window so
            // an enemy or hazard hit on the same frame as the transition trigger
            // cannot stall the sequence by routing through hurt / hazard recovery.
            // Duration is intentionally generous; the timer expires harmlessly if
            // the transition finishes early. The outgoing hero is destroyed mid-window
            // when the scene unloads, so this only matters during fade-out.
            if (_heroHealth != null) _heroHealth.GrantTemporaryInvincibility(this, 3f);

            if (GameCameras.Instance != null)
                yield return StartCoroutine(GameCameras.Instance.FadeOut());

            AsyncOperation load = SceneManager.LoadSceneAsync(targetScene);
            while (!load.isDone)
                yield return null;
            // OnSceneLoaded fires SceneInit, then repopulates _hero and resolves saved respawn marker.

            State = GameState.EnteringLevel;

            if (GameCameras.Instance != null)
                GameCameras.Instance.SetBlack();

            if (_hasPendingNormalDeathRespawn)
            {
                CompletePendingNormalDeathRespawn(targetScene);

                if (GameCameras.Instance != null)
                    GameCameras.Instance.RebindForSceneEntry();

                yield return new WaitForSecondsRealtime(0.1f);

                if (GameCameras.Instance != null)
                    yield return StartCoroutine(GameCameras.Instance.FadeIn());

                State = GameState.Playing;
                completed = true;
                yield break;
            }

            // Phase A — placement: resolve destination gate, place hero, lock control.
            // Synchronous; happens behind the still-black screen.
            TransitionPoint dest = !string.IsNullOrEmpty(destinationPassageGuid)
                ? TransitionPoint.FindByPassageGuid(destinationPassageGuid)
                : null;

            if (dest == null && !string.IsNullOrEmpty(destinationPassageGuid))
            {
                Debug.LogError($"[GameManager] No TransitionPoint with passage GUID '{destinationPassageGuid}' found in '{SceneManager.GetActiveScene().name}'. Using scene fallback placement.");
                PlaceHeroAtMissingPassageFallback(destinationPassageGuid);
                if (_hero != null)
                {
                    fallbackLockedHero = _hero;
                    fallbackLockedHero.AddControlLock(this);
                    fallbackControlLockAdded = true;
                }
            }

            if (dest != null && _hero != null)
            {
                _hero.BeginSceneEntryPlacement(dest);
                // Refresh fallback so a post-entry hazard returns the hero to the
                // gate-placed position, not the scene's authored hero position.
                _sceneFallbackPosition = _hero.transform.position;
            }

            // Rebind camera to the placed hero position.
            if (GameCameras.Instance != null)
                GameCameras.Instance.RebindForSceneEntry();

            yield return new WaitForSecondsRealtime(0.1f);

            // Phase B+C — fade-in and entry motion start simultaneously so the scene
            // is revealed while the hero is already walking in from the gate.
            // We fire the fade non-blocking, run the motion to completion, then await
            // the fade at the tail in case it outlasts the motion.
            Coroutine entryFadeIn = null;
            if (GameCameras.Instance != null)
                entryFadeIn = StartCoroutine(GameCameras.Instance.FadeIn());

            if (dest != null && _hero != null)
            {
                _hero.BeginSceneEntryMotion(dest);
                while (_hero != null && _hero.IsEnteringScene)
                    yield return null;
            }

            // Ensure the fade finishes even when entry motion is very short.
            if (entryFadeIn != null)
                yield return entryFadeIn;

            if (fallbackControlLockAdded && fallbackLockedHero != null)
            {
                fallbackLockedHero.RemoveControlLock(this);
                fallbackControlLockAdded = false;
            }

            // Phase D — hand control back. HeroSceneEntry's finally has removed its own
            // control lock; GameManager promotes state to Playing.
            State = GameState.Playing;
            completed = true;
        }
        finally
        {
            if (!completed && controlLockAdded && lockedHero != null && lockedHero == _hero)
                lockedHero.RemoveControlLock(this);

            if (fallbackControlLockAdded && fallbackLockedHero != null)
                fallbackLockedHero.RemoveControlLock(this);

            if (!completed && (State == GameState.Loading || State == GameState.EnteringLevel || State == GameState.ExitingLevel))
                State = GameState.Playing;

            ClearPendingNormalDeathRespawn();
            _isTransitioning = false;
        }
    }

    private void PlaceHeroAtMissingPassageFallback(string missingPassageGuid)
    {
        if (_hero == null)
            return;

        RespawnMarker[] markers = FindObjectsByType<RespawnMarker>(FindObjectsSortMode.InstanceID);
        if (markers.Length > 0)
        {
            RespawnMarker marker = markers[0];
            _hero.transform.position = marker.RespawnPosition;
            _hero.ForceFacingDirection(marker.FacingDirection);
            _sceneFallbackPosition = marker.RespawnPosition;
            Debug.LogError($"[GameManager] Missing destination passage GUID '{missingPassageGuid}'. Placed hero at fallback RespawnMarker '{marker.Key}'.", marker);
            return;
        }

        _sceneFallbackPosition = _hero.transform.position;
        Debug.LogError($"[GameManager] Missing destination passage GUID '{missingPassageGuid}' and no RespawnMarker exists in scene '{SceneManager.GetActiveScene().name}'. Hero remains at authored scene position.");
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneInit?.Invoke(scene);

        // Cache the hero for this scene
        _hero       = FindFirstObjectByType<HeroController>();
        _heroHealth = _hero != null ? _hero.GetComponent<HeroHealthComponent>() : null;

        ResolveActiveRespawnMarkerFromSave();
        PlaceHeroAtSavedRespawnIfRequested();

        // Cache after any initial placement so the fallback is a known-safe starting position.
        _sceneFallbackPosition = _hero != null ? _hero.transform.position : Vector3.zero;
    }

    private void ResolveActiveRespawnMarkerFromSave()
    {
        string key = SaveManager.Instance?.ActiveRespawnMarkerKey;
        if (string.IsNullOrEmpty(key)) return;

        string respawnScene = SaveManager.Instance?.ActiveRespawnSceneName;
        if (!string.IsNullOrEmpty(respawnScene) && respawnScene != SceneManager.GetActiveScene().name)
        {
            _activeRespawnMarker = null;
            return;
        }

        RespawnMarker[] markers = FindObjectsByType<RespawnMarker>(FindObjectsSortMode.None);
        RespawnMarker match = null;

        foreach (RespawnMarker marker in markers)
        {
            if (marker.Key != key) continue;

            if (match != null)
            {
                Debug.LogWarning($"[GameManager] Multiple RespawnMarkers with key '{key}' found in scene '{SceneManager.GetActiveScene().name}'. Using first match.");
                break;
            }

            match = marker;
        }

        if (match != null)
        {
            _activeRespawnMarker = match;
            Debug.Log($"[GameManager] Restored active respawn marker: {key}");
        }
        else
        {
            Debug.LogWarning($"[GameManager] No RespawnMarker with key '{key}' found in checkpoint scene '{SceneManager.GetActiveScene().name}'. In-session respawn will use a fallback marker.");
        }
    }

    private void PlaceHeroAtSavedRespawnIfRequested()
    {
        if (!_placeHeroAtSavedRespawnOnNextSceneLoad) return;
        _placeHeroAtSavedRespawnOnNextSceneLoad = false;

        if (_activeRespawnMarker == null)
        {
            string key = SaveManager.Instance?.ActiveRespawnMarkerKey;
            string respawnScene = SaveManager.Instance?.ActiveRespawnSceneName;
            bool loadedCheckpointScene = string.IsNullOrEmpty(respawnScene)
                || respawnScene == SceneManager.GetActiveScene().name;

            if (!string.IsNullOrEmpty(key) && loadedCheckpointScene)
                Debug.LogWarning($"[GameManager] Cannot place hero at saved respawn marker '{key}' — marker was not resolved. Hero remains at authored position.");
            return;
        }

        if (_hero == null) return;

        _hero.transform.position = _activeRespawnMarker.RespawnPosition;
        _hero.ForceFacingDirection(_activeRespawnMarker.FacingDirection);
        Debug.Log($"[GameManager] Placed hero at saved respawn marker: {_activeRespawnMarker.Key}");
    }

    // -------------------------------------------------------------------------
    // Respawn
    // -------------------------------------------------------------------------

    public void BeginRespawnSequence()
    {
        if (_respawnOrRecoveryInProgress || _isTransitioning)
        {
            return;
        }

        _respawnOrRecoveryInProgress = true;

        string currentScene = SceneManager.GetActiveScene().name;
        string respawnScene = SaveManager.Instance?.ActiveRespawnSceneName ?? "";
        string markerKey = SaveManager.Instance?.ActiveRespawnMarkerKey ?? "";

        if (string.IsNullOrEmpty(respawnScene))
        {
            respawnScene = currentScene;
        }

        if (respawnScene != currentScene)
        {
            if (IsSceneLoadable(respawnScene))
            {
                _hasPendingNormalDeathRespawn = true;
                _pendingNormalDeathRespawnScene = respawnScene;
                _pendingNormalDeathRespawnMarkerKey = markerKey;

                if (BeginSceneTransition(respawnScene))
                    return;

                ClearPendingNormalDeathRespawn();
                Debug.LogWarning($"[GameManager] Cross-scene respawn transition to '{respawnScene}' could not start. Falling back to current scene respawn.");
            }
            else
            {
                Debug.LogWarning($"[GameManager] Saved respawn scene '{respawnScene}' is not loadable. Falling back to current scene respawn.");
            }
        }

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        bool completed = false;
        try
        {
            CameraShakeRequester.ShakeStop();

            if (GameCameras.Instance != null)
                yield return StartCoroutine(GameCameras.Instance.FadeOut());

            RespawnMarker marker = IsActiveRespawnInCurrentScene()
                ? _activeRespawnMarker
                : ResolveRespawnMarkerInLoadedScene(SaveManager.Instance?.ActiveRespawnMarkerKey);
            if (marker == null)
                marker = ResolveRespawnFallbackMarkerInLoadedScene();

            ApplyNormalDeathRespawn(marker, SceneManager.GetActiveScene().name);

            if (GameCameras.Instance != null)
            {
                GameCameras.Instance.Target.SnapToHero();
                GameCameras.Instance.Controller.SnapToTarget();
            }

            yield return new WaitForSecondsRealtime(0.1f);

            if (GameCameras.Instance != null)
                yield return StartCoroutine(GameCameras.Instance.FadeIn());

            completed = true;
        }
        finally
        {
            _respawnOrRecoveryInProgress = false;
            if (!completed && (State == GameState.Loading || State == GameState.EnteringLevel || State == GameState.ExitingLevel))
                State = GameState.Playing;
        }
    }

    private void CompletePendingNormalDeathRespawn(string loadedScene)
    {
        if (!string.IsNullOrEmpty(_pendingNormalDeathRespawnScene) && loadedScene != _pendingNormalDeathRespawnScene)
        {
            Debug.LogWarning($"[GameManager] Pending respawn expected scene '{_pendingNormalDeathRespawnScene}', but '{loadedScene}' loaded. Resolving in the loaded scene.");
        }

        string markerKey = _pendingNormalDeathRespawnMarkerKey;
        RespawnMarker marker = ResolveRespawnMarkerInLoadedScene(markerKey);
        if (marker == null && !string.IsNullOrEmpty(markerKey))
        {
            Debug.LogWarning($"[GameManager] RespawnMarker '{markerKey}' was not found in checkpoint scene '{loadedScene}'. Using first available RespawnMarker.");
        }

        if (marker == null)
        {
            marker = ResolveRespawnFallbackMarkerInLoadedScene();
        }

        ApplyNormalDeathRespawn(marker, loadedScene);
    }

    private void ApplyNormalDeathRespawn(RespawnMarker marker, string sceneName)
    {
        if (_hero != null)
        {
            if (marker != null)
            {
                _hero.transform.position = marker.RespawnPosition;
                _hero.ForceFacingDirection(marker.FacingDirection);
                _activeRespawnMarker = marker;
                _sceneFallbackPosition = marker.RespawnPosition;
            }
            else
            {
                Debug.LogError($"[GameManager] No RespawnMarker found in scene '{sceneName}'. Falling back to cached scene entry position.");
                _hero.transform.position = _sceneFallbackPosition;
            }

        }

        if (_heroHealth != null)
        {
            _heroHealth.RestoreFullHealth();
            _heroHealth.GrantDefaultInvincibility(this);
        }

        if (_hero != null)
            _hero.ResetAfterRespawn();

        SaveManager.Instance?.SetCurrentScene(sceneName);
    }

    private void ClearPendingNormalDeathRespawn()
    {
        _hasPendingNormalDeathRespawn = false;
        _pendingNormalDeathRespawnScene = "";
        _pendingNormalDeathRespawnMarkerKey = "";
        if (_respawnOrRecoveryInProgress)
            _respawnOrRecoveryInProgress = false;
    }

    private bool IsSceneLoadable(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
    }

    private RespawnMarker ResolveRespawnMarkerInLoadedScene(string markerKey)
    {
        if (string.IsNullOrEmpty(markerKey))
            return null;

        RespawnMarker[] markers = FindObjectsByType<RespawnMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        RespawnMarker match = null;
        foreach (RespawnMarker marker in markers)
        {
            if (marker == null || marker.Key != markerKey)
                continue;

            if (match != null)
            {
                Debug.LogWarning($"[GameManager] Multiple RespawnMarkers with key '{markerKey}' found in scene '{SceneManager.GetActiveScene().name}'. Using first match.");
                break;
            }

            match = marker;
        }

        return match;
    }

    private RespawnMarker ResolveRespawnFallbackMarkerInLoadedScene()
    {
        RespawnMarker[] markers = FindObjectsByType<RespawnMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (markers.Length == 0)
            return null;
        return markers[0];
    }

    private bool IsActiveRespawnInCurrentScene()
    {
        if (_activeRespawnMarker == null)
            return false;

        string key = SaveManager.Instance?.ActiveRespawnMarkerKey ?? "";
        if (string.IsNullOrEmpty(key) || _activeRespawnMarker.Key != key)
            return false;

        return _activeRespawnMarker.gameObject.scene == SceneManager.GetActiveScene();
    }

    public void BeginHazardRecoverySequence(HazardContact contact)
    {
        if (_respawnOrRecoveryInProgress)
            return;

        _respawnOrRecoveryInProgress = true;

        // Block enemy contact damage for the full recovery window immediately, before the
        // first coroutine yield, so no FixedUpdate flush can sneak damage through.
        float iFrames = contact.RecoveryProfile != null ? contact.RecoveryProfile.RecoveryIFrameDuration : 0.75f;
        if (_heroHealth != null && iFrames > 0f)
            _heroHealth.GrantTemporaryInvincibility(this, iFrames);

        StartCoroutine(HazardRecoveryRoutine(contact));
    }

    private IEnumerator HazardRecoveryRoutine(HazardContact contact)
    {
        HazardRecoveryProfile profile = contact.RecoveryProfile;
        HazardRespawnMarker   marker  = contact.RespawnMarker;
        float impactDelay     = profile != null ? profile.ImpactDelay     : 0.18f;
        float blackScreenHold = profile != null ? profile.BlackScreenHold : 0.1f;
        // Negative = let CameraFade use its own default; override only when profile specifies a value.
        float fadeOutDur      = profile != null ? profile.FadeOutDuration : -1f;
        float fadeInDur       = profile != null ? profile.FadeInDuration  : -1f;

        bool controlLockAdded = false;
        try
        {
            if (_hero != null)
            {
                _hero.AddControlLock(this);
                controlLockAdded = true;
            }

            if (impactDelay > 0f)
                yield return new WaitForSecondsRealtime(impactDelay);

            if (GameCameras.Instance != null)
                yield return StartCoroutine(GameCameras.Instance.FadeOut(fadeOutDur));

            CameraShakeRequester.ShakeStop();

            if (_hero != null)
            {
                if (marker != null)
                {
                    _hero.transform.position = marker.RespawnPosition;
                    _hero.ForceFacingDirection(marker.FacingDirection);
                }
                else
                {
                    Debug.LogWarning("[GameManager] Recoverable hazard had no HazardRespawnMarker assigned. Falling back to nearest RespawnMarker or cached scene entry position.");
                    _hero.transform.position = FindNearestRespawnMarkerPosition();
                }

                _hero.ResetAfterHazardRecovery();
            }

            if (GameCameras.Instance != null)
            {
                GameCameras.Instance.Target.SnapToHero();
                GameCameras.Instance.Controller.SnapToTarget();

                if (blackScreenHold > 0f)
                    yield return new WaitForSecondsRealtime(blackScreenHold);

                yield return StartCoroutine(GameCameras.Instance.FadeIn(fadeInDur));
            }

            if (controlLockAdded && _hero != null)
            {
                _hero.RemoveControlLock(this);
                controlLockAdded = false;
            }
        }
        finally
        {
            // Guaranteed cleanup: runs on normal completion, StopCoroutine, or uncaught exception.
            if (controlLockAdded && _hero != null)
                _hero.RemoveControlLock(this);
            _respawnOrRecoveryInProgress = false;
        }
    }

    private Vector3 FindNearestRespawnMarkerPosition()
    {
        RespawnMarker[] markers = FindObjectsByType<RespawnMarker>(FindObjectsSortMode.None);
        if (markers.Length == 0)
        {
            Debug.LogError("[GameManager] No RespawnMarker found in scene. Falling back to cached scene entry position. Add at least one RespawnMarker to scenes with hazards.");
            return _sceneFallbackPosition;
        }

        if (_hero == null)
            return markers[0].RespawnPosition;

        RespawnMarker nearest = markers[0];
        float bestDist = Vector3.SqrMagnitude(markers[0].transform.position - _hero.transform.position);
        for (int i = 1; i < markers.Length; i++)
        {
            float d = Vector3.SqrMagnitude(markers[i].transform.position - _hero.transform.position);
            if (d < bestDist)
            {
                bestDist = d;
                nearest = markers[i];
            }
        }
        return nearest.RespawnPosition;
    }

    // -------------------------------------------------------------------------
    // Pause / Unpause
    // -------------------------------------------------------------------------

    public void Pause()
    {
        if (State == GameState.Paused) return;

        State = GameState.Paused;
        Time.timeScale = 0f;
        if (_hero != null) _hero.AddControlLock(this);
    }

    public void Unpause()
    {
        if (State != GameState.Paused) return;

        State = GameState.Playing;
        Time.timeScale = 1f;
        if (_hero != null) _hero.RemoveControlLock(this);
    }

    // -------------------------------------------------------------------------
    // Hit stop
    // -------------------------------------------------------------------------

    public void HitStop(float duration)
    {
        if (State == GameState.Paused) return;

        if (_hitStopCoroutine != null)
            StopCoroutine(_hitStopCoroutine);

        _hitStopCoroutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);

        if (State != GameState.Paused)
            Time.timeScale = 1f;

        _hitStopCoroutine = null;
    }
}
