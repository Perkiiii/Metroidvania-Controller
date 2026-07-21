using System.Collections.Generic;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    internal class WorldGraphEditorSettings : ScriptableSingleton<WorldGraphEditorSettings>
    {
        [SerializeField, Range(0, 1)] private float _nodeBackgroundOpacity = .55f;

        [Header("Hierarchy Port Highlighter")] 
        [SerializeField] private InspectorHighlighterMode _hierarchyHighlighterMode = InspectorHighlighterMode.NamesAndColors;

        [Header("Toolbar")] 
        [SerializeField] private bool _enableExtensions = true;
        [SerializeField] private bool _showTransitionManagerShortcut = false;
        [SerializeField] private bool _showScenesDropdown = true;
        
        [Header("Graph Save")]
        [SerializeField] private bool _forceRefreshAffectedScenesOnSave = true;

        [Header("Validation")]
        [SerializeField] private SceneTestBase[] _customTests;

        [Header("Transition Manager")]
        [SerializeField] private WGEProjectConfig _projectConfig;

        public float NodeBackgroundOpacity => _nodeBackgroundOpacity;
        public InspectorHighlighterMode HierarchyHighlighterMode => _hierarchyHighlighterMode;
        public bool ForceRefreshAffectedScenesOnSave => _forceRefreshAffectedScenesOnSave;

        public IReadOnlyList<ISceneTest> Tests => _customTests;
        public bool IsExtensionsEnabled => _enableExtensions;
        public bool CanRefreshToolbar => _enableExtensions && (_showTransitionManagerShortcut || _showScenesDropdown);
        public bool ShowTransitionManagerShortcut => _showTransitionManagerShortcut;
        public bool ShowScenesDropdown => _showScenesDropdown;
        public WGEProjectConfig ProjectConfig => _projectConfig;
    }

    public enum InspectorHighlighterMode
    {
        NamesAndColors,
        OnlyNames,
        Nothing
    }
}
