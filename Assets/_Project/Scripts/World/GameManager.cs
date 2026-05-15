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
    }

    // -------------------------------------------------------------------------
    // Respawn (same scene)
    // -------------------------------------------------------------------------

    public void BeginRespawnSequence()
    {
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        CameraShakeRequester.ShakeStop();

        if (GameCameras.Instance != null)
            yield return StartCoroutine(GameCameras.Instance.Fade.FadeOut());

        // Teleport hero to nearest respawn marker
        Vector3 respawnPos = FindNearestRespawnMarkerPosition();
        if (_hero != null)
            _hero.transform.position = respawnPos;

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
