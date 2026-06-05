using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace WorldGraphEditor.Editor
{
    [CustomEditor(typeof(SceneNodeInspectorHelper))]
    internal class SceneNodeInspectorHelperEditor : UnityEditor.Editor
    {
        private SerializedProperty _nodeNameProperty;
        private SerializedProperty _sceneAssetProperty;
        private SerializedProperty _ports;

        private SceneNodeInspectorHelper _inspectorHelper;

        private GUIStyle _guiHeaderStyle;

        private void OnEnable()
        {
            if (target is not SceneNodeInspectorHelper drawHelper)
                return;
            
            _inspectorHelper = drawHelper;
            _nodeNameProperty = serializedObject.FindProperty("_nodeName");
            _sceneAssetProperty = serializedObject.FindProperty("_sceneAsset");
            _ports = serializedObject.FindProperty("_portsData");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            CreateHeaderStyle();
            
            EditorGUI.BeginChangeCheck();

            DrawNodeFields();
            DrawPorts();
            DrawErrorBoxes();
            DrawScenePreview();
            
            if (EditorGUI.EndChangeCheck()) 
                EditorUtility.SetDirty(_inspectorHelper);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawScenePreview()
        {
            var sceneAsset = _inspectorHelper.SceneAsset;
            if (sceneAsset == null)
                return;

            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(sceneAsset));
            var texture = WGEAssetPathUtility.LoadAsset<Texture2D>($"Editor/Screenshots/{guid}.png", false);

            if (texture == null)
                return;

            var maxWidth = EditorGUIUtility.currentViewWidth - 24;
            var aspect = (float) texture.width / texture.height;
            var height = maxWidth / aspect;

            GUILayout.Space(12);
            GUILayout.Label("Scene Preview", _guiHeaderStyle);
            GUILayout.Space(4);

            var rect = GUILayoutUtility.GetRect(maxWidth, height, GUILayout.ExpandWidth(false),
                GUILayout.ExpandHeight(false));
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, false);
        }

        private void CreateHeaderStyle()
        {
            if (_guiHeaderStyle != null)
                return;
            
            var textColor = new Color(0f, 1f, 0.58f);

            _guiHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16,
                normal = {textColor = textColor},
                hover = {textColor = textColor}
            };
        }

        private void DrawNodeFields()
        {
            GUILayout.Label("Node Data", _guiHeaderStyle);
            
            EditorGUILayout.PropertyField(_nodeNameProperty, new GUIContent("Name"));
            EditorGUILayout.PropertyField(_sceneAssetProperty, new GUIContent("Scene Asset"));
        }

        private void DrawPorts()
        {
            if (_inspectorHelper.Ports == null || _inspectorHelper.Ports.Count == 0)
                return;

            GUILayout.Space(12);
            GUILayout.Label("Ports", _guiHeaderStyle);
            
            var indexedPorts = _inspectorHelper.Ports
                .Select((port, index) => (index, port))
                .OrderBy(pair => pair.port.portColor.ToString())
                .ToList();

            for (int sortedIndex = 0; sortedIndex < indexedPorts.Count; sortedIndex++)
            {
                DrawPortField(indexedPorts, sortedIndex);
            }
        }

        private void DrawPortField(List<(int Index, Port Port)> indexedPorts, int sortedIndex)
        {
            var originalIndex = indexedPorts[sortedIndex].Index;
            var port = indexedPorts[sortedIndex].Port;
            var portDirection = GetDirection(port.direction == Direction.Input, port.orientation == Orientation.Horizontal);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            var colorRect = GUILayoutUtility.GetRect(5, EditorGUIUtility.singleLineHeight);
            EditorGUI.DrawRect(new Rect(colorRect.x, colorRect.y, 5, colorRect.height), port.portColor);

            var newPortName = EditorGUILayout.TextField($"Port {sortedIndex + 1} - {portDirection}", port.portName);
                
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                RemovePort(port.GetGuid(), originalIndex);
            }

            EditorGUILayout.EndHorizontal();
                
            if (newPortName != port.portName)
            {
                _ports.GetArrayElementAtIndex(originalIndex).FindPropertyRelative("Name").stringValue = newPortName;
            }

            EditorGUILayout.EndVertical();
        }

        private static PortDirection GetDirection(bool isInput, bool isHorizontal)
        {
            return isInput switch
            {
                true when isHorizontal => PortDirection.Left,
                false when isHorizontal => PortDirection.Right,
                true when true => PortDirection.Top,
                _ => PortDirection.Bottom
            };
        }
        
        private void RemovePort(string guid, int index)
        {
            if (_inspectorHelper.CanSafelyRemoved(index))
            {
                _inspectorHelper.RemovePort(guid, index);
                return;
            }

            if (EditorUtility.DisplayDialog(
                    "Confirm Deletion",
                    "Port is linked to other, are you sure want to delete this port?",
                    "Yes",
                    "No"))
            {
                _inspectorHelper.RemovePort(guid, index);
            }
        }

        private void DrawErrorBoxes()
        {
            if (_inspectorHelper.IsPortsNamesValid())
            {
                GUILayout.Space(12);
                EditorGUILayout.HelpBox(
                    "Port names must be unique and cannot be empty or consist only of spaces.", MessageType.Error);
            }

            if (_inspectorHelper.IsNodeContainsSceneDuplicate())
            {
                GUILayout.Space(12);
                EditorGUILayout.HelpBox(
                    "The graph already contains a node associated with the same scene. " +
                    "Saving the graph in this state may disrupt existing port configurations within the affected scenes." +
                    "To ensure proper functionality, remove duplicate nodes.", MessageType.Error);
            }

            if (_inspectorHelper.IsNodeHasNoSceneAsset())
            {
                GUILayout.Space(12);
                EditorGUILayout.HelpBox(
                    "Node MUST contain a scene.", MessageType.Error);
            }
        }
    }
}
