using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public abstract class InteractableBase : MonoBehaviour
{
    [SerializeField] private InteractPriority priority = InteractPriority.Normal;
    [SerializeField] private bool isDisabled;

    public InteractPriority Priority => priority;
    public bool IsDisabled => isDisabled;

    // Override in subclass to offset the prompt above the object (future HUD use).
    public virtual Vector3 PromptPosition => transform.position;

    public abstract void Interact();

    protected void SetDisabled(bool disabled)
    {
        isDisabled = disabled;
        if (disabled)
            InteractManager.Instance?.Unregister(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDisabled) return;
        if (other.GetComponentInParent<HeroController>() != null)
            InteractManager.Instance?.Register(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.GetComponentInParent<HeroController>() != null)
            InteractManager.Instance?.Unregister(this);
    }

    private void OnDisable()
    {
        InteractManager.Instance?.Unregister(this);
    }
}
