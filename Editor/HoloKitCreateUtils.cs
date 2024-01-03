// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileCopyrightText: Copyright 2020 Unity Technologies ApS
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT AND LicenseRef-Unity-Companion-License

using UnityEngine;
using UnityEditor;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace HoloInteractive.XR.HoloKit.Editor
{
    static class HoloKitCreateUtils
    {
        [MenuItem("GameObject/XR/HoloKit/XR Origin")]
        static void CreateHoloKitXROrigin(MenuCommand menuCommand)
        {
            var context = menuCommand.context as GameObject;
            var parent = context != null ? context.transform : null;
            var xrOrigin = CreateHoloKitXROriginWithParent(parent);
            Selection.activeGameObject = xrOrigin.gameObject;
        }

        static XROrigin CreateHoloKitXROriginWithParent(Transform parent)
        {
            var originGo = ObjectFactory.CreateGameObject("HoloKit XR Origin", typeof(XROrigin));
            CreateUtils.Place(originGo, parent);

            var offsetGo = ObjectFactory.CreateGameObject("Camera Offset");
            CreateUtils.Place(offsetGo, originGo.transform);

            var arCamera = CreateARMainCamera();
            CreateUtils.Place(arCamera.gameObject, offsetGo.transform);

            arCamera.gameObject.AddComponent<HoloKitCameraManager>();

            var origin = originGo.GetComponent<XROrigin>();
            origin.CameraFloorOffsetObject = offsetGo;
            origin.Camera = arCamera;

            Undo.RegisterCreatedObjectUndo(originGo, "Create HoloKit XR Origin");
            return origin;
        }

        static Camera CreateARMainCamera()
        {
            var mainCam = Camera.main;
            if (mainCam != null)
            {
                Debug.LogWarningFormat(
                    mainCam.gameObject,
                    "XR Origin Main Camera requires the \"MainCamera\" Tag, but the current scene contains another enabled Camera tagged \"MainCamera\". For AR to function properly, remove the \"MainCamera\" Tag from \'{0}\' or disable it.",
                    mainCam.name);
            }

            var cameraGo = ObjectFactory.CreateGameObject(
                "Main Camera",
                typeof(Camera),
                typeof(AudioListener),
                typeof(ARCameraManager),
                typeof(ARCameraBackground),
                typeof(TrackedPoseDriver));

            var camera = cameraGo.GetComponent<Camera>();
            Undo.RecordObject(camera, "Configure Camera");
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            camera.depth = -2f;

            var trackedPoseDriver = cameraGo.GetComponent<TrackedPoseDriver>();

            Undo.RecordObject(trackedPoseDriver, "Configure Tracked Pose Driver");
            var positionAction = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotationAction = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
            trackedPoseDriver.positionInput = new InputActionProperty(positionAction);
            trackedPoseDriver.rotationInput = new InputActionProperty(rotationAction);
            return camera;
        }

        [MenuItem("GameObject/UI/HoloKit/Default UI Canvas")]
        static void CreateHoloKitDefaultUICanvas(MenuCommand menuCommand)
        {
            var context = menuCommand.context as GameObject;
            var parent = context != null ? context.transform : null;
            var canvas = CreateHoloKitDefaultUICanvasWithParent(parent);
            Selection.activeGameObject = canvas.gameObject;
        }

        static Canvas CreateHoloKitDefaultUICanvasWithParent(Transform parent)
        {
            var canvasGo = ObjectFactory.CreateGameObject("HoloKit Default UI Canvas", typeof(Canvas));
            CreateUtils.Place(canvasGo, parent);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            _ = canvasGo.AddComponent<CanvasScaler>();
            _ = canvasGo.AddComponent<GraphicRaycaster>();
            var defaultUICanavs = canvasGo.AddComponent<UI.HoloKitDefaultUICanvas>();

            // Render mode button
            var renderModeButtonGo = ObjectFactory.CreateGameObject("Switch Render Mode Button", typeof(RectTransform));
            CreateUtils.Place(renderModeButtonGo, canvasGo.transform);
            var renderModeButtonRectTransform = renderModeButtonGo.GetComponent<RectTransform>();
            renderModeButtonRectTransform.anchorMin = new(1f, 1f);
            renderModeButtonRectTransform.anchorMax = new(1f, 1f);
            renderModeButtonRectTransform.anchoredPosition = new(-100f, -40f);
            renderModeButtonRectTransform.sizeDelta = new(200f, 100f);
            renderModeButtonRectTransform.pivot = new(1f, 1f);
            _ = renderModeButtonGo.AddComponent<CanvasRenderer>();
            var renderModeButtonImage = renderModeButtonGo.AddComponent<Image>();
            renderModeButtonImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            renderModeButtonImage.type = Image.Type.Sliced;
            var renderModeButton = renderModeButtonGo.AddComponent<Button>();
            //button.onClick.AddListener(defaultUICanavs.SwitchRenderMode);
            SerializedObject renderModeSerializedButton = new SerializedObject(renderModeButton);
            SerializedProperty renderModeOnClickProp = renderModeSerializedButton.FindProperty("m_OnClick");
            int renderModeNumListeners = renderModeOnClickProp.FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize;
            renderModeOnClickProp.FindPropertyRelative("m_PersistentCalls.m_Calls").InsertArrayElementAtIndex(renderModeNumListeners);
            SerializedProperty renderModeAddedElement = renderModeOnClickProp.FindPropertyRelative("m_PersistentCalls.m_Calls").GetArrayElementAtIndex(renderModeNumListeners);
            renderModeAddedElement.FindPropertyRelative("m_Target").objectReferenceValue = defaultUICanavs;
            renderModeAddedElement.FindPropertyRelative("m_MethodName").stringValue = "SwitchRenderMode";
            //addedElement.FindPropertyRelative("m_Mode").enumValueIndex = 1;
            renderModeAddedElement.FindPropertyRelative("m_CallState").enumValueIndex = 2;
            renderModeSerializedButton.ApplyModifiedProperties();

            var renderModeButtonTextGo = ObjectFactory.CreateGameObject("Text (Legacy)", typeof(RectTransform));
            CreateUtils.Place(renderModeButtonTextGo, renderModeButtonGo.transform);
            _ = renderModeButtonTextGo.AddComponent<CanvasRenderer>();
            var renderModeButtonText = renderModeButtonTextGo.AddComponent<Text>();
            renderModeButtonText.text = "Stereo";
            renderModeButtonText.fontStyle = FontStyle.Bold;
            renderModeButtonText.fontSize = 36;
            renderModeButtonText.alignment = TextAnchor.MiddleCenter;
            renderModeButtonText.color = Color.black;
            defaultUICanavs.RenderModeButtonText = renderModeButtonText;

#if UNITY_IOS
            // Record button
            var recordButtonGo = ObjectFactory.CreateGameObject("Record Button", typeof(RectTransform));
            CreateUtils.Place(recordButtonGo, canvasGo.transform);
            var recordButtonRectTransform = recordButtonGo.GetComponent<RectTransform>();
            recordButtonRectTransform.anchorMin = new(0f, 1f);
            recordButtonRectTransform.anchorMax = new(0f, 1f);
            recordButtonRectTransform.anchoredPosition = new(100f, -40f);
            recordButtonRectTransform.sizeDelta = new(300f, 100f);
            recordButtonRectTransform.pivot = new(0f, 1f);
            _ = recordButtonGo.AddComponent<CanvasRenderer>();
            var recordButtonImage = recordButtonGo.AddComponent<Image>();
            recordButtonImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            recordButtonImage.type = Image.Type.Sliced;
            var recordButton = recordButtonGo.AddComponent<Button>();
            SerializedObject recordSerializedButton = new SerializedObject(recordButton);
            SerializedProperty recordOnClickProp = recordSerializedButton.FindProperty("m_OnClick");
            int recordNumListeners = recordOnClickProp.FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize;
            recordOnClickProp.FindPropertyRelative("m_PersistentCalls.m_Calls").InsertArrayElementAtIndex(recordNumListeners);
            SerializedProperty recordAddedElement = recordOnClickProp.FindPropertyRelative("m_PersistentCalls.m_Calls").GetArrayElementAtIndex(recordNumListeners);
            recordAddedElement.FindPropertyRelative("m_Target").objectReferenceValue = defaultUICanavs;
            recordAddedElement.FindPropertyRelative("m_MethodName").stringValue = "ToggleRecording";
            //addedElement.FindPropertyRelative("m_Mode").enumValueIndex = 1;
            recordAddedElement.FindPropertyRelative("m_CallState").enumValueIndex = 2;
            recordSerializedButton.ApplyModifiedProperties();

            var recordButtonTextGo = ObjectFactory.CreateGameObject("Text (Legacy)", typeof(RectTransform));
            CreateUtils.Place(recordButtonTextGo, recordButtonGo.transform);
            _ = recordButtonTextGo.AddComponent<CanvasRenderer>();
            var recordButtonText = recordButtonTextGo.AddComponent<Text>();
            recordButtonText.text = "Start Recording";
            recordButtonText.fontStyle = FontStyle.Bold;
            recordButtonText.fontSize = 36;
            recordButtonText.alignment = TextAnchor.MiddleCenter;
            recordButtonText.color = Color.black;
            defaultUICanavs.RecordButtonText = recordButtonText;
#endif

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create HoloKit Default UI Canvas");

            // Check for EventSystem
            EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                // Create new EventSystem
                var eventSystemGameObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                // Register it for undo
                Undo.RegisterCreatedObjectUndo(eventSystemGameObject, "Create EventSystem");
            }

            return canvas;
        }
    }
}
