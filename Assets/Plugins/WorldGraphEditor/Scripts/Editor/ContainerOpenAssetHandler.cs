using UnityEditor;
using UnityEditor.Callbacks;

#if UNITY_6000_5_OR_NEWER
using UnityEngine;
#endif

namespace WorldGraphEditor.Editor
{
    internal static class ContainerOpenAssetHandler
    {
        
#if UNITY_6000_5_OR_NEWER
        [OnOpenAsset(1)]
        public static bool OnContainerOpen(EntityId id)
        {
            Debug.Log("entity");
            
            var obj = EditorUtility.EntityIdToObject(id);
            
            if (obj is not WorldGraphContainer saveFile) 
                return false;
            
            WorldBuilderGraph.OpenWindow(saveFile);
            return true;
        }
#else

        [OnOpenAsset(1)]
        public static bool OnContainerOpen(int id)
        {
            var obj = EditorUtility.InstanceIDToObject(id);
            
            if (obj is not WorldGraphContainer saveFile) 
                return false;
            
            WorldBuilderGraph.OpenWindow(saveFile);
            return true;
        }
#endif
    }
}