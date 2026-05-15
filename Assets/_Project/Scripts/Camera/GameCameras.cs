using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameCameras : MonoBehaviour
{
    public static GameCameras Instance { get; private set; }

    [SerializeField] private CameraController cameraController;
    [SerializeField] private CameraTarget     cameraTarget;
    [SerializeField] private CameraFade       cameraFade;
    [SerializeField] private CameraShakeCueService shakeCues;
    [SerializeField] private Camera           hudCamera;

    public CameraController Controller => cameraController;
    public CameraTarget     Target     => cameraTarget;
    public CameraFade       Fade       => cameraFade;
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
        cameraTarget?.SceneInit();
        cameraController?.SceneInit();
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
        if (cameraFade == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(request.Direction == CameraFadeDirection.Out
            ? cameraFade.FadeOut(request.Duration)
            : cameraFade.FadeIn(request.Duration));
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
}
