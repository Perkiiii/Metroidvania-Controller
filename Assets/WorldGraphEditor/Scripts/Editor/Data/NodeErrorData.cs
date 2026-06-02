using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class NodeErrorData
    {
        private readonly VisualElement _target;
        private readonly string _styleName;

        public bool IsSceneDuplicate {get; private  set;}
        public bool IsWrongPortName {get; private  set;}
        public bool IsEmptySceneAsset {get; private  set;}

        public bool HasErrors => IsSceneDuplicate || IsWrongPortName || IsEmptySceneAsset;
        
        public NodeErrorData(VisualElement sceneNode, string errorStyle)
        {
            _target = sceneNode;
            _styleName = errorStyle;
        }

        public void AddError(ErrorType errorType)
        {
            if (errorType == ErrorType.PortWrongName)
                IsWrongPortName = true;
            if (errorType == ErrorType.HandledSceneDuplicate)
                IsSceneDuplicate = true;
            if (errorType == ErrorType.EmptySceneAsset)
                IsEmptySceneAsset = true;
            
            if (HasErrors)
                _target.AddToClassList(_styleName);
        }

        public void RemoveError(ErrorType errorType)
        {
            if (errorType == ErrorType.PortWrongName)
                IsWrongPortName = false;
            if (errorType == ErrorType.HandledSceneDuplicate)
                IsSceneDuplicate = false;
            if (errorType == ErrorType.EmptySceneAsset)
                IsEmptySceneAsset = false;

            if (!HasErrors)
                _target.RemoveFromClassList("scene-node-duplicate");
        }
    }
}