using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal static class GraphUtility
    {
        internal static StyleSheet GetStyleSheet(string path)
        {
            return WGEAssetPathUtility.LoadStyleSheet(path);
        }

        internal static void AddStyleSheet(VisualElement element, string path)
        {
            var styleSheet = GetStyleSheet(path);

            if (styleSheet == null)
                return;

            element.styleSheets.Add(styleSheet);
        }

        internal static VisualElement GetVisualElement(string style = "")
        {
            var container = new VisualElement();
            container.AddToClassList(style);
            
            return container;
        }
        
        internal static T GetNodeAtPosition<T>(UQueryState<Node> nodes, Vector2 position) where T : Node
        {
            return (from element in nodes
                let worldRect = element.GetPosition()
                where worldRect.Contains(position)
                select element as T).FirstOrDefault();
        }

        internal static ContextualEdge CreateEdge(Port existingPort, Port newPort, bool isInput)
        {
            var newEdge = new ContextualEdge();
            
            if (isInput)
            {
                newEdge.input = existingPort;
                newEdge.output = newPort;
            }
            else
            {
                newEdge.output = existingPort;
                newEdge.input = newPort;
            }

            newPort?.Connect(newEdge);
            existingPort?.Connect(newEdge);

            UndoRedoUtility.AddNew(newEdge);
            UndoRedoUtility.Record(newEdge, "Edge created");

            return newEdge;
        }
        
        internal static Edge[] GetEdges(SceneNode node)
        {
            return node.Ports.Select(port => port.connections).SelectMany(edgesToRemove => edgesToRemove).ToArray();
        }
        
        internal static void ClearEdges(IEnumerable<Edge> edges)
        {
            foreach (var edge in edges)
            {
                RemoveEdge(edge);
            }
        }

        internal static Port CreatePort(Direction direction, Orientation orientation, string portName, string guid,
            bool isAdditional)
        {
            var port = Port.Create<ContextualEdge>(orientation, direction, Port.Capacity.Single, typeof(float));
            port.InitData();
            port.GetData().IsAdditional = isAdditional;
            port.GetData().Guid = guid;

            if (isAdditional)
            {
                port.portColor = new Color(1f, 0.72f, 0f);
                port.portCapLit = true;
                port.SetEnabled(false);
            }
            else
            {
                port.portColor = orientation == Orientation.Horizontal
                    ? new Color(0f, 0.84f, 1f)
                    : new Color(0f, 1f, 0.58f);
            }
            
            port.portName = portName;

            return port;
        }
        
        internal static void LoadScene(SceneAsset sceneAsset)
        {
            var proceed = EditorUtility.DisplayDialog(
                "Warning: Undo/Redo Data Will Be Lost",
                "When loading a new scene, Unity automatically clears all Undo/Redo data. " +
                "Only the last change for restoring the graph will remain available. Do you want to continue?",
                "Continue", "Cancel"
            );

            if (!proceed) 
                return;
            
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) 
                return;
            
            var path = AssetDatabase.GetAssetPath(sceneAsset);
                
            if (!string.IsNullOrEmpty(path))
            {
                EditorSceneManager.OpenScene(path);
                UndoRedoUtility.Init(UndoRedoUtility.Data);
            }
            else
            {
                Debug.LogWarning("Scene asset path is empty, cannot load scene.");
            }
        }

        internal static GraphViewChange RegisterGraphChanges(GraphViewChange changes)
        {
            if (changes.movedElements != null)
            {
                foreach (var element in changes.movedElements)
                {
                    if (element is not SceneNode node)
                        continue;

                    var newPosition = node.GetPosition().position;
                    UndoRedoUtility.Record(node, newPosition, "Node moved");
                }

                return changes;
            }

            if (changes.elementsToRemove != null)
            {
                foreach (var element in changes.elementsToRemove)
                {
                    if (element is SceneNode node)
                    {
                        var edges = GetEdges(node);

                        foreach (var edge in edges)
                        {
                            RemoveEdge(edge);
                            UndoRedoUtility.UnRecord(edge, "Edge deleted");
                        }
                        
                        UndoRedoUtility.RemoveNode(node.Guid);
                        UndoRedoUtility.UnRecord(node, "Node deleted");
                    }
                    else if (element is Edge edge)
                    {
                        UndoRedoUtility.RemoveEdge(edge.GetGuid());
                        UndoRedoUtility.UnRecord(edge, "Edge deleted");
                    }
                }
            }
            
            if (changes.edgesToCreate != null)
            {
                foreach (var edge in changes.edgesToCreate)
                {
                    UndoRedoUtility.AddNew(edge);
                    UndoRedoUtility.Record(edge, "Edge created");
                }
            }

            return changes;
        }
        
        internal static void RemoveEdge(Edge edge)
        {
            UndoRedoUtility.RemoveEdge(edge.GetGuid());
            
            edge.Disconnect();
            edge.RemoveFromHierarchy();
        }
    }
}
