using UnityEngine;

// Runs once at application start from the boot scene (Build Index 0).
// Instantiates every persistent singleton, then loads the first gameplay scene.
public sealed class Bootstrap : MonoBehaviour
{
    [SerializeField] private GameManager gameManagerPrefab;
    [SerializeField] private AudioManager audioManagerPrefab;
    [SerializeField] private GameCameras gameCamerasPrefab;

    // Change this field in the Inspector when SampleScene is replaced with the real first scene.
    [SerializeField] private string firstScene = "SampleScene";

    private void Awake()
    {
        Instantiate(gameManagerPrefab);
        Instantiate(audioManagerPrefab);
        if (gameCamerasPrefab != null) Instantiate(gameCamerasPrefab);
        // SaveManager and InteractManager will be instantiated here in later milestones.
    }

    private void Start()
    {
        GameManager.Instance.BeginSceneTransition(firstScene);
    }
}
