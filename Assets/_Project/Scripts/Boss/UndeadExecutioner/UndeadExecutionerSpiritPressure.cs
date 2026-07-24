using System;
using System.Collections;
using Animancer;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class UndeadExecutionerSpiritPressure : MonoBehaviour
{
    public event Action Completed;

    [SerializeField] private GameObject presentationRoot;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private AnimancerComponent animancer;
    [SerializeField] private AnimationClip appearingClip;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip deathClip;
    [SerializeField] private EnemyAttackController attackController;
    [SerializeField] private DamageHero damageHero;
    [SerializeField] private float animationFadeDuration = 0.04f;
    [SerializeField] private float appearanceFailSafeDuration = 0.8f;
    [SerializeField] private float idleTelegraphDuration = 0.35f;
    [SerializeField] private float deathFailSafeDuration = 0.65f;

    private Coroutine pressureRoutine;
    private AnimancerState activeState;
    private bool active;
    private bool completionSent;

    public bool IsActive => active;
    public EnemyAttackController AttackController => attackController;
    public DamageHero DamageHero => damageHero;
    public GameObject PresentationRoot => presentationRoot;
    public AnimationClip AppearingClip => appearingClip;
    public AnimationClip IdleClip => idleClip;
    public AnimationClip DeathClip => deathClip;

    private void Awake()
    {
        if (presentationRoot == null)
        {
            presentationRoot = gameObject;
        }

        SetPresentationActive(false);
    }

    private void OnDisable()
    {
        Interrupt();
    }

    public bool Configure(UndeadExecutionerConfig config)
    {
        if (config == null || attackController == null)
        {
            return false;
        }

        UndeadExecutionerConfig.AttackTiming timing = config.spiritTiming;
        if (!attackController.TryConfigureTimings(
                timing.startup,
                timing.active,
                timing.recovery,
                timing.cooldown))
        {
            return false;
        }

        if (damageHero != null)
        {
            damageHero.damageDealt = config.spiritDamage;
        }

        return true;
    }

    public bool Begin(float worldX, float worldY)
    {
        if (active || attackController == null || animancer == null)
        {
            return false;
        }

        transform.position = new Vector3(worldX, worldY, transform.position.z);
        active = true;
        completionSent = false;
        SetPresentationActive(true);
        pressureRoutine = StartCoroutine(PressureRoutine());
        return true;
    }

    public void Interrupt()
    {
        active = false;
        completionSent = false;

        if (pressureRoutine != null)
        {
            StopCoroutine(pressureRoutine);
            pressureRoutine = null;
        }

        ClearActiveEndEvent();
        attackController?.InterruptAttack(false);
        SetPresentationActive(false);
    }

    private IEnumerator PressureRoutine()
    {
        yield return PlayAndWait(appearingClip, appearanceFailSafeDuration);

        if (!active)
        {
            yield break;
        }

        PlayLoop(idleClip);
        yield return new WaitForSeconds(idleTelegraphDuration);

        if (!active || !attackController.BeginAttack())
        {
            FinishImmediately();
            yield break;
        }

        float attackFailSafe = Mathf.Max(
            0.1f,
            appearanceFailSafeDuration + idleTelegraphDuration + deathFailSafeDuration);
        float elapsed = 0f;
        while (active && attackController.IsAttacking && elapsed < attackFailSafe)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        attackController.InterruptAttack(false);
        yield return PlayAndWait(deathClip, deathFailSafeDuration);
        FinishImmediately();
    }

    private IEnumerator PlayAndWait(AnimationClip clip, float failSafeDuration)
    {
        bool ended = false;
        if (clip != null && animancer != null)
        {
            activeState = animancer.Play(clip, animationFadeDuration, FadeMode.FromStart);
            activeState.Events(this).OnEnd = () =>
            {
                ClearActiveEndEvent();
                ended = true;
            };
        }
        else
        {
            ended = true;
        }

        float elapsed = 0f;
        while (active && !ended && elapsed < Mathf.Max(0f, failSafeDuration))
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearActiveEndEvent();
    }

    private void PlayLoop(AnimationClip clip)
    {
        ClearActiveEndEvent();
        if (clip != null && animancer != null)
        {
            activeState = animancer.Play(clip, animationFadeDuration, FadeMode.FromStart);
        }
    }

    private void FinishImmediately()
    {
        if (!active)
        {
            return;
        }

        active = false;
        pressureRoutine = null;
        attackController?.InterruptAttack(false);
        SetPresentationActive(false);

        if (completionSent)
        {
            return;
        }

        completionSent = true;
        Completed?.Invoke();
    }

    private void ClearActiveEndEvent()
    {
        if (activeState == null)
        {
            return;
        }

        activeState.Events(this).OnEnd = null;
        activeState = null;
    }

    private void SetPresentationActive(bool value)
    {
        if (presentationRoot != null && presentationRoot != gameObject)
        {
            presentationRoot.SetActive(value);
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.enabled = value;
        }
    }
}
