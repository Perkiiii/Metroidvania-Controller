using MoreMountains.Feedbacks;
using UnityEngine;

public sealed class HeroAttackImpactFeedbackController : MonoBehaviour
{
    [Header("Terrain Impact Feedbacks")]
    [SerializeField] private MMF_Player sideTerrainImpact;
    [SerializeField] private MMF_Player upTerrainImpact;
    [SerializeField] private MMF_Player downTerrainImpact;
    [SerializeField] private MMF_Player fallbackTerrainImpact;

    [Header("Connect Feedback")]
    [Tooltip("Optional non-camera MMF_Player triggered once per swing on the first confirmed enemy hit or clash. Camera shake is routed through CameraEventService.")]
    [SerializeField] private MMF_Player attackConnectFeedback;

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

    // Called by HeroAttackAction on the first confirmed enemy hit or clash per swing.
    // worldPosition is the hit contact point in world space.
    public void PlayConnectFeedback(Vector2 worldPosition)
    {
        attackConnectFeedback?.PlayFeedbacks(new Vector3(worldPosition.x, worldPosition.y, 0f));
    }
}
