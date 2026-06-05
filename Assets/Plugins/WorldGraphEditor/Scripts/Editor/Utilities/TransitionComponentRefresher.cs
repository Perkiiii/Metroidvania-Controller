using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
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
            var manager = TransitionManager.LoadFromResources();

            if (manager?.Container == null || !manager.Container.HasData)
                return;
            
            var ports = ObjectUtility.FindObjectsByInterface<ITransitionComponent>();
            var context = new RefreshContext(manager);
            
            foreach (var port in ports)
            {
                port.Refresh(context);
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