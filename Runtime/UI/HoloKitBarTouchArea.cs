// SPDX-FileCopyrightText: Copyright 2026 Reality Design Lab <dev@reality.design>
// SPDX-FileContributor: Botao Amber Hu <amber@reality.design>
// SPDX-License-Identifier: MIT

using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HoloKit.UI
{
    /// <summary>
    /// One stereo bar touch area: reports hold, tap and horizontal drag of the first finger on it.
    /// Works with either input backend through the EventSystem.
    /// </summary>
    internal sealed class HoloKitBarTouchArea : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        const int NoPointer = int.MinValue;

        /// A finger went down (true) or up (false) on this area.
        public Action<bool> Held;
        /// A finger lifted on this area without dragging.
        public Action Tapped;
        /// A drag started (true) or ended (false).
        public Action<bool> Dragging;
        /// Horizontal drag distance in pixels since the finger went down (negative is left).
        public Action<float> Dragged;

        int m_Pointer = NoPointer;
        int m_LastPointer = NoPointer;
        int m_DragPointer = NoPointer;
        bool m_Dragging;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (m_Pointer != NoPointer)
                return;
            m_Pointer = m_LastPointer = eventData.pointerId;
            Held?.Invoke(true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != m_Pointer)
                return;
            m_Pointer = NoPointer;
            Held?.Invoke(false);
        }

        // uGUI still clicks after a drag on the same object: a slide is not a tap, and neither
        // is a second finger.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!eventData.dragging && !m_Dragging && eventData.pointerId == m_LastPointer)
                Tapped?.Invoke();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != m_Pointer)
                return;
            m_Dragging = true;
            m_DragPointer = eventData.pointerId;
            Dragging?.Invoke(true);
            Dragged?.Invoke(eventData.position.x - eventData.pressPosition.x);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_Dragging && eventData.pointerId == m_DragPointer)
                Dragged?.Invoke(eventData.position.x - eventData.pressPosition.x);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!m_Dragging || eventData.pointerId != m_DragPointer)
                return;
            m_Dragging = false;
            m_DragPointer = NoPointer;
            Dragging?.Invoke(false);
        }

        // The EventSystem sends nothing to an inactive area, so a panel that replaces the bar
        // mid-drag ends that drag and hold here.
        void OnDisable()
        {
            if (m_Dragging)
            {
                m_Dragging = false;
                m_DragPointer = NoPointer;
                Dragging?.Invoke(false);
            }
            if (m_Pointer != NoPointer)
            {
                m_Pointer = NoPointer;
                Held?.Invoke(false);
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                OnDisable();
        }
    }
}
