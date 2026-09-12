using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authoring inspector for a seasonal weather transition matrix.
///
/// The matrix is kept as the row-major serialized array owned by SeasonDefinition. This editor
/// only presents that data as a FROM/TO grid and uses SerializedProperty for every mutation so
/// normal Inspector undo and prefab/asset dirty tracking continue to work.
/// </summary>
[CustomEditor(typeof(SeasonDefinition))]
public sealed class SeasonDefinitionEditor : Editor
{
    private const float RowLabelWidth = 112f;
    private const float ColumnWidth = 72f;
    private const float RowTotalWidth = 82f;
    private const float ActionsWidth = 174f;
    private const float ActionButtonWidth = 83f;

    private SerializedProperty seasonIdProperty;
    private SerializedProperty displayNameProperty;
    private SerializedProperty entryWeatherTypeProperty;
    private SerializedProperty transitionWeightsProperty;
    private SerializedProperty fixedWeatherSlotsProperty;

    private Vector2 matrixScrollPosition;
    private GUIStyle matrixHeaderStyle;
    private static bool weatherTypeCountCached;
    private static int cachedWeatherTypeCount;

    private GUIStyle MatrixHeaderStyle
    {
        get
        {
            if (matrixHeaderStyle == null)
            {
                matrixHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };
            }

            return matrixHeaderStyle;
        }
    }

    private void OnEnable()
    {
        seasonIdProperty = serializedObject.FindProperty("seasonId");
        displayNameProperty = serializedObject.FindProperty("displayName");
        entryWeatherTypeProperty = serializedObject.FindProperty("entryWeatherType");
        transitionWeightsProperty = serializedObject.FindProperty("transitionWeights");
        fixedWeatherSlotsProperty = serializedObject.FindProperty("fixedWeatherSlots");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (!HasExpectedProperties())
        {
            EditorGUILayout.HelpBox(
                "SeasonDefinitionEditor could not find one or more expected serialized fields. " +
                "The default Inspector is shown so the asset remains editable.",
                MessageType.Error);
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();
            return;
        }

        DrawSeasonFields();
        DrawTransitionMatrix();
        DrawFixedWeatherSlots();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSeasonFields()
    {
        DrawSection("Season");
        EditorGUILayout.PropertyField(seasonIdProperty, new GUIContent("Season ID"));
        EditorGUILayout.PropertyField(displayNameProperty, new GUIContent("Display Name"));
        EditorGUILayout.PropertyField(entryWeatherTypeProperty, new GUIContent("Entry Weather"));
    }

    private void DrawTransitionMatrix()
    {
        DrawSection("Transition Weights");

        int weatherTypeCount = CurrentWeatherTypeCount;
        MatrixResizeKind resizeKind = ClassifyMatrixLength(
            transitionWeightsProperty.arraySize,
            weatherTypeCount,
            out int serializedDimension);

        switch (resizeKind)
        {
            case MatrixResizeKind.Current:
                DrawCurrentMatrix(weatherTypeCount);
                break;

            case MatrixResizeKind.SafeExpansion:
                EditorGUILayout.HelpBox(
                    $"The serialized transition matrix is {serializedDimension}x{serializedDimension}, " +
                    $"but the current WeatherType enum has {weatherTypeCount} values. " +
                    "Existing FROM/TO cells can be preserved while adding zero-valued cells for the new weather.",
                    MessageType.Info);

                if (GUILayout.Button("Resize Matrix (Preserve Existing Cells)"))
                {
                    ResizeMatrixPreservingCells(serializedDimension, weatherTypeCount);
                }

                break;

            case MatrixResizeKind.DestructiveShrink:
                EditorGUILayout.HelpBox(
                    $"The serialized transition matrix is {serializedDimension}x{serializedDimension}, " +
                    $"but the current WeatherType enum has only {weatherTypeCount} values. " +
                    "Rows and columns for removed weather values would be discarded. No data has been changed.",
                    MessageType.Warning);

                if (GUILayout.Button("Resize Matrix (Discard Removed Cells)"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Shrink transition matrix?",
                        $"This will discard FROM/TO cells for {serializedDimension - weatherTypeCount} removed weather value(s). " +
                        "The operation cannot be recovered except through Undo.",
                        "Resize and Discard",
                        "Cancel");

                    if (confirmed)
                    {
                        ResizeMatrixPreservingCells(serializedDimension, weatherTypeCount);
                    }
                }

                EditorGUILayout.HelpBox(
                    "To keep the current values, restore the append-only WeatherType enum or cancel the resize. " +
                    "The matrix remains untouched until the confirmation above is accepted.",
                    MessageType.Info);
                break;

            case MatrixResizeKind.Ambiguous:
            default:
                EditorGUILayout.HelpBox(
                    $"The transition matrix contains {transitionWeightsProperty.arraySize} serialized value(s), " +
                    $"which cannot be mapped safely to the current {weatherTypeCount}x{weatherTypeCount} matrix. " +
                    "No values have been changed. Repair the serialized array or use the confirmed reset below.",
                    MessageType.Warning);

                if (GUILayout.Button("Reset Matrix (Discard Existing Values)"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Reset transition matrix?",
                        "This will discard every serialized transition weight and create a zero-filled matrix " +
                        $"for the current {weatherTypeCount} weather value(s).",
                        "Reset Matrix",
                        "Cancel");

                    if (confirmed)
                    {
                        SetMatrixValues(new int[weatherTypeCount * weatherTypeCount]);
                    }
                }

                EditorGUILayout.HelpBox(
                    "After a reset, use Set Uniform on each FROM row (or author individual cells) so every " +
                    "row has a positive total before validating the season.",
                    MessageType.Info);
                break;
        }
    }

    private void DrawCurrentMatrix(int weatherTypeCount)
    {
        EditorGUILayout.HelpBox(
            "Weights are authored as non-negative integer values. Each row is indexed FROM the row weather " +
            "to the TO column weather; runtime validation requires every row total to be greater than zero.",
            MessageType.Info);

        matrixScrollPosition = EditorGUILayout.BeginScrollView(matrixScrollPosition, false, true);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("FROM / TO", MatrixHeaderStyle, GUILayout.Width(RowLabelWidth));
        for (int toIndex = 0; toIndex < weatherTypeCount; toIndex++)
        {
            GUILayout.Label(
                $"TO\n{GetWeatherTypeName(toIndex)}",
                MatrixHeaderStyle,
                GUILayout.Width(ColumnWidth));
        }

        GUILayout.Label("Row Total", MatrixHeaderStyle, GUILayout.Width(RowTotalWidth));
        GUILayout.Label("Actions", MatrixHeaderStyle, GUILayout.Width(ActionsWidth));
        EditorGUILayout.EndHorizontal();

        for (int fromIndex = 0; fromIndex < weatherTypeCount; fromIndex++)
        {
            DrawMatrixRow(fromIndex, weatherTypeCount);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawMatrixRow(int fromIndex, int weatherTypeCount)
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(
            $"FROM {GetWeatherTypeName(fromIndex)}",
            EditorStyles.miniBoldLabel,
            GUILayout.Width(RowLabelWidth));

        long rowTotal = 0L;
        for (int toIndex = 0; toIndex < weatherTypeCount; toIndex++)
        {
            int serializedIndex = fromIndex * weatherTypeCount + toIndex;
            SerializedProperty cellProperty = transitionWeightsProperty.GetArrayElementAtIndex(serializedIndex);
            rowTotal += cellProperty.intValue;
            EditorGUILayout.PropertyField(cellProperty, GUIContent.none, GUILayout.Width(ColumnWidth));
        }

        GUILayout.Label(rowTotal.ToString(), GUILayout.Width(RowTotalWidth));

        EditorGUILayout.BeginHorizontal(GUILayout.Width(ActionsWidth));
        if (GUILayout.Button("Set Uniform", GUILayout.Width(ActionButtonWidth)))
        {
            SetUniformRow(fromIndex, weatherTypeCount);
        }

        if (GUILayout.Button("Clear Row", GUILayout.Width(ActionButtonWidth)))
        {
            ClearRow(fromIndex, weatherTypeCount);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawFixedWeatherSlots()
    {
        DrawSection("Fixed Weather Slots");
        EditorGUILayout.PropertyField(
            fixedWeatherSlotsProperty,
            new GUIContent(
                "Fixed Slots",
                "Optional concrete day/slot weather values. Duplicate keys and invalid values are rejected by SeasonDefinition validation."),
            true);
    }

    private void ResizeMatrixPreservingCells(int sourceDimension, int destinationDimension)
    {
        int[] sourceValues = ReadMatrixValues();
        int[] remappedValues = RemapRowMajorPreservingCells(
            sourceValues,
            sourceDimension,
            destinationDimension);
        SetMatrixValues(remappedValues);
    }

    private int[] ReadMatrixValues()
    {
        int[] values = new int[transitionWeightsProperty.arraySize];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = transitionWeightsProperty.GetArrayElementAtIndex(i).intValue;
        }

        return values;
    }

    private void SetMatrixValues(IReadOnlyList<int> values)
    {
        transitionWeightsProperty.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
        {
            transitionWeightsProperty.GetArrayElementAtIndex(i).intValue = values[i];
        }
    }

    private void SetUniformRow(int fromIndex, int weatherTypeCount)
    {
        int firstIndex = fromIndex * weatherTypeCount;
        for (int toIndex = 0; toIndex < weatherTypeCount; toIndex++)
        {
            transitionWeightsProperty
                .GetArrayElementAtIndex(firstIndex + toIndex)
                .intValue = 1;
        }
    }

    private void ClearRow(int fromIndex, int weatherTypeCount)
    {
        int firstIndex = fromIndex * weatherTypeCount;
        for (int toIndex = 0; toIndex < weatherTypeCount; toIndex++)
        {
            transitionWeightsProperty
                .GetArrayElementAtIndex(firstIndex + toIndex)
                .intValue = 0;
        }
    }

    private bool HasExpectedProperties()
    {
        return seasonIdProperty != null
            && displayNameProperty != null
            && entryWeatherTypeProperty != null
            && transitionWeightsProperty != null
            && fixedWeatherSlotsProperty != null;
    }

    private static void DrawSection(string label)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }

    /// <summary>
    /// Reads the runtime WeatherTypeUtility.Count without making the editor assembly depend on the
    /// utility's internal accessibility. The enum fallback is only for an incomplete compile/domain
    /// reload; normal project compilation always resolves the authoritative runtime constant.
    /// </summary>
    private static int CurrentWeatherTypeCount
    {
        get
        {
            if (weatherTypeCountCached)
            {
                return cachedWeatherTypeCount;
            }

            Type utilityType = typeof(WeatherType).Assembly.GetType("WeatherTypeUtility", false);
            FieldInfo countField = utilityType?.GetField(
                "Count",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            object rawCount = countField?.GetRawConstantValue();
            if (rawCount is int authoritativeCount && authoritativeCount > 0)
            {
                cachedWeatherTypeCount = authoritativeCount;
            }
            else
            {
                cachedWeatherTypeCount = Enum.GetValues(typeof(WeatherType)).Length;
            }

            weatherTypeCountCached = true;
            return cachedWeatherTypeCount;
        }
    }

    private static string GetWeatherTypeName(int ordinal)
    {
        string name = Enum.GetName(typeof(WeatherType), ordinal);
        return string.IsNullOrEmpty(name) ? $"Weather {ordinal}" : name;
    }

    /// <summary>
    /// Describes how a serialized row-major array relates to the current append-only enum.
    /// This pure seam keeps destructive and ambiguous reshapes out of the automatic draw path.
    /// </summary>
    internal enum MatrixResizeKind
    {
        Current,
        SafeExpansion,
        DestructiveShrink,
        Ambiguous
    }

    internal static MatrixResizeKind ClassifyMatrixLength(
        int serializedLength,
        int currentDimension,
        out int serializedDimension)
    {
        serializedDimension = 0;
        if (currentDimension <= 0 || serializedLength < 0)
        {
            return MatrixResizeKind.Ambiguous;
        }

        if (serializedLength == currentDimension * currentDimension)
        {
            serializedDimension = currentDimension;
            return MatrixResizeKind.Current;
        }

        if (!TryGetSquareDimension(serializedLength, out serializedDimension))
        {
            return MatrixResizeKind.Ambiguous;
        }

        if (serializedDimension < currentDimension)
        {
            return MatrixResizeKind.SafeExpansion;
        }

        if (serializedDimension > currentDimension)
        {
            return MatrixResizeKind.DestructiveShrink;
        }

        return MatrixResizeKind.Ambiguous;
    }

    internal static bool TryGetSquareDimension(int serializedLength, out int dimension)
    {
        dimension = 0;
        if (serializedLength <= 0)
        {
            return false;
        }

        int candidate = (int)Math.Sqrt(serializedLength);
        if (candidate <= 0 || candidate > serializedLength / candidate)
        {
            return false;
        }

        if (candidate * candidate != serializedLength)
        {
            return false;
        }

        dimension = candidate;
        return true;
    }

    /// <summary>
    /// Copies the overlap of two row-major square matrices by logical FROM/TO ordinal. Existing
    /// cells stay at the same ordinal when a weather value is appended; new cells are zero.
    /// </summary>
    internal static int[] RemapRowMajorPreservingCells(
        IReadOnlyList<int> source,
        int sourceDimension,
        int destinationDimension)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (sourceDimension <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceDimension));
        }

        if (destinationDimension <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationDimension));
        }

        int expectedSourceLength = checked(sourceDimension * sourceDimension);
        if (source.Count != expectedSourceLength)
        {
            throw new ArgumentException(
                "The source matrix length does not match sourceDimension.",
                nameof(source));
        }

        int[] destination = new int[checked(destinationDimension * destinationDimension)];
        int overlapDimension = Math.Min(sourceDimension, destinationDimension);
        for (int fromIndex = 0; fromIndex < overlapDimension; fromIndex++)
        {
            for (int toIndex = 0; toIndex < overlapDimension; toIndex++)
            {
                int sourceIndex = fromIndex * sourceDimension + toIndex;
                int destinationIndex = fromIndex * destinationDimension + toIndex;
                destination[destinationIndex] = source[sourceIndex];
            }
        }

        return destination;
    }
}
