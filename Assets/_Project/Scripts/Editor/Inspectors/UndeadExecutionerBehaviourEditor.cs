using UnityEditor;
using UnityEngine;

// Adds a small read-only debug section below the default Inspector. Does not replace or
// restructure the default field layout -- this is deliberately not a full custom boss editor.
//
// Exists because EnemyAttackController.TryConfigureTimings overwrites the serialized
// Startup/Active/Recovery/Cooldown fields shown directly on ComboFirst/ComboSecond/ShadowBurst
// and the spirit's own EnemyAttackController at PrepareForEncounter time. Selecting one of those
// components in the Inspector while not in Play Mode shows unused prefab defaults, not the real
// timing. UndeadExecutionerConfig remains the single source of truth; this only surfaces it.
[CustomEditor(typeof(UndeadExecutionerBehaviour))]
public sealed class UndeadExecutionerBehaviourEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        UndeadExecutionerBehaviour behaviour = (UndeadExecutionerBehaviour)target;
        DrawEffectiveTimings(behaviour);
        DrawRuntimeState(behaviour);

        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private static void DrawEffectiveTimings(UndeadExecutionerBehaviour behaviour)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Effective Attack Timings (from UndeadExecutionerConfig)", EditorStyles.boldLabel);

        UndeadExecutionerConfig config = behaviour.Config;
        if (config == null)
        {
            EditorGUILayout.HelpBox(
                "No UndeadExecutionerConfig assigned. The Startup/Active/Recovery/Cooldown fields " +
                "on ComboFirst/ComboSecond/ShadowBurst/Spirit's EnemyAttackController components are " +
                "unused prefab defaults until a config is assigned.",
                MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            "PrepareForEncounter applies these values to ComboFirst/ComboSecond/ShadowBurst/Spirit " +
            "via TryConfigureTimings. The serialized fields shown directly on those EnemyAttackController " +
            "components are prefab defaults that get overwritten at runtime -- tune combat timing here, " +
            "not there.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(true))
        {
            DrawTiming("Combo First", config.comboFirstTiming);
            DrawTiming("Combo Second", config.comboSecondTiming);
            DrawTiming("Shadow Burst", config.shadowBurstTiming);
            DrawTiming("Spirit Pressure", config.spiritTiming);
        }
    }

    private static void DrawTiming(string label, UndeadExecutionerConfig.AttackTiming timing)
    {
        EditorGUILayout.LabelField(
            label,
            $"Startup {timing.startup:0.###}s / Active {timing.active:0.###}s / Recovery {timing.recovery:0.###}s / Cooldown {timing.cooldown:0.###}s");
    }

    private static void DrawRuntimeState(UndeadExecutionerBehaviour behaviour)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Runtime State (Play Mode)", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to see live State, Current Attack, Phase, Prepared, hover height, and arena limits.",
                MessageType.Info);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.LabelField("Prepared", behaviour.IsPrepared.ToString());
            EditorGUILayout.LabelField("State", behaviour.State.ToString());
            EditorGUILayout.LabelField("Current Attack", behaviour.CurrentAttack.ToString());
            EditorGUILayout.LabelField("Phase Two", behaviour.IsPhaseTwo.ToString());
            EditorGUILayout.LabelField("Authored Hover Y", behaviour.AuthoredHoverY.ToString("0.###"));
            EditorGUILayout.LabelField(
                "Arena Limits",
                behaviour.ArenaLeftLimit != null && behaviour.ArenaRightLimit != null
                    ? $"{behaviour.ArenaLeftLimit.position.x:0.###} .. {behaviour.ArenaRightLimit.position.x:0.###}"
                    : "not assigned");
        }
    }
}
