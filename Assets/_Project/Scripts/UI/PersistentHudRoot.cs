using UnityEngine;

/// <summary>
/// Persistent HUD composition root. It is intended to be a child of the persistent
/// _GameCameras prefab; views subscribe directly to persistent state assets.
/// </summary>
public sealed class PersistentHudRoot : MonoBehaviour
{
    public static PersistentHudRoot Instance { get; private set; }

    [SerializeField] private PlayerHealthState healthState;
    [SerializeField] private PlayerResourceState resourceState;
    [SerializeField] private PlayerResourceConfig resourceConfig;
    [SerializeField] private HealthDisplay healthDisplay;
    [SerializeField] private ResourceDisplay resourceDisplay;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        WireDisplays();
    }

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;
        WireDisplays();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool HasValidDependencies => healthState != null
        && resourceState != null
        && resourceConfig != null
        && healthDisplay != null
        && resourceDisplay != null;

    private void WireDisplays()
    {
        if (healthDisplay != null)
            healthDisplay.Configure(healthState);
        if (resourceDisplay != null)
            resourceDisplay.Configure(resourceState, resourceConfig);
    }
}
