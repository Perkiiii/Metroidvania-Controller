using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BossEncounterDebugTools
{
    private const string ClearDefeatedRecordMenu =
        "CONTEXT/BossEncounterController/Debug: Clear Defeated Record And Reload Scene";

    [MenuItem(ClearDefeatedRecordMenu)]
    private static void ClearDefeatedRecordAndReloadScene(MenuCommand command)
    {
        BossEncounterController controller = command.context as BossEncounterController;
        if (controller == null || controller.Definition == null || controller.WorldStateRegistry == null)
        {
            Debug.LogWarning(
                "[BossEncounterDebugTools] The selected encounter is missing its definition or registry; nothing was changed.",
                controller);
            return;
        }

        SaveManager saveManager = SaveManager.Instance;
        if (!Application.isPlaying || saveManager == null || saveManager.CurrentSave == null)
        {
            Debug.LogWarning(
                "[BossEncounterDebugTools] Enter Play Mode through Boot so an initialized SaveManager can persist the debug reset. Nothing was changed.",
                controller);
            return;
        }

        string encounterId = controller.Definition.EncounterId;
        if (!controller.WorldStateRegistry.EditorClearEncounterDefeatedRecord(encounterId))
        {
            Debug.LogWarning(
                $"[BossEncounterDebugTools] Encounter '{encounterId}' has no defeated record to clear.",
                controller);
            return;
        }

        saveManager.Save();
        Debug.Log(
            $"[BossEncounterDebugTools] Cleared and saved defeated record '{encounterId}'. Reloading '{controller.gameObject.scene.path}' through the normal scene flow.",
            controller);

        string sceneName = controller.gameObject.scene.name;
        if (GameManager.Instance == null || !GameManager.Instance.BeginSceneTransition(sceneName))
        {
            Debug.LogWarning(
                "[BossEncounterDebugTools] The normal scene transition could not start; reloading the same scene directly.",
                controller);
            SceneManager.LoadScene(sceneName);
        }
    }

    [MenuItem(ClearDefeatedRecordMenu, true)]
    private static bool ValidateClearDefeatedRecordAndReloadScene(MenuCommand command)
    {
        return command.context is BossEncounterController;
    }
}
