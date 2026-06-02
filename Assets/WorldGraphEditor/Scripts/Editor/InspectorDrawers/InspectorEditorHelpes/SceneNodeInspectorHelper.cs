using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal class SceneNodeInspectorHelper : ScriptableObject
    {
        [SerializeField] private string _nodeName;
        [SerializeField] private SceneAsset _sceneAsset;
        [SerializeField] private PortData[] _portsData;

        internal static SceneNodeInspectorHelper Instance { get; private set; }
        internal SceneAsset SceneAsset => _sceneAsset;
        internal IReadOnlyList<Port> Ports => _ports;
        
        private List<Port> _ports;
        private SceneNode _sceneNode;

        private void OnDisable()
        {
            Instance = null;
        }

        internal void Init(SceneNode sceneNode)
        {
            Instance = this;
            
            _sceneNode = sceneNode;
            _nodeName = sceneNode.Name;
            _sceneAsset = sceneNode.SceneAsset;
            _portsData = sceneNode.Ports.GetData(false).ToArray();
            _ports = sceneNode.Ports.ToList();
        }

        internal bool IsNodeContainsSceneDuplicate()
        {
            return _sceneNode != null && _sceneNode.ErrorData.IsSceneDuplicate;
        }

        internal bool IsPortsNamesValid()
        {
            var hasDuplicates = _portsData.HasDuplicatesPorts(item => item.Name);
            var hasEmptyName = _portsData.Any(port => string.IsNullOrWhiteSpace(port.Name));

            var isInvalid = hasDuplicates || hasEmptyName;

            if (isInvalid)
                _sceneNode?.ErrorData.AddError(ErrorType.PortWrongName);
            else
                _sceneNode?.ErrorData.RemoveError(ErrorType.PortWrongName);

            return isInvalid;
        }

        internal bool CanSafelyRemoved(int index) => !_ports[index].connected && !_ports[index].IsAdditional();
        
        internal void RemovePort(string guid, int index)
        {
            _sceneNode.RemovePort(guid);
            _ports.RemoveAt(index);
        }
        
        internal bool IsNodeHasNoSceneAsset()
        {
            return _sceneNode != null && _sceneNode.ErrorData.IsEmptySceneAsset;
        }
        
        private void OnValidate()
        {
            _sceneNode?.UpdateData(new NodeChangesData(_nodeName,  _sceneAsset, _portsData));
        }
    }
}