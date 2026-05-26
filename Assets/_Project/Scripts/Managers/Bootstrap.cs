using UnityEngine;

// Runs once at application start from the boot scene (Build Index 0).
// Instantiates every persistent singleton, then loads the first gameplay scene.
public sealed class Bootstrap : MonoBehaviour
{
    [SerializeField] private GameManager gameManagerPrefab;
    [SerializeField] private SaveManager saveManagerPrefab;
    [SerializeField] private AudioManager audioManagerPrefab;
    [SerializeField] private GameCameras gameCamerasPrefab;
    [SerializeField] private InteractManager interactManagerPrefab;

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
        string startupScene = SaveManager.Instance.GetStartupScene(firstScene);
        GameManager.Instance.RequestSavedRespawnPlacementOnNextSceneLoad();
        GameManager.Instance.BeginSceneTransition(startupScene);
    }
}
