using UnityEngine;

[DisallowMultipleComponent]
public sealed class EnemyAttackAnimationEvents : MonoBehaviour
{
    [SerializeField] private EnemyAttackController attackController;

    private void Awake()
    {
        if (attackController == null)
        {
            attackController = GetComponentInParent<EnemyAttackController>();
        }
    }

    public void OpenAttackWindow()
    {
        attackController?.OpenAttackWindow();
    }

    public void CloseAttackWindow()
    {
        attackController?.CloseAttackWindow();
    }

    public void CompleteAttack()
    {
        attackController?.CompleteAttack();
    }
}
