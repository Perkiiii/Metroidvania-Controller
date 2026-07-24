using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Project/Boss/Undead Executioner Config", fileName = "UndeadExecutionerConfig")]
public sealed class UndeadExecutionerConfig : ScriptableObject
{
    [Serializable]
    public struct AttackTiming
    {
        [Min(0f)] public float startup;
        [Min(0f)] public float active;
        [Min(0f)] public float recovery;
        [Min(-1f)] public float cooldown;
    }

    [Header("Presentation")]
    public AnimationClip idleClip;
    public AnimationClip phaseTwoIdleClip;
    public AnimationClip executionerComboClip;
    public AnimationClip shadowBurstClip;
    public AnimationClip summonClip;
    public AnimationClip deathClip;
    [Min(0f)] public float animationFadeDuration = 0.05f;
    [Min(0f)] public float introDuration = 0.7f;
    [Min(0f)] public float attackFailSafePadding = 0.35f;
    [Min(0f)] public float phaseTransitionFailSafePadding = 0.35f;
    [Min(0f)] public float deathFailSafeDuration = 2.25f;

    [Header("Neutral and Glide")]
    [Min(0f)] public float phaseOneDecisionDelay = 0.65f;
    [Min(0f)] public float phaseTwoDecisionDelay = 0.45f;
    [Min(0f)] public float tooCloseDistance = 1.5f;
    [Min(0f)] public float comboMinimumRange = 1.5f;
    [Min(0f)] public float comboMaximumRange = 2.5f;
    [Min(0f)] public float preferredDistanceMinimum = 3f;
    [Min(0f)] public float preferredDistanceMaximum = 5f;
    [Min(0f)] public float glideMinimumDistance = 2f;
    [Min(0f)] public float glideMaximumDistance = 4f;
    [Min(0f)] public float glideSpeed = 2.5f;
    [Min(0f)] public float arenaEdgeClearance = 0.75f;
    [Min(0f)] public float heroCrossingClearance = 0.8f;
    [Min(0f)] public float glideStopTolerance = 0.08f;

    [Header("Combat")]
    [Range(0.05f, 0.95f)] public float phaseTwoHealthRatio = 0.5f;
    [Min(1)] public int comboDamage = 1;
    [Min(1)] public int shadowBurstDamage = 1;
    public AttackTiming comboFirstTiming = new AttackTiming
    {
        startup = 0.3f,
        active = 0.2f,
        recovery = 0.3f,
        cooldown = 0f
    };
    public AttackTiming comboSecondTiming = new AttackTiming
    {
        startup = 0.3f,
        active = 0.2f,
        recovery = 0.35f,
        cooldown = 1f
    };
    public AttackTiming shadowBurstTiming = new AttackTiming
    {
        startup = 0.45f,
        active = 0.22f,
        recovery = 0.45f,
        cooldown = 1.6f
    };

    [Header("Spirit Pressure")]
    [Min(0f)] public float spiritInterval = 5.25f;
    [Min(0f)] public float spiritHeroClearance = 1.25f;
    [Min(0f)] public float spiritWallClearance = 1f;
    [Min(0f)] public float spiritVerticalOffset = 0f;
    [Min(1)] public int spiritDamage = 1;
    public AttackTiming spiritTiming = new AttackTiming
    {
        startup = 0.45f,
        active = 0.18f,
        recovery = 0.35f,
        cooldown = 0f
    };

    public float GetDecisionDelay(bool phaseTwo)
    {
        return phaseTwo ? phaseTwoDecisionDelay : phaseOneDecisionDelay;
    }
}
