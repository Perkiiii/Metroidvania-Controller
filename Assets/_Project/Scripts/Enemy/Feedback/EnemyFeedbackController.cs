using MoreMountains.Feedbacks;
using UnityEngine;

public sealed class EnemyFeedbackController : MonoBehaviour
{
    [Header("Hit Feedbacks")]
    [SerializeField] private MMF_Player hitSparkFeedback;
    [SerializeField] private MMF_Player pogoSparkFeedback;
    [SerializeField] private MMF_Player bodyHitFeedback;

    [Header("Death Feedbacks")]
    [SerializeField] private MMF_Player deathFeedback;

    // isPogo=false: normal or killing hit  — spark at hit.Point + body reaction
    // isPogo=true:  downslash/pogo follow-up — pogo spark at hit.Point only
    //   (body reaction already fired from the normal-hit path that precedes this call)
    public void PlayHeroHit(HeroAttackHit hit, bool isPogo)
    {
        Vector3 hitPoint = new Vector3(hit.Point.x, hit.Point.y, transform.position.z);

        if (isPogo)
        {
            MMF_Player spark = pogoSparkFeedback != null ? pogoSparkFeedback : hitSparkFeedback;
            spark?.PlayFeedbacks(hitPoint);
        }
        else
        {
            hitSparkFeedback?.PlayFeedbacks(hitPoint);
            bodyHitFeedback?.PlayFeedbacks(transform.position);
        }
    }

    public void PlayDeath(Vector3 position)
    {
        deathFeedback?.PlayFeedbacks(position);
    }
}
