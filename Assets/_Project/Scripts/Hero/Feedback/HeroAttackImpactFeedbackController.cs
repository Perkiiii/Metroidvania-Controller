using MoreMountains.Feedbacks;
using UnityEngine;

public sealed class HeroAttackImpactFeedbackController : MonoBehaviour
{
    [Header("Terrain Impact Feedbacks")]
    [SerializeField] private MMF_Player sideTerrainImpact;
    [SerializeField] private MMF_Player upTerrainImpact;
    [SerializeField] private MMF_Player downTerrainImpact;
    [SerializeField] private MMF_Player fallbackTerrainImpact;

    // Called by HeroAttackAction when a swing whiffs into terrain.
    // worldPosition is the computed terrain contact point.
    public void PlayTerrainImpact(HeroAttackDirection direction, Vector3 worldPosition)
    {
        MMF_Player player = direction switch
        {
            HeroAttackDirection.Side => sideTerrainImpact != null ? sideTerrainImpact : fallbackTerrainImpact,
            HeroAttackDirection.Up   => upTerrainImpact   != null ? upTerrainImpact   : fallbackTerrainImpact,
            HeroAttackDirection.Down => downTerrainImpact != null ? downTerrainImpact : fallbackTerrainImpact,
            _                        => fallbackTerrainImpact
        };

        player?.PlayFeedbacks(worldPosition);
    }
}
