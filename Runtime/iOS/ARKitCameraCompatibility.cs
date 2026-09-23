// SPDX-License-Identifier: MIT
#if UNITY_IOS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace HoloKit.iOS
{
    // iOS 27 + SDK 27: use ARKit's absolute interface pose, display transform
    // and projection together. Device tests found an incorrect background matrix,
    // missing pose roll and, in portrait, a projection scaled by the image aspect
    // ratio in ARKit XR 6.4.3. No accumulated rotations.
    [DisallowMultipleComponent]
    public sealed class ARKitCameraCompatibility : MonoBehaviour
    {
        readonly float[] view = new float[16], projection = new float[16], display = new float[8];
        readonly List<XRSessionSubsystem> sessions = new();
        ARCameraBackground background;
        Camera arCamera;
        Material correctedBackground;

        void Awake()
        {
            background = GetComponent<ARCameraBackground>();
            arCamera = GetComponent<Camera>();
        }
        void OnDestroy()
        {
            if (background != null && background.customMaterial == correctedBackground)
            {
                background.useCustomMaterial = false;
                background.customMaterial = null;
            }
            if (correctedBackground != null) Destroy(correctedBackground);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
#if !UNITY_EDITOR
            if (!UnityEngine.iOS.Device.systemVersion.StartsWith("27.")) return;
            Attach();
            // Every later scene brings its own AR camera; attach to it as well.
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, _) => Attach();
#endif
        }

        static void Attach()
        {
            foreach (var camera in FindObjectsByType<ARCameraManager>(FindObjectsSortMode.None))
                if (camera.GetComponent<ARKitCameraCompatibility>() == null)
                    camera.gameObject.AddComponent<ARKitCameraCompatibility>();
        }

        void OnEnable() => Application.onBeforeRender += ApplyPose;
        void OnDisable() => Application.onBeforeRender -= ApplyPose;

        [BeforeRenderOrder(100)] // after TrackedPoseDriver's pose write
        void ApplyPose()
        {
#if !UNITY_EDITOR
            if (sessions.Count == 0) SubsystemManager.GetSubsystems(sessions);
            if (sessions.Count == 0 || !sessions[0].running) return;
            if (HoloKit_GetARCameraReference(sessions[0].nativePtr, (int)Screen.orientation,
                Screen.width, Screen.height, view, projection, display) == 0) return;
            var nativeView = new Matrix4x4();
            for (int i=0; i<16; i++) nativeView[i] = view[i];
            var flip = Matrix4x4.Scale(new Vector3(1,1,-1));
            transform.localRotation = (flip * nativeView.inverse * flip).rotation;
            if (arCamera != null) arCamera.projectionMatrix = ProjectionMatrix(projection, arCamera.nearClipPlane, arCamera.farClipPlane);
            if (background != null && background.material != null)
            {
                if (correctedBackground == null)
                {
                    // Reuse Unity's shader, texture bindings and depth keywords.
                    correctedBackground = new Material(background.material);
                    background.customMaterial = correctedBackground;
                    background.useCustomMaterial = true;
                }
                correctedBackground.SetMatrix("_UnityDisplayTransform", DisplayMatrix(display));
            }
#endif
        }

        // ARKit's projection for this orientation and viewport, with the camera's own clip planes.
        public static Matrix4x4 ProjectionMatrix(float[] p, float near, float far)
        {
            var result = new Matrix4x4();
            for (int i = 0; i < 16; i++) result[i] = p[i];
            result.m22 = -(far + near) / (far - near);
            result.m23 = -2 * far * near / (far - near);
            return result;
        }

        // ARKit maps image -> UIKit view. Invert, flip viewport V, and
        // transpose for the ARKit shader's row-vector multiplication.
        public static Matrix4x4 DisplayMatrix(float[] d)
        {
            var imageToView = Matrix4x4.identity;
            imageToView.m00 = d[0]; imageToView.m10 = d[1];
            imageToView.m01 = d[2]; imageToView.m11 = d[3];
            imageToView.m03 = d[4]; imageToView.m13 = d[5];
            var inverse = imageToView.inverse;
            var result = Matrix4x4.identity;
            result.m00 = inverse.m00; result.m10 = -inverse.m01;
            result.m20 = inverse.m01 + inverse.m03;
            result.m01 = inverse.m10; result.m11 = -inverse.m11;
            result.m21 = inverse.m11 + inverse.m13;
            return result;
        }

        [DllImport("__Internal")]
        static extern int HoloKit_GetARCameraReference(IntPtr session, int orientation, int width, int height,
            [Out] float[] view, [Out] float[] projection, [Out] float[] display);
    }
}
#endif
