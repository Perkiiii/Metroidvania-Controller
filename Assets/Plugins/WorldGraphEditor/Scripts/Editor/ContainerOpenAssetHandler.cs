using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal static class ContainerOpenAssetHandler
    {
        [OnOpenAsset(1)]
        public static bool OnContainerOpen(int id, int line)
        {
            var obj = ResolveEditorObject(id);

            if (obj is not WorldGraphContainer saveFile)
                return false;

            WorldBuilderGraph.OpenWindow(saveFile);
            return true;
        }

        private static Object ResolveEditorObject(int id)
        {
#if UNITY_6000_3_OR_NEWER
            return EditorUtility.EntityIdToObject(id);
#else
            return EditorUtility.InstanceIDToObject(id);
#endif
        }
    }
}
