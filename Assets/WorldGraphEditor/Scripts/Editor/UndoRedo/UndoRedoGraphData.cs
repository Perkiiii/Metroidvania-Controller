using System.Collections.Generic;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal class UndoRedoGraphData : ScriptableSingleton<UndoRedoGraphData>
    {
        [SerializeField, ReadOnlyField] public WorldGraphContainer LastContainer;
        
        [SerializeField, ReadOnlyField] public List<SceneNodeData> SceneNodeData = new();
        [SerializeField, ReadOnlyField] public List<EdgeData> EdgesData = new();
        
        [SerializeField, ReadOnlyField] public List<string> SelectedElementGuids = new();
    }
}