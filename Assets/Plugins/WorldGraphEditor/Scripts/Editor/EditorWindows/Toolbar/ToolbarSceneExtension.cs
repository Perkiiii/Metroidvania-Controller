#if !UNITY_6000_3_OR_NEWER

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace WorldGraphEditor.Editor
{
    [InitializeOnLoad]
    internal static class ToolbarSceneExtension
    {
        static ToolbarSceneExtension()
        {
            EditorApplication.update += TryAddToolbarUI;
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) => RefreshScenes();
        }
        
        private static ToolbarMenu _sceneDropdown;
        private static ToolbarMenu _modeDropdown;

        private static Object _toolbarObject;
        private static FieldInfo _toolbarInfo;

        private static int _modeIndex;
        private static int _sceneIndex;

        private static string[] _sceneNames = Array.Empty<string>();
        private static readonly string[] _modes = { "Neighbours", "Build Settings", "All Scenes" };
        private const string _TOOLBAR_SELECTED_MODE = "Toolbar_Selected_Mode";

        internal static void Refresh()
        {
            TryAddToolbarUI();
        }

        internal static void Disable()
        {
            GetToolbar(out _toolbarObject, out _toolbarInfo);
            
            if (_toolbarInfo?.GetValue(_toolbarObject) is not VisualElement root)
                return;
            
            var oldWrapper = root.Q<VisualElement>("WGEToolbarWrapper");
            oldWrapper?.RemoveFromHierarchy();
        }

        private static void TryAddToolbarUI()
        {
            var settings = WorldGraphEditorSettings.Instance;
            
            if (!settings.CanRefreshToolbar)
                return;

            GetToolbar(out _toolbarObject, out _toolbarInfo);
            
            if (_toolbarInfo?.GetValue(_toolbarObject) is not VisualElement root || root.Q<VisualElement>("WGEToolbarWrapper") != null)
                return;

            _modeIndex = EditorPrefs.GetInt(_TOOLBAR_SELECTED_MODE, 0);
            RefreshScenes();
            
            var leftContainer = root.Q("ToolbarZoneLeftAlign");
            var wrapper = CreateToolbarWrapper(settings);
            leftContainer.Add(wrapper);
        }

        private static void GetToolbar(out Object toolbar, out FieldInfo info)
        {
            if (_toolbarObject != null && _toolbarInfo != null)
            {
                toolbar = _toolbarObject;
                info = _toolbarInfo;
                return;
            }
            
            var toolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType); 
            
            if (toolbars.Length == 0)
            {
                toolbar = null;
                info = null;
                return;
            }
            
            info = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            toolbar = toolbars[0];
        }

        private static VisualElement CreateToolbarWrapper(WorldGraphEditorSettings settings)
        {
            var wrapper = new VisualElement
            {
                name = "WGEToolbarWrapper",
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.FlexEnd 
                }
            };

            if (settings.ShowScenesDropdown)
            {
                _modeDropdown = new ToolbarMenu
                {
                    text = _modes[_modeIndex],
                    style = {height = 18}
                };

                for (int i = 0; i < _modes.Length; i++)
                {
                    var idx = i;
                    _modeDropdown.menu.AppendAction(_modes[i], _ =>
                    {
                        _modeIndex = idx;
                        _modeDropdown.text = _modes[idx];
                        RefreshScenes();
                        EditorPrefs.SetInt(_TOOLBAR_SELECTED_MODE, _modeIndex);
                    });
                }

                _sceneDropdown = new ToolbarMenu
                {
                    text = _sceneNames.ElementAtOrDefault(_sceneIndex) ?? "No Scene",
                    style = {height = 18}
                };
            }
            
            RefreshSceneDropdownMenu();

            if (settings.ShowTransitionManagerShortcut)
            {
                var transitionManagerShortcutButton =
                    new ToolbarButton(TransitionManagerPrefabCreator.CreateOrOpenPrefab)
                    {
                        text = "TM", tooltip = "Opens or creates a new Transition Manager",
                        style = {height = 20, unityTextAlign = TextAnchor.MiddleCenter}
                    };

                wrapper.Add(transitionManagerShortcutButton);
            }

            if (settings.ShowScenesDropdown)
            {
                wrapper.Add(new VisualElement { style = { width = 12 } });
                wrapper.Add(_modeDropdown);
                wrapper.Add(new VisualElement { style = { width = 4 } });
                wrapper.Add(_sceneDropdown);
            }

            return wrapper;
        }

        private static void RefreshScenes()
        {
            if (_modeIndex == 0)
            {
                var editorData = WGEProjectConfig.Instance.GetEditorGraph();
                
                if (editorData != null)
                {
                    var activeScenePath = SceneManager.GetActiveScene().path;

                    if (!editorData.TryGetSceneDataByPath(activeScenePath, out var sceneData))
                    {
                        _sceneNames = Array.Empty<string>();
                        return;
                    }

                    _sceneNames = editorData.GetNeighboursData(sceneData, true).Select(n => n.SceneAsset?.name).ToArray();
                }
                else
                {
                    _sceneNames = Array.Empty<string>();
                }
            }
            else if (_modeIndex == 1)
            {
                _sceneNames = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => Path.GetFileNameWithoutExtension(s.path))
                    .ToArray();
            }
            else
            {
                _sceneNames = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories)
                    .Select(Path.GetFileNameWithoutExtension)
                    .ToArray();
            }

            _sceneIndex = Mathf.Clamp(_sceneIndex, 0, _sceneNames.Length - 1);
            if (_sceneDropdown != null)
            {
                _sceneDropdown.text = _sceneNames.Length > 0 ? _sceneNames[_sceneIndex] : "No Scene";
                RefreshSceneDropdownMenu();
            }
        }

        private static void RefreshSceneDropdownMenu()
        {
            if (_sceneDropdown == null)
                return;
            
            _sceneDropdown.menu.ClearItems();
            foreach (var scene in _sceneNames)
            {
                _sceneDropdown.menu.AppendAction(scene, _ =>
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        return;

                    _sceneIndex = Array.IndexOf(_sceneNames, scene);
                    _sceneDropdown.text = scene;

                    var path = AssetDatabase.FindAssets($"t:Scene {scene}")
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == scene);

                    if (!string.IsNullOrEmpty(path))
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                });
            }
        }
    }
}

#endif