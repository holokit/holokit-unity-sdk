// SPDX-FileCopyrightText: Copyright 2023 Holo Interactive <dev@holoi.com>
// SPDX-FileContributor: Yuchen Zhang <yuchen@holoi.com>
// SPDX-License-Identifier: MIT

namespace HoloInteractive.XR.HoloKit
{
    public interface IGazeRaycastInteractable
    {
        public void OnSelectionEntered();

        public void OnSelectionExited();

        public void OnSelected(float deltaTime);
    }
}
