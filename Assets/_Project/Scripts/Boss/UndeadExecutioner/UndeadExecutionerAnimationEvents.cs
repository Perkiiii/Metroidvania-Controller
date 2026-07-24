using UnityEngine;

[DisallowMultipleComponent]
public sealed class UndeadExecutionerAnimationEvents : MonoBehaviour
{
    [SerializeField] private UndeadExecutionerBehaviour behaviour;

    public UndeadExecutionerBehaviour Behaviour => behaviour;

    public void ComboFirstOpen()
    {
        behaviour?.HandleComboFirstOpen();
    }

    public void ComboFirstClose()
    {
        behaviour?.HandleComboFirstClose();
    }

    public void ComboSecondBegin()
    {
        behaviour?.HandleComboSecondBegin();
    }

    public void ComboSecondOpen()
    {
        behaviour?.HandleComboSecondOpen();
    }

    public void ComboSecondClose()
    {
        behaviour?.HandleComboSecondClose();
    }

    public void ComboComplete()
    {
        behaviour?.HandleComboComplete();
    }

    public void ShadowBurstOpen()
    {
        behaviour?.HandleShadowBurstOpen();
    }

    public void ShadowBurstClose()
    {
        behaviour?.HandleShadowBurstClose();
    }

    public void ShadowBurstComplete()
    {
        behaviour?.HandleShadowBurstComplete();
    }

    public void SummonSpirit()
    {
        behaviour?.HandleSummonSpirit();
    }
}
