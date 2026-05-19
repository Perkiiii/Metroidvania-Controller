using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroAudioController : MonoBehaviour
{
    [Header("Sources")]
    [SerializeField] private AudioSource jump;
    [SerializeField] private AudioSource land;
    [SerializeField] private AudioSource dash;
    [SerializeField] private AudioSource wallJump;
    [SerializeField] private AudioSource wallSlide;
    [SerializeField] private AudioSource takeDamage;
    [SerializeField] private AudioSource death;
    [SerializeField] private AudioSource footstep;
    [SerializeField] private AudioSource terrainImpact;

    [Header("Pitch")]
    [SerializeField] private Vector2 jumpPitchRange = Vector2.one;
    [SerializeField] private Vector2 landPitchRange = Vector2.one;
    [SerializeField] private Vector2 dashPitchRange = Vector2.one;
    [SerializeField] private Vector2 wallJumpPitchRange = Vector2.one;
    [SerializeField] private Vector2 wallSlidePitchRange = Vector2.one;
    [SerializeField] private Vector2 takeDamagePitchRange = Vector2.one;
    [SerializeField] private Vector2 deathPitchRange = Vector2.one;
    [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.9f, 1.1f);
    [SerializeField] private Vector2 terrainImpactPitchRange = new Vector2(0.95f, 1.05f);

    private HeroConfig config;
    private HeroStateBlackboard blackboard;
    private bool wasFootstepAllowed;

    public void Initialize(HeroConfig heroConfig, HeroStateBlackboard stateBlackboard)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
    }

    public void Tick()
    {
        if (blackboard != null && !blackboard.wallSliding)
        {
            StopWallSlide();
        }

        bool footstepAllowed = IsFootstepAllowed();
        if (footstepAllowed && !wasFootstepAllowed)
        {
            TryPlayFootstep();
        }
        else if (!footstepAllowed)
        {
            wasFootstepAllowed = false;
            StopFootstep();
        }

        wasFootstepAllowed = footstepAllowed;
    }

    private void OnDisable()
    {
        wasFootstepAllowed = false;
        StopWallSlide();
        StopFootstep();
    }

    public void PlayJump() => PlaySource(jump, jumpPitchRange);
    public void PlayLand() => PlaySource(land, landPitchRange);
    public void PlayDash() => PlaySource(dash, dashPitchRange);
    public void PlayWallJump() => PlaySource(wallJump, wallJumpPitchRange);
    public void PlayTakeDamage() => PlaySource(takeDamage, takeDamagePitchRange);
    public void PlayDeath() => PlaySource(death, deathPitchRange);
    public void PlayTerrainImpact() => PlaySource(terrainImpact, terrainImpactPitchRange);

    public void PlayWallSlide()
    {
        if (wallSlide != null && wallSlide.isPlaying)
        {
            return;
        }

        PlaySource(wallSlide, wallSlidePitchRange);
    }

    public void AnimEventFootstep()
    {
        if (!CanPlayAnimationFootstep())
        {
            return;
        }

        TryPlayFootstep();
    }

    public void StopWallSlide()
    {
        if (wallSlide != null && wallSlide.isPlaying)
        {
            wallSlide.Stop();
        }
    }

    public void StopFootstep()
    {
        if (footstep != null && footstep.isPlaying)
        {
            footstep.Stop();
        }
    }

    private bool IsFootstepAllowed()
    {
        return config != null
            && blackboard != null
            && blackboard.grounded
            && blackboard.moving
            && !IsPushingIntoWall()
            && !blackboard.dashing
            && !blackboard.recoiling
            && !blackboard.controlLocked
            && blackboard.actorState != HeroActorState.Hurt
            && blackboard.actorState != HeroActorState.Dead;
    }

    private bool CanPlayAnimationFootstep()
    {
        return IsFootstepAllowed()
            && Mathf.Abs(blackboard.velocity.x) >= config.footstepMinSpeed;
    }

    private bool IsPushingIntoWall()
    {
        return blackboard.touchingWallFront
            && Mathf.Abs(blackboard.desiredMoveX) > config.horizontalInputDeadZone
            && Mathf.Sign(blackboard.desiredMoveX) == blackboard.FacingDirection;
    }

    private void TryPlayFootstep()
    {
        if (footstep == null || footstep.isPlaying)
        {
            return;
        }

        PlaySource(footstep, footstepPitchRange);
    }

    private static void PlaySource(AudioSource source, Vector2 pitchRange)
    {
        if (source == null || source.clip == null)
        {
            return;
        }

        float minPitch = Mathf.Min(pitchRange.x, pitchRange.y);
        float maxPitch = Mathf.Max(pitchRange.x, pitchRange.y);
        source.pitch = Random.Range(minPitch, maxPitch);
        source.Play();
    }
}
