using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    [CustomEditor(typeof(ProjectValidationResult))]
    internal class ProjectValidationResultEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            var containerField = new PropertyField(serializedObject.FindProperty("_container"));
            var ignoreShortcutsField = new PropertyField(serializedObject.FindProperty("_ignoreShortcuts"));
            var considerAdditionalPortsField = new PropertyField(serializedObject.FindProperty("_considerAdditionalPorts"));
            var sccField = new PropertyField(serializedObject.FindProperty("_scc"), "Graph");
            var sccHelpBox =
                new HelpBox(
                    "A Strongly Connected Component (SCC) is a group of scenes in a graph where every scene is reachable from every other scene by following scene connections.",
                    HelpBoxMessageType.Info);

            containerField.Bind(serializedObject);
            ignoreShortcutsField.Bind(serializedObject);
            considerAdditionalPortsField.Bind(serializedObject);
            sccField.Bind(serializedObject);

            var btn = new Button(() =>
                {
                    ((ProjectValidationResult)target).Refresh();

                    serializedObject.Update(); 
                    sccField.Bind(serializedObject); 
                })
                { text = "Refresh SCC Rules" };

            root.Add(containerField);
            root.Add(sccHelpBox);
            root.Add(ignoreShortcutsField);
            root.Add(considerAdditionalPortsField);
            root.Add(sccField);
            root.Add(btn);

            return root;
        }
    }
}