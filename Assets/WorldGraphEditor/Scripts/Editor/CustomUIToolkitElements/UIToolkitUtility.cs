using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using WorldGraphEditor.Editor.Overlays;

namespace WorldGraphEditor.Editor
{
    internal static class UIToolkitUtility
    {
        public const string ERROR_ICON_NAME = "console.erroricon";
        public const string WARNING_ICON_NAME = "console.warnicon";
        public const string INFO_ICON_NAME = "console.infoicon";

        public static Texture CORRECT_ICON_WGE => AssetDatabase.LoadAssetAtPath<Texture>("Assets/WorldGraphEditor/Scripts/Editor/Icons/success_icon.png");
        public static Texture WARNING_ICON_WGE => AssetDatabase.LoadAssetAtPath<Texture>("Assets/WorldGraphEditor/Scripts/Editor/Icons/warning_icon.png");
        public static Texture ERROR_ICON_WGE => AssetDatabase.LoadAssetAtPath<Texture>("Assets/WorldGraphEditor/Scripts/Editor/Icons/error_icon.png");
        
        public static Label CreateLabel(string text, MessageColor color)
        {
            var label = new Label(text.SetColor(color))
            {
                style = {alignSelf = Align.Center}
            };
            return label;
        }

        public static VisualElement CreateIcon(string iconName, float size)
        {
            var icon = EditorGUIUtility.IconContent(iconName).image;
            var image = new Image
            {
                image = icon,
                style = { width = size, height = size, marginTop = 0 }
            };
            return image;
        }
        
        public static VisualElement CreateIcon(Texture icon, float size)
        {
            var image = new Image
            {
                image = icon,
                style = { width = size, height = size, marginTop = 0 }
            };
            return image;
        }

        public static void FillSceneStatusElement(HeaderWithListElement sceneStatusElement, WorldGraphContainer container, SceneCompletionData sceneCompletionData)
        {
            var message = OverlayUtility.GetSceneStatus(out var isSceneValid);
            var color = MessageColor.Default;
            var iconName = WARNING_ICON_NAME;

            if (isSceneValid)
            {
                MessageColor ratioColor;
                var wrongPorts = new List<VisualElement>();

                if (sceneCompletionData.Duplicates.Length > 0)
                {
                    ratioColor = MessageColor.Red;
                    iconName = ERROR_ICON_NAME;
                }
                else if (sceneCompletionData.Missing.Length > 0)
                {
                    ratioColor = MessageColor.Yellow;
                    iconName = WARNING_ICON_NAME;
                }
                else
                {
                    ratioColor = MessageColor.Green;
                    iconName = INFO_ICON_NAME;
                }

                FillDuplicates(wrongPorts, sceneCompletionData, container, "Duplicates:");
                FillMissing(wrongPorts, sceneCompletionData, "Missing:");

                var ratio = sceneCompletionData.Ratio;
                var sceneData = $"{sceneCompletionData.NodeName}: {ratio}".SetColor(ratioColor);
                message = $"Ports at {sceneData}";

                sceneStatusElement.AddToList(wrongPorts);
            }
            
            var sceneInfoLabel = CreateLabel(message, color);

            var headerItems = new[]
            {
                CreateIcon(iconName, 20),
                sceneInfoLabel
            };

            sceneStatusElement.SetHeader(headerItems);
        }

        private static void FillDuplicates(List<VisualElement> wrongPorts, SceneCompletionData sceneCompletionData, WorldGraphContainer container, string header)
        {
            if (sceneCompletionData.Duplicates.Length == 0)
                return;
            
            wrongPorts.Add(new Separator());
            wrongPorts.Add(new Label(header.SetColor(MessageColor.Red)));

            foreach (var duplicate in sceneCompletionData.Duplicates)
            {
                if (duplicate is not MonoBehaviour beh)
                    continue;
                
                var name = container.EditorData.GetPortData(duplicate.GetGuid()).Name;
                wrongPorts.Add(new DuplicateListElement(name, beh.gameObject));
            }
        }

        private static void FillMissing(List<VisualElement> wrongPorts, SceneCompletionData sceneCompletionData, string header)
        {
            if (sceneCompletionData.Missing.Length == 0)
                return;
            
            wrongPorts.Add(new Separator());
            wrongPorts.Add(new Label(header.SetColor(MessageColor.Yellow)));

            foreach (var missing in sceneCompletionData.Missing)
            {
                wrongPorts.Add(new Label(missing.Name)
                {
                    style = {left = 8}
                });
            }
        }
        
        public static Button GetLoadSceneButton(string scenePath, string text)
        {
            return new Button(() => LoadSceneButtonCallback(scenePath)) {text = text};
        }

        public static void LoadSceneButtonCallback(string scenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) 
                return;
            
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        public static void LoadSceneButtonCallback(SceneAsset sceneAsset) => LoadSceneButtonCallback(AssetDatabase.GetAssetPath(sceneAsset));
    }
}