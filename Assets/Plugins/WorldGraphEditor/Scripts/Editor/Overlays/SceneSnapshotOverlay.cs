using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor.Overlays
{
    [Overlay(typeof(SceneView), "Screenshot Utility WGE", defaultDockZone = DockZone.RightColumn)]
    internal class SceneSnapshotOverlay : Overlay
    {
        private Vector3Field _centerField;
        private Vector3Field _rotationField;
        private Vector2Field _sizeField;
        
        private Quaternion _rotationQuat = Quaternion.identity;
        private Vector3 _rotationEuler = Vector3.zero;

        private bool _isRotationMode = false;
        private Slider _cameraDistanceField;
        private EnumField _cameraTypeField;
        private EnumField _resolutionField;
        private EnumField _npotScaleField;
        private CameraType _cameraType = CameraType.Perspective;
        private ScreenshotResolutionType _resolutionType = ScreenshotResolutionType.Low;
        private TextureImporterNPOTScale _npotScaleType = TextureImporterNPOTScale.None;

        private Label _infoLabel;
        private Label _sizeInfoLabel;
        private Label _autoCaptureStatusLabel;
        private Button _enableAutoCaptureButton;
        private VisualElement _disabledPanel;
        private VisualElement _enabledPanel;
        private Button _modeToggleButton;
        private Button _savePresetButton;
        private Button _saveAsDefaultButton;
        private Button _resetButton;
        
        private static SceneSnapshotOverlay _instance;

        private static bool IsOverlayDisabled => _instance == null || !_instance.displayed || _instance.collapsed;
        private static bool _isContainerExists => WGEProjectConfig.Instance.Container != null;
        
        private enum CameraType
        {
            Perspective,
            Orthographic
        }
        
        private UndoPropertyModification[] OnPostProcessModifications(UndoPropertyModification[] modifications)
        {
            ChangeEnabledStatus();
            return modifications;
        }

        private void ChangeEnabledStatus()
        {
            if (IsOverlayDisabled)
                return;
            
            _savePresetButton.SetEnabled(_isContainerExists);
            _saveAsDefaultButton.SetEnabled(_isContainerExists);
        }

        public override VisualElement CreatePanelContent()
        {
            var root = SetupComponents();
            LoadScreenshotData();
            
            _rotationField.RegisterValueChangedCallback(evt =>
            {
                UndoRedoSnapshotOverlayData.Instance.RegisterChanges(GetScreenshotSettings());
                _rotationEuler = evt.newValue;
                _rotationQuat = Quaternion.Euler(_rotationEuler);
            });
            
            _cameraTypeField.RegisterValueChangedCallback(evt =>
            {
                UndoRedoSnapshotOverlayData.Instance.RegisterChanges(GetScreenshotSettings());
                _cameraType = (CameraType) evt.newValue;
            });
            
            _centerField.RegisterValueChangedCallback(_ => UndoRedoSnapshotOverlayData.Instance.RegisterChanges(GetScreenshotSettings()));
            _sizeField.RegisterValueChangedCallback(_ => UndoRedoSnapshotOverlayData.Instance.RegisterChanges(GetScreenshotSettings()));
            _cameraDistanceField.RegisterValueChangedCallback(_ => UndoRedoSnapshotOverlayData.Instance.RegisterChanges(GetScreenshotSettings()));
            _resolutionField.RegisterValueChangedCallback(_ => UndoRedoSnapshotOverlayData.Instance.RegisterChanges(GetScreenshotSettings()));
            _npotScaleField.RegisterValueChangedCallback(_ => UndoRedoSnapshotOverlayData.Instance.RegisterChanges(GetScreenshotSettings()));
            
            UndoRedoSnapshotOverlayData.Instance.RegisterChanges(GetScreenshotSettings());
            
            return root;
        }

        public override void OnCreated()
        {
            _instance = this;
            
            SceneView.duringSceneGui += OnSceneGUI;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            Undo.postprocessModifications += OnPostProcessModifications;
            Undo.undoRedoPerformed += OnUndoRedo;
            SceneScreenshotsData.AutoCaptureSettingsChanged += OnAutoCaptureSettingsChanged;
        }

        private void OnAutoCaptureSettingsChanged()
        {
            if (IsOverlayDisabled)
                return;

            RefreshAutoCaptureStatus();
            RefreshInfoLabel();
        }

        private void OnUndoRedo()
        {
            if (IsOverlayDisabled) 
                return;
            
            ApplyScreenshotData(UndoRedoSnapshotOverlayData.Instance.ScreenshotSettings);
            RefreshAutoCaptureStatus();
            RefreshInfoLabel();
        }

        public override void OnWillBeDestroyed()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            Undo.postprocessModifications -= OnPostProcessModifications;
            Undo.undoRedoPerformed -= OnUndoRedo;
            SceneScreenshotsData.AutoCaptureSettingsChanged -= OnAutoCaptureSettingsChanged;
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (IsOverlayDisabled)
                return;
            
            LoadScreenshotData();
            UndoRedoSnapshotOverlayData.Instance.RegisterChanges(GetScreenshotSettings());
        }

        private void Reset()
        {
            ApplyScreenshotData(SceneScreenshotSettings.Default);

            if (!_isContainerExists)
                return;
            
            SceneScreenshotsData.Instance.RemoveScreenshotSettings(GetScreenshotSettings());
            RefreshInfoLabel();
        }
        
        private void SaveAsDefault()
        {
            SceneScreenshotsData.Instance.SaveAsDefaultScreenshotSettings(GetScreenshotSettings());
            RefreshInfoLabel();
        }

        private void SavePreset()
        {
            SceneScreenshotsData.Instance.SaveScreenshotSettings(GetScreenshotSettings());
            RefreshInfoLabel();
        }
        
        private void TakeScreenshot()
        {
            SceneScreenshotsData.RaiseScreenshotAboutToCapture();

            var texture = SceneScreenshotUtility.CaptureSceneScreenshot(GetScreenshotSettings());

            try
            {
                SceneScreenshotUtility.SaveScreenshot(texture, GetScreenshotSettings().NpotScale,
                    AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path));
                RefreshInfoLabel();
                SceneScreenshotsData.RaiseScreenshotCaptured();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private SceneScreenshotSettings GetScreenshotSettings()
        {
            return new SceneScreenshotSettings
            {
                SceneGuid = AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path),
                Distance = _cameraDistanceField.value,
                IsOrthographic = (CameraType) _cameraTypeField.value == CameraType.Orthographic,
                Resolution = (ScreenshotResolutionType) _resolutionField.value,
                NpotScale = (TextureImporterNPOTScale) _npotScaleField.value,
                Center = _centerField.value,
                Rotation = Quaternion.Euler(_rotationField.value),
                Size = _sizeField.value
            };
        }

        private VisualElement SetupComponents()
        {
            var root = new VisualElement();
            var buttonsTopRoot = new VisualElement {style = {flexDirection = FlexDirection.Column}};
            var buttonsBottomRoot = new VisualElement {style = {flexDirection = FlexDirection.Row}};
            
            _cameraDistanceField = new Slider(0, 50)
            {
                label = "Distance",
                showInputField = true
            };

            _resolutionField = new EnumField("Resolution", _resolutionType);
            _npotScaleField = new EnumField("Non-Power of 2", _npotScaleType);
            _cameraTypeField = new EnumField("Type", _cameraType);

            _centerField = new Vector3Field("Position");
            _rotationField = new Vector3Field("Rotation");
            _sizeField = new Vector2Field("Size");
            
            _modeToggleButton = new Button { text = "Toggle Rotation" };

            _modeToggleButton.clicked += () =>
            {
                _isRotationMode = !_isRotationMode;
                _modeToggleButton.text = _isRotationMode ? "Toggle Move" : "Toggle Rotation";
            };

            _savePresetButton = new Button(SavePreset)
            {
                text = "Save Scene Preset",
                tooltip = "Save the current capture settings for this scene.",
                style = {flexGrow = 1}
            };

            _saveAsDefaultButton = new Button(SaveAsDefault)
            {
                text = "Save as Global Default",
                tooltip = "Set the current capture settings as the default for all scenes that don't have custom settings.",
                style = {flexGrow = 1}
            };

            _resetButton = new Button(Reset)
            {
                text = "Reset to Built-In", 
                tooltip = "Reset capture settings to the built-in defaults (not \"Global Default\").",
                style = {flexGrow = 1}
            };
            
            ChangeEnabledStatus();
            
            buttonsTopRoot.Add(_savePresetButton);
            buttonsTopRoot.Add(_saveAsDefaultButton);
            buttonsTopRoot.Add(_resetButton);
            
            buttonsBottomRoot.Add(new Button(TakeScreenshot)
            {
                text = "Take Screenshot",
                tooltip = "Capture a preview image of the current scene using these settings.",
                style = {flexGrow = 1}
            });

            _infoLabel = new Label();
            _sizeInfoLabel = new Label();
            _autoCaptureStatusLabel = new Label($"Scene preview is {"Disabled".SetColor(MessageColor.Yellow)} for this scene");
            _enableAutoCaptureButton = new Button(EnableAutoCaptureForCurrentScene)
            {
                text = "Enable Auto-Preview",
                tooltip = "Re-enable automatic scene preview capture for this scene."
            };

            var cameraSettings = new Foldout {text = "Camera Settings"};
            var imageSettings = new Foldout {text = "Image Settings"};
            
            cameraSettings.Add(_centerField);
            cameraSettings.Add(_rotationField);
            cameraSettings.Add(_sizeField);
            
            cameraSettings.Add(_cameraDistanceField);
            cameraSettings.Add(_cameraTypeField);
            
            imageSettings.Add(_resolutionField);
            imageSettings.Add(_npotScaleField);
            
            _disabledPanel = new VisualElement {style = {flexDirection = FlexDirection.Column}};
            _disabledPanel.Add(new Separator());
            _disabledPanel.Add(_autoCaptureStatusLabel);
            _disabledPanel.Add(new Separator());
            _disabledPanel.Add(_enableAutoCaptureButton);

            _enabledPanel = new VisualElement {style = {flexDirection = FlexDirection.Column}};
            _enabledPanel.Add(new Separator());
            _enabledPanel.Add(_modeToggleButton);
            _enabledPanel.Add(new Separator());
            _enabledPanel.Add(_infoLabel);
            _enabledPanel.Add(_sizeInfoLabel);
            _enabledPanel.Add(new Separator());
            _enabledPanel.Add(cameraSettings);
            _enabledPanel.Add(imageSettings);
            _enabledPanel.Add(new Separator());
            _enabledPanel.Add(buttonsTopRoot);
            _enabledPanel.Add(new Separator());
            _enabledPanel.Add(buttonsBottomRoot);

            root.Add(_disabledPanel);
            root.Add(_enabledPanel);

            _centerField.style.minWidth = 350;

            RefreshAutoCaptureStatus();
            
            return root;
        }

        private void LoadScreenshotData()
        {
            if (IsOverlayDisabled)
                return;
            
            var sceneGuid = AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path);
            var container = WGEProjectConfig.Instance.Container;
            var data = container == null ? SceneScreenshotSettings.Default : SceneScreenshotsData.Instance.GetScreenshotData(sceneGuid, out _);

            RefreshInfoLabel();
            RefreshAutoCaptureStatus();
            ApplyScreenshotData(data);
        }

        private void EnableAutoCaptureForCurrentScene()
        {
            var sceneGuid = AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path);
            SceneScreenshotsData.Instance.SetAutoCaptureEnabled(sceneGuid, true);
        }

        private void RefreshAutoCaptureStatus()
        {
            var sceneGuid = AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path);
            var isEnabled = SceneScreenshotsData.Instance.IsAutoCaptureEnabled(sceneGuid);
            _disabledPanel.style.display = isEnabled ? DisplayStyle.None : DisplayStyle.Flex;
            _enabledPanel.style.display = isEnabled ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshInfoLabel()
        {
            var sceneGuid = AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path);
            var texture = SceneScreenshotUtility.GetScreenshotTexture(sceneGuid);
            var storageSize = SceneScreenshotUtility.GetScreenshotFileSize(sceneGuid);
            var gpuSize = texture == null ? 0 : (long) texture.width * texture.height / 2;
            SceneScreenshotsData.Instance.GetScreenshotData(sceneGuid, out var hasCustomData);
            RefreshInfoLabel(hasCustomData, storageSize, gpuSize);
        }

        private void RefreshInfoLabel(bool hasCustomData, long storageSize, long gpuSize)
        {
            var coloredMessage = hasCustomData ? "\"Custom\"".SetColor(MessageColor.Green) : "\"Global Default\"".SetColor(MessageColor.Yellow);
            var infoMessage = $"Using {coloredMessage} capture settings";
            var sizeMessage = storageSize > 0 
                ? $"Screenshot size: {SceneScreenshotUtility.FormatFileSize(storageSize)} | {SceneScreenshotUtility.FormatFileSize(gpuSize)}" 
                /*? $"Screenshot size: {SceneScreenshotUtility.FormatFileSize(gpuSize)}" */
                : "Screenshot not found";

            _infoLabel.text = infoMessage;
            _sizeInfoLabel.text = sizeMessage;
        }

        private void ApplyScreenshotData(SceneScreenshotSettings data)
        {
            _sizeField.value = data.Size;
            _centerField.value = data.Center;
            _rotationQuat = data.Rotation;
            _cameraDistanceField.value = data.Distance;
            
            _resolutionField.value = data.Resolution;
            _npotScaleType = data.NpotScale;
            _cameraType = data.IsOrthographic ? CameraType.Orthographic : CameraType.Perspective;

            _resolutionType = data.Resolution;
            _npotScaleField.value = _npotScaleType;
            _cameraTypeField.value = _cameraType;

            _rotationEuler = _rotationQuat.eulerAngles;
            _rotationField.value = _rotationEuler;
        }
        
        private void OnSceneGUI(SceneView sceneView)
        {
            if (IsOverlayDisabled)
                return;

            var sceneGuid = AssetDatabase.AssetPathToGUID(SceneManager.GetActiveScene().path);
            if (!SceneScreenshotsData.Instance.IsAutoCaptureEnabled(sceneGuid))
                return;

            Handles.color = Color.green;
            
            EditorGUI.BeginChangeCheck();
            var rotation = _rotationQuat;

            if (!_isRotationMode)
            {
                _centerField.value = Handles.PositionHandle(_centerField.value, _rotationQuat);
            }
            else
            {
                rotation = Handles.RotationHandle(_rotationQuat, _centerField.value);
            }

            var right = (_rotationQuat * Vector3.right).normalized;
            var up = (_rotationQuat * Vector3.forward).normalized;
            var forward = (_rotationQuat * Vector3.up).normalized;

            var corners = new Vector3[4];
            corners[0] = _centerField.value - right * _sizeField.value.x / 2f - up * _sizeField.value.y / 2f;
            corners[1] = _centerField.value + right * _sizeField.value.x / 2f - up * _sizeField.value.y / 2f;
            corners[2] = _centerField.value + right * _sizeField.value.x / 2f + up * _sizeField.value.y / 2f;
            corners[3] = _centerField.value - right * _sizeField.value.x / 2f + up * _sizeField.value.y / 2f;

            Handles.DrawSolidRectangleWithOutline(corners, new Color(0, 1, 0, 0.05f), Color.green);

            var handleRight = _centerField.value + right * _sizeField.value.x / 2f;
            var handleLeft = _centerField.value - right * _sizeField.value.x / 2f;
            var handleTop = _centerField.value + up * _sizeField.value.y / 2f;
            var handleBottom = _centerField.value - up * _sizeField.value.y / 2f;

            var handleSize = HandleUtility.GetHandleSize(_centerField.value) * 0.1f;

            var newHandleRight = Handles.Slider(handleRight, right, handleSize, Handles.CubeHandleCap, 0.01f);
            var newHandleLeft = Handles.Slider(handleLeft, -right, handleSize, Handles.CubeHandleCap, 0.01f);

            var newHandleTop = Handles.Slider(handleTop, up, handleSize, Handles.CubeHandleCap, 0.01f);
            var newHandleBottom = Handles.Slider(handleBottom, -up, handleSize, Handles.CubeHandleCap, 0.01f);
            
            var x = Vector3.Dot(newHandleRight - newHandleLeft, right);
            var y = Vector3.Dot(newHandleTop - newHandleBottom, up);
            
            if (EditorGUI.EndChangeCheck())
            {
                _sizeField.value = new Vector3(x, y);
                
                if (_isRotationMode)
                {
                    _rotationQuat = rotation;
                    _rotationEuler = _rotationQuat.eulerAngles;
                    _rotationField.value = _rotationEuler;
                }
            }
            
            var cameraPos = DrawCamera(forward, handleSize);
            DrawCameraFrustum(cameraPos, _centerField.value, right, up, forward);
            DrawLabels(cameraPos, handleSize);
        }

        private Vector3 DrawCamera(Vector3 forward, float handleSize)
        {
            var cameraPos = _centerField.value + forward * _cameraDistanceField.value;
            Handles.color = Color.cyan;
            Handles.SphereHandleCap(0, cameraPos, Quaternion.identity, handleSize, UnityEngine.EventType.Repaint);

            Handles.DrawLine(cameraPos, _centerField.value);
            return cameraPos;
        }

        private void DrawLabels(Vector3 cameraPos, float handleSize)
        {
            Handles.Label(_centerField.value + Vector3.up * HandleUtility.GetHandleSize(_centerField.value),
                $"Size: {_sizeField.value.x:F2} x {_sizeField.value.y:F2} \nDistance: {_cameraDistanceField.value}");
            Handles.Label(cameraPos + Vector3.up * handleSize, $"{_cameraType} Camera");
        }

        private void DrawCameraFrustum(Vector3 cameraPos, Vector3 planeCenter, Vector3 right, Vector3 up,
            Vector3 forward)
        {
            Handles.color = new Color(1, 0.5f, 0, 0.8f);

            var corners = new[]
            {
                planeCenter - right * _sizeField.value.x / 2f - up * _sizeField.value.y / 2f,
                planeCenter + right * _sizeField.value.x / 2f - up * _sizeField.value.y / 2f,
                planeCenter + right * _sizeField.value.x / 2f + up * _sizeField.value.y / 2f,
                planeCenter - right * _sizeField.value.x / 2f + up * _sizeField.value.y / 2f
            };

            if (_cameraType == CameraType.Perspective)
            {
                foreach (var corner in corners)
                    Handles.DrawLine(cameraPos, corner);
            }
            else
            {
                var depth = _cameraDistanceField.value;
                var offset = forward * depth;

                var backCorners = new Vector3[4];
                for (int i = 0; i < 4; i++)
                    backCorners[i] = corners[i] + offset;

                for (int i = 0; i < 4; i++)
                {
                    Handles.DrawLine(backCorners[i], backCorners[(i + 1) % 4]);
                    Handles.DrawLine(corners[i], backCorners[i]);
                }
            }
        }
    }
}