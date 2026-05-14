using UnityEngine;

[DisallowMultipleComponent]
public class DamageHero : MonoBehaviour
{
    [SerializeField] public int damageDealt = 1;
    [SerializeField] private int hazardType = 1;
    [SerializeField] private bool shadowDashHazard;
    [SerializeField] private bool resetOnEnable;

    private int initialDamage;

    public int DamageDealt => damageDealt;
    public int HazardType => hazardType;
    public bool ShadowDashHazard => shadowDashHazard;

    protected virtual void Awake()
    {
        initialDamage = damageDealt;
    }

    protected virtual void OnEnable()
    {
        if (resetOnEnable)
        {
            damageDealt = initialDamage;
        }
    }
}
