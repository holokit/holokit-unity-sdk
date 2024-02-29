// SPDX-FileCopyrightText: Copyright 2023 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Yuchen Zhang <yuchenz27@outlook.com>
// SPDX-License-Identifier: MIT

namespace HoloKit
{
    public interface IGazeRaycastInteractable
    {
        public void OnSelectionEntered();

        public void OnSelectionExited();

        public void OnSelected(float deltaTime);
    }
}
