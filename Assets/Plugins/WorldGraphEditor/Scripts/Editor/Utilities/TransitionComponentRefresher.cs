using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldGraphEditor.Editor
{
    
    [InitializeOnLoad]
    internal static class TransitionComponentRefresher
    {
        static TransitionComponentRefresher()
        {
            Undo.postprocessModifications += Refresh;
            EditorApplication.contextualPropertyMenu += HandlePropChanged;
            EditorApplication.hierarchyChanged += RefreshFromHierarchy;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }
        
        public static void RefreshPorts()
        {
            if (EditorApplication.isPlaying)
                return;

            var resolver = WGEProjectConfig.Instance;
            
            if (resolver.Container == null || resolver.GetEditorGraph() == null)
                return;
            
            RefreshAll(new RefreshContext(resolver));
        }
        
        private static void RefreshAll(RefreshContext context)
        {
            var ports = ObjectUtility.FindObjectsByInterface<ITransitionComponent>();
            
            foreach (var port in ports)
            {
                /*PortsDropdown.ResolveChangedSelection = false;*/
                port.Refresh(context);

                /*if (PortsDropdown.ResolveChangedSelection && port is Component component)
                {
                    EditorUtility.SetDirty(component);

                    if (component.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
                }*/
            }
            
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RefreshPorts();
        }
        
        private static void RefreshFromHierarchy()
        {
            RefreshPorts();
        }
        
        private static void HandlePropChanged(GenericMenu menu, SerializedProperty property)
        {
            RefreshPorts();
        }

        private static UndoPropertyModification[] Refresh(UndoPropertyModification[] modifications)
        {
            if (modifications.Any(mod => mod.currentValue.target is ITransitionComponent))
            {
                RefreshPorts();
            }

            return modifications;
        }
    }
}

