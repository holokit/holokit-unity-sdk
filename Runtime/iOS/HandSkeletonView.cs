// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using System.Collections.Generic;
using UnityEngine;

namespace HoloKit.iOS
{
    /// <summary>
    /// Draws the first tracked hand as the HoloKit Swift SDK's Hands example does: yellow joint
    /// spheres (8 mm radius) and cyan bones (6 mm thick), unlit. It draws over the camera image and
    /// the scene, so AR occlusion (the real hand's own LiDAR depth) never hides it.
    /// <see cref="HandTrackingManager"/> adds it when Hand Joints Visibility is on.
    /// </summary>
    [RequireComponent(typeof(HandTrackingManager))]
    public class HandSkeletonView : MonoBehaviour
    {
        public const float JointRadius = 0.008f;
        public const float BoneThickness = 0.006f;
        const string ShaderResource = "HoloKit/HandSkeleton";

        HandTrackingManager m_Hands;
        Transform m_Root;
        readonly List<Transform> m_Joints = new();
        readonly List<Transform> m_Bones = new();
        readonly List<Vector3> m_JointPositions = new();
        readonly List<(Vector3 Start, Vector3 End)> m_BonePositions = new();
        Material m_JointMaterial, m_BoneMaterial;

        /// <summary>Joints and bones drawn this frame (0 when no hand is tracked).</summary>
        public int VisibleJointCount => m_JointPositions.Count;
        public int VisibleBoneCount => m_BonePositions.Count;

        void Awake()
        {
            m_Hands = GetComponent<HandTrackingManager>();
            var shader = Resources.Load<Shader>(ShaderResource);
            if (shader == null)
            {
                Debug.LogWarning($"[HandSkeletonView] Shader Resources/{ShaderResource} is missing; occlusion may hide the hand.");
                shader = Shader.Find("Unlit/Color");
            }
            // Joints draw after bones, so they sit on top where the two overlap.
            m_BoneMaterial = new Material(shader) { color = Color.cyan, renderQueue = 4000 };
            m_JointMaterial = new Material(shader) { color = Color.yellow, renderQueue = 4001 };

            m_Root = new GameObject("Hand Skeleton").transform;
            m_Root.SetParent(transform, false);
            for (int i = 0; i < HandSkeleton.MaxJoints; i++)
                m_Joints.Add(Part(PrimitiveType.Sphere, $"Joint {i}", m_JointMaterial, Vector3.one * (2 * JointRadius)));
            for (int i = 0; i < HandSkeleton.MaxBones; i++)
                m_Bones.Add(Part(PrimitiveType.Cube, $"Bone {i}", m_BoneMaterial, Vector3.one * BoneThickness));
        }

        Transform Part(PrimitiveType type, string name, Material material, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Destroy(part.GetComponent<Collider>());
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            part.transform.SetParent(m_Root, false);
            part.transform.localScale = scale;
            part.SetActive(false);
            return part.transform;
        }

        void OnDisable()
        {
            m_JointPositions.Clear();
            m_BonePositions.Clear();
            Apply();
        }

        void OnDestroy()
        {
            Destroy(m_JointMaterial);
            Destroy(m_BoneMaterial);
        }

        void LateUpdate()
        {
            var detector = m_Hands.HandPoseDetector;
            // HANDS OFF (a disabled manager) hides the skeleton even if a late Vision result arrives.
            if (m_Hands.isActiveAndEnabled && detector != null && detector.HandCount > 0)
                HandSkeleton.Build(m_Hands.HandPoses3D[0], detector.HandPosesConfidence[0], m_JointPositions, m_BonePositions);
            else
            {
                m_JointPositions.Clear();
                m_BonePositions.Clear();
            }
            Apply();
        }

        void Apply()
        {
            for (int i = 0; i < m_Joints.Count; i++)
            {
                bool visible = i < m_JointPositions.Count;
                m_Joints[i].gameObject.SetActive(visible);
                if (visible)
                    m_Joints[i].position = m_JointPositions[i];
            }
            for (int i = 0; i < m_Bones.Count; i++)
            {
                bool visible = i < m_BonePositions.Count;
                m_Bones[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                var (start, end) = m_BonePositions[i];
                var delta = end - start;
                float length = Mathf.Max(delta.magnitude, 1e-4f);
                m_Bones[i].SetPositionAndRotation((start + end) / 2, Quaternion.FromToRotation(Vector3.up, delta / length));
                m_Bones[i].localScale = new Vector3(BoneThickness, length, BoneThickness);
            }
        }
    }
}
#endif
