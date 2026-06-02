using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
#if !UNITY_6000_3_OR_NEWER
using System.Reflection;
#endif
using UnityEditor;
using UnityEditor.SceneManagement;
#if UNITY_6000_3_OR_NEWER
using UnityEditor.Toolbars;
#else
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
#if !UNITY_6000_3_OR_NEWER
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
#endif

namespace WorldGraphEditor.Editor
{
    [InitializeOnLoad]
    internal static class ToolbarSceneExtension
    {
        private const string TOOLBAR_ELEMENT_ID = "WorldGraphEditor";
        private const string TOOLBAR_SELECTED_MODE = "Toolbar_Selected_Mode";
        private static readonly string[] Modes = { "Neighbours", "Build Settings", "All Scenes" };

        private static int _modeIndex;
        private static int _sceneIndex;
        private static string[] _sceneNames = Array.Empty<string>();

#if !UNITY_6000_3_OR_NEWER
        private static ToolbarMenu _sceneDropdown;
        private static ToolbarMenu _modeDropdown;
        private static Object _toolbarObject;
        private static FieldInfo _toolbarInfo;
#endif

        static ToolbarSceneExtension()
        {
            _modeIndex = EditorPrefs.GetInt(TOOLBAR_SELECTED_MODE, 0);
            EditorSceneManager.activeSceneChangedInEditMode += (_, _) =>
            {
                RefreshScenes();
#if UNITY_6000_3_OR_NEWER
                MainToolbar.Refresh(TOOLBAR_ELEMENT_ID);
#endif
            };

#if !UNITY_6000_3_OR_NEWER
            EditorApplication.update += TryAddToolbarUI;
#endif
        }

        internal static void Refresh()
        {
            RefreshScenes();
#if UNITY_6000_3_OR_NEWER
            MainToolbar.Refresh(TOOLBAR_ELEMENT_ID);
#else
            TryAddToolbarUI();
#endif
        }

        internal static void Disable()
        {
#if UNITY_6000_3_OR_NEWER
            MainToolbar.Refresh(TOOLBAR_ELEMENT_ID);
#else
            GetToolbar(out _toolbarObject, out _toolbarInfo);
            
            if (_toolbarInfo?.GetValue(_toolbarObject) is not VisualElement root)
                return;
            
            var oldWrapper = root.Q<VisualElement>("WGEToolbarWrapper");
            oldWrapper?.RemoveFromHierarchy();
#endif
        }

#if UNITY_6000_3_OR_NEWER
        [MainToolbarElement(
            TOOLBAR_ELEMENT_ID,
            defaultDockPosition = MainToolbarDockPosition.Left)]
        private static IEnumerable<MainToolbarElement> CreateToolbar()
        {
            var settings = WorldGraphEditorSettings.Instance;
            if (!settings.CanRefreshToolbar)
                yield break;

            RefreshScenes();

            if (settings.ShowTransitionManagerShortcut)
            {
                yield return new MainToolbarButton(
                    new MainToolbarContent("TM", null, "Opens or creates a new Transition Manager"),
                    TransitionManagerPrefabCreator.CreateOrOpenPrefab);
            }

            if (!settings.ShowScenesDropdown)
                yield break;

            yield return new MainToolbarButton(
                new MainToolbarContent(Modes[_modeIndex], null, "World Graph Editor scene list mode"),
                ShowModeMenu);

            yield return new MainToolbarButton(
                new MainToolbarContent(GetSceneButtonText(), null, "Open a scene from the World Graph Editor scene list"),
                ShowSceneMenu);
        }

        private static void ShowModeMenu()
        {
            var menu = new GenericMenu();
            for (int i = 0; i < Modes.Length; i++)
            {
                int index = i;
                menu.AddItem(new GUIContent(Modes[i]), _modeIndex == index, () =>
                {
                    _modeIndex = index;
                    EditorPrefs.SetInt(TOOLBAR_SELECTED_MODE, _modeIndex);
                    RefreshScenes();
                    MainToolbar.Refresh(TOOLBAR_ELEMENT_ID);
                });
            }

            menu.ShowAsContext();
        }

        private static void ShowSceneMenu()
        {
            RefreshScenes();

            var menu = new GenericMenu();
            if (_sceneNames.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No Scene"));
                menu.ShowAsContext();
                return;
            }

            foreach (string scene in _sceneNames)
            {
                string capturedScene = scene;
                menu.AddItem(new GUIContent(capturedScene), _sceneNames.ElementAtOrDefault(_sceneIndex) == capturedScene, () =>
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        return;

                    _sceneIndex = Array.IndexOf(_sceneNames, capturedScene);
                    OpenSceneByName(capturedScene);
                    MainToolbar.Refresh(TOOLBAR_ELEMENT_ID);
                });
            }

            menu.ShowAsContext();
        }
#else
        private static void TryAddToolbarUI()
        {
            var settings = WorldGraphEditorSettings.Instance;
            
            if (!settings.CanRefreshToolbar)
                return;

            GetToolbar(out _toolbarObject, out _toolbarInfo);
            
            if (_toolbarInfo?.GetValue(_toolbarObject) is not VisualElement root || root.Q<VisualElement>("WGEToolbarWrapper") != null)
                return;

            _modeIndex = EditorPrefs.GetInt(TOOLBAR_SELECTED_MODE, 0);
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
                    text = Modes[_modeIndex],
                    style = {height = 18}
                };

                for (int i = 0; i < Modes.Length; i++)
                {
                    var idx = i;
                    _modeDropdown.menu.AppendAction(Modes[i], _ =>
                    {
                        _modeIndex = idx;
                        _modeDropdown.text = Modes[idx];
                        RefreshScenes();
                        EditorPrefs.SetInt(TOOLBAR_SELECTED_MODE, _modeIndex);
                    });
                }

                _sceneDropdown = new ToolbarMenu
                {
                    text = GetSceneButtonText(),
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
#endif

        private static void RefreshScenes()
        {
            _modeIndex = Mathf.Clamp(_modeIndex, 0, Modes.Length - 1);

            if (_modeIndex == 0)
            {
                var container = TransitionManager.LoadFromResources()?.Container;
                _sceneNames = container != null && container.HasData && container.EditorData != null
                    ? container.EditorData.GetNeighboursData(SceneManager.GetActiveScene().buildIndex, true)
                        .Select(n => n.SceneAsset.name)
                        .ToArray()
                    : Array.Empty<string>();
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

            _sceneIndex = _sceneNames.Length > 0
                ? Mathf.Clamp(_sceneIndex, 0, _sceneNames.Length - 1)
                : 0;

#if !UNITY_6000_3_OR_NEWER
            if (_sceneDropdown != null)
            {
                _sceneDropdown.text = GetSceneButtonText();
                RefreshSceneDropdownMenu();
            }
#endif
        }

        private static string GetSceneButtonText()
        {
            return _sceneNames.Length > 0 ? _sceneNames[_sceneIndex] : "No Scene";
        }

        private static void OpenSceneByName(string scene)
        {
            var path = AssetDatabase.FindAssets($"t:Scene {scene}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == scene);

            if (!string.IsNullOrEmpty(path))
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

#if !UNITY_6000_3_OR_NEWER
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
                    OpenSceneByName(scene);
                });
            }
        }
#endif
    }
}
