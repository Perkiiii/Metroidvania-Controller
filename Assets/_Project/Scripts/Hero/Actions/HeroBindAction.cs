using System;
using UnityEngine;

// Plain-C# action for the first-pass grounded, hold-to-heal Bind.
public sealed class HeroBindAction
{
    private readonly PlayerResourceConfig config;
    private readonly HeroStateBlackboard blackboard;
    private readonly HeroInputReader input;
    private readonly HeroMotor motor;
    private readonly PlayerHealthState healthState;
    private readonly PlayerResourceState resourceState;
    private readonly PlayerAbilityState abilityState;
    private readonly Func<bool> canPlayAnimation;
    private readonly Action stopAnimation;

    private float elapsed;
    private bool active;
    private bool completionCommitted;

    public HeroBindAction(
        PlayerResourceConfig resourceConfig,
        HeroStateBlackboard stateBlackboard,
        HeroInputReader inputReader,
        HeroMotor heroMotor,
        PlayerHealthState playerHealthState,
        PlayerResourceState playerResourceState,
        PlayerAbilityState playerAbilityState,
        Func<bool> bindAnimationAvailable,
        Action bindAnimationStop)
    {
        config = resourceConfig;
        blackboard = stateBlackboard;
        input = inputReader;
        motor = heroMotor;
        healthState = playerHealthState;
        resourceState = playerResourceState;
        abilityState = playerAbilityState;
        canPlayAnimation = bindAnimationAvailable;
        stopAnimation = bindAnimationStop;
    }

    public bool IsBinding => active;
    public bool HasCompleted { get; private set; }
    public float Elapsed => elapsed;

    public void Tick(float deltaTime)
    {
        if (!active)
        {
            if (input != null && input.BindPressedThisFrame && input.BindHeld)
            {
                TryStart();
            }

            return;
        }

        if (!CanContinue())
        {
            Cancel();
            return;
        }

        elapsed += Mathf.Max(0f, deltaTime);
        if (elapsed >= config.bindDuration)
        {
            TryComplete();
        }
    }

    public void FixedTick()
    {
        if (!active)
        {
            return;
        }

        if (!blackboard.grounded)
        {
            Cancel();
            return;
        }

        motor?.HoldStationary();
    }

    public void CompleteFromAnimation()
    {
        if (!active || completionCommitted)
        {
            return;
        }

        // The animation is a completion signal, not a way to bypass the configured
        // uninterrupted hold duration. The timer remains the fail-safe.
        if (elapsed >= config.bindDuration)
        {
            TryComplete();
        }
    }

    public void Cancel()
    {
        if (!active && (blackboard == null || !blackboard.binding))
        {
            return;
        }

        active = false;
        completionCommitted = false;
        elapsed = 0f;
        ExitBindingState();
    }

    public void Reset()
    {
        Cancel();
        HasCompleted = false;
    }

    private bool TryStart()
    {
        if (!CanStart())
        {
            return false;
        }

        active = true;
        HasCompleted = false;
        completionCommitted = false;
        elapsed = 0f;

        blackboard.binding = true;
        blackboard.actorState = HeroActorState.Binding;
        input.ConsumeAttackBuffer();
        input.ConsumeJumpBuffer();
        motor?.StopJumpSustain();
        motor?.SetNormalMovementSuppressed(true);
        motor?.HoldStationary();
        return true;
    }

    private bool CanStart()
    {
        if (abilityState == null || !abilityState.bindUnlocked)
        {
            return false;
        }

        if (config == null
            || config.bindCostParts <= 0
            || config.bindHealAmount <= 0
            || config.bindDuration <= 0f
            || blackboard == null
            || input == null
            || healthState == null
            || resourceState == null
            || canPlayAnimation == null
            || !canPlayAnimation())
        {
            return false;
        }

        if (blackboard.binding
            || blackboard.controlLocked
            || blackboard.inputBlocked
            || blackboard.attacking
            || blackboard.attackRecovering
            || blackboard.dashing
            || blackboard.wallSliding
            || blackboard.wallJumping
            || blackboard.recoiling
            || blackboard.actorState == HeroActorState.Hurt
            || blackboard.actorState == HeroActorState.Dead
            || !blackboard.grounded
            || healthState.IsDepleted
            || !healthState.CanHeal(config.bindHealAmount)
            || !resourceState.CanAfford(config.bindCostParts))
        {
            return false;
        }

        return input.BindPressedThisFrame && input.BindHeld;
    }

    private bool CanContinue()
    {
        return active
            && input != null
            && input.BindHeld
            && !input.BindReleasedThisFrame
            && blackboard != null
            && blackboard.binding
            && blackboard.grounded
            && !blackboard.controlLocked
            && !blackboard.inputBlocked
            && !blackboard.attacking
            && !blackboard.attackRecovering
            && !blackboard.dashing
            && !blackboard.wallSliding
            && !blackboard.wallJumping
            && !blackboard.recoiling
            && blackboard.actorState != HeroActorState.Hurt
            && blackboard.actorState != HeroActorState.Dead
            && healthState != null
            && !healthState.IsDepleted
            && healthState.CanHeal(config.bindHealAmount)
            && resourceState != null
            && resourceState.CanAfford(config.bindCostParts)
            && canPlayAnimation != null
            && canPlayAnimation();
    }

    private void TryComplete()
    {
        if (!active || completionCommitted || elapsed < config.bindDuration)
        {
            return;
        }

        if (!CanContinue())
        {
            Cancel();
            return;
        }

        completionCommitted = true;
        if (!resourceState.TrySpend(config.bindCostParts))
        {
            completionCommitted = false;
            Cancel();
            return;
        }

        int healed = healthState.Heal(config.bindHealAmount);
        if (healed <= 0)
        {
            // This is unreachable after the preflight checks in the single-threaded
            // gameplay loop, but retain the resource invariant if a future subscriber
            // mutates health during a notification.
            resourceState.Gain(config.bindCostParts);
            completionCommitted = false;
            Cancel();
            return;
        }

        active = false;
        HasCompleted = true;
        completionCommitted = false;
        elapsed = 0f;
        ExitBindingState();
    }

    private void ExitBindingState()
    {
        if (blackboard != null)
        {
            blackboard.binding = false;
            if (blackboard.actorState == HeroActorState.Binding)
            {
                blackboard.actorState = blackboard.grounded
                    ? HeroActorState.Grounded
                    : HeroActorState.Airborne;
            }
        }

        motor?.SetNormalMovementSuppressed(false);
        stopAnimation?.Invoke();
    }
}
