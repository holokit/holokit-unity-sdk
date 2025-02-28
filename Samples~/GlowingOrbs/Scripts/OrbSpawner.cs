// SPDX-FileCopyrightText: Copyright 2023-2025 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Sizheng Hao <sizheng@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;
using HoloKit.iOS;

namespace HoloKit.Samples.GlowingOrbs
{
    public class OrbSpawner : MonoBehaviour
    {
        [SerializeField] private HandGestureRecognitionManager m_HandGestureRecognitionManager;

        [SerializeField] private Transform m_SpawnHandJoint;

        [SerializeField] private GameObject m_OrbPrefab;

        [SerializeField] private float m_DistOffset = 0.3f;

        [SerializeField] private float m_InitialForce = 5f;

        [SerializeField] private float m_Lifetime = 6f;

        [SerializeField] private float m_Cooldown = 1f;

        private Transform m_CenterEyePose;

        private float m_LastSpawnTime;

        private void Start()
        {
            m_CenterEyePose = FindObjectOfType<HoloKitCameraManager>().transform;

            // Register the callback
            m_HandGestureRecognitionManager.OnHandGestureChanged += OnHandGestureChanged;
        }

        private void OnHandGestureChanged(HandGesture handGesture)
        {
            if (handGesture == HandGesture.Five)
            {
                if (Time.time - m_LastSpawnTime < m_Cooldown)
                    return;

                m_LastSpawnTime = Time.time;
                // Instantiate orb
                var direction = (m_SpawnHandJoint.position - m_CenterEyePose.position).normalized;
                GameObject orb = Instantiate(m_OrbPrefab, m_SpawnHandJoint.position + m_DistOffset * direction, Quaternion.identity);

                // Add initial velocity
                orb.GetComponent<Rigidbody>().AddForce(m_InitialForce * direction);
                Destroy(orb, m_Lifetime);
            }
        }
    }
}
#endif
