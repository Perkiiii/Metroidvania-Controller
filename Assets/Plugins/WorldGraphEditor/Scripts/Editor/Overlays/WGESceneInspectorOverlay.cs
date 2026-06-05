using System.Linq;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using WorldGraphEditor.Editor.Tests;

namespace WorldGraphEditor.Editor.Overlays
{
    [Overlay(typeof(SceneView), "Scene Inspector WGE", true, defaultDockZone = DockZone.LeftColumn)]
    internal class WGESceneInspectorOverlay : Overlay
    {
        private VisualElement _root;
        private VisualElement _topElement;
        private VisualElement _containerStatusElement;
        private HeaderWithListElement _sceneStatusElement;
        private VisualElement _bottomElement;
        
        public override VisualElement CreatePanelContent()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            GraphSaveUtility.OnContainerSaved += RefreshElements;
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
            EditorApplication.hierarchyChanged -= RefreshElements;
            Undo.postprocessModifications -= OnPostProcessModifications;
            
            base.OnWillBeDestroyed();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode) => RefreshElements();

        private void RefreshElements()
        {
            RefreshTopElement();
            
            var container = TransitionManager.LoadFromResources()?.Container;
            
            RefreshBottomElement(container);
        }
        
        private void CreateMarkup()
        {
            _root = new VisualElement();
            _topElement = new VisualElement();
            _containerStatusElement = new VisualElement();
            _sceneStatusElement = new HeaderWithListElement();
            _bottomElement = new VisualElement();

            _root.style.flexDirection = FlexDirection.Column;
            _topElement.style.flexDirection = FlexDirection.Column;

            _bottomElement.style.flexDirection = FlexDirection.Row;
            _bottomElement.style.justifyContent = Justify.Center;
            _containerStatusElement.style.flexDirection = FlexDirection.Row;
            
            _root.Add(new Separator());
            _root.Add(_topElement);
            _root.Add(new Separator());
            _root.Add(_bottomElement);
        }
        
        private void RefreshBottomElement(WorldGraphContainer container)
        {
            _bottomElement.Clear();

            var validateButton = new Button(() => GetOrCreateValidationAsset(container))
            {
                text = "Validate",
                style = { flexGrow = 1}
            };
            validateButton.SetEnabled(container != null && !container.ContainsErrors() && container.HasData);

            _bottomElement.Add(validateButton);
            
            _bottomElement.Add(new Button(TransitionManagerPrefabCreator.CreateOrOpenPrefab)
            {
                text = "Open TM",
                style = { flexGrow = 1}
            });
        }
        
        private static void GetOrCreateValidationAsset(WorldGraphContainer container)
        {
            var containerTests = WorldGraphEditorSettings.Instance.Tests?.Where(item => item != null);
            
            if (containerTests != null)
            {
                var tests = new ISceneTest[] {new MissingPortsTest(), new PortsDuplicatesTest()}.Concat(containerTests);
                var testsData = SceneCompletionValidator.GetAllSceneTestResults(container.EditorData.SceneNodeData, tests);

                if (testsData == null)
                    return;
                
                container.EditorData.SetTestsData(testsData);
            }

            var result = ProjectValidationResult.Instance;
            
            result.SetContainer(container);
            
            EditorUtility.SetDirty(result);
            EditorUtility.SetDirty(container);
            
            AssetDatabase.SaveAssetIfDirty(result);
            AssetDatabase.SaveAssetIfDirty(container);
            
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(result);
        }

        private void RefreshTopElement()
        {
            _containerStatusElement.Clear();
            _sceneStatusElement.Dispose();
            _topElement.Clear();
            
            var containerStatus = OverlayUtility.GetContainerStatus(out var isDataValid);
            var containerInfoLabel =
                UIToolkitUtility.CreateLabel(containerStatus, isDataValid ? MessageColor.Default : MessageColor.Red);
            var containerIconName = isDataValid ? UIToolkitUtility.INFO_ICON_NAME : UIToolkitUtility.ERROR_ICON_NAME;

            _containerStatusElement.Add(UIToolkitUtility.CreateIcon(containerIconName, 20));
            _containerStatusElement.Add(containerInfoLabel);

            if (isDataValid)
            {
                var container = TransitionManager.LoadFromResources().Container;
                var sceneData = container.EditorData
                    .GetSceneDataByPath(SceneManager.GetActiveScene().path, out var isDataExists);
                
                var sceneCompletionData = isDataExists
                    ? SceneCompletionValidator.GetCurrentSceneCompletionData(sceneData)
                    : default;
                
                UIToolkitUtility.FillSceneStatusElement(_sceneStatusElement, container, sceneCompletionData);
            }

            _topElement.Add(_containerStatusElement);
            _topElement.Add(_sceneStatusElement);
        }
    }
}