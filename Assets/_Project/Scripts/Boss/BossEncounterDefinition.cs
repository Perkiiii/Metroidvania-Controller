using UnityEngine;

[CreateAssetMenu(menuName = "Project/Boss/Boss Encounter Definition", fileName = "BossEncounterDefinition")]
public sealed class BossEncounterDefinition : ScriptableObject
{
    [SerializeField] private string encounterId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite displayIcon;

    public string EncounterId => encounterId;
    public string DisplayName => displayName;
    public Sprite DisplayIcon => displayIcon;
}
