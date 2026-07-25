using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneTransitionManager
{
    private static readonly SceneTransitionTraceMarker[] EmptyMarkers = new SceneTransitionTraceMarker[0];

    private readonly GameManager owner;
    private readonly SceneLoader sceneLoader;
    private SceneTransitionTrace activeTrace;
    private SceneTransitionTrace lastTrace;
    private int nextTraceId;

    public bool IsTransitioning { get; private set; }
    public SceneTransitionTrace ActiveTrace => activeTrace;
    public SceneTransitionTrace LastTrace => activeTrace ?? lastTrace;
    public IReadOnlyList<SceneTransitionTraceMarker> LastMarkers => LastTrace != null ? LastTrace.Markers : EmptyMarkers;

    public SceneTransitionManager(GameManager owner, SceneLoader sceneLoader)
    {
        this.owner = owner;
        this.sceneLoader = sceneLoader;
    }

    public bool Begin(SceneTransitionRequest request)
    {
        if (IsTransitioning)
        {
            Trace(SceneTransitionTraceMarker.RequestReceived, $"Rejected duplicate request to '{request.TargetScene}'.");
            Trace(SceneTransitionTraceMarker.RequestRejectedAlreadyTransitioning, request.SourceDescription);
            Debug.LogWarning($"[SceneTransitionManager] Ignoring transition to '{request.TargetScene}' - a transition is already in progress.");
            return false;
        }

        activeTrace = new SceneTransitionTrace(++nextTraceId, request);
        Trace(SceneTransitionTraceMarker.RequestReceived, request.SourceDescription);

        if (string.IsNullOrEmpty(request.TargetScene))
        {
            Debug.LogError("[SceneTransitionManager] Transition request has empty targetScene; rejected.");
            FailAndCloseActiveTrace("Empty targetScene.");
            return false;
        }

        if (!sceneLoader.CanLoad(request.TargetScene))
        {
            Debug.LogError($"[SceneTransitionManager] Scene '{request.TargetScene}' is not in Build Settings; rejected.");
            FailAndCloseActiveTrace($"Scene '{request.TargetScene}' is not loadable.");
            return false;
        }

        Trace(SceneTransitionTraceMarker.RequestAccepted);
        IsTransitioning = true;
        owner.StartCoroutine(TransitionRoutine(request));
        return true;
    }

    private IEnumerator TransitionRoutine(SceneTransitionRequest request)
    {
        FadeProfile fadeProfile = owner.GetFadeProfileFor(request);
        HeroController lockedHero = null;
        bool controlLockAdded = false;
        HeroController fallbackLockedHero = null;
        bool fallbackControlLockAdded = false;
        bool completed = false;
        CameraRequestHandle cameraFreeze = default;

        try
        {
            owner.SetGameState(GameState.Loading);
            CameraShakeRequester.ShakeStop();

            if (owner.CurrentHero != null)
            {
                lockedHero = owner.CurrentHero;
                lockedHero.AddControlLock(this);
                controlLockAdded = true;
                Trace(SceneTransitionTraceMarker.ControlLocked);
            }

            if (owner.CurrentHeroHealth != null)
                owner.CurrentHeroHealth.GrantTemporaryInvincibility(this, 3f);

            if (GameCameras.Instance != null)
            {
                cameraFreeze = GameCameras.Instance.FreezeForSceneTransition(this);
                Trace(SceneTransitionTraceMarker.CameraFreezeRequested);
                Trace(SceneTransitionTraceMarker.FadeOutStarted);
                yield return owner.StartCoroutine(GameCameras.Instance.FadeOut(fadeProfile));
                Trace(SceneTransitionTraceMarker.FadeOutComplete);
            }

            Trace(SceneTransitionTraceMarker.SceneLoadStarted);
            bool loadSucceeded = false;
            yield return owner.StartCoroutine(sceneLoader.LoadSingle(request.TargetScene, succeeded => loadSucceeded = succeeded));
            if (!loadSucceeded)
            {
                TraceFailure($"SceneLoader failed to load '{request.TargetScene}'.");
                yield break;
            }

            Trace(SceneTransitionTraceMarker.SceneLoaded);

            owner.SetGameState(GameState.EnteringLevel);

            if (GameCameras.Instance != null)
                GameCameras.Instance.SetBlack();

            if (owner.HasPendingNormalDeathRespawn)
            {
                Trace(SceneTransitionTraceMarker.DestinationResolved, "Normal death respawn.");
                owner.CompletePendingNormalDeathRespawn(request.TargetScene);
                Trace(SceneTransitionTraceMarker.HeroPlaced);

                if (GameCameras.Instance != null)
                {
                    Trace(SceneTransitionTraceMarker.CameraRebindStarted);
                    GameCameras.Instance.RebindForSceneEntry();
                    Trace(SceneTransitionTraceMarker.CameraRebindComplete);
                }

                yield return new WaitForSecondsRealtime(0.1f);

                if (GameCameras.Instance != null)
                {
                    Trace(SceneTransitionTraceMarker.FadeInStarted);
                    yield return owner.StartCoroutine(GameCameras.Instance.FadeIn(fadeProfile));
                    Trace(SceneTransitionTraceMarker.FadeInComplete);
                }

                Trace(SceneTransitionTraceMarker.EntryMotionComplete, "Skipped.");
                owner.SetGameState(GameState.Playing);
                Trace(SceneTransitionTraceMarker.GameplayRestored);
                completed = true;
                yield break;
            }

            TransitionPoint dest = !string.IsNullOrEmpty(request.DestinationPassageGuid)
                ? TransitionPoint.FindByPassageGuid(request.DestinationPassageGuid)
                : null;
            bool heroPlacementTraced = false;

            if (dest != null)
            {
                Trace(SceneTransitionTraceMarker.DestinationResolved, dest.name);
            }
            else if (string.IsNullOrEmpty(request.DestinationPassageGuid))
            {
                Trace(SceneTransitionTraceMarker.DestinationResolved, "No destination passage requested.");
            }

            if (dest == null && !string.IsNullOrEmpty(request.DestinationPassageGuid))
            {
                Debug.LogError($"[SceneTransitionManager] No TransitionPoint with passage GUID '{request.DestinationPassageGuid}' found in '{SceneManager.GetActiveScene().name}'. Using scene fallback placement.");
                Trace(SceneTransitionTraceMarker.DestinationResolved, $"Missing passage '{request.DestinationPassageGuid}'. Using fallback.");
                owner.PlaceHeroAtMissingPassageFallback(request.DestinationPassageGuid);
                Trace(SceneTransitionTraceMarker.HeroPlaced);
                heroPlacementTraced = true;

                if (owner.CurrentHero != null)
                {
                    fallbackLockedHero = owner.CurrentHero;
                    fallbackLockedHero.AddControlLock(this);
                    fallbackControlLockAdded = true;
                }
            }

            if (dest != null && owner.CurrentHero != null)
            {
                owner.CurrentHero.BeginSceneEntryPlacement(dest);
                owner.SetSceneFallbackPosition(owner.CurrentHero.transform.position);
                Trace(SceneTransitionTraceMarker.HeroPlaced);
                heroPlacementTraced = true;
            }
            else if (!heroPlacementTraced && dest == null && owner.CurrentHero != null)
            {
                Trace(SceneTransitionTraceMarker.HeroPlaced, "Authored or saved respawn placement.");
                heroPlacementTraced = true;
            }

            if (GameCameras.Instance != null)
            {
                Trace(SceneTransitionTraceMarker.CameraRebindStarted);
                GameCameras.Instance.RebindForSceneEntry();
                Trace(SceneTransitionTraceMarker.CameraRebindComplete);
            }

            yield return new WaitForSecondsRealtime(0.1f);

            Coroutine entryFadeIn = null;
            if (GameCameras.Instance != null)
            {
                Trace(SceneTransitionTraceMarker.FadeInStarted);
                entryFadeIn = owner.StartCoroutine(GameCameras.Instance.FadeIn(fadeProfile));
            }

            if (dest != null && owner.CurrentHero != null)
            {
                Trace(SceneTransitionTraceMarker.EntryMotionStarted);
                owner.CurrentHero.BeginSceneEntryMotion(dest);
                while (owner.CurrentHero != null && owner.CurrentHero.IsEnteringScene)
                    yield return null;
                Trace(SceneTransitionTraceMarker.EntryMotionComplete);
            }
            else
            {
                Trace(SceneTransitionTraceMarker.EntryMotionComplete, "Skipped.");
            }

            if (entryFadeIn != null)
            {
                yield return entryFadeIn;
                Trace(SceneTransitionTraceMarker.FadeInComplete);
            }

            if (fallbackControlLockAdded && fallbackLockedHero != null)
            {
                fallbackLockedHero.RemoveControlLock(this);
                fallbackControlLockAdded = false;
            }

            owner.SetGameState(GameState.Playing);
            Trace(SceneTransitionTraceMarker.GameplayRestored);
            completed = true;
        }
        finally
        {
            cameraFreeze.Release();

            if (!completed && activeTrace != null && !activeTrace.IsFailed)
                TraceFailure("Transition coroutine exited before completion.");

            if (!completed && controlLockAdded && lockedHero != null && lockedHero == owner.CurrentHero)
                lockedHero.RemoveControlLock(this);

            if (fallbackControlLockAdded && fallbackLockedHero != null)
                fallbackLockedHero.RemoveControlLock(this);

            if (!completed && (owner.State == GameState.Loading || owner.State == GameState.EnteringLevel || owner.State == GameState.ExitingLevel))
                owner.SetGameState(GameState.Playing);

            owner.ClearPendingNormalDeathRespawn();
            IsTransitioning = false;
            CloseActiveTrace(completed);
        }
    }

    private void Trace(SceneTransitionTraceMarker marker, string detail = "")
    {
        if (activeTrace == null)
            return;

        activeTrace.Add(marker, owner != null ? owner.State : GameState.Playing, detail);
    }

    private void TraceFailure(string detail)
    {
        if (activeTrace == null || activeTrace.IsFailed)
            return;

        activeTrace.MarkFailed(detail);
        Trace(SceneTransitionTraceMarker.TransitionFailed, detail);
    }

    private void FailAndCloseActiveTrace(string detail)
    {
        TraceFailure(detail);
        CloseActiveTrace(false);
    }

    private void CloseActiveTrace(bool completed)
    {
        if (activeTrace == null)
            return;

        if (completed && !activeTrace.IsFailed)
            activeTrace.MarkComplete();

        Trace(SceneTransitionTraceMarker.CleanupComplete);
        activeTrace.Close();
        lastTrace = activeTrace;
        activeTrace = null;
    }
}
