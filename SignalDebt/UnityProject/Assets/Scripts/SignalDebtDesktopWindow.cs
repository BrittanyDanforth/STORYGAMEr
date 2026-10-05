using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SignalDebt
{
    /// <summary>Window chrome only. It never changes camera, cursor, or campaign state.</summary>
    public sealed class SignalDebtDesktopWindow : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerClickHandler
    {
        private RectTransform window, desktop;
        private Func<bool> canInteract;
        private Vector2 pointerStart, positionStart, previousDesktopSize;
        private Vector2 normalAnchorMin, normalAnchorMax, normalSize, normalOffset;
        private readonly Vector3[] corners = new Vector3[4];
        private bool maximized, dragging;

        public bool IsMaximized { get { return maximized; } }

        [Serializable]
        public sealed class LayoutState
        {
            public float offsetX;
            public float offsetY;
            public bool maximized;
        }

        public void Initialize(RectTransform application, RectTransform workspace, Func<bool> inputAvailable)
        {
            window = application;
            desktop = workspace;
            canInteract = inputAvailable;
            normalAnchorMin = window.anchorMin;
            normalAnchorMax = window.anchorMax;
            normalSize = window.sizeDelta;
            RememberNormalOffset();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = false;
            if (!CanInteract() || maximized || eventData.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(desktop, eventData.position, eventData.pressEventCamera, out pointerStart)) return;
            positionStart = window.anchoredPosition;
            dragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging || !CanInteract() || maximized) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(desktop, eventData.position, eventData.pressEventCamera, out Vector2 point)) return;
            window.anchoredPosition = positionStart + point - pointerStart;
            ClampToDesktop();
            RememberNormalOffset();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && eventData.clickCount == 2)
                ToggleMaximize();
        }

        public void ToggleMaximize()
        {
            if (!CanInteract()) return;
            dragging = false;
            if (!maximized)
            {
                RememberNormalOffset();
            }
            maximized = !maximized;
            ApplyLayout();
        }

        public LayoutState CaptureLayout()
        {
            if (!maximized) RememberNormalOffset();
            return new LayoutState { offsetX = normalOffset.x, offsetY = normalOffset.y, maximized = maximized };
        }

        public bool RestoreLayout(LayoutState state)
        {
            if (state == null || !IsFinite(state.offsetX) || !IsFinite(state.offsetY)) return false;
            normalOffset = new Vector2(Mathf.Clamp(state.offsetX, -1f, 1f), Mathf.Clamp(state.offsetY, -1f, 1f));
            maximized = state.maximized;
            dragging = false;
            ApplyLayout();
            return true;
        }

        private void ApplyLayout()
        {
            if (window == null || desktop == null) return;
            if (maximized)
            {
                window.anchorMin = Vector2.zero; window.anchorMax = Vector2.one;
                // Leave both the OS system bar and taskbar available.
                window.offsetMin = new Vector2(12f, 82f);
                window.offsetMax = new Vector2(-12f, -58f);
            }
            else
            {
                window.anchorMin = normalAnchorMin; window.anchorMax = normalAnchorMax;
                window.sizeDelta = normalSize;
                window.anchoredPosition = new Vector2(normalOffset.x * desktop.rect.width, normalOffset.y * desktop.rect.height);
            }
            ClampToDesktop();
            if (!maximized) RememberNormalOffset();
        }

        private void RememberNormalOffset()
        {
            if (window == null || desktop == null || desktop.rect.width <= 0f || desktop.rect.height <= 0f) return;
            normalOffset = new Vector2(window.anchoredPosition.x / desktop.rect.width, window.anchoredPosition.y / desktop.rect.height);
        }

        private static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

        public void ClampToDesktop()
        {
            if (window == null || desktop == null) return;
            window.GetWorldCorners(corners);
            Vector3 lower = desktop.InverseTransformPoint(corners[0]);
            Vector3 upper = desktop.InverseTransformPoint(corners[2]);
            Rect bounds = desktop.rect;
            float left = bounds.xMin + 12f, right = bounds.xMax - 12f;
            float bottom = bounds.yMin + 82f, top = bounds.yMax - 58f;
            Vector2 delta = Vector2.zero;
            if (lower.x < left) delta.x = left - lower.x;
            else if (upper.x > right) delta.x = right - upper.x;
            if (lower.y < bottom) delta.y = bottom - lower.y;
            else if (upper.y > top) delta.y = top - upper.y;
            window.anchoredPosition += delta;
        }

        private bool CanInteract() { return canInteract != null && canInteract(); }

        private void LateUpdate()
        {
            if (desktop == null || previousDesktopSize == desktop.rect.size) return;
            previousDesktopSize = desktop.rect.size;
            ApplyLayout();
        }

        private void OnDisable() { dragging = false; }
    }
}
