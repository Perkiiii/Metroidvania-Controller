using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Shared shallow confirmation modal (e.g. Quit-to-Main-Menu confirmation). Owns its own focus
/// while open and restores the invoking control on close. Package A1 does not build a generic
/// modal stack — this is the single modal layer used by the root Pause menu.
/// </summary>
public sealed class ConfirmationModal : MonoBehaviour
{
    [SerializeField] private GameObject visualRoot;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private EventSystem eventSystem;

    private Action onConfirm;
    private Action onCancel;
    private Selectable invokerToRestore;

    public bool IsOpen => visualRoot != null && visualRoot.activeSelf;
    public Selectable FirstSelection => cancelButton != null ? cancelButton : confirmButton;

    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancelClicked);
    }

    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirmClicked);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancelClicked);
    }

    public void Show(Selectable invoker, Action confirmCallback, Action cancelCallback)
    {
        invokerToRestore = invoker;
        onConfirm = confirmCallback;
        onCancel = cancelCallback;

        if (visualRoot != null) visualRoot.SetActive(true);
        UISelectionUtility.Select(eventSystem, FirstSelection);
    }

    /// <summary>
    /// Closes the modal. Restores the invoking control's selection unless the root that owns it
    /// is also closing (<paramref name="restoreInvokerSelection"/> false).
    /// </summary>
    public void Close(bool restoreInvokerSelection)
    {
        if (visualRoot != null) visualRoot.SetActive(false);

        if (restoreInvokerSelection)
        {
            UISelectionUtility.Select(eventSystem, invokerToRestore);
        }

        onConfirm = null;
        onCancel = null;
        invokerToRestore = null;
    }

    /// <summary>Back/Cancel input while this modal is open: same as clicking Cancel.</summary>
    public void HandleBack()
    {
        HandleCancelClicked();
    }

    private void HandleConfirmClicked()
    {
        Action confirm = onConfirm;
        Close(restoreInvokerSelection: true);
        confirm?.Invoke();
    }

    private void HandleCancelClicked()
    {
        Action cancel = onCancel;
        Close(restoreInvokerSelection: true);
        cancel?.Invoke();
    }
}
