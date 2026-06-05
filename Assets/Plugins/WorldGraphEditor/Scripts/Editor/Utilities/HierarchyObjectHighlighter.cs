using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldGraphEditor.Editor
{
    [InitializeOnLoad]
    internal class HierarchyObjectHighlighter
    {
        private static Texture2D _gradientTexture;
        private static GUIStyle _portStyle;
        private static TransitionManager _transitionManager;
        private static WorldGraphContainer _container;

        private const float _OPACITY = .3f;

        static HierarchyObjectHighlighter()
        {
            EditorApplication.contextualPropertyMenu += HandlePropChanged;
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;
            CreateGradientTexture();
        }

        private static void HandlePropChanged(GenericMenu menu, SerializedProperty property)
        {
            EditorApplication.RepaintHierarchyWindow();
        }

        private static GUIStyle GetGuiStyle()
        {
            return new GUIStyle(EditorStyles.label)
            {
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleRight,
                normal =
                {
                    textColor = ColorStyleUtility.TextColor
                },
                fontSize = 12
            };
        }

        private static WorldGraphContainer GetContainer()
        {
            if (_transitionManager == null)
                _transitionManager = TransitionManager.LoadFromResources();

            return _transitionManager?.Container;
        }

        private static void HandleHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            if (ResolveEditorObject(instanceID) is not GameObject gameObject ||
                !gameObject.TryGetComponent<ITransitionComponent>(out var component))
                return;

            if (WorldGraphEditorSettings.Instance.HierarchyHighlighterMode == InspectorHighlighterMode.Nothing)
                return;

            _portStyle ??= GetGuiStyle();
            _container = GetContainer();

            var (name, endColor) = DeterminePortLabelAndColor(component);

            if (WorldGraphEditorSettings.Instance.HierarchyHighlighterMode == InspectorHighlighterMode.NamesAndColors)
            {
                UpdateGradientTexture(Color.clear, endColor);
                GUI.DrawTexture(selectionRect, _gradientTexture);
            }

            EditorGUI.LabelField(selectionRect, $"({name})", _portStyle);
        }

        private static (string Name, Color EndColor) DeterminePortLabelAndColor(ITransitionComponent component)
        {
            var errorColor = new Color(1f, 0f, 0f, _OPACITY);

            if (_container == null)
            {
                return ("CONTAINER NOT FOUND", errorColor);
            }

            if (_container.ContainsErrors())
            {
                return ("CONTAINER ERRORS", errorColor);
            }

            if (!_container.HasData)
            {
                return ("NO DATA", errorColor);
            }

            var sceneData = _container.EditorData.GetSceneDataByPath(SceneManager.GetActiveScene().path, out var hasData);
            if (!hasData)
            {
                return ("NO DATA FOR SCENE", errorColor);
            }

            if (sceneData.PortsData == null || sceneData.PortsData.Length == 0)
            {
                return ("NO PORTS", errorColor);
            }

            var data = _container.EditorData.GetPortData(component.GetGuid());
            var endColor = data.GetColor(_OPACITY);

            return (data.Name, endColor);
        }

        private static void CreateGradientTexture()
        {
            if (_gradientTexture == null)
            {
                _gradientTexture = new Texture2D(16, 1)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        private static void UpdateGradientTexture(Color startColor, Color endColor)
        {
            for (int i = 0; i < _gradientTexture.width; i++)
            {
                var t = i / (float)(_gradientTexture.width - 1);
                _gradientTexture.SetPixel(i, 0, Color.Lerp(startColor, endColor, t));
            }
            _gradientTexture.Apply();
        }

        private static Object ResolveEditorObject(int id)
        {
#if UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject(id);
#else
            return EditorUtility.InstanceIDToObject(id);
#endif
        }
    }
}
