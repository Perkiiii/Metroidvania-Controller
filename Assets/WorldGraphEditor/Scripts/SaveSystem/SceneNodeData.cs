#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor
{
    [Serializable]
    public struct SceneNodeData : IEquatable<SceneNodeData>, ISceneData
    {
        public string NodeName;
        public SceneAsset SceneAsset;
        public string SceneAssetGuid;
        public string ScenePath;
        public int BuildIndex;
        public string Guid;
        public Vector2 Position;
        public PortData[] PortsData;

        public SceneNodeData(SceneNodeData sceneNodeData, string newPath, int newBuildIndex)
        {
            NodeName = sceneNodeData.NodeName;
            SceneAsset = sceneNodeData.SceneAsset;
            ScenePath = newPath;
            BuildIndex = newBuildIndex;
            Guid = sceneNodeData.Guid;
            Position = sceneNodeData.Position;
            PortsData = sceneNodeData.PortsData;
            
            SceneAssetGuid = sceneNodeData.SceneAssetGuid;
        }

        public int GetBuildIndex() => BuildIndex;
        public IEnumerable<string> GetPortsGuid() => PortsData.Select(item => item.Guid);
        
        public bool Equals(SceneNodeData other)
        {
            return NodeName == other.NodeName && Guid == other.Guid && SceneAsset == other.SceneAsset && Position.Equals(other.Position) && PortsData.SequenceEqual(other.PortsData);
        }
        
        public override bool Equals(object obj)
        {
            return obj is SceneNodeData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(NodeName, Guid, ScenePath, BuildIndex, Position, PortsData);
        }
        
        public static bool operator ==(SceneNodeData left, SceneNodeData right)
        {
            return left.Equals(right);
        }
        
        public static bool operator !=(SceneNodeData left, SceneNodeData right)
        {
            return !(left == right);
        }

        public void RefreshSceneAsset(SceneAsset sceneAsset)
        {
            SceneAsset = sceneAsset;
        }
    }
}

#endif