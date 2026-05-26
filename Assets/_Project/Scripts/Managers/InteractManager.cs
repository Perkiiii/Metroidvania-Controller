using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class InteractManager : MonoBehaviour
{
    public static InteractManager Instance { get; private set; }

    [SerializeField] private InputActionReference interactAction;

    private readonly List<InteractableBase> available = new List<InteractableBase>();
    private int interactCooldownFrames;

    public InteractableBase CurrentInteractable { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SceneInit += OnSceneInit;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SceneInit -= OnSceneInit;
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            return;

        if (interactCooldownFrames > 0)
        {
            interactCooldownFrames--;
            return;
        }

        if (CurrentInteractable == null)
            return;

        if (interactAction != null && interactAction.action.WasPressedThisFrame())
        {
            CurrentInteractable.Interact();
            interactCooldownFrames = 5;
            RefreshCurrent();
        }
    }

    public void Register(InteractableBase interactable)
    {
        if (interactable == null || available.Contains(interactable))
            return;

        available.Add(interactable);
        RefreshCurrent();
    }

    public void Unregister(InteractableBase interactable)
    {
        available.Remove(interactable);
        RefreshCurrent();
    }

    private void RefreshCurrent()
    {
        available.RemoveAll(item => item == null || item.IsDisabled);

        InteractableBase best = null;
        for (int i = 0; i < available.Count; i++)
        {
            if (best == null || available[i].Priority > best.Priority)
                best = available[i];
        }

        CurrentInteractable = best;
    }

    private void OnSceneInit(Scene _)
    {
        available.Clear();
        CurrentInteractable = null;
    }
}
