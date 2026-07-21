using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal static class GraphExtensions
    {
        internal static string GetUnityGuid(this Object obj)
        {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
        }

        internal static PortCustomUserData GetData(this Port port) => port.userData as PortCustomUserData;
        
        internal static void InitData(this Port port) => port.userData = new PortCustomUserData();
        
        internal static string GetGuid(this Port port) => port.GetData().Guid;

        internal static bool IsAdditional(this Port port) => port.GetData().IsAdditional;

        internal static string GetGuid(this Edge edge) => edge.userData as string;
        
        internal static void SetGuid(this Edge edge, string guid) => edge.userData = new string(guid);
        
        internal static int GetBuildIndex(this SceneAsset sceneAsset)
        {
            var path = AssetDatabase.GetAssetPath(sceneAsset);
            var activeIndex = 0;

            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                    continue;
                
                if (scene.path == path)
                    return activeIndex;

                activeIndex++;
            }
            
            return -1;
        }

        internal static string GetFormattedPath(this SceneAsset sceneAsset)
        {
            var fullPath = AssetDatabase.GetAssetPath(sceneAsset);
            var pathWithoutAssets = fullPath.StartsWith("Assets/WorldGraphEditor/Examples") 
                ? fullPath["Assets/WorldGraphEditor/Examples/".Length..] 
                : fullPath;
            
            var lastDotIndex = pathWithoutAssets.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                pathWithoutAssets = pathWithoutAssets[..lastDotIndex];
            }

            return pathWithoutAssets;
        }

        internal static void Disconnect(this Edge edge)
        {
            edge?.output?.Disconnect(edge);
            edge?.input?.Disconnect(edge);
        }

        internal static SceneNodeData GetDataWithPorts(this SceneNode node, bool connectedOnly)
        {
            return new SceneNodeData
            {
                NodeName = node.Name,
                Guid = node.Guid,
                SceneAsset = node.SceneAsset,
                BuildIndex = node.SceneAsset.GetBuildIndex(),
                ScenePath = node.SceneAsset.GetFormattedPath(),
                Position = node.GetPosition().position,
                PortsData = node.Ports.GetData(connectedOnly).ToArray(),
                
                SceneAssetGuid = node.SceneAssetGuid,
                
#if WGE_ADDRESSABLES
                Address = AddressablesAddressResolver.TryResolveSceneAddress(node.SceneAssetGuid),
#endif
            };
        }

        internal static IEnumerable<SceneRuntimeData> GetRuntimeData(this IList<SceneNodeData> data)
        {
            return data.Select(item => new SceneRuntimeData
            {
                SceneName = item.SceneAsset.name,
                NodeName = item.NodeName,
                BuildIndex = item.BuildIndex,
                PortsData = item.PortsData.GetRuntimeData().ToArray(),
#if WGE_ADDRESSABLES
                Address = AddressablesAddressResolver.TryResolveSceneAddress(item.SceneAssetGuid),
#endif
            });
        }
        
        internal static IEnumerable<ConnectionRuntimeData> GetRuntimeData(this IEnumerable<EdgeData> data)
        {
            return data.Select(static item => new ConnectionRuntimeData
            {
                IsFacingRight = item.IsFacingRight,
                ToPortGuid = item.ToPortGuid,
                FromPortGuid = item.FromPortGuid,
                TransitionType = item.TransitionType
            });
        }

        internal static IEnumerable<PortRuntimeData> GetRuntimeData(this IEnumerable<PortData> portsData)
        {
            return portsData.Select(static port => new PortRuntimeData
            {
                Name = port.Name,
                Guid = port.Guid,
                IsAdditional = port.IsAdditional
            });
        }

        internal static IEnumerable<PortData> GetData(this IEnumerable<Port> ports, bool connectedOnly)
        {
            var portsToSave = connectedOnly ? ports.Where(static port => port.connected || port.IsAdditional()) : ports;

            return portsToSave.Select(static port => new PortData
            {
                IsInput = port.direction == Direction.Input,
                IsHorizontal = port.orientation == Orientation.Horizontal,
                Name = port.portName,
                Guid = port.GetGuid(),
                IsAdditional = port.IsAdditional(),
            });
        }

        internal static EdgeData GetData(this Edge edge)
        {
            var fromPortGuid = edge.input?.GetGuid();
            var toPortGuid =  edge.output?.GetGuid();
            var isFacingRight = false;
            var edgeType = TransitionType.Undirected;

            if (edge is DirectedEdge directedEdge)
            {
                isFacingRight = directedEdge.IsFacingRight;
                edgeType = directedEdge.TransitionType;
            }
            
            return new EdgeData
            {
                FromPortGuid = fromPortGuid,
                ToPortGuid = toPortGuid,
                IsFacingRight = isFacingRight,
                TransitionType = edgeType,
                EdgeGuid = edge.GetGuid()
            };
        }

        internal static Color GetColor(this PortData data, float opacity = 1)
        {
            return data.IsAdditional 
                ? new Color(1f, 0.72f, 0f, opacity) 
                : data.IsHorizontal 
                    ? new Color(0f, 0.84f, 1f, opacity) 
                    : new Color(0f, 1f, 0.58f, opacity);
        }
    }
}