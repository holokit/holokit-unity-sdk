// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using System;
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
}
