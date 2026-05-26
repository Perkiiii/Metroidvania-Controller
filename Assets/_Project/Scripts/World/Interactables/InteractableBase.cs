using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public abstract class InteractableBase : MonoBehaviour
{
    [SerializeField] private InteractPriority priority = InteractPriority.Normal;
    [SerializeField] private bool isDisabled;

    public InteractPriority Priority => priority;
    public bool IsDisabled => isDisabled;
    protected HeroController CurrentHero { get; private set; }

    // Override in subclass to offset the prompt above the object (future HUD use).
    public virtual Vector3 PromptPosition => transform.position;

    public abstract void Interact();

    protected void SetDisabled(bool disabled)
    {
        isDisabled = disabled;
        if (disabled)
        {
            CurrentHero = null;
            InteractManager.Instance?.Unregister(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isDisabled) return;
        HeroController hero = other.GetComponentInParent<HeroController>();
        if (hero == null) return;

        CurrentHero = hero;
        InteractManager.Instance?.Register(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        HeroController hero = other.GetComponentInParent<HeroController>();
        if (hero != null)
        {
            if (CurrentHero == hero)
                CurrentHero = null;
            InteractManager.Instance?.Unregister(this);
        }
    }

    private void OnDisable()
    {
        CurrentHero = null;
        InteractManager.Instance?.Unregister(this);
    }
}
