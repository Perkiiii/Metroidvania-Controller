using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public GameState State { get; private set; }

    // Fired after every scene load, before anything scene-local has run its Start.
    // Scene systems (HeroController, HUD, etc.) subscribe here rather than relying on Awake order.
    public event Action<Scene> SceneInit;

    private Coroutine _hitStopCoroutine;

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

    // Full sequence (stub): control lock → fade out → load → SceneInit → position → fade in.
    // For Milestone 0, only the async load is implemented; fade and hero positioning come in Milestone 1.
    public void BeginSceneTransition(string targetScene, string entryMarkerTag = "")
    {
        StartCoroutine(TransitionRoutine(targetScene));
    }

    private IEnumerator TransitionRoutine(string targetScene)
    {
        State = GameState.Loading;
        AsyncOperation load = SceneManager.LoadSceneAsync(targetScene);
        while (!load.isDone)
        {
            yield return null;
        }

        State = GameState.Playing;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneInit?.Invoke(scene);
    }

    // -------------------------------------------------------------------------
    // Pause / Unpause
    // -------------------------------------------------------------------------

    public void Pause()
    {
        if (State == GameState.Paused)
        {
            return;
        }

        State = GameState.Paused;
        Time.timeScale = 0f;
        // Hero control lock: HeroController subscribes to this via an event or direct call.
        // TODO (Milestone 1): add hero control lock token here once HeroController is integrated.
    }

    public void Unpause()
    {
        if (State != GameState.Paused)
        {
            return;
        }

        State = GameState.Playing;
        Time.timeScale = 1f;
        // TODO (Milestone 1): remove hero control lock token here.
    }

    // -------------------------------------------------------------------------
    // Hit stop
    // -------------------------------------------------------------------------

    // Freezes time for `duration` real-world seconds. Ignored while paused (timeScale is already 0).
    public void HitStop(float duration)
    {
        if (State == GameState.Paused)
        {
            return;
        }

        if (_hitStopCoroutine != null)
        {
            StopCoroutine(_hitStopCoroutine);
        }

        _hitStopCoroutine = StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);

        if (State != GameState.Paused)
        {
            Time.timeScale = 1f;
        }

        _hitStopCoroutine = null;
    }
}
