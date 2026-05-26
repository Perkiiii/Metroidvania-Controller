using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TransitionPoint))]
public sealed class TransitionPointEditor : Editor
{
    private SerializedProperty gateKey;
    private SerializedProperty gateSide;
    private SerializedProperty targetScene;
    private SerializedProperty entryGateKey;
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

    private void OnEnable()
    {
        gateKey = serializedObject.FindProperty("gateKey");
        gateSide = serializedObject.FindProperty("gateSide");
        targetScene = serializedObject.FindProperty("targetScene");
        entryGateKey = serializedObject.FindProperty("entryGateKey");
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
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawIdentity();
        DrawOutgoingTransition();
        DrawActivation();
        DrawIncomingEntry();
        DrawGateSpecificTuning();
        DrawAdvanced();
        DrawValidationMessages();
        DrawButtons();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawIdentity()
    {
        DrawSection("Identity");
        DrawStringField(gateKey, new GUIContent("Gate Key", "Stable key for this gate. Destination gates are resolved by this value."));
        DrawEnumField<GateSide>(gateSide, new GUIContent("Gate Side", "Direction or type of this gate."));
    }

    private void DrawOutgoingTransition()
    {
        DrawSection("Outgoing Transition");
        DrawStringField(targetScene, new GUIContent("Target Scene", "Build Settings scene name to load when this gate is activated."));
        DrawStringField(entryGateKey, new GUIContent("Entry Gate Key", "Destination gateKey inside the target scene."));

        if (IsBlank(targetScene.stringValue) && IsBlank(entryGateKey.stringValue))
        {
            EditorGUILayout.HelpBox(
                "Destination-only gate: blank targetScene and entryGateKey means this gate can be used as an arrival point without starting an outgoing transition.",
                MessageType.Info);
        }
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

        if (IsBlank(gateKey.stringValue))
            EditorGUILayout.HelpBox("gateKey is blank. Give this gate a stable key before using it as a destination.", MessageType.Warning);

        bool hasTargetScene = !IsBlank(targetScene.stringValue);
        bool hasEntryGateKey = !IsBlank(entryGateKey.stringValue);

        if (hasTargetScene && !hasEntryGateKey)
            EditorGUILayout.HelpBox("targetScene is set, but entryGateKey is blank.", MessageType.Warning);

        if (!hasTargetScene && hasEntryGateKey)
            EditorGUILayout.HelpBox("entryGateKey is set, but targetScene is blank.", MessageType.Warning);

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

        if (GUILayout.Button("Copy Gate Key"))
        {
            EditorGUIUtility.systemCopyBuffer = gateKey.stringValue;
        }

        if (GUILayout.Button("Set Door Defaults"))
        {
            gateSide.intValue = (int)GateSide.Door;
            isDoor.boolValue = true;
            requireInteract.boolValue = true;
            serializedObject.ApplyModifiedProperties();
        }

        if (GUILayout.Button("Set Edge Gate Defaults"))
        {
            isDoor.boolValue = false;
            requireInteract.boolValue = false;
            serializedObject.ApplyModifiedProperties();
        }
    }

    private bool HasAllExpectedProperties()
    {
        return gateKey != null
            && gateSide != null
            && targetScene != null
            && entryGateKey != null
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

    private static void DrawSection(string label)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }

    private static void DrawStringField(SerializedProperty property, GUIContent label)
    {
        Rect rect = EditorGUILayout.GetControlRect();
        EditorGUI.BeginProperty(rect, label, property);
        property.stringValue = EditorGUI.TextField(rect, label, property.stringValue);
        EditorGUI.EndProperty();
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

    private static void DrawObjectField<TObject>(SerializedProperty property, GUIContent label) where TObject : Object
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
