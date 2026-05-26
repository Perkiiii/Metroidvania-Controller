using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

public sealed class GameCameras : MonoBehaviour
{
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
    private Coroutine freezeRoutine;

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
        CameraEventService.FreezeRequested += OnCameraFreezeRequested;
        SceneManager.sceneLoaded += OnSceneLoaded;
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
        CameraEventService.FreezeRequested -= OnCameraFreezeRequested;
        SceneManager.sceneLoaded -= OnSceneLoaded;
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
        if (Instance == this)
            Instance = null;

        if (GameManager.Instance != null)
            GameManager.Instance.SceneInit -= OnSceneInit;
    }

    private void OnSceneInit(Scene scene)
    {
        DisableExternalAudioListeners();
        cameraTarget?.SceneInit();
        cameraController?.SceneInit();
    }

    public void RebindForSceneEntry()
    {
        if (cameraController != null) cameraController.SceneInit();
        if (cameraTarget != null)     cameraTarget.SnapToHero();
        if (cameraController != null) cameraController.SnapToTarget();
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

    public IEnumerator FadeIn(float duration = -1f)
    {
        if (fade == null)
        {
            Debug.LogWarning("[GameCameras] FadeIn requested, but no CameraFade is assigned. Continuing without a screen fade.", this);
            yield break;
        }

        yield return StartCoroutine(fade.FadeIn(duration));
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

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisableExternalAudioListeners();
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

    private void OnCameraFreezeRequested(CameraFreezeRequest request)
    {
        if (cameraController == null)
        {
            return;
        }

        if (freezeRoutine != null)
        {
            StopCoroutine(freezeRoutine);
            freezeRoutine = null;
        }

        if (request.Kind == CameraFreezeKind.Release)
        {
            cameraController.StopFreeze(true);
            return;
        }

        cameraController.FreezeInPlace(request.Kind == CameraFreezeKind.Hard);
        if (request.Duration > 0f)
        {
            freezeRoutine = StartCoroutine(ReleaseFreezeAfter(request.Duration));
        }
    }

    private System.Collections.IEnumerator ReleaseFreezeAfter(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        cameraController?.StopFreeze(true);
        freezeRoutine = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (fade == null)
            Debug.LogWarning("[GameCameras] No CameraFade assigned. Scene transitions will continue, but no screen fade will be shown.", this);
    }
#endif
}
