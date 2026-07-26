using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

// Camera Phase 3 authoring validation: presentation tuning, Timeline clip configuration, scene
// presentation adapters, and the SampleScene4 vertical slice.
//
// Geometry, trigger, axis, and layer contracts for CameraBoundsVolume/CameraLockArea remain owned
// by CameraPhaseOneValidator; this menu item runs that existing entry point afterwards so one
// action still covers the whole camera contract. Nothing here rewrites authored tuning.
public static class CameraPhaseThreeValidator
{
    private const string CameraPrefabPath = "Assets/_Project/Prefabs/Managers/_GameCameras.prefab";
    private const string BossScenePath = "Assets/_Project/Scenes/SampleScene4.unity";

    [MenuItem("Tools/Project/Validate Camera Phase 3")]
    public static void ValidateCameraPhaseThree()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogWarning("[CameraPhaseThreeValidator] Validation cancelled because a loaded scene has unsaved changes.");
                return;
            }
        }

        int issues = ValidatePersistentPrefab();
        bool bossSceneSeen = false;
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
            {
                if (!buildScene.enabled || string.IsNullOrWhiteSpace(buildScene.path))
                {
                    continue;
                }

                Scene scene = EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
                issues += ValidateScene(scene);

                if (buildScene.path == BossScenePath)
                {
                    bossSceneSeen = true;
                    issues += ValidateBossSlice(scene);
                }
            }
        }
        finally
        {
            if (previousSetup != null && previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
        }

        if (!bossSceneSeen)
        {
            Debug.LogWarning(
                $"[CameraPhaseThreeValidator] '{BossScenePath}' is not an enabled Build Settings scene, so the boss presentation slice was not validated.");
        }

        if (issues == 0)
        {
            Debug.Log("[CameraPhaseThreeValidator] Camera Phase 3 validation passed.");
        }
        else
        {
            Debug.LogWarning($"[CameraPhaseThreeValidator] Found {issues} issue(s). See earlier logs.");
        }

        CameraPhaseOneValidator.ValidateCameraPhaseOne();
    }

    internal static int ValidatePersistentPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[CameraPhaseThreeValidator] Missing persistent camera prefab at '{CameraPrefabPath}'.");
            return 1;
        }

        GameCameras cameras = prefab.GetComponent<GameCameras>();
        if (cameras == null || cameras.Controller == null)
        {
            Debug.LogError($"[CameraPhaseThreeValidator] '{CameraPrefabPath}' has no GameCameras/CameraController to resolve presentation requests.", prefab);
            return 1;
        }

        SerializedObject controllerObject = new SerializedObject(cameras.Controller);
        SerializedProperty configProperty = controllerObject.FindProperty("config");
        CameraConfig config = configProperty != null ? configProperty.objectReferenceValue as CameraConfig : null;

        if (config == null)
        {
            Debug.LogError(
                "[CameraPhaseThreeValidator] The persistent CameraController has no CameraConfig assigned. "
                + "Shared presentation zoom and blend defaults would silently fall back to serialized prefab values.",
                cameras.Controller);
            return 1;
        }

        return ValidateSharedPresentationConfig(config);
    }

    internal static int ValidateSharedPresentationConfig(CameraConfig config)
    {
        int issues = 0;
        issues += ValidateTransition(config, "presentationEnterTransition", config.presentationEnterTransition);
        issues += ValidateTransition(config, "presentationChangeTransition", config.presentationChangeTransition);
        issues += ValidateTransition(config, "presentationReleaseTransition", config.presentationReleaseTransition);

        issues += ValidateFiniteNonNegative(config, "presentationPaddingX", config.presentationPaddingX);
        issues += ValidateFiniteNonNegative(config, "presentationPaddingY", config.presentationPaddingY);
        issues += ValidateFiniteNonNegative(config, "zoomOutDampTime", config.zoomOutDampTime);
        issues += ValidateFiniteNonNegative(config, "zoomInDampTime", config.zoomInDampTime);
        issues += ValidateFiniteNonNegative(config, "maxZoomSpeed", config.maxZoomSpeed);
        issues += ValidateFiniteNonNegative(config, "zoomHysteresis", config.zoomHysteresis);
        issues += ValidateFiniteNonNegative(config, "zoomContractHysteresis", config.zoomContractHysteresis);
        issues += ValidateFiniteNonNegative(config, "presentationCentreDampTime", config.presentationCentreDampTime);

        issues += ValidateFinitePositive(config, "minZoom", config.minZoom);
        issues += ValidateFinitePositive(config, "maxZoom", config.maxZoom);

        if (float.IsFinite(config.minZoom) && float.IsFinite(config.maxZoom) && config.minZoom > config.maxZoom)
        {
            Debug.LogError(
                $"[CameraPhaseThreeValidator] '{config.name}' minZoom ({config.minZoom}) is greater than maxZoom ({config.maxZoom}).",
                config);
            issues++;
        }

        return issues;
    }

    internal static int ValidateScene(Scene scene)
    {
        int issues = 0;
        List<BossEncounterCameraPresenter> presenters = new List<BossEncounterCameraPresenter>();
        List<CameraPresentationReceiver> receivers = new List<CameraPresentationReceiver>();
        List<PlayableDirector> directors = new List<PlayableDirector>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            presenters.AddRange(root.GetComponentsInChildren<BossEncounterCameraPresenter>(true));
            receivers.AddRange(root.GetComponentsInChildren<CameraPresentationReceiver>(true));
            directors.AddRange(root.GetComponentsInChildren<PlayableDirector>(true));
        }

        for (int i = 0; i < presenters.Count; i++)
        {
            issues += ValidatePresenter(presenters[i], presenters);
        }

        for (int i = 0; i < receivers.Count; i++)
        {
            issues += ValidateReceiver(receivers[i]);
        }

        for (int i = 0; i < directors.Count; i++)
        {
            issues += ValidateDirector(directors[i]);
        }

        return issues;
    }

    internal static int ValidatePresenter(
        BossEncounterCameraPresenter presenter,
        IReadOnlyList<BossEncounterCameraPresenter> allPresenters)
    {
        if (presenter == null)
        {
            return 0;
        }

        int issues = 0;

        if (presenter.Encounter == null)
        {
            issues += Error(presenter, "has no BossEncounterController assigned, so it can never acquire or release presentation.");
        }
        else
        {
            for (int i = 0; i < allPresenters.Count; i++)
            {
                BossEncounterCameraPresenter other = allPresenters[i];
                if (other == null || other == presenter || other.Encounter != presenter.Encounter)
                {
                    continue;
                }

                issues += Error(
                    presenter,
                    $"shares encounter '{other.Encounter.name}' with '{GetHierarchyPath(other.transform)}'. "
                    + "Two adapters would acquire competing presentation requests for the same beats.");
                break;
            }
        }

        if (presenter.RequiresBossFocus && presenter.BossFocus == null)
        {
            issues += Error(presenter, "requires a boss focus Transform but none is assigned.");
        }

        if (presenter.RequiresRewardFocus
            && presenter.RewardFocus == null
            && (presenter.Encounter == null || presenter.Encounter.RewardRoot == null))
        {
            issues += Error(presenter, "requires a reward focus but neither rewardFocus nor the encounter's reward root is assigned.");
        }

        if (presenter.PhaseSource != null && presenter.PhaseSource as IBossPresentationPhaseSource == null)
        {
            issues += Error(
                presenter,
                $"phaseSource '{presenter.PhaseSource.GetType().Name}' does not implement {nameof(IBossPresentationPhaseSource)}.");
        }

        if (presenter.IntroDirector != null && !HasCameraPresentationTrack(presenter.IntroDirector))
        {
            issues += Error(
                presenter,
                $"introDirector '{presenter.IntroDirector.name}' has no {nameof(CameraPresentationTrack)}, so playing it would produce no camera presentation.");
        }

        issues += ValidateSerializedPresenterSettings(presenter);
        return issues;
    }

    private static int ValidateSerializedPresenterSettings(BossEncounterCameraPresenter presenter)
    {
        int issues = 0;
        SerializedObject serialized = new SerializedObject(presenter);
        string[] fields = { "introSettings", "combatSettings", "phaseSettings", "defeatSettings", "rewardSettings" };

        for (int i = 0; i < fields.Length; i++)
        {
            SerializedProperty property = serialized.FindProperty(fields[i]);
            if (property == null)
            {
                continue;
            }

            issues += ValidateSettings(presenter, $"{fields[i]} on '{GetHierarchyPath(presenter.transform)}'", ReadSettings(property));
        }

        string[] durations = { "phaseFocusDuration", "rewardRevealDuration" };
        for (int i = 0; i < durations.Length; i++)
        {
            SerializedProperty property = serialized.FindProperty(durations[i]);
            if (property == null)
            {
                continue;
            }

            if (!float.IsFinite(property.floatValue) || property.floatValue < 0f)
            {
                issues += Error(presenter, $"{durations[i]} must be finite and non-negative (was {property.floatValue}).");
            }
        }

        return issues;
    }

    internal static int ValidateReceiver(CameraPresentationReceiver receiver)
    {
        if (receiver == null)
        {
            return 0;
        }

        if (receiver.Lifetime != CameraRequestLifetime.Persistent)
        {
            return 0;
        }

        return Error(
            receiver,
            "is authored with Persistent request lifetime. Scene-authored Timeline content must use Scene lifetime; "
            + "a persistent request would survive its own scene unloading.");
    }

    internal static int ValidateDirector(PlayableDirector director)
    {
        if (director == null || director.playableAsset is not TimelineAsset timeline)
        {
            return 0;
        }

        int issues = 0;
        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track is not CameraPresentationTrack presentationTrack)
            {
                continue;
            }

            CameraPresentationReceiver bound =
                director.GetGenericBinding(presentationTrack) as CameraPresentationReceiver;
            if (bound == null)
            {
                issues += Error(
                    director,
                    $"track '{presentationTrack.name}' has no {nameof(CameraPresentationReceiver)} bound, so its clips would do nothing.");
            }

            foreach (TimelineClip clip in presentationTrack.GetClips())
            {
                if (clip.asset is not CameraPresentationClip asset)
                {
                    continue;
                }

                string label = $"clip '{clip.displayName}' on track '{presentationTrack.name}'";
                issues += ValidateSettings(director, label, asset.settings);

                if (!asset.settings.RequiresTargets)
                {
                    continue;
                }

                int resolved = 0;
                for (int i = 0; asset.targets != null && i < asset.targets.Length; i++)
                {
                    if (asset.targets[i].Resolve(director) != null)
                    {
                        resolved++;
                    }
                }

                if (resolved == 0)
                {
                    issues += Error(
                        director,
                        $"{label} uses {asset.settings.mode} but resolves no exposed target reference through this director.");
                }
            }
        }

        return issues;
    }

    private static bool HasCameraPresentationTrack(PlayableDirector director)
    {
        if (director.playableAsset is not TimelineAsset timeline)
        {
            return false;
        }

        foreach (TrackAsset track in timeline.GetOutputTracks())
        {
            if (track is CameraPresentationTrack)
            {
                return true;
            }
        }

        return false;
    }

    internal static int ValidateBossSlice(Scene scene)
    {
        List<BossEncounterController> encounters = new List<BossEncounterController>();
        List<BossEncounterCameraPresenter> presenters = new List<BossEncounterCameraPresenter>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            encounters.AddRange(root.GetComponentsInChildren<BossEncounterController>(true));
            presenters.AddRange(root.GetComponentsInChildren<BossEncounterCameraPresenter>(true));
        }

        if (encounters.Count == 0)
        {
            return 0;
        }

        int issues = 0;
        for (int i = 0; i < encounters.Count; i++)
        {
            BossEncounterController encounter = encounters[i];
            bool covered = false;
            for (int j = 0; j < presenters.Count; j++)
            {
                if (presenters[j] != null && presenters[j].Encounter == encounter)
                {
                    covered = true;
                    break;
                }
            }

            if (!covered)
            {
                Debug.LogError(
                    $"[CameraPhaseThreeValidator] '{scene.path}::{GetHierarchyPath(encounter.transform)}' has no "
                    + $"{nameof(BossEncounterCameraPresenter)}. Camera Phase 3 boss presentation would be missing for this encounter.",
                    encounter);
                issues++;
            }
        }

        return issues;
    }

    private static CameraPresentationSettings ReadSettings(SerializedProperty property)
    {
        return new CameraPresentationSettings
        {
            mode = (CameraPresentationMode)property.FindPropertyRelative("mode").enumValueIndex,
            worldPoint = property.FindPropertyRelative("worldPoint").vector2Value,
            framingOffset = property.FindPropertyRelative("framingOffset").vector2Value,
            paddingX = property.FindPropertyRelative("paddingX").floatValue,
            paddingY = property.FindPropertyRelative("paddingY").floatValue,
            autoZoom = property.FindPropertyRelative("autoZoom").boolValue,
            authoredZoom = property.FindPropertyRelative("authoredZoom").floatValue,
            overrideZoomLimits = property.FindPropertyRelative("overrideZoomLimits").boolValue,
            minZoom = property.FindPropertyRelative("minZoom").floatValue,
            maxZoom = property.FindPropertyRelative("maxZoom").floatValue,
            overrideBlend = property.FindPropertyRelative("overrideBlend").boolValue,
            blendIn = ReadTransition(property.FindPropertyRelative("blendIn")),
            blendOut = ReadTransition(property.FindPropertyRelative("blendOut")),
            weight = property.FindPropertyRelative("weight").floatValue
        };
    }

    private static CameraTransitionSettings ReadTransition(SerializedProperty property)
    {
        return new CameraTransitionSettings
        {
            dampTimeX = property.FindPropertyRelative("dampTimeX").floatValue,
            dampTimeY = property.FindPropertyRelative("dampTimeY").floatValue,
            blendDuration = property.FindPropertyRelative("blendDuration").floatValue,
            resetVelocity = property.FindPropertyRelative("resetVelocity").boolValue,
            applyImmediate = property.FindPropertyRelative("applyImmediate").boolValue
        };
    }

    internal static int ValidateSettings(Object context, string label, CameraPresentationSettings settings)
    {
        int issues = 0;

        if (!settings.IsFinite())
        {
            Debug.LogError($"[CameraPhaseThreeValidator] '{context.name}' {label} has a non-finite value (NaN/Infinity).", context);
            issues++;
        }

        if (!settings.IsNonNegative())
        {
            Debug.LogError(
                $"[CameraPhaseThreeValidator] '{context.name}' {label} has negative padding/blend values or a non-positive zoom.",
                context);
            issues++;
        }

        if (!settings.HasValidZoomLimitOrder())
        {
            Debug.LogError(
                $"[CameraPhaseThreeValidator] '{context.name}' {label} overrides zoom limits with minZoom ({settings.minZoom}) greater than maxZoom ({settings.maxZoom}).",
                context);
            issues++;
        }

        return issues;
    }

    private static int ValidateTransition(Object context, string label, CameraTransitionSettings settings)
    {
        if (!settings.IsFinite())
        {
            Debug.LogError($"[CameraPhaseThreeValidator] '{context.name}' {label} has a non-finite value (NaN/Infinity).", context);
            return 1;
        }

        if (!settings.IsNonNegative())
        {
            Debug.LogError($"[CameraPhaseThreeValidator] '{context.name}' {label} has a negative damp time or blend duration.", context);
            return 1;
        }

        return 0;
    }

    private static int ValidateFiniteNonNegative(Object context, string label, float value)
    {
        if (float.IsFinite(value) && value >= 0f)
        {
            return 0;
        }

        Debug.LogError($"[CameraPhaseThreeValidator] '{context.name}' {label} must be finite and non-negative (was {value}).", context);
        return 1;
    }

    private static int ValidateFinitePositive(Object context, string label, float value)
    {
        if (float.IsFinite(value) && value > 0f)
        {
            return 0;
        }

        Debug.LogError($"[CameraPhaseThreeValidator] '{context.name}' {label} must be finite and greater than zero (was {value}).", context);
        return 1;
    }

    private static int Error(Component context, string message)
    {
        Debug.LogError(
            $"[CameraPhaseThreeValidator] '{context.gameObject.scene.path}::{GetHierarchyPath(context.transform)}' {message}",
            context);
        return 1;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = $"{transform.name}/{path}";
        }

        return path;
    }
}
