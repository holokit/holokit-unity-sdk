// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

#if UNITY_IOS
using UnityEngine;

namespace HoloInteractive.XR.HoloKit.iOS
{
    [RequireComponent(typeof(GazeRaycastInteractor))]
    public class GazeGestureInteractor : MonoBehaviour
    {
        private HandGestureRecognitionManager m_HandGestureRecognitionManager;

        private GazeRaycastInteractor m_GazeRaycastInteractor;

        private void Start()
        {
            m_HandGestureRecognitionManager = FindObjectOfType<HandGestureRecognitionManager>();
            if (m_HandGestureRecognitionManager == null)
            {
                Debug.LogWarning("[GazeGestureInteractor] Failed to find HandGestureRecognitionManager");
                return;
            }
            m_HandGestureRecognitionManager.OnHandGestureChanged += OnHandGestureChanged;

            m_GazeRaycastInteractor = GetComponent<GazeRaycastInteractor>();
        }

        private void OnHandGestureChanged(HandGesture handGesture)
        {
            if (handGesture == HandGesture.Pinched && m_GazeRaycastInteractor.Target != null)
            {
                if (m_GazeRaycastInteractor.Target is IGazeGestureInteractable)
                {
                    IGazeGestureInteractable interactable = (IGazeGestureInteractable)m_GazeRaycastInteractor.Target;
                    interactable.OnGestureSelected();
                }
            }
        }
    }
}
#endif
