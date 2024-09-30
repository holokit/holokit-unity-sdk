using UnityEngine;
#if XR_HANDS_1_4_OR_NEWER
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;
using Unity.Collections;

namespace HoloKit
{
    public class HoloKitHandsSubsystem : XRHandSubsystem
    { 
        private HoloKitHandsProvider handsProvider => provider as HoloKitHandsProvider;

       [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor()
        {
            XRHandSubsystemDescriptor.Register(new XRHandSubsystemDescriptor.Cinfo
            {
                id = "HoloKit XR Hands",
                providerType = typeof(HoloKitHandsProvider),
                subsystemTypeOverride = typeof(HoloKitHandsSubsystem),
            });
        }
        
        public new void Start()
        {
            base.Start();
            handsProvider.Setup(this);
        }
    }
    
    public class HoloKitHandsProvider : XRHandSubsystemProvider
    {
        private XRHandProviderUtility.SubsystemUpdater subsystemUpdater;
        private iOS.HandTrackingManager handTrackingManager;

        public HoloKitHandsProvider()
        {
        }

        public override void Start()
        {
            //handTrackingManager = GetComponent<iOS.HandTrackingManager>();
        }
        
        public void Setup(XRHandSubsystem subsystem)
        {
            if (subsystemUpdater == null)
            {
                subsystemUpdater = new XRHandProviderUtility.SubsystemUpdater(subsystem);
                subsystemUpdater?.Start();
            }
        }

        public override void Stop()
        {
            subsystemUpdater?.Stop();
        }

        public override void Destroy()
        {
            subsystemUpdater?.Destroy();
            subsystemUpdater = null;
        }

        public override void GetHandLayout(NativeArray<bool> handJointsInLayout)
        {
            handJointsInLayout[XRHandJointID.Palm.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.Wrist.ToIndex()] = true;

            handJointsInLayout[XRHandJointID.ThumbMetacarpal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.ThumbProximal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.ThumbDistal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.ThumbTip.ToIndex()] = true;

            handJointsInLayout[XRHandJointID.IndexMetacarpal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.IndexProximal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.IndexIntermediate.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.IndexDistal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.IndexTip.ToIndex()] = true;

            handJointsInLayout[XRHandJointID.MiddleMetacarpal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.MiddleProximal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.MiddleIntermediate.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.MiddleDistal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.MiddleTip.ToIndex()] = true;

            handJointsInLayout[XRHandJointID.RingMetacarpal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.RingProximal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.RingIntermediate.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.RingDistal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.RingTip.ToIndex()] = true;

            handJointsInLayout[XRHandJointID.LittleMetacarpal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.LittleProximal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.LittleIntermediate.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.LittleDistal.ToIndex()] = true;
            handJointsInLayout[XRHandJointID.LittleTip.ToIndex()] = true;
        }

        public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(XRHandSubsystem.UpdateType updateType,
            ref Pose leftHandRootPose, NativeArray<XRHandJoint> leftHandJoints,
            ref Pose rightHandRootPose, NativeArray<XRHandJoint> rightHandJoints)
        {
            
            // if (!updateHandsAllowed)
            //     return XRHandSubsystem.UpdateSuccessFlags.None;
            //
            // UpdateData(Handedness.Left, leftHandData, leftHandJoints, ref leftHandRootPose);
            //
            // UpdateData(Handedness.Right, rightHandData, rightHandJoints, ref rightHandRootPose);
            //
             var successFlags = XRHandSubsystem.UpdateSuccessFlags.None;
            // if (leftHandData.enabled)
                 successFlags |= XRHandSubsystem.UpdateSuccessFlags.LeftHandRootPose | XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints;
            //
            // if (rightHandData.enabled)
                 successFlags |= XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose | XRHandSubsystem.UpdateSuccessFlags.RightHandJoints;
            return successFlags;
        }
    }
}
#endif
