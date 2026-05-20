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

    public bool IsRespawnOrRecoveryInProgress => _respawnOrRecoveryInProgress;

    public void RequestSavedRespawnPlacementOnNextSceneLoad()
    {
        _placeHeroAtSavedRespawnOnNextSceneLoad = true;
    }

    public void SetActiveRespawnMarker(RespawnMarker marker)
    {
        _activeRespawnMarker = marker;
        SaveManager.Instance?.SetActiveRespawnMarkerKey(marker?.Key);
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

    public void BeginSceneTransition(string targetScene, string entryMarkerTag = "")
    {
        StartCoroutine(TransitionRoutine(targetScene, entryMarkerTag));
    }

    private IEnumerator TransitionRoutine(string targetScene, string entryMarkerTag)
    {
        State = GameState.Loading;
        CameraShakeRequester.ShakeStop();

        if (GameCameras.Instance != null)
            yield return StartCoroutine(GameCameras.Instance.Fade.FadeOut());

        AsyncOperation load = SceneManager.LoadSceneAsync(targetScene);
        while (!load.isDone)
            yield return null;

        // SceneInit fires inside OnSceneLoaded — camera and hero are repositioned there
        PositionHeroAtMarker(entryMarkerTag);

        if (GameCameras.Instance != null)
        {
            GameCameras.Instance.Target.SnapToHero();
            GameCameras.Instance.Controller.SnapToTarget();
        }

        yield return new WaitForSecondsRealtime(0.1f);

        if (GameCameras.Instance != null)
            yield return StartCoroutine(GameCameras.Instance.Fade.FadeIn());

        State = GameState.Playing;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneInit?.Invoke(scene);

        // Cache the hero for this scene
        _hero       = FindFirstObjectByType<HeroController>();
        _heroHealth = _hero != null ? _hero.GetComponent<HeroHealthComponent>() : null;

        ResolveActiveRespawnMarkerFromSave();
        PlaceHeroAtSavedRespawnIfRequested();
    }

    private void ResolveActiveRespawnMarkerFromSave()
    {
        string key = SaveManager.Instance?.ActiveRespawnMarkerKey;
        if (string.IsNullOrEmpty(key)) return;

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
            Debug.LogWarning($"[GameManager] No RespawnMarker with key '{key}' found in scene '{SceneManager.GetActiveScene().name}'. In-session respawn will fall back to nearest marker.");
        }
    }

    private void PlaceHeroAtSavedRespawnIfRequested()
    {
        if (!_placeHeroAtSavedRespawnOnNextSceneLoad) return;
        _placeHeroAtSavedRespawnOnNextSceneLoad = false;

        if (_activeRespawnMarker == null)
        {
            string key = SaveManager.Instance?.ActiveRespawnMarkerKey;
            if (!string.IsNullOrEmpty(key))
                Debug.LogWarning($"[GameManager] Cannot place hero at saved respawn marker '{key}' — marker was not resolved. Hero remains at authored position.");
            return;
        }

        if (_hero == null) return;

        _hero.transform.position = _activeRespawnMarker.RespawnPosition;
        _hero.ForceFacingDirection(_activeRespawnMarker.FacingDirection);
        Debug.Log($"[GameManager] Placed hero at saved respawn marker: {_activeRespawnMarker.Key}");
    }

    // -------------------------------------------------------------------------
    // Respawn (same scene)
    // -------------------------------------------------------------------------

    public void BeginRespawnSequence()
    {
        if (_respawnOrRecoveryInProgress)
        {
            return;
        }

        _respawnOrRecoveryInProgress = true;
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        CameraShakeRequester.ShakeStop();

        if (GameCameras.Instance != null)
            yield return StartCoroutine(GameCameras.Instance.Fade.FadeOut());

        // Use the active respawn marker when set; fall back to nearest marker in scene.
        if (_activeRespawnMarker != null)
        {
            if (_hero != null)
            {
                _hero.transform.position = _activeRespawnMarker.RespawnPosition;
                _hero.ForceFacingDirection(_activeRespawnMarker.FacingDirection);
            }
        }
        else
        {
            if (_hero != null)
                _hero.transform.position = FindNearestRespawnMarkerPosition();
        }

        if (_heroHealth != null)
            _heroHealth.RestoreFullHealth();

        if (_hero != null)
            _hero.ResetAfterRespawn();

        if (GameCameras.Instance != null)
        {
            GameCameras.Instance.Target.SnapToHero();
            GameCameras.Instance.Controller.SnapToTarget();
        }

        yield return new WaitForSecondsRealtime(0.1f);

        if (GameCameras.Instance != null)
            yield return StartCoroutine(GameCameras.Instance.Fade.FadeIn());

        _respawnOrRecoveryInProgress = false;
    }

    public void BeginHazardRecoverySequence(HazardRespawnMarker marker)
    {
        if (_respawnOrRecoveryInProgress)
        {
            return;
        }

        _respawnOrRecoveryInProgress = true;
        StartCoroutine(HazardRecoveryRoutine(marker));
    }

    private IEnumerator HazardRecoveryRoutine(HazardRespawnMarker marker)
    {
        CameraShakeRequester.ShakeStop();

        if (_hero != null)
        {
            _hero.AddControlLock(this);
        }

        if (GameCameras.Instance != null)
            yield return StartCoroutine(GameCameras.Instance.Fade.FadeOut());

        if (_hero != null)
        {
            if (marker != null)
            {
                _hero.transform.position = marker.RespawnPosition;
                _hero.ForceFacingDirection(marker.FacingDirection);
            }
            else
            {
                Debug.LogWarning("[GameManager] Recoverable hazard had no HazardRespawnMarker assigned. Falling back to nearest normal RespawnMarker.");
                _hero.transform.position = FindNearestRespawnMarkerPosition();
            }

            _hero.ResetAfterHazardRecovery();
        }

        if (GameCameras.Instance != null)
        {
            GameCameras.Instance.Target.SnapToHero();
            GameCameras.Instance.Controller.SnapToTarget();
        }

        yield return new WaitForSecondsRealtime(0.1f);

        if (GameCameras.Instance != null)
            yield return StartCoroutine(GameCameras.Instance.Fade.FadeIn());

        if (_hero != null)
        {
            _hero.RemoveControlLock(this);
        }

        _respawnOrRecoveryInProgress = false;
    }

    private Vector3 FindNearestRespawnMarkerPosition()
    {
        if (_hero == null) return Vector3.zero;

        RespawnMarker[] markers = FindObjectsByType<RespawnMarker>(FindObjectsSortMode.None);
        if (markers.Length == 0) return _hero.transform.position;

        RespawnMarker nearest = markers[0];
        float bestDist = Vector3.SqrMagnitude(markers[0].transform.position - _hero.transform.position);
        for (int i = 1; i < markers.Length; i++)
        {
            float d = Vector3.SqrMagnitude(markers[i].transform.position - _hero.transform.position);
            if (d < bestDist) { bestDist = d; nearest = markers[i]; }
        }
        return nearest.transform.position;
    }

    private void PositionHeroAtMarker(string markerTag)
    {
        if (string.IsNullOrEmpty(markerTag)) return;
        GameObject marker = GameObject.FindWithTag(markerTag);
        if (marker != null && _hero != null)
            _hero.transform.position = marker.transform.position;
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
