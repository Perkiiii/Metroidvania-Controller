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
        private static WorldGraphContainer _container;
        
        private const float _OPACITY = .3f;

        static HierarchyObjectHighlighter()
        {
            EditorApplication.contextualPropertyMenu += HandlePropChanged;
            WGEEditorEvents.SettingsChanged += OnProjectSettingChanged;
            
#if UNITY_6000_5_OR_NEWER
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += HandleHierarchyWindowItemOnGUI;
#else
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;
#endif
            
            CreateGradientTexture();
        }

        private static void OnProjectSettingChanged(WGEEditorChangeType obj)
        {
            if ((obj & WGEEditorChangeType.ProjectConfig) != 0 || (obj & WGEEditorChangeType.GraphEditor) != 0)
                EditorApplication.RepaintHierarchyWindow();
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
            return WGEProjectConfig.Instance.Container;
        }

#if UNITY_6000_5_OR_NEWER
        private static void HandleHierarchyWindowItemOnGUI(EntityId entityId, Rect selectionRect)
        {
            if (EditorUtility.EntityIdToObject(entityId) is not GameObject gameObject ||
                !gameObject.TryGetComponent<ITransitionComponent>(out var component))
                return;
            
            DrawTransitionComponentInfo(selectionRect, component);
        }
#else
        private static void HandleHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            if (EditorUtility.InstanceIDToObject(instanceID) is not GameObject gameObject ||
                !gameObject.TryGetComponent<ITransitionComponent>(out var component))
                return;

            DrawTransitionComponentInfo(selectionRect, component);
        }
#endif

        private static void DrawTransitionComponentInfo(Rect selectionRect, ITransitionComponent component)
        {
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
            
            if (_container.HasErrors())
            {
                return ("CONTAINER ERRORS", errorColor);
            }

            if (!_container.HasData)
            {
                return ("NO DATA", errorColor);
            }

            var hasData = _container.EditorGraph.TryGetSceneDataByPath(SceneManager.GetActiveScene().path, out var sceneData);
            
            if (!hasData)
            {
                return ("NO DATA FOR SCENE", errorColor);
            }

            if (sceneData.PortsData == null || sceneData.PortsData.Length == 0)
            {
                return ("NO PORTS", errorColor);
            }
            
            if (_container.EditorGraph.TryGetPortData(component.GetGuid(), out var data))
                return (data.Name, data.GetColor(_OPACITY));
                
            return ("UNSET", errorColor);
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
    }
}