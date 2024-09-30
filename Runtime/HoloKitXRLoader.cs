using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine;
using System;

namespace HoloKit
{
    public class HoloKitXRLoader
    {
        private static HoloKitXRLoader loader;
        
        public HoloKitHandsSubsystem HandsSubsystem { get; private set; }

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
            HandsSubsystem?.Start();
        }

        /// <inheritdoc />
        public void Stop()
        {
            // TODO: Handle the XRDevice
            HandsSubsystem?.Stop();
        }
        public void OnDestroy()
        {
            HandsSubsystem?.Destroy();
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

            if (loader.settings.EnableXRHandSubsystem)
            {
                SubsystemManager.GetSubsystemDescriptors<XRHandSubsystemDescriptor>(xrHandsSubsystemDescriptors);

                if (xrHandsSubsystemDescriptors.Count > 0)
                {
                    foreach (var descriptor in xrHandsSubsystemDescriptors)
                    {
                        if (String.Compare(descriptor.id, HoloKitXRConstants.handSubsystemId, true) == 0)
                        {
                            loader.handsSubsystem = descriptor.Create() as HoloKitHandsSubsystem;
                            break;
                        }
                    }
                }
                if (loader.HandSubsystem == null)
                {
                    Debug.LogError($"{typeof(ViconHandSubsystem).Name} failed to configure!");
                }
                else
                {
                    loader.HandSubsystem?.Start();
                    Debug.Log($"{typeof(ViconHandSubsystem).Name} configured!");
                }
            }
        }
    }
}