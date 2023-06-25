// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace HoloInteractive.XR.HoloKit.iOS
{
    public class EnvironmentDepthImage : IDisposable
    {
        public int Width;
        public int Height;
        public NativeArray<float> Pixels;

        public unsafe EnvironmentDepthImage(AROcclusionManager occlusionManager)
        {
            if (occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage cpuImage))
            {
                var conversionParams = new XRCpuImage.ConversionParams
                {
                    inputRect = new RectInt(0, 0, cpuImage.width, cpuImage.height),
                    outputDimensions = new Vector2Int(cpuImage.width, cpuImage.height),
                    outputFormat = TextureFormat.RFloat,
                    transformation = XRCpuImage.Transformation.None
                };
                Width = conversionParams.outputDimensions.x;
                Height = conversionParams.outputDimensions.y;
                Pixels = new NativeArray<float>(cpuImage.GetConvertedDataSize(conversionParams), Allocator.Temp);
                cpuImage.Convert(conversionParams, new IntPtr(Pixels.GetUnsafePtr()), Pixels.Length);
                cpuImage.Dispose();
            }
        }

        public float GetDepth(Vector2 location)
        {
            int x = Mathf.FloorToInt(location.x * Width);
            int y = Mathf.FloorToInt(location.y * Height);
            return Pixels[y * Width + x];
        }

        public void Dispose()
        {
            if (Pixels.IsCreated)
                Pixels.Dispose();
        }
    }

    public class EnvironmentDepthManager : IDisposable
    {
        IntPtr m_Ptr;

        public EnvironmentDepthManager()
        {
            List<XRSessionSubsystem> xrSessionSubsystems = new();
            SubsystemManager.GetSubsystems(xrSessionSubsystems);
            if (xrSessionSubsystems.Count == 0)
            {
                Debug.LogWarning("Cannot find XRSessionSubsystem");
                return;
            }
            XRSessionSubsystem xrSessionSubsystem = xrSessionSubsystems[0];
            m_Ptr = InitWithARSession(xrSessionSubsystem.nativePtr);
        }

        public float GetDepth(Vector2 location)
        {
            return GetDepth(m_Ptr, location.x, location.y);
        }

        public Vector3 UnprojectScreenPoint(Vector2 location, float depth)
        {
            UnprojectScreenPoint(m_Ptr, location.x, location.y, depth, out float x, out float y, out float z);
            return new Vector3(x, y, z);
        }

        public void Dispose()
        {
            if (m_Ptr != IntPtr.Zero)
            {
                NativeApi.CFRelease(ref m_Ptr);
                m_Ptr = IntPtr.Zero;
            }
        }

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_EnvironmentDepthManager_initWithARSession")]
        static extern IntPtr InitWithARSession(IntPtr arSessionPtr);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_EnvironmentDepthManager_getDepth")]
        static extern float GetDepth(IntPtr self, float x, float y);

        [DllImport("__Internal", EntryPoint = "HoloInteractiveHoloKit_EnvironmentDepthManager_unprojectScreenPoint")]
        static extern void UnprojectScreenPoint(IntPtr self, float locationX, float locationY, float depth, out float x, out float y, out float z);
    }
}
