// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

using UnityEngine;

namespace HoloInteractive.XR.HoloKit
{
    public class GazeRaycaster : MonoBehaviour
    {
        private Transform centerEyePose;

        private IGazeRaycastInteractable target;

        private void Start()
        {
            var holokitCameraManager = FindObjectOfType<HoloKitCameraManager>();
            centerEyePose = holokitCameraManager.CenterEyePose;
        }

        private void Update()
        {
            Ray ray = new Ray(centerEyePose.position, centerEyePose.forward);
            if (Physics.Raycast(ray, out RaycastHit hitInfo))
            {
                if (hitInfo.transform.TryGetComponent<IGazeRaycastInteractable>(out var interactable))
                {
                    // A new target is selected
                    if (target == null)
                    {
                        target = interactable;
                        target.OnSelectionEntered();
                        target.OnSelected(Time.deltaTime);
                    }
                    // Still selecting the old target
                    else if (target == interactable)
                    {
                        target.OnSelected(Time.deltaTime);
                    }
                    // Target switched in the last frame
                    else if (target != interactable)
                    {
                        target.OnSelectionExited();
                        target = interactable;
                        target.OnSelectionEntered();
                        target.OnSelected(Time.deltaTime);
                    }
                }
            }
            else
            {
                if (target != null)
                {
                    target.OnSelectionExited();
                    target = null;
                }
            }
        }
    }
}
