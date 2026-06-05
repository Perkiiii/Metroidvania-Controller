using UnityEngine;
using WorldGraphEditor;

// Runs once at application start from the boot scene (Build Index 0).
// Instantiates every persistent singleton, then loads the first gameplay scene.
public sealed class Bootstrap : MonoBehaviour
{
    [SerializeField] private GameManager gameManagerPrefab;
    [SerializeField] private SaveManager saveManagerPrefab;
    [SerializeField] private AudioManager audioManagerPrefab;
    [SerializeField] private GameCameras gameCamerasPrefab;
    [SerializeField] private InteractManager interactManagerPrefab;
    [SerializeField] private WorldGraphContainer worldGraphContainer;

    // Change this field in the Inspector when SampleScene is replaced with the real first scene.
    [SerializeField] private string firstScene = "SampleScene";

    private void Awake()
    {
        Instantiate(gameManagerPrefab);
        Instantiate(saveManagerPrefab);
        Instantiate(audioManagerPrefab);
        if (gameCamerasPrefab != null) Instantiate(gameCamerasPrefab);
        if (interactManagerPrefab != null) Instantiate(interactManagerPrefab);
    }

    private void Start()
    {
        SaveManager.Instance.LoadOrCreate(0);

        if (worldGraphContainer == null)
            Debug.LogError("[Bootstrap] worldGraphContainer is not assigned. Scene transitions will not resolve. Assign UnderbrewWorldGraph.asset to this component in the Boot scene Inspector.", this);

        WorldGraphTransitionResolver.SetContainer(worldGraphContainer);

        // Verify that WGE's Bootstrapper did not auto-instantiate a TransitionManager before
        // Underbrew boot. WorldGraphEditor.Bootstrapper.Execute() runs at
        // RuntimeInitializeLoadType.BeforeSceneLoad — before any Awake — and creates a
        // PersistentSingleton<TransitionManager> if _autoLoad is true on the prefab.
        // That singleton conflicts with GameManager's scene-loading flow. _autoLoad must always
        // be false on Assets/WorldGraphEditor/Resources/TransitionManager.prefab.
        Debug.Assert(
            Object.FindAnyObjectByType<TransitionManager>() == null,
            "[Underbrew] WGE TransitionManager instance found at runtime. " +
            "Set _autoLoad to false on Assets/WorldGraphEditor/Resources/TransitionManager.prefab.");

        string startupScene = SaveManager.Instance.GetStartupScene(firstScene);
        GameManager.Instance.RequestSavedRespawnPlacementOnNextSceneLoad();
        GameManager.Instance.BeginSceneTransition(new SceneTransitionRequest(
            startupScene,
            kind: SceneTransitionKind.Startup,
            sourceDescription: "bootstrap startup"));
    }
}
