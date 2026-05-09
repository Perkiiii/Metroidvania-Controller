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
        Dash,
        Attack
    }

    [SerializeField] private HeroAnimationLibrary animationLibrary;
    [SerializeField] private AnimancerComponent animancer;

    private HeroConfig config;
    private HeroStateBlackboard blackboard;
    private HeroMotor motor;
    private LinearMixerState locomotionMixer;
    private VisualState currentVisualState;

    public void Initialize(
        HeroConfig heroConfig,
        HeroStateBlackboard stateBlackboard,
        HeroMotor heroMotor,
        AnimancerComponent animancerComponent,
        HeroAnimationLibrary library = null)
    {
        config = heroConfig;
        blackboard = stateBlackboard;
        motor = heroMotor;
        animancer = animancerComponent != null ? animancerComponent : animancer;
        animationLibrary = library != null ? library : animationLibrary;

        EnsureAnimationLibrary();
        BuildLocomotionMixer();
    }

    public void TickVisuals()
    {
        if (config == null || blackboard == null || motor == null || animancer == null || animationLibrary == null)
        {
            return;
        }

        Vector2 velocity = motor.Velocity;
        if (blackboard.attacking)
        {
            PlayActionClip(GetAttackClip(), VisualState.Attack);
        }
        else if (blackboard.dashing)
        {
            PlayActionClip(animationLibrary.dash, VisualState.Dash);
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

#if UNITY_EDITOR
        animationLibrary = UnityEditor.AssetDatabase.LoadAssetAtPath<HeroAnimationLibrary>(
            "Assets/_Project/ScriptableObjects/Hero/HeroAnimationLibrary.asset");
        if (animationLibrary != null)
        {
            return;
        }

        animationLibrary = ScriptableObject.CreateInstance<HeroAnimationLibrary>();
        animationLibrary.idle = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/HeroIdle.anim");
        animationLibrary.walk = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/HeroWalk.anim");
        animationLibrary.run = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/HeroRun.anim");
        animationLibrary.jump = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/HeroJump.anim");
        animationLibrary.fall = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Animations/HeroFall.anim");
#endif
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
