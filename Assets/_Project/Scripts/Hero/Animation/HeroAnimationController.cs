using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HeroAnimationController : MonoBehaviour
{
    private enum VisualState
    {
        None,
        Locomotion,
        Jump,
        Fall,
        WallSlide,
        WallJump,
        Dash,
        LedgeClimb,
        Attack,
        Bind,
        Hurt,
        Dead
    }

    [SerializeField] private HeroAnimationLibrary animationLibrary;
    [SerializeField] private AnimancerComponent animancer;

    private HeroConfig config;
    private HeroStateBlackboard blackboard;
    private HeroMotor motor;
    private HeroActionController actions;
    public event Action DeathAnimationComplete;

    private LinearMixerState locomotionMixer;
    private AnimancerState activeAttackState;
    private AnimancerState activeBindState;
    private AnimancerState activeLedgeClimbState;
    private VisualState currentVisualState;
    private int playedAttackVersion = -1;

    public bool CanPlayBindAnimation => animancer != null && animationLibrary != null && animationLibrary.bind != null;

    public void Initialize(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroMotor heroMotor,
        AnimancerComponent animancerComponent,
        HeroActionController actionController,
        HeroAnimationLibrary library = null)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        motor = heroMotor;
        animancer = animancerComponent != null ? animancerComponent : animancer;
        actions = actionController;
        animationLibrary = library != null ? library : animationLibrary;

        EnsureAnimationLibrary();
        if (animationLibrary != null && animationLibrary.bind == null)
        {
            Debug.LogWarning("[HeroAnimationController] Bind clip is missing. Bind will remain unavailable until HeroAnimationLibrary.bind is assigned.", this);
        }
        BuildLocomotionMixer();
    }

    public void TickVisuals()
    {
        if (config == null || blackboard == null || motor == null || animancer == null || animationLibrary == null)
        {
            return;
        }

        Vector2 velocity = motor.Velocity;
        if (blackboard.actorState == HeroActorState.Dead)
        {
            PlayDeathClip();
        }
        else if (blackboard.actorState == HeroActorState.Hurt)
        {
            PlayActionClip(animationLibrary.hurt, VisualState.Hurt);
        }
        else if (blackboard.ledgeClimbing)
        {
            PlayLedgeClimbClip();
        }
        else if (blackboard.binding)
        {
            PlayBindClip();
        }
        else if (blackboard.attacking)
        {
            PlayAttackClip(GetAttackClip());
        }
        else if (blackboard.dashing)
        {
            PlayActionClip(animationLibrary.dash, VisualState.Dash);
        }
        else if (blackboard.wallJumping && animationLibrary.wallJump != null)
        {
            PlayActionClip(animationLibrary.wallJump, VisualState.WallJump);
        }
        else if (blackboard.wallSliding)
        {
            PlayActionClip(animationLibrary.wallSlide, VisualState.WallSlide);
        }
        else if (blackboard.jumpSustaining || blackboard.rising || velocity.y > 0.01f)
        {
            PlayAirClip(animationLibrary.jump, VisualState.Jump);
        }
        else if (blackboard.grounded)
        {
            PlayLocomotion(Mathf.Abs(velocity.x));
        }
        else
        {
            PlayAirClip(animationLibrary.fall, VisualState.Fall);
        }
    }

    private void BuildLocomotionMixer()
    {
        if (animancer == null || animationLibrary == null)
        {
            return;
        }

        List<float> thresholds = new List<float>(3);
        locomotionMixer = new LinearMixerState
        {
            ExtrapolateSpeed = false
        };
        locomotionMixer.SetGraph(animancer.Graph);

        AddLocomotionClip(animationLibrary.idle, config != null ? config.locomotionIdleThreshold : 0f, thresholds);
        AddLocomotionClip(animationLibrary.walk, config != null ? config.locomotionWalkThreshold : 4.32f, thresholds);
        AddLocomotionClip(animationLibrary.run, config != null ? config.locomotionRunThreshold : 6.5f, thresholds);

        if (locomotionMixer.ChildCount > 0)
        {
            locomotionMixer.SetThresholds(thresholds.ToArray());
        }
    }

    private void EnsureAnimationLibrary()
    {
        if (animationLibrary != null)
        {
            return;
        }

        Debug.LogError("[HeroAnimationController] HeroAnimationLibrary is not assigned. Assign it on HeroController or HeroAnimationController before play.", this);
    }

    private void AddLocomotionClip(AnimationClip clip, float threshold, List<float> thresholds)
    {
        if (clip == null || locomotionMixer == null)
        {
            return;
        }

        locomotionMixer.Add(clip);
        thresholds.Add(threshold);
    }

    private void PlayLocomotion(float speed)
    {
        if (locomotionMixer == null || locomotionMixer.ChildCount == 0)
        {
            PlayAirClip(animationLibrary.idle, VisualState.Locomotion);
            return;
        }

        locomotionMixer.Parameter = speed;
        if (currentVisualState == VisualState.Locomotion)
        {
            return;
        }

        animancer.Play(locomotionMixer, config.locomotionFadeDuration);
        currentVisualState = VisualState.Locomotion;
    }

    private void PlayAirClip(AnimationClip clip, VisualState state)
    {
        if (clip == null || currentVisualState == state)
        {
            return;
        }

        animancer.Play(clip, config.airFadeDuration, FadeMode.FromStart);
        currentVisualState = state;
    }

    private void PlayActionClip(AnimationClip clip, VisualState state)
    {
        if (clip == null || currentVisualState == state)
        {
            return;
        }

        animancer.Play(clip, config.actionFadeDuration, FadeMode.FromStart);
        currentVisualState = state;
    }

    private void PlayBindClip()
    {
        if (!CanPlayBindAnimation || currentVisualState == VisualState.Bind)
        {
            return;
        }

        activeBindState = animancer.Play(animationLibrary.bind, config.actionFadeDuration, FadeMode.FromStart);
        activeBindState.Events(this).OnEnd = () =>
        {
            if (activeBindState != null)
            {
                activeBindState.Events(this).OnEnd = null;
                activeBindState = null;
            }

            actions?.CompleteBindFromAnimation();
        };
        currentVisualState = VisualState.Bind;
    }

    public void StopBindAnimation()
    {
        if (activeBindState != null)
        {
            activeBindState.Events(this).OnEnd = null;
            activeBindState = null;
        }

        if (currentVisualState == VisualState.Bind && animancer != null)
        {
            animancer.Stop();
            currentVisualState = VisualState.None;
        }
    }

    private void PlayLedgeClimbClip()
    {
        if (currentVisualState == VisualState.LedgeClimb)
        {
            return;
        }

        if (animationLibrary.ledgeClimb == null)
        {
            animancer.Stop();
            currentVisualState = VisualState.LedgeClimb;
            return;
        }

        activeLedgeClimbState = animancer.Play(
            animationLibrary.ledgeClimb,
            config.actionFadeDuration,
            FadeMode.FromStart);
        float gameplayDuration = config.ledgeCatchDuration
            + config.ledgePullUpDuration
            + config.ledgeSettleDuration;
        if (gameplayDuration > Mathf.Epsilon && activeLedgeClimbState.Duration > Mathf.Epsilon)
        {
            activeLedgeClimbState.Speed = activeLedgeClimbState.Duration / gameplayDuration;
        }

        activeLedgeClimbState.Events(this).OnEnd = () =>
        {
            if (activeLedgeClimbState != null)
            {
                activeLedgeClimbState.Events(this).OnEnd = null;
                activeLedgeClimbState = null;
            }

            actions?.CompleteLedgeClimbFromAnimation();
        };
        currentVisualState = VisualState.LedgeClimb;
    }

    public void StopLedgeClimbAnimation()
    {
        if (activeLedgeClimbState != null)
        {
            activeLedgeClimbState.Events(this).OnEnd = null;
            activeLedgeClimbState = null;
        }

        if (currentVisualState == VisualState.LedgeClimb && animancer != null)
        {
            animancer.Stop();
            currentVisualState = VisualState.None;
        }
    }

    private void PlayDeathClip()
    {
        if (currentVisualState == VisualState.Dead)
            return;

        currentVisualState = VisualState.Dead;

        if (animationLibrary.death == null)
        {
            Debug.LogWarning("[HeroAnimationController] Death clip is missing — firing DeathAnimationComplete immediately.");
            DeathAnimationComplete?.Invoke();
            return;
        }

        AnimancerState deathState = animancer.Play(animationLibrary.death, config.actionFadeDuration, FadeMode.FromStart);
        deathState.Events(this).OnEnd = () =>
        {
            deathState.IsPlaying = false;       // Hold last frame; prevents time advancing past end
            deathState.Events(this).OnEnd = null; // Clear handler so it doesn't re-fire every frame
            DeathAnimationComplete?.Invoke();
        };
    }

    private void PlayAttackClip(AnimationClip clip)
    {
        int attackVersion = actions != null ? actions.AttackVersion : 0;
        if (clip == null || (currentVisualState == VisualState.Attack && playedAttackVersion == attackVersion))
        {
            CompleteAttackIfAnimationFinished(attackVersion);
            return;
        }

        AnimancerState state = animancer.Play(clip, config.actionFadeDuration, FadeMode.FromStart);
        state.Events(this).OnEnd = () => CompleteCurrentAttack(attackVersion);
        actions?.SetAttackFallbackTimeout(state.Duration + 0.25f);
        activeAttackState = state;
        playedAttackVersion = attackVersion;
        currentVisualState = VisualState.Attack;
    }

    private void CompleteAttackIfAnimationFinished(int attackVersion)
    {
        if (activeAttackState == null || activeAttackState.IsLooping || activeAttackState.NormalizedTime < activeAttackState.NormalizedEndTime)
        {
            return;
        }

        CompleteCurrentAttack(attackVersion);
    }

    private void CompleteCurrentAttack(int attackVersion)
    {
        if (actions == null || actions.AttackVersion != attackVersion)
        {
            return;
        }

        actions.CompleteAttackFromAnimation();
        activeAttackState = null;
        currentVisualState = VisualState.None;
        TickVisuals();
    }

    private AnimationClip GetAttackClip()
    {
        return blackboard.attackDirection switch
        {
            HeroAttackDirection.Up => animationLibrary.attackUp != null ? animationLibrary.attackUp : animationLibrary.attackSide,
            HeroAttackDirection.Down => animationLibrary.attackDown != null ? animationLibrary.attackDown : animationLibrary.attackSide,
            _ => animationLibrary.attackSide
        };
    }
}
