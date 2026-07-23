using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WorldGraphEditor;

[CustomEditor(typeof(TransitionPoint))]
public sealed class TransitionPointEditor : Editor
{
    private SerializedProperty assignedPort;
    private SerializedProperty assignedGuid;
    private SerializedProperty targetSceneFromGraph;
    private SerializedProperty gateSide;
    private SerializedProperty entryOffset;
    private SerializedProperty entryFacingOverride;
    private SerializedProperty entryRunInDuration;
    private SerializedProperty entryDropSpeed;
    private SerializedProperty bottomThrowHorizontal;
    private SerializedProperty bottomThrowVertical;
    private SerializedProperty bottomThrowDuration;
    private SerializedProperty bottomGateSpawnLift;
    private SerializedProperty entryMaxFallbackTime;
    private SerializedProperty isDoor;
    private SerializedProperty requireInteract;
    private SerializedProperty linkedRespawnMarker;

    private WGEProjectConfig _wgeConfig;
    private string _refreshError;
    private bool _guidIsStale;

    private void OnEnable()
    {
        assignedPort = serializedObject.FindProperty("_assignedPort");
        assignedGuid = serializedObject.FindProperty("_assignedGuid");
        targetSceneFromGraph = serializedObject.FindProperty("_targetScene");
        gateSide = serializedObject.FindProperty("gateSide");
        entryOffset = serializedObject.FindProperty("entryOffset");
        entryFacingOverride = serializedObject.FindProperty("entryFacingOverride");
        entryRunInDuration = serializedObject.FindProperty("entryRunInDuration");
        entryDropSpeed = serializedObject.FindProperty("entryDropSpeed");
        bottomThrowHorizontal = serializedObject.FindProperty("bottomThrowHorizontal");
        bottomThrowVertical = serializedObject.FindProperty("bottomThrowVertical");
        bottomThrowDuration = serializedObject.FindProperty("bottomThrowDuration");
        bottomGateSpawnLift = serializedObject.FindProperty("bottomGateSpawnLift");
        entryMaxFallbackTime = serializedObject.FindProperty("entryMaxFallbackTime");
        isDoor = serializedObject.FindProperty("isDoor");
        requireInteract = serializedObject.FindProperty("requireInteract");
        linkedRespawnMarker = serializedObject.FindProperty("linkedRespawnMarker");

        _wgeConfig = WGEProjectConfig.Instance;
        TryRefreshFromWorldGraph();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (!HasAllExpectedProperties())
        {
            EditorGUILayout.HelpBox(
                "TransitionPointEditor could not find one or more expected serialized fields. The default Inspector is shown so scene data remains editable.",
                MessageType.Error);
            DrawDefaultInspector();
            ApplyAndMarkDirtyIfChanged();
            return;
        }

        DrawWorldGraphPort();
        DrawIdentity();
        DrawActivation();
        DrawIncomingEntry();
        DrawGateSpecificTuning();
        DrawAdvanced();
        DrawValidationMessages();
        DrawButtons();

        ApplyAndMarkDirtyIfChanged();
    }

    private void DrawWorldGraphPort()
    {
        DrawSection("World Graph Port");

        if (_refreshError != null)
            EditorGUILayout.HelpBox($"WGE refresh failed: {_refreshError}", MessageType.Warning);

        EditorGUILayout.PropertyField(assignedPort, new GUIContent("Assigned Port"), true);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(assignedGuid, new GUIContent("GUID (read-only)"));
            EditorGUILayout.PropertyField(targetSceneFromGraph, new GUIContent("Target Scene (read-only)"));
        }

        if (IsBlank(GetCurrentAssignedGuid()))
        {
            EditorGUILayout.HelpBox(
                "No WGE port assigned. Select a port from the dropdown to enable transitions.",
                MessageType.Warning);
        }
        else if (_guidIsStale)
        {
            EditorGUILayout.HelpBox(
                "The assigned WGE port no longer exists in the graph (renamed or deleted). " +
                "The original GUID has been preserved — re-select the correct port from the dropdown, " +
                "or run Tools → Project → Validate Transition Gate Links.",
                MessageType.Warning);
        }
    }

    // Called in OnEnable (initial population), when the assigned port GUID changes
    // (ApplyAndMarkDirtyIfChanged), and via the manual button in DrawButtons.
    // Not called on every repaint to avoid querying WGE EditorData every frame.
    //
    // Mutation guard: PassageBase.Refresh() calls PortsDropdown.SetData(), which calls
    // RefreshSelectedData() when the assigned port no longer exists in the graph (renamed or
    // deleted). RefreshSelectedData() silently overwrites _selectedGuid and _selectedName with
    // _guidData[0] and its display name. We snapshot both fields before Refresh, detect the
    // mutation via a temporary SerializedObject, and restore the originals so the serialized
    // scene data is never changed without an explicit user action in the dropdown.
    private void TryRefreshFromWorldGraph()
    {
        if (_wgeConfig == null)
        {
            _refreshError = "WGEProjectConfig asset not found or could not be created.";
            return;
        }

        // Snapshot _selectedGuid and _selectedName before Refresh.
        SerializedProperty guidProp = assignedPort?.FindPropertyRelative("_selectedGuid");
        SerializedProperty nameProp = assignedPort?.FindPropertyRelative("_selectedName");
        string guidBefore = guidProp?.stringValue ?? "";
        string nameBefore = nameProp?.stringValue ?? "";

        try
        {
            ((TransitionPoint)target).Refresh(new RefreshContext(_wgeConfig));
            _refreshError = null;
        }
        catch (Exception e)
        {
            _refreshError = e.Message;
            return;
        }

        // Detect whether Refresh mutated _selectedGuid via a fresh SerializedObject that reads
        // the post-Refresh live object state. This must happen before serializedObject.Update()
        // so the mutation has not yet entered the main serialized object's change tracking.
        bool mutated = false;
        if (guidProp != null)
        {
            var checkSO = new SerializedObject(target);
            SerializedProperty checkGuid = checkSO.FindProperty("_assignedPort")?.FindPropertyRelative("_selectedGuid");
            SerializedProperty checkName = checkSO.FindProperty("_assignedPort")?.FindPropertyRelative("_selectedName");
            if (checkGuid != null && checkGuid.stringValue != guidBefore)
            {
                // Restore both fields so the serialized state is consistent with what the user set.
                mutated = true;
                checkGuid.stringValue = guidBefore;
                if (checkName != null)
                    checkName.stringValue = nameBefore;
                checkSO.ApplyModifiedProperties();
            }
        }

        _guidIsStale = mutated;

        // Sync the main serializedObject after restoration. This picks up the _assignedGuid and
        // _targetScene display-field changes from Refresh while leaving _selectedGuid unchanged.
        serializedObject.Update();
    }

    private void DrawIdentity()
    {
        DrawSection("Identity");
        DrawEnumField<GateSide>(gateSide, new GUIContent("Gate Side", "Direction or type of this gate."));
    }

    private void DrawActivation()
    {
        DrawSection("Activation");
        DrawBoolField(isDoor, new GUIContent("Is Door", "Doors can require interact or auto-trigger per instance. Normal edge gates auto-trigger."));

        using (new EditorGUI.DisabledScope(!isDoor.boolValue))
        {
            DrawBoolField(requireInteract, new GUIContent("Require Interact", "When enabled, this door only transitions through the interact flow."));
        }
    }

    private void DrawIncomingEntry()
    {
        DrawSection("Incoming Entry");
        DrawVector2Field(entryOffset, new GUIContent("Entry Offset", "World-space offset from this gate to the hero placement point."));
        DrawEnumField<EntryFacing>(entryFacingOverride, new GUIContent("Entry Facing Override", "Optional facing override applied when the hero arrives at this gate."));

        if (entryFacingOverride.intValue == (int)EntryFacing.None)
        {
            EditorGUILayout.HelpBox(
                "EntryFacing.None does not carry facing across scenes. The destination hero uses its default/current facing unless this gate forces a direction.",
                MessageType.Info);
        }
    }

    private void DrawGateSpecificTuning()
    {
        DrawSection("Gate-Specific Entry Tuning");

        GateSide side = (GateSide)gateSide.intValue;
        switch (side)
        {
            case GateSide.Left:
            case GateSide.Right:
                DrawFloatField(entryRunInDuration, new GUIContent("Entry Run In Duration"));
                DrawFloatField(entryMaxFallbackTime, new GUIContent("Entry Max Fallback Time"));
                break;

            case GateSide.Top:
                DrawFloatField(entryDropSpeed, new GUIContent("Entry Drop Speed"));
                DrawFloatField(entryMaxFallbackTime, new GUIContent("Entry Max Fallback Time"));
                break;

            case GateSide.Bottom:
                DrawFloatField(bottomThrowHorizontal, new GUIContent("Bottom Throw Horizontal"));
                DrawFloatField(bottomThrowVertical, new GUIContent("Bottom Throw Vertical"));
                DrawFloatField(bottomThrowDuration, new GUIContent("Bottom Throw Duration"));
                DrawFloatField(bottomGateSpawnLift, new GUIContent("Bottom Gate Spawn Lift"));
                DrawFloatField(entryMaxFallbackTime, new GUIContent("Entry Max Fallback Time"));
                break;

            case GateSide.Door:
                DrawFloatField(entryRunInDuration, new GUIContent("Entry Run In Duration"));
                break;

            case GateSide.Unknown:
            default:
                EditorGUILayout.HelpBox("No entry tuning is shown until Gate Side is set.", MessageType.Info);
                break;
        }
    }

    private void DrawAdvanced()
    {
        DrawSection("Advanced / Reserved");
        DrawObjectField<RespawnMarker>(linkedRespawnMarker, new GUIContent("Linked Respawn Marker"));
        EditorGUILayout.HelpBox(
            "linkedRespawnMarker is reserved for a later respawn policy pass. It is not used by TransitionPoint at runtime in the current implementation.",
            MessageType.Info);
    }

    private void DrawValidationMessages()
    {
        DrawSection("Validation");

        GateSide side = (GateSide)gateSide.intValue;

        if (side == GateSide.Unknown)
            EditorGUILayout.HelpBox("GateSide is Unknown. Pick Left, Right, Top, Bottom, or Door.", MessageType.Warning);

        if (isDoor.boolValue && side != GateSide.Door)
            EditorGUILayout.HelpBox("This is marked as a Door, but Gate Side is not Door.", MessageType.Warning);

        if (side == GateSide.Door && !isDoor.boolValue)
            EditorGUILayout.HelpBox("Gate Side is Door, but isDoor is false.", MessageType.Warning);

        Collider2D collider = ((TransitionPoint)target).GetComponent<Collider2D>();
        if (collider == null)
        {
            EditorGUILayout.HelpBox("No Collider2D found on this GameObject.", MessageType.Error);
        }
        else if (!collider.isTrigger)
        {
            EditorGUILayout.HelpBox("Collider2D exists but isTrigger is false. TransitionPoint requires a trigger collider.", MessageType.Warning);
        }

        if ((side == GateSide.Left || side == GateSide.Right || side == GateSide.Door) && entryRunInDuration.floatValue <= 0f)
            EditorGUILayout.HelpBox("entryRunInDuration should be greater than 0 for Left, Right, and Door gates.", MessageType.Warning);

        if (side == GateSide.Top && entryDropSpeed.floatValue <= 0f)
            EditorGUILayout.HelpBox("entryDropSpeed should be greater than 0 for Top gates.", MessageType.Warning);

        if (side == GateSide.Bottom)
        {
            if (bottomThrowVertical.floatValue <= 0f)
                EditorGUILayout.HelpBox("bottomThrowVertical should be greater than 0 for Bottom gates.", MessageType.Warning);

            if (bottomThrowDuration.floatValue <= 0f)
                EditorGUILayout.HelpBox("bottomThrowDuration should be greater than 0 for Bottom gates.", MessageType.Warning);
        }
    }

    private void DrawButtons()
    {
        DrawSection("Authoring");

        if (GUILayout.Button("Copy Passage GUID"))
        {
            EditorGUIUtility.systemCopyBuffer = GetCurrentAssignedGuid();
        }

        if (GUILayout.Button("Refresh World Graph Data"))
            TryRefreshFromWorldGraph();

        if (GUILayout.Button("Set Door Defaults"))
        {
            gateSide.intValue = (int)GateSide.Door;
            isDoor.boolValue = true;
            requireInteract.boolValue = true;
        }

        if (GUILayout.Button("Set Edge Gate Defaults"))
        {
            isDoor.boolValue = false;
            requireInteract.boolValue = false;
        }
    }

    private bool HasAllExpectedProperties()
    {
        return assignedPort != null
            && assignedGuid != null
            && targetSceneFromGraph != null
            && gateSide != null
            && entryOffset != null
            && entryFacingOverride != null
            && entryRunInDuration != null
            && entryDropSpeed != null
            && bottomThrowHorizontal != null
            && bottomThrowVertical != null
            && bottomThrowDuration != null
            && bottomGateSpawnLift != null
            && entryMaxFallbackTime != null
            && isDoor != null
            && requireInteract != null
            && linkedRespawnMarker != null;
    }

    private void ApplyAndMarkDirtyIfChanged()
    {
        // Snapshot the GUID before applying so we can tell whether the port assignment changed.
        string guidBefore = assignedPort?.FindPropertyRelative("_selectedGuid")?.stringValue ?? "";

        bool changed = serializedObject.ApplyModifiedProperties();
        if (!changed) return;

        TransitionPoint transitionPoint = (TransitionPoint)target;
        EditorUtility.SetDirty(transitionPoint);
        if (transitionPoint.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(transitionPoint.gameObject.scene);

        // Only call Refresh when the assigned port GUID itself changed (i.e. the user picked a
        // different port in the dropdown). Refreshing for unrelated changes such as entry offset
        // or gate side is unnecessary, and calling Refresh on those changes is the exact path
        // that can expose the PassageBase.Refresh() / RefreshSelectedData() silent mutation
        // described in TryRefreshFromWorldGraph. When the user picks a valid port from the
        // dropdown, that GUID exists in the current graph data, so SetData's early-exit fires
        // and RefreshSelectedData() is never called.
        string guidAfter = assignedPort?.FindPropertyRelative("_selectedGuid")?.stringValue ?? "";
        if (guidAfter != guidBefore)
            TryRefreshFromWorldGraph();
    }

    private string GetCurrentAssignedGuid()
    {
        SerializedProperty selectedGuid = assignedPort.FindPropertyRelative("_selectedGuid");
        if (selectedGuid != null)
            return selectedGuid.stringValue;

        return ((TransitionPoint)target).GetGuid();
    }

    private static void DrawSection(string label)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }

    private static void DrawBoolField(SerializedProperty property, GUIContent label)
    {
        Rect rect = EditorGUILayout.GetControlRect();
        EditorGUI.BeginProperty(rect, label, property);
        property.boolValue = EditorGUI.Toggle(rect, label, property.boolValue);
        EditorGUI.EndProperty();
    }

    private static void DrawFloatField(SerializedProperty property, GUIContent label)
    {
        Rect rect = EditorGUILayout.GetControlRect();
        EditorGUI.BeginProperty(rect, label, property);
        property.floatValue = EditorGUI.FloatField(rect, label, property.floatValue);
        EditorGUI.EndProperty();
    }

    private static void DrawVector2Field(SerializedProperty property, GUIContent label)
    {
        Rect rect = EditorGUILayout.GetControlRect(true);
        EditorGUI.BeginProperty(rect, label, property);
        property.vector2Value = EditorGUI.Vector2Field(rect, label, property.vector2Value);
        EditorGUI.EndProperty();
    }

    private static void DrawEnumField<TEnum>(SerializedProperty property, GUIContent label) where TEnum : System.Enum
    {
        Rect rect = EditorGUILayout.GetControlRect();
        TEnum currentValue = (TEnum)System.Enum.ToObject(typeof(TEnum), property.intValue);
        EditorGUI.BeginProperty(rect, label, property);
        TEnum nextValue = (TEnum)EditorGUI.EnumPopup(rect, label, currentValue);
        property.intValue = System.Convert.ToInt32(nextValue);
        EditorGUI.EndProperty();
    }

    private static void DrawObjectField<TObject>(SerializedProperty property, GUIContent label) where TObject : UnityEngine.Object
    {
        Rect rect = EditorGUILayout.GetControlRect();
        EditorGUI.BeginProperty(rect, label, property);
        property.objectReferenceValue = EditorGUI.ObjectField(
            rect,
            label,
            property.objectReferenceValue,
            typeof(TObject),
            true);
        EditorGUI.EndProperty();
    }

    private static bool IsBlank(string value)
    {
        return string.IsNullOrWhiteSpace(value);
    }
}
