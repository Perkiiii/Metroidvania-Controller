using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Read-only runtime diagnostics appended below the default Inspector. Does not add editable
// fields, reorder locks, or mutate scene state -- Camera Phase 1 lock selection and Phase 2
// transition ownership remain entirely in CameraController/GameCameras.
[CustomEditor(typeof(CameraController))]
public sealed class CameraControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CameraController controller = (CameraController)target;
        DrawRuntimeState(controller);

        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private static void DrawRuntimeState(CameraController controller)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Runtime Diagnostics (Play Mode)", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to see live mode, lock selection, transition, and legal-region state.", MessageType.Info);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.LabelField("Resolved Mode", controller.Mode.ToString());

            CameraLockArea currentLock = controller.CurrentLockArea;
            EditorGUILayout.LabelField("Selected Lock", currentLock != null ? currentLock.name : "(none — room follow)");
            if (currentLock != null)
            {
                EditorGUILayout.LabelField("Selected Priority / Sequence", $"{currentLock.Priority} / {controller.CurrentLockEntrySequence}");
            }

            CameraBoundsVolume bounds = controller.CurrentBoundsVolume;
            EditorGUILayout.LabelField("Active Bounds Volume", bounds != null ? bounds.name : "(none)");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Registered Locks (selection order)", EditorStyles.miniBoldLabel);
            IReadOnlyList<CameraLockArea> registered = controller.GetRegisteredLockAreasSorted();
            if (registered.Count == 0)
            {
                EditorGUILayout.LabelField("(none registered)");
            }
            else
            {
                for (int i = 0; i < registered.Count; i++)
                {
                    CameraLockArea area = registered[i];
                    if (area == null)
                    {
                        continue;
                    }

                    string marker = area == currentLock ? " <- selected" : string.Empty;
                    EditorGUILayout.LabelField($"  {i + 1}. {area.name} (P{area.Priority}){marker}");
                }
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Transition", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Cause", controller.CurrentTransitionCause.ToString());
            EditorGUILayout.LabelField("Active", controller.IsTransitioning.ToString());
            EditorGUILayout.LabelField("Progress", $"{controller.TransitionProgress:P0} ({controller.TransitionElapsed:0.###}s / {controller.TransitionDuration:0.###}s)");
            EditorGUILayout.LabelField(
                "Source -> Destination Lock",
                $"{DescribeLock(controller.TransitionSourceLockArea)} -> {DescribeLock(controller.TransitionDestinationLockArea)}");
            EditorGUILayout.LabelField("Last Application", controller.LastApplicationWasImmediate ? "Immediate" : "Live");
            EditorGUILayout.LabelField("Damp Time X / Y", $"{controller.CurrentDampTimeX:0.###} / {controller.CurrentDampTimeY:0.###}");

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Legal Region / Destination", EditorStyles.miniBoldLabel);
            CameraLegalRegion region = controller.GetLegalRegion();
            EditorGUILayout.LabelField("Legal X", region.XConstrained ? $"{region.MinX:0.##} .. {region.MaxX:0.##}" : "(unconstrained)");
            EditorGUILayout.LabelField("Legal Y", region.YConstrained ? $"{region.MinY:0.##} .. {region.MaxY:0.##}" : "(unconstrained)");
            EditorGUILayout.LabelField("Current Destination", controller.CurrentDestination.ToString("0.##"));
            EditorGUILayout.LabelField("Rendered Position", controller.RenderedPosition.ToString("0.##"));

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Freeze Requests", EditorStyles.miniBoldLabel);
            DrawFreezeState();
        }
    }

    private static void DrawFreezeState()
    {
        GameCameras cameras = GameCameras.Instance;
        if (cameras == null)
        {
            EditorGUILayout.LabelField("(no persistent GameCameras instance)");
            return;
        }

        IReadOnlyList<CameraFreezeSnapshot> snapshots = cameras.GetFreezeSnapshots();
        EditorGUILayout.LabelField("Active Count", snapshots.Count.ToString());
        for (int i = 0; i < snapshots.Count; i++)
        {
            CameraFreezeSnapshot snapshot = snapshots[i];
            string duration = snapshot.RemainingSeconds < 0f ? "indefinite" : $"{snapshot.RemainingSeconds:0.##}s remaining";
            EditorGUILayout.LabelField($"  {i + 1}. {snapshot.Kind} from '{snapshot.SourceLabel}' ({duration})");
        }
    }

    private static string DescribeLock(CameraLockArea area)
    {
        return area != null ? area.name : "(follow)";
    }
}
