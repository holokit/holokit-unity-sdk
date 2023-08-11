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

namespace HoloInteractive.XR.HoloKit.Editor
{
    static class HoloKitCreateUtil
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

            var buttonGo = ObjectFactory.CreateGameObject("Switch Render Mode Button", typeof(RectTransform));
            CreateUtils.Place(buttonGo, canvasGo.transform);
            var buttonRectTransform = buttonGo.GetComponent<RectTransform>();
            buttonRectTransform.anchorMin = new(1f, 1f);
            buttonRectTransform.anchorMax = new(1f, 1f);
            buttonRectTransform.anchoredPosition = new(-100f, -40f);
            buttonRectTransform.sizeDelta = new(200f, 100f);
            buttonRectTransform.pivot = new(1f, 1f);
            _ = buttonGo.AddComponent<CanvasRenderer>();
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            buttonImage.type = Image.Type.Sliced;
            var button = buttonGo.AddComponent<Button>();
            //button.onClick.AddListener(defaultUICanavs.SwitchRenderMode);
            SerializedObject serializedButton = new SerializedObject(button);
            SerializedProperty onClickProp = serializedButton.FindProperty("m_OnClick");
            int numListeners = onClickProp.FindPropertyRelative("m_PersistentCalls.m_Calls").arraySize;
            onClickProp.FindPropertyRelative("m_PersistentCalls.m_Calls").InsertArrayElementAtIndex(numListeners);
            SerializedProperty addedElement = onClickProp.FindPropertyRelative("m_PersistentCalls.m_Calls").GetArrayElementAtIndex(numListeners);
            addedElement.FindPropertyRelative("m_Target").objectReferenceValue = defaultUICanavs;
            addedElement.FindPropertyRelative("m_MethodName").stringValue = "SwitchRenderMode";
            //addedElement.FindPropertyRelative("m_Mode").enumValueIndex = 1;
            addedElement.FindPropertyRelative("m_CallState").enumValueIndex = 2;
            serializedButton.ApplyModifiedProperties();

            var buttonTextGo = ObjectFactory.CreateGameObject("Text (Legacy)", typeof(RectTransform));
            CreateUtils.Place(buttonTextGo, buttonGo.transform);
            _ = buttonTextGo.AddComponent<CanvasRenderer>();
            var buttonText = buttonTextGo.AddComponent<Text>();
            buttonText.text = "Stereo";
            buttonText.fontStyle = FontStyle.Bold;
            buttonText.fontSize = 36;
            buttonText.alignment = TextAnchor.MiddleCenter;
            buttonText.color = Color.black;
            defaultUICanavs.ButtonText = buttonText;

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create HoloKit Default UI Canvas");
            return canvas;
        }
    }
}
