using UnityEngine;
using System.Collections.Generic;
#if XR_HANDS_1_5_OR_NEWER
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.ProviderImplementation;
using Unity.Collections;

namespace HoloKit
{
    public class HoloKitHandSubsystem : XRHandSubsystem
    { 
        public static string HandsSubsystemId = "HoloKit XR Hands";

        private HoloKitHandProvider HandProvider => provider as HoloKitHandProvider;

       [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor()
        {
            XRHandSubsystemDescriptor.Register(new XRHandSubsystemDescriptor.Cinfo
            {
                id = HandsSubsystemId,
                providerType = typeof(HoloKitHandProvider),
                subsystemTypeOverride = typeof(HoloKitHandSubsystem),
            });
        }

        public void SetHandPoses(Handedness handedness, Dictionary<XRHandJointID, Pose> poses)
        {
            HandProvider.SetHandPoses(handedness, poses);
        }

        public new void Start()
        {
            base.Start();
            HandProvider.Setup(this);
        }
    }
    
    public class HoloKitHandProvider : XRHandSubsystemProvider
    {
        private Dictionary<Handedness, Dictionary<XRHandJointID, Pose>> handsPoses = new Dictionary<Handedness, Dictionary<XRHandJointID, Pose>>();
        private Dictionary<Handedness, bool> recomputeHandsPoses = new Dictionary<Handedness, bool>();
        private XRHandProviderUtility.SubsystemUpdater subsystemUpdater;
        private Dictionary<XRHandJointID, bool> handLayout = new Dictionary<XRHandJointID, bool>()
        {
            {XRHandJointID.Palm, false},
            {XRHandJointID.Wrist, true},

            {XRHandJointID.ThumbMetacarpal, true},
            {XRHandJointID.ThumbProximal, true},
            {XRHandJointID.ThumbDistal, true},
            {XRHandJointID.ThumbTip, true},

            {XRHandJointID.IndexMetacarpal, true},
            {XRHandJointID.IndexProximal, true},
            {XRHandJointID.IndexIntermediate, true},
            {XRHandJointID.IndexDistal, true},
            {XRHandJointID.IndexTip, true},

            {XRHandJointID.MiddleMetacarpal, true},
            {XRHandJointID.MiddleProximal, true},
            {XRHandJointID.MiddleIntermediate, true},
            {XRHandJointID.MiddleDistal, true},
            {XRHandJointID.MiddleTip, true},

            {XRHandJointID.RingMetacarpal, true},
            {XRHandJointID.RingProximal, true},
            {XRHandJointID.RingIntermediate, true},
            {XRHandJointID.RingDistal, true},
            {XRHandJointID.RingTip, true},

            {XRHandJointID.LittleMetacarpal, true},
            {XRHandJointID.LittleProximal, true},
            {XRHandJointID.LittleIntermediate, true},
            {XRHandJointID.LittleDistal, true},
            {XRHandJointID.LittleTip, true},
        };

        public void Setup(XRHandSubsystem subsystem)
        {
            if (subsystemUpdater == null)
            {
                subsystemUpdater = new XRHandProviderUtility.SubsystemUpdater(subsystem);
                subsystemUpdater?.Start();
            }
        }

        /// <inheritdoc />
        public override void Start()
        {
            handsPoses.Clear();
        }

        /// <inheritdoc />
        public override void Stop()
        {
            subsystemUpdater?.Stop();
            handsPoses.Clear();
        }

        /// <inheritdoc />
        public override void Destroy()
        {
            subsystemUpdater?.Destroy();
            subsystemUpdater = null;
        }

        public override void GetHandLayout(NativeArray<bool> handJointsInLayout)
        {
                        handJointsInLayout[XRHandJointID.Palm.ToIndex()] = handLayout[XRHandJointID.Palm];
            handJointsInLayout[XRHandJointID.Wrist.ToIndex()] = handLayout[XRHandJointID.Wrist];

            handJointsInLayout[XRHandJointID.ThumbMetacarpal.ToIndex()] = handLayout[XRHandJointID.ThumbMetacarpal];
            handJointsInLayout[XRHandJointID.ThumbProximal.ToIndex()] = handLayout[XRHandJointID.ThumbProximal];
            handJointsInLayout[XRHandJointID.ThumbDistal.ToIndex()] = handLayout[XRHandJointID.ThumbDistal];
            handJointsInLayout[XRHandJointID.ThumbTip.ToIndex()] = handLayout[XRHandJointID.ThumbTip];

            handJointsInLayout[XRHandJointID.IndexMetacarpal.ToIndex()] = handLayout[XRHandJointID.IndexMetacarpal];
            handJointsInLayout[XRHandJointID.IndexProximal.ToIndex()] = handLayout[XRHandJointID.IndexProximal];
            handJointsInLayout[XRHandJointID.IndexIntermediate.ToIndex()] = handLayout[XRHandJointID.IndexIntermediate];
            handJointsInLayout[XRHandJointID.IndexDistal.ToIndex()] = handLayout[XRHandJointID.IndexDistal];
            handJointsInLayout[XRHandJointID.IndexTip.ToIndex()] = handLayout[XRHandJointID.IndexTip];

            handJointsInLayout[XRHandJointID.MiddleMetacarpal.ToIndex()] = handLayout[XRHandJointID.MiddleMetacarpal];
            handJointsInLayout[XRHandJointID.MiddleProximal.ToIndex()] = handLayout[XRHandJointID.MiddleProximal];
            handJointsInLayout[XRHandJointID.MiddleIntermediate.ToIndex()] = handLayout[XRHandJointID.MiddleIntermediate];
            handJointsInLayout[XRHandJointID.MiddleDistal.ToIndex()] = handLayout[XRHandJointID.MiddleDistal];
            handJointsInLayout[XRHandJointID.MiddleTip.ToIndex()] = handLayout[XRHandJointID.MiddleTip];

            handJointsInLayout[XRHandJointID.RingMetacarpal.ToIndex()] = handLayout[XRHandJointID.RingMetacarpal];
            handJointsInLayout[XRHandJointID.RingProximal.ToIndex()] = handLayout[XRHandJointID.RingProximal];
            handJointsInLayout[XRHandJointID.RingIntermediate.ToIndex()] = handLayout[XRHandJointID.RingIntermediate];
            handJointsInLayout[XRHandJointID.RingDistal.ToIndex()] = handLayout[XRHandJointID.RingDistal];
            handJointsInLayout[XRHandJointID.RingTip.ToIndex()] = handLayout[XRHandJointID.RingTip];

            handJointsInLayout[XRHandJointID.LittleMetacarpal.ToIndex()] = handLayout[XRHandJointID.LittleMetacarpal];
            handJointsInLayout[XRHandJointID.LittleProximal.ToIndex()] = handLayout[XRHandJointID.LittleProximal];
            handJointsInLayout[XRHandJointID.LittleIntermediate.ToIndex()] = handLayout[XRHandJointID.LittleIntermediate];
            handJointsInLayout[XRHandJointID.LittleDistal.ToIndex()] = handLayout[XRHandJointID.LittleDistal];
            handJointsInLayout[XRHandJointID.LittleTip.ToIndex()] = handLayout[XRHandJointID.LittleTip];
        }

        public override XRHandSubsystem.UpdateSuccessFlags TryUpdateHands(XRHandSubsystem.UpdateType updateType,
            ref Pose leftHandRootPose, NativeArray<XRHandJoint> leftHandJoints,
            ref Pose rightHandRootPose, NativeArray<XRHandJoint> rightHandJoints)
        {
            var successFlags = XRHandSubsystem.UpdateSuccessFlags.None;
            if (UpdateJointData(Handedness.Left, leftHandJoints, ref leftHandRootPose))
            {
                successFlags |= XRHandSubsystem.UpdateSuccessFlags.LeftHandRootPose | XRHandSubsystem.UpdateSuccessFlags.LeftHandJoints;
            }

            if (UpdateJointData(Handedness.Right, rightHandJoints, ref rightHandRootPose))
            {
                successFlags |= XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose | XRHandSubsystem.UpdateSuccessFlags.RightHandJoints;
            }

            return successFlags;
        }

        /// <summary>
        /// Update the poses of the joints.
        /// </summary>
        internal void SetHandPoses(Handedness handedness, Dictionary<XRHandJointID, Pose> poses)
        {
            this.handsPoses[handedness] = poses;
            recomputeHandsPoses[handedness] = true;
        }

        /// <summary>
        /// Populate the handJoints array.
        /// </summary>
        protected bool UpdateJointData(Handedness handedness, NativeArray<XRHandJoint> handJoints, ref Pose handRootPose)
        {
            if (!handsPoses.ContainsKey(handedness))
            {
                return false;
            }

            var handPoseCache = handsPoses[handedness];
            bool recompute = recomputeHandsPoses[handedness];

            for (int jointIndex = XRHandJointID.BeginMarker.ToIndex(); jointIndex < XRHandJointID.EndMarker.ToIndex(); ++jointIndex)
            {
                XRHandJointID jointID = XRHandJointIDUtility.FromIndex(jointIndex);

                if (!handLayout[jointID])
                {
                    continue;
                }

                if (handPoseCache.TryGetValue(jointID, out Pose pose))
                {
                    if (recompute)
                    {
                        pose.rotation = Quaternion.LookRotation(pose.up, -pose.forward);  // Accounting for the different coordinate system used.
                        handPoseCache[jointID] = pose;
                    }

                    handJoints[jointIndex] = XRHandProviderUtility.CreateJoint(handedness, XRHandJointTrackingState.Pose, jointID, pose);
                }
            }

            recomputeHandsPoses[handedness] = false;
            handPoseCache.TryGetValue(XRHandJointID.Wrist, out handRootPose);
            return true;
        }
    }
}
#endif
