using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor.Overlays
{
    [Overlay(typeof(SceneView), "Scene Inspector WGE", true, defaultDockZone = DockZone.LeftColumn)]
    internal class WGESceneInspectorOverlay : Overlay
    {
        private VisualElement _root;
        private VisualElement _topElement;
        private VisualElement _containerStatusElement;
        private VisualElement _managerStatusElement;
        private HeaderWithListElement _sceneStatusElement;
        private VisualElement _bottomElement;
        
        public override VisualElement CreatePanelContent()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            GraphSaveUtility.OnContainerSaved += RefreshElements;
            WGEEditorEvents.SettingsChanged += OnSettingsChanged;
            EditorApplication.hierarchyChanged += RefreshElements;
            Undo.postprocessModifications += OnPostProcessModifications;
            
            CreateMarkup();
            RefreshElements();

            return _root;
        }

        private UndoPropertyModification[] OnPostProcessModifications(UndoPropertyModification[] modifications)
        {
            foreach (var mod in modifications)
            {
                var target = mod.currentValue.target;

                if (target is not (Component and ITransitionComponent)) 
                    continue;
                
                RefreshElements();
                break;
            }

            return modifications;
        }

        public override void OnWillBeDestroyed()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            GraphSaveUtility.OnContainerSaved -= RefreshElements;
            WGEEditorEvents.SettingsChanged -= OnSettingsChanged;
            EditorApplication.hierarchyChanged -= RefreshElements;
            Undo.postprocessModifications -= OnPostProcessModifications;
            
            base.OnWillBeDestroyed();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RefreshElements();
        }

        private void OnSettingsChanged(WGEEditorChangeType changeType)
        {
            if (changeType.HasFlag(WGEEditorChangeType.ProjectConfig))
            {
                RefreshElements();
            }
        }

        private void RefreshElements()
        {
            RefreshTopElement();
            RefreshBottomElement();
        }
        
        private void CreateMarkup()
        {
            _root = new VisualElement();
            _topElement = new VisualElement();
            _containerStatusElement = new VisualElement();
            _managerStatusElement = new VisualElement();
            _sceneStatusElement = new HeaderWithListElement();
            _bottomElement = new VisualElement();

            _root.style.flexDirection = FlexDirection.Column;
            _topElement.style.flexDirection = FlexDirection.Column;

            _bottomElement.style.flexDirection = FlexDirection.Row;
            _bottomElement.style.justifyContent = Justify.Center;
            _containerStatusElement.style.flexDirection = FlexDirection.Row;
            _managerStatusElement.style.flexDirection = FlexDirection.Row;
            
            _root.Add(new Separator());
            _root.Add(_topElement);
            _root.Add(new Separator());
            _root.Add(_bottomElement);
        }
        
        private void RefreshBottomElement()
        {
            _bottomElement.Clear();

            _bottomElement.Add(new Button(() => SettingsService.OpenProjectSettings("Project/World Graph Editor"))
            {
                text = "Project Settings",
                style = { flexGrow = 1 }
            });
        }

        private void RefreshTopElement()
        {
            _containerStatusElement.Clear();
            _managerStatusElement.Clear();
            _sceneStatusElement.Dispose();
            _topElement.Clear();
            
            var containerStatus = OverlayUtility.GetContainerStatus(out var isDataValid);
            var managerStatus = OverlayUtility.GetManagerStatus();
            var managerMessage = $"Transition Manager: {managerStatus}";
            var managerInfoLabel = UIToolkitUtility.CreateLabel(managerMessage, MessageColor.Default);
            var containerInfoLabel = UIToolkitUtility.CreateLabel(containerStatus, isDataValid ? MessageColor.Default : MessageColor.Red);
            var containerIconName = isDataValid ? UIToolkitUtility.INFO_ICON_NAME : UIToolkitUtility.ERROR_ICON_NAME;

            _containerStatusElement.Add(UIToolkitUtility.CreateIcon(containerIconName, 20));
            _containerStatusElement.Add(containerInfoLabel);
            _managerStatusElement.Add(UIToolkitUtility.CreateIcon(UIToolkitUtility.INFO_ICON_NAME, 20));
            _managerStatusElement.Add(managerInfoLabel);

            if (isDataValid)
            {
                var editorGraph = WGEProjectConfig.Instance.GetEditorGraph()!;
                var hasData = editorGraph.TryGetSceneDataByPath(SceneManager.GetActiveScene().path, out var sceneData);
                
                var sceneCompletionData = hasData
                    ? SceneCompletionValidator.GetCurrentSceneCompletionData(sceneData)
                    : default;
                
                UIToolkitUtility.FillSceneStatusElement(_sceneStatusElement, editorGraph, sceneCompletionData);
            }

            _topElement.Add(_containerStatusElement);
            _topElement.Add(_managerStatusElement);
            _topElement.Add(_sceneStatusElement);
        }
    }
}