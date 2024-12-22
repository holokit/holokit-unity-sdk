using UnityEngine.XR;
#if XR_HANDS_1_5_OR_NEWER
using UnityEngine.XR.Hands;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace HoloKit
{
    public class HoloKitXRLoader: ScriptableObject
    {
        private static HoloKitXRLoader loader;
        static List<XRHandSubsystemDescriptor> xrHandsSubsystemDescriptors = new();

        public HoloKitHandSubsystem HandSubsystem { get; private set; }

        private void Awake()
        {
            loader = this;
        }

        private void OnEnable()
        {
            // Duplicate because of how Unity handles these calls!
            loader = this;
        }

        public void Start()
        {
            // TODO: Handle the XRDevice
            HandSubsystem?.Start();
        }

        /// <inheritdoc />
        public void Stop()
        {
            // TODO: Handle the XRDevice
            HandSubsystem?.Stop();
        }
        public void OnDestroy()
        {
            HandSubsystem?.Destroy();
            loader = null;
        }
        
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        internal static void Initialize()
        {
            if (loader == null)
            {
                Debug.LogError($"Loader is not set");
                return;
            }

            //if (loader.EnableXRHandSubsystem)
            {
                SubsystemManager.GetSubsystemDescriptors<XRHandSubsystemDescriptor>(xrHandsSubsystemDescriptors);

                if (xrHandsSubsystemDescriptors.Count > 0)
                {
                    foreach (var descriptor in xrHandsSubsystemDescriptors)
                    {
                        if (String.Compare(descriptor.id, HoloKitHandSubsystem.HandsSubsystemId, true) == 0)
                        {
                            loader.HandSubsystem = descriptor.Create() as HoloKitHandSubsystem;
                            break;
                        }
                    }
                }
                if (loader.HandSubsystem == null)
                {
                    Debug.LogError($"{typeof(HoloKitHandSubsystem).Name} failed to configure!");
                }
                else
                {
                    loader.HandSubsystem?.Start();
                    Debug.Log($"{typeof(HoloKitHandSubsystem).Name} configured!");
                }
            }
        }
    }
}
#endif