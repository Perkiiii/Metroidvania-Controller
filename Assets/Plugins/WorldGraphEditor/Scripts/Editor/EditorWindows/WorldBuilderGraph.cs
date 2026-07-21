using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class WorldBuilderGraph : EditorWindow
    {
        internal bool IsContainerExists => _container != null;

        private WorldBuilderGraphView _graphView;
        private SceneNodeInspectorHelper _nodeInspectorHelper;

        private WorldGraphContainer _container;
        private Button _loadLastEditedContainerButton;
        private Button _refreshScenePreviewButton;
        private Button _captureScenesButton;
        
        private const string _DEFAULT_WINDOW_NAME = "No handled container";
        
        [MenuItem("Window/World Graph Editor/Open empty graph")]
        internal static void OpenWithEmptyData()
        {
            GetEditorWindow(_DEFAULT_WINDOW_NAME);
        }

        [MenuItem("Window/World Graph Editor/Open last edited")]
        internal static void OpenWithLastSavedData()
        {
            GetCashedData((data, isContainerExists) =>
            {
                if (isContainerExists)
                    OpenWindow(data.LastContainer);
                else
                    OpenWithEmptyData();
            });
        }

        internal static void OpenWindow(WorldGraphContainer container)
        {
            var window = GetEditorWindow(container.name);
            
            if (window._graphView == null)
                window.GenerateGraphView();
            
            GraphSaveUtility.GetInstance(window._graphView).LoadViaContainer(container);
            UndoRedoUtility.Init(container);
            window.SetContainer(container);
        }
                
        internal void ShowInInspector(SceneNode node)
        {
            GenerateInspectorHelpers();
            
            _nodeInspectorHelper.Init(node);
            Selection.activeObject = _nodeInspectorHelper;
        }

        private static void GetCashedData(Action<UndoRedoGraphData, bool> callback)
        {
            var data = UndoRedoGraphData.Instance;
            
            var isContainerExists = data.LastContainer != null;
            callback?.Invoke(data, isContainerExists);
        }

        private static WorldBuilderGraph GetEditorWindow(string windowName)
        {
            var window = GetWindow<WorldBuilderGraph>();
            var iconPath = EditorGUIUtility.isProSkin
                ? "Scripts/Editor/Icons/wgb_window_dark_icon.png"
                : "Scripts/Editor/Icons/wgb_window_light_icon.png";

            var image = WGEAssetPathUtility.LoadTexture(iconPath);
            window.titleContent = new GUIContent($" {windowName}", image);

            return window;
        }

        internal void SetContainer(WorldGraphContainer container)
        {
            GetCashedData((data, isContainerExists) =>
            {
                var previous = data?.LastContainer;
                _container = container;
                _loadLastEditedContainerButton?.SetEnabled(isContainerExists && previous != container);
                _refreshScenePreviewButton?.SetEnabled(isContainerExists);
                _captureScenesButton?.SetEnabled(isContainerExists);

                if (container == null)
                    return;

                var windowName = container.name;
                GetEditorWindow(windowName);
            });
        }
        
        private void OnEnable()
        {
            GenerateGraphView();
            GenerateToolBar();
            GenerateInspectorHelpers();
        }
        
        private void OnDisable()
        {
            if (_graphView is not null)
            {
                _graphView.OnMouseSelected -= OnMouseSelectionChanged;
                _graphView.Unsubscribe();
                rootVisualElement.Remove(_graphView);
            }

            _container = null;
            UndoRedoUtility.SaveUndoRedoData();
        }

        private void GenerateGraphView()
        {
            _graphView = new WorldBuilderGraphView(this, _container);
            
            _graphView.StretchToParentSize();
            _graphView.OnMouseSelected += OnMouseSelectionChanged;
            
            rootVisualElement.Add(_graphView);
        }

        private void OnMouseSelectionChanged(List<ISelectable> selection)
        {
            if (selection == null || selection.Count == 0)
            {
                Selection.activeObject = null;
                return;
            }

            if (selection[0] is not SceneNode sceneNode)
                return;

            ShowInInspector(sceneNode);
        }

        private void GenerateToolBar()
        {
            var toolbar = new UnityEditor.UIElements.Toolbar();
            toolbar.styleSheets.Add(WGEAssetPathUtility.LoadStyleSheet("Scripts/Editor/EditorWindows/Styles/Toolbar.uss"));

            var leftContainer = new VisualElement {style = { flexDirection = FlexDirection.Row, flexGrow = 1}};
            var rightContainer = new VisualElement {style = { flexDirection = FlexDirection.Row, flexGrow = 1, justifyContent = Justify.FlexEnd}};
            
            var saveButton = new Button(() => RequestDataOperation(true)) {text = "Save"};
            var loadButton = new Button(() => RequestDataOperation(false)) {text = "Open"};
            _refreshScenePreviewButton = new Button(RefreshScenePreviews) {text = "Refresh Preview"};
            _captureScenesButton = new Button(CaptureAvailableScenes) {text = "Capture Scene Previews"};
            _refreshScenePreviewButton.SetEnabled(false);
            _captureScenesButton.SetEnabled(false);
            
            _loadLastEditedContainerButton = new Button(OpenWithLastSavedData) {text = "Open last edited"};

            GetCashedData((_, isContainerExists) =>
            {
                _loadLastEditedContainerButton.SetEnabled(isContainerExists);
                
                leftContainer.Add(saveButton);
                leftContainer.Add(loadButton);
                leftContainer.Add(_loadLastEditedContainerButton);
                
                rightContainer.Add(_captureScenesButton);
                rightContainer.Add(_refreshScenePreviewButton);
            
                toolbar.Add(leftContainer);
                toolbar.Add(rightContainer);
                
                rootVisualElement.Add(toolbar);
            });
        }

        private void CaptureAvailableScenes()
        {
            _graphView.ClearNodePreviews();
            SceneScreenshotUtility.CaptureAvailableScenes(_container);
        }

        private void CaptureAllScenes()
        {
            _graphView.ClearNodePreviews();
            SceneScreenshotUtility.CaptureAllScenes(_container);
        }

        private void RefreshScenePreviews()
        {
            _graphView.RefreshScenePreviews();
        }

        private void RequestDataOperation(bool isSaveOperation)
        {
            var saveUtility = GraphSaveUtility.GetInstance(_graphView);
            
            if (isSaveOperation)
                saveUtility.Save(_container);
            else
            {
                SetContainer(saveUtility.LoadViaPath());
                UndoRedoUtility.Init(_container);
            }
        }
        
        private void GenerateInspectorHelpers()
        {
            if (_nodeInspectorHelper != null)
                return;
            
            _nodeInspectorHelper = CreateInstance<SceneNodeInspectorHelper>();
            _nodeInspectorHelper.name = "Node Draw Helper";
        }
    }
}
