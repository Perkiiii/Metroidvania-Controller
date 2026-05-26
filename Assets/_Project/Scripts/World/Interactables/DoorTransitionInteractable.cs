using UnityEngine;

[RequireComponent(typeof(TransitionPoint))]
public sealed class DoorTransitionInteractable : InteractableBase
{
    private TransitionPoint transitionPoint;

    private void Awake()
    {
        transitionPoint = GetComponent<TransitionPoint>();
        if (transitionPoint != null && !transitionPoint.RequireInteract)
            SetDisabled(true);
    }

    public override void Interact()
    {
        if (transitionPoint == null)
        {
            Debug.LogWarning($"[DoorTransitionInteractable] '{name}' has no TransitionPoint.", this);
            return;
        }

        transitionPoint.TryActivateFromInteract(CurrentHero);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TransitionPoint tp = GetComponent<TransitionPoint>();
        if (tp == null)
        {
            Debug.LogWarning($"[DoorTransitionInteractable] '{name}' is missing a TransitionPoint component.", this);
            return;
        }

        if (!tp.IsDoor)
            Debug.LogWarning($"[DoorTransitionInteractable] '{name}' is on a TransitionPoint that is not a door.", this);
    }
#endif
}
