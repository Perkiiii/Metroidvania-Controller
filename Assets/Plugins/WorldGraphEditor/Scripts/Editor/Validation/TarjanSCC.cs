using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldGraphEditor.Editor
{
    internal class TarjanSCC
    {
        private int _currentIndex = 0;
        private readonly Stack<SceneNodeData> _stack;
        private readonly Dictionary<SceneNodeData, int> _index;
        private readonly Dictionary<SceneNodeData, int> _lowLink;
        private readonly HashSet<SceneNodeData> _onStack;
        private readonly List<List<SceneNodeData>> _components;
        private readonly Dictionary<SceneNodeData, List<SceneNodeData>> _graph;
        
        private List<SceneNodeData> _nodesWithAdditionalPort;
        
        public TarjanSCC()
        {
            _currentIndex = 0;
            _stack = new Stack<SceneNodeData>();
            _index = new Dictionary<SceneNodeData, int>();
            _lowLink = new Dictionary<SceneNodeData, int>();
            _onStack = new HashSet<SceneNodeData>();
            _components = new List<List<SceneNodeData>>();
            _nodesWithAdditionalPort = new List<SceneNodeData>();
            _graph = new Dictionary<SceneNodeData, List<SceneNodeData>>();
        }

        public List<List<SceneNodeData>> GetStronglyConnectedComponents(WorldGraphContainer container, bool ignoreAdditionalPorts, bool ignoreShortcuts)
        {
            PrepareAdditionalPortNodes(container.EditorGraph.GetScenesData());
            BuildGraph(container, ignoreShortcuts);

            foreach (var nodeData in  container.EditorGraph.GetScenesData())
            {
                if (!_index.ContainsKey(nodeData))
                    StrongConnect(nodeData, ignoreAdditionalPorts);
            }

            return _components;
        }
        
        private void PrepareAdditionalPortNodes(IReadOnlyList<SceneNodeData> sceneNodesData)
        {
            _nodesWithAdditionalPort =
                sceneNodesData.Where(nodeData => nodeData.PortsData.Any(item => item.IsAdditional)).ToList();
        }

        private void BuildGraph(WorldGraphContainer container, bool ignoreShortcuts)
        {
            foreach (var nodeData in container.EditorGraph.GetScenesData())
            {
                _graph[nodeData] = new List<SceneNodeData>();
            }

            foreach (var nodeData in container.EditorGraph.GetScenesData())
            {
                var neighbours = container.EditorGraph.GetNeighboursData(nodeData, ignoreShortcuts);

                foreach (var neighbour in neighbours)
                {
                    _graph[nodeData].Add(neighbour);
                }
            }
        }
        
        private void StrongConnect(SceneNodeData vNodeData, bool ignoreAdditionalPorts)
        {
            _index[vNodeData] = _currentIndex;
            _lowLink[vNodeData] = _currentIndex;
            _currentIndex++;
            
            _stack.Push(vNodeData);
            _onStack.Add(vNodeData);

            foreach (var wNeighbour in _graph[vNodeData])
            {
                if (!_index.TryGetValue(wNeighbour, out var value))
                {
                    StrongConnect(wNeighbour, ignoreAdditionalPorts);
                    _lowLink[vNodeData] = Math.Min(_lowLink[vNodeData], _lowLink[wNeighbour]);
                }
                else if (_onStack.Contains(wNeighbour))
                {
                    _lowLink[vNodeData] = Math.Min(_lowLink[vNodeData], value);
                }
            }
            
            if (!ignoreAdditionalPorts && vNodeData.PortsData.Any(static item => item.IsAdditional))
            {
                foreach (var additionalNode in _nodesWithAdditionalPort)
                {
                    if (EqualityComparer<SceneNodeData>.Default.Equals(additionalNode, vNodeData))
                        continue;

                    if (!_index.TryGetValue(additionalNode, out _))
                    {
                        StrongConnect(additionalNode, false);
                        _lowLink[vNodeData] = Math.Min(_lowLink[vNodeData], _lowLink[additionalNode]);
                    }
                    else if (_onStack.Contains(additionalNode))
                    {
                        _lowLink[vNodeData] = Math.Min(_lowLink[vNodeData], _index[additionalNode]);
                    }
                }
            }

            if (_lowLink[vNodeData] != _index[vNodeData]) 
                return;
            
            {
                var component = new List<SceneNodeData>();

                SceneNodeData wNeighbour;

                do
                {
                    wNeighbour = _stack.Pop();
                    _onStack.Remove(wNeighbour);
                    component.Add(wNeighbour);
                    
                } while (wNeighbour != vNodeData);
                
                _components.Add(component);
            }
        }
    }
}