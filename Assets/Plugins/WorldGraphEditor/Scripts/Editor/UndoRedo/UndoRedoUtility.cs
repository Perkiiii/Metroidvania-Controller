using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal static class UndoRedoUtility
    {
        internal static UndoRedoGraphData Data => UndoRedoGraphData.Instance;
        
        private static readonly Dictionary<string, SceneNode> _nodesLookup = new();
        private static readonly Dictionary<string, Edge> _edgesLookup = new();

        private static List<SceneNodeData> _initNodesData = new();
        private static List<EdgeData> _initEdgesData = new();
        
        public static IReadOnlyDictionary<string, SceneNode> NodesLookup => _nodesLookup;
        public static IReadOnlyDictionary<string, Edge> EdgesLookup => _edgesLookup;

        public static bool IsChanged()
        {
            var nodes= _nodesLookup.Values.ToArray();
            var edges = _edgesLookup.Values.ToArray();

            if (nodes.Length != _initNodesData.Count || edges.Length != _initEdgesData.Count)
                return true;

            var nodesData = Data.SceneNodeData;
            var edgesData = Data.EdgesData;
            
            var nodesChanged = !_initNodesData.OrderBy(data => data.Guid)
                .SequenceEqual(nodesData.OrderBy(data => data.Guid));
            
            if (nodesChanged)
                return true;
            
            var edgesChanged = !_initEdgesData.OrderBy(data => data.EdgeGuid)
                .SequenceEqual(edgesData.OrderBy(data => data.EdgeGuid));

            return edgesChanged;
        }

        public static void Init(UndoRedoGraphData oldData)
        {
            Undo.RegisterCompleteObjectUndo(Data, "Load From Old Data");
            
            InitializeData(oldData.LastContainer, oldData.SceneNodeData, oldData.EdgesData);
            
            EditorUtility.SetDirty(Data);
            AssetDatabase.SaveAssetIfDirty(Data);
        }

        public static void Init(WorldGraphContainer container)
        {
            Undo.RegisterCompleteObjectUndo(Data, "Load From Container");

            InitializeData(container, container.EditorGraph?.GetScenesData(), container.EditorGraph?.GetEdgesData());
            

            EditorUtility.SetDirty(Data);
            AssetDatabase.SaveAssetIfDirty(Data);
        }

        private static void InitializeData(WorldGraphContainer container, IReadOnlyList<SceneNodeData> sceneNodesData, IReadOnlyList<EdgeData> edgesData)
        {
            Data.SceneNodeData = sceneNodesData == null ? new List<SceneNodeData>() : sceneNodesData.ToList();
            Data.EdgesData = edgesData == null ? new List<EdgeData>() : edgesData.ToList();
            Data.LastContainer = container;

            _initEdgesData = Data.EdgesData == null 
                ? new List<EdgeData>() 
                : new List<EdgeData>(Data.EdgesData);
            
            _initNodesData = Data.SceneNodeData == null
                ? new List<SceneNodeData>()
                : new List<SceneNodeData>(Data.SceneNodeData);
        }

        public static void SetSelected(string guid)
        {
            Undo.RegisterCompleteObjectUndo(Data, "Graph item selected");

            if (!Data.SelectedElementGuids.Contains(guid))
                Data.SelectedElementGuids.Add(guid);
        }
        
        public static void SetUnselected(string guid)
        {
            Undo.RegisterCompleteObjectUndo(Data, "Graph item unselected");

            Data.SelectedElementGuids.Remove(guid);
        }

        public static void Record(SceneNode node, Vector2 position, string message)
        {
            Undo.RegisterCompleteObjectUndo(Data, message);
            
            var sceneNodeData = node.GetDataWithPorts(false);
            sceneNodeData.Position = position;

            var index = Data.SceneNodeData.FindIndex(item => item.Guid == sceneNodeData.Guid);

            if (index == -1)
            {
                Data.SceneNodeData.Add(sceneNodeData);
            }
            else
            {
                Data.SceneNodeData[index] = sceneNodeData;
            }
        }

        public static void Record(Edge edge, string message)
        {
            Undo.RegisterCompleteObjectUndo(Data, message);

            var edgeData = edge.GetData();
            var index = Data.EdgesData.FindIndex(item =>
                item.FromPortGuid == edge.input.GetGuid() ||
                item.ToPortGuid == edge.output.GetGuid());
            
            if (index == -1)
            {
                Data.EdgesData.Add(edgeData);
            }
            else
            {
                Data.EdgesData[index] = edgeData;
            }
        }

        public static void UnRecord(SceneNode node, string message)
        {
            Undo.RegisterCompleteObjectUndo(Data, message);

            foreach (var port in node.Ports)
            {
                if (port.connected)
                    UnRecordPort(port);
            }

            Data.SceneNodeData.RemoveAll(item => item.Guid == node.Guid);
        }

        public static void UnRecord(Edge edge, string message)
        {
            Undo.RegisterCompleteObjectUndo(Data, message);
            Data.EdgesData.RemoveAll(item =>
                item.FromPortGuid == edge.input.GetGuid() || 
                item.ToPortGuid == edge.output.GetGuid());
        }
        
        
        public static void ClearLookup()
        {
            _nodesLookup.Clear();
            _edgesLookup.Clear();
        }

        public static void SaveUndoRedoData()
        {
            EditorUtility.SetDirty(Data);
            AssetDatabase.SaveAssetIfDirty(Data);
        }

        public static void Replace(Edge newEdge)
        {
            _edgesLookup[newEdge.GetGuid()] = newEdge;
        }

        public static void AddNew(Edge edge)
        {
            _edgesLookup.Add(edge.GetGuid(), edge);
        }

        public static void AddNew(SceneNode node)
        {
            _nodesLookup.Add(node.Guid, node);
        }

        public static bool RemoveEdge(string guid, out Edge edge)
        {
            return _edgesLookup.Remove(guid, out edge);
        }

        public static void RemoveEdge(string guid)
        {
            _edgesLookup.Remove(guid);
        }

        public static bool RemoveNode(string guid, out SceneNode sceneNode)
        {
            return _nodesLookup.Remove(guid, out sceneNode);
        }

        public static void RemoveNode(string guid)
        {
            _nodesLookup.Remove(guid);
        }

        public static IEnumerable<string> GetRemovedEdges()
        {
            return _edgesLookup.Keys.Except(Data.EdgesData.Select(data => data.EdgeGuid));
        }

        public static IEnumerable<string> GetRemovedNodes()
        {
            return _nodesLookup.Keys.Except(Data.SceneNodeData.Select(data => data.Guid));
        }

        public static bool IsChanged(SceneNodeData sceneNodeData, out SceneNode existingNode)
        {
            if (!_nodesLookup.TryGetValue(sceneNodeData.Guid, out existingNode))
                return true;
                    
            var existingNodeData = existingNode.GetDataWithPorts(false);
            return sceneNodeData != existingNodeData;
        }

        public static bool IsChanged(EdgeData edgeData, out Edge existingEdge)
        {
            if (!_edgesLookup.TryGetValue(edgeData.EdgeGuid, out existingEdge))
                return true;
            
            var existingNodeData = existingEdge.GetData();
            return edgeData != existingNodeData;
        }
        
        private static void UnRecordPort(Port port)
        {
            Data.EdgesData.RemoveAll(item =>
                item.FromPortGuid == port.GetGuid() ||
                item.ToPortGuid == port.GetGuid());
        }
    }
}