using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WorldGraphEditor.Editor.Tests;

namespace WorldGraphEditor.Editor
{
    internal class ProjectValidationResult : ScriptableSingleton<ProjectValidationResult>
    {
        [SerializeField] private WorldGraphContainer _container;
        [Space]
        [SerializeField] private bool _ignoreShortcuts;
        [SerializeField] private bool _considerAdditionalPorts;
        [Space]
        [SerializeField] private SCCGroup[] _scc;
        
        public override string FolderPath => WGEAssetPathUtility.RootPath;
        
        public void SetContainer(WorldGraphContainer container)
        {
            _container = container;
            Refresh();
        }
        
        public void Refresh()
        {
            _container.Initialize();
            _scc = SceneCompletionValidator.GetSccFormattedData(_container, !_considerAdditionalPorts, _ignoreShortcuts).ToArray();
        }
        
        [ContextMenu("To JSON")]
        private void ToJson()
        {
            var assetPath = AssetDatabase.GetAssetPath(this);
            var jsonPath = Path.ChangeExtension(assetPath, ".json");
            var json = JsonUtility.ToJson(this, true);
            
            File.WriteAllText(jsonPath, json);
            AssetDatabase.Refresh();
        }
    }
}
