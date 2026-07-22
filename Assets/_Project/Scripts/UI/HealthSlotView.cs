using UnityEngine;
using UnityEngine.UI;

public enum HealthSlotVisualState
{
    Empty,
    Filled,
    Bonus
}

/// <summary>Artwork-agnostic presentation element for one normal or bonus health slot.</summary>
public sealed class HealthSlotView : MonoBehaviour
{
    [SerializeField] private GameObject emptyVisual;
    [SerializeField] private GameObject filledVisual;
    [SerializeField] private GameObject bonusVisual;
    [SerializeField] private Image stateImage;
    [SerializeField] private Color emptyColor = Color.gray;
    [SerializeField] private Color filledColor = Color.white;
    [SerializeField] private Color bonusColor = Color.yellow;

    public HealthSlotVisualState State { get; private set; }

    public void SetState(HealthSlotVisualState state)
    {
        State = state;
        SetActive(emptyVisual, state == HealthSlotVisualState.Empty);
        SetActive(filledVisual, state == HealthSlotVisualState.Filled);
        SetActive(bonusVisual, state == HealthSlotVisualState.Bonus);

        if (stateImage != null)
            stateImage.color = state == HealthSlotVisualState.Empty ? emptyColor
                : state == HealthSlotVisualState.Bonus ? bonusColor : filledColor;
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
            target.SetActive(value);
    }
}
