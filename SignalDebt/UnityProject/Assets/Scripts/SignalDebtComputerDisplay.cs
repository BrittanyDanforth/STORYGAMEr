using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SignalDebt
{
    /// <summary>
    /// Presents one desktop Canvas on the authored laptop and in its focused view.
    /// No second desktop or screen texture owns independent application state.
    /// </summary>
    public sealed class SignalDebtComputerDisplay : MonoBehaviour
    {
        private const float AuthoredWidth = 0.397f, AuthoredHeight = 0.229f;
        private const float SurfaceOffset = 0.004f, FramingMargin = 1.04f;
        private static readonly Vector2 DesktopSize = new Vector2(1600f, 900f);

        public Vector3 ScreenCenter { get; private set; }
        // Canvas fronts face local -Z; the camera looks in local +Z toward the screen.
        public Quaternion ViewRotation { get; private set; } = Quaternion.identity;

        private float screenWidth = AuthoredWidth, screenHeight = AuthoredHeight;
        private readonly List<RendererState> staticArtwork = new List<RendererState>();
        private CanvasState originalCanvas;

        private sealed class RendererState
        {
            public Renderer renderer;
            public bool enabled;
        }

        private sealed class CanvasState
        {
            public Canvas canvas;
            public Transform parent;
            public int siblingIndex;
            public Vector3 localPosition, localScale;
            public Quaternion localRotation;
            public Vector2 anchorMin, anchorMax, pivot, sizeDelta;
            public RenderMode renderMode;
            public Camera camera;
            public int sortingOrder;
            public CanvasScaler scaler;
            public bool addedScaler;
            public CanvasScaler.ScaleMode scaleMode;
            public Vector2 referenceResolution;
            public CanvasScaler.ScreenMatchMode screenMatchMode;
            public float matchWidthOrHeight, referencePixelsPerUnit, dynamicPixelsPerUnit, scaleFactor;
            public GraphicRaycaster raycaster;
            public bool raycasterEnabled;
        }

        public void Initialize(Transform focus, Transform seat, MeshFilter screen = null, Transform artworkRoot = null)
        {
            RestoreStaticScreen();
            staticArtwork.Clear();
            // The scene builder binds the original apartment screen explicitly.
            // Additional client computers must never become this desktop by name search.
            if (screen == null || screen.sharedMesh == null || !ReadScreenFrame(screen, seat))
            {
                UseAnchorFrame(focus, seat);
                Debug.LogWarning("Signal Debt: Laptop_Screen mesh was not found or has invalid bounds. " +
                    "Using the authored LaptopFocus/DeskSeat display fallback; inspect the apartment import.", this);
            }

            // Leave the actual screen, bezel, keyboard and notification lighting intact.
            if (artworkRoot != null)
            {
                foreach (MeshRenderer renderer in artworkRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!renderer.name.StartsWith("Screen_", System.StringComparison.Ordinal)) continue;
                    staticArtwork.Add(new RendererState { renderer = renderer, enabled = renderer.enabled });
                }
            }
        }

        private bool ReadScreenFrame(MeshFilter mesh, Transform seat)
        {
            Bounds bounds = mesh.sharedMesh.bounds;
            // World AABBs lose the lid tilt. Transform each LOCAL bounds axis instead;
            // TransformVector also includes the FBX wrapper's negative/nonuniform scale.
            Vector3[] edges =
            {
                mesh.transform.TransformVector(Vector3.right * bounds.size.x),
                mesh.transform.TransformVector(Vector3.up * bounds.size.y),
                mesh.transform.TransformVector(Vector3.forward * bounds.size.z)
            };
            int thin = 0, wide = 0;
            for (int i = 1; i < edges.Length; i++)
            {
                if (edges[i].sqrMagnitude < edges[thin].sqrMagnitude) thin = i;
                if (edges[i].sqrMagnitude > edges[wide].sqrMagnitude) wide = i;
            }
            if (thin == wide || edges[thin].magnitude <= 0f) return false;
            int tall = 3 - thin - wide;
            Vector3 up = edges[tall].normalized;
            if (Vector3.Dot(up, Vector3.up) < 0f) up = -up;
            Vector3 normal = Vector3.Cross(edges[wide], up).normalized;
            if (normal.sqrMagnitude < 0.9f) return false;
            Vector3 center = mesh.transform.TransformPoint(bounds.center);
            Vector3 observer = seat != null ? seat.position + Vector3.up * 1.18f
                : center - mesh.transform.forward;
            if (Vector3.Dot(normal, observer - center) < 0f) normal = -normal;

            screenWidth = edges[wide].magnitude;
            screenHeight = edges[tall].magnitude;
            float thickness = Mathf.Abs(Vector3.Dot(edges[thin], normal));
            ScreenCenter = center + normal * (thickness * 0.5f + SurfaceOffset);
            ViewRotation = Quaternion.LookRotation(-normal, up);
            return screenWidth > 0.01f && screenHeight > 0.01f;
        }

        private void UseAnchorFrame(Transform focus, Transform seat)
        {
            screenWidth = AuthoredWidth;
            screenHeight = AuthoredHeight;
            Vector3 center = focus != null ? focus.position : seat != null
                ? seat.position + seat.forward * 1.05f + Vector3.up * 0.97f
                : transform.position + Vector3.up;
            Vector3 outward = seat != null ? Vector3.ProjectOnPlane(seat.position - center, Vector3.up)
                : focus != null ? -Vector3.ProjectOnPlane(focus.forward, Vector3.up) : Vector3.back;
            if (outward.sqrMagnitude < 0.001f) outward = Vector3.back;
            // The authored lid leans back 12 degrees from vertical.
            Vector3 normal = outward.normalized * Mathf.Cos(12f * Mathf.Deg2Rad)
                + Vector3.up * Mathf.Sin(12f * Mathf.Deg2Rad);
            ScreenCenter = center + normal * SurfaceOffset;
            ViewRotation = Quaternion.LookRotation(-normal, Vector3.up);
        }

        public Vector3 GetFocusedPosition(Camera camera)
        {
            float fov = camera != null ? Mathf.Clamp(camera.fieldOfView, 1f, 179f) : 76f;
            float aspect = camera != null ? Mathf.Max(0.1f, camera.aspect) : 16f / 9f;
            float halfVertical = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            float distanceForHeight = screenHeight * 0.5f / halfVertical;
            float distanceForWidth = screenWidth * 0.5f / (halfVertical * aspect);
            float distance = Mathf.Max(distanceForHeight, distanceForWidth) * FramingMargin;
            if (camera != null) distance = Mathf.Max(distance, camera.nearClipPlane + 0.025f);
            return ScreenCenter - ViewRotation * Vector3.forward * distance;
        }

        public void Mount(Canvas canvas)
        {
            if (canvas == null) return;
            RememberCanvas(canvas);
            HideStaticScreen();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 0;
            RectTransform rect = canvas.GetComponent<RectTransform>();
            // A world-root Canvas avoids mirroring text under a negative-scale FBX root.
            rect.SetParent(null, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = DesktopSize;
            rect.SetPositionAndRotation(ScreenCenter, ViewRotation);
            rect.localScale = new Vector3(screenWidth / DesktopSize.x, screenHeight / DesktopSize.y, 0.001f);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                scaler.dynamicPixelsPerUnit = 1f;
                scaler.referencePixelsPerUnit = 100f;
            }
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
        }

        public void Focus(Canvas canvas)
        {
            if (canvas == null) return;
            RememberCanvas(canvas);
            HideStaticScreen();
            RectTransform rect = canvas.GetComponent<RectTransform>();
            rect.SetParent(originalCanvas.parent, false);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = DesktopSize;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
            }
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = true;
        }

        private void RememberCanvas(Canvas canvas)
        {
            if (originalCanvas != null && originalCanvas.canvas == canvas) return;
            RestoreCanvas();
            RectTransform rect = canvas.GetComponent<RectTransform>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            bool addedScaler = scaler == null;
            if (addedScaler) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            originalCanvas = new CanvasState
            {
                canvas = canvas, parent = rect.parent, siblingIndex = rect.GetSiblingIndex(),
                localPosition = rect.localPosition, localRotation = rect.localRotation, localScale = rect.localScale,
                anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot, sizeDelta = rect.sizeDelta,
                renderMode = canvas.renderMode, camera = canvas.worldCamera, sortingOrder = canvas.sortingOrder,
                scaler = scaler, addedScaler = addedScaler,
                raycaster = raycaster, raycasterEnabled = raycaster != null && raycaster.enabled
            };
            if (scaler == null) return;
            originalCanvas.scaleMode = scaler.uiScaleMode;
            originalCanvas.referenceResolution = scaler.referenceResolution;
            originalCanvas.screenMatchMode = scaler.screenMatchMode;
            originalCanvas.matchWidthOrHeight = scaler.matchWidthOrHeight;
            originalCanvas.referencePixelsPerUnit = scaler.referencePixelsPerUnit;
            originalCanvas.dynamicPixelsPerUnit = scaler.dynamicPixelsPerUnit;
            originalCanvas.scaleFactor = scaler.scaleFactor;
        }

        private void HideStaticScreen()
        {
            foreach (RendererState item in staticArtwork)
                if (item.renderer != null) item.renderer.enabled = false;
        }

        public void RestoreStaticScreen()
        {
            foreach (RendererState item in staticArtwork)
                if (item.renderer != null) item.renderer.enabled = item.enabled;
        }

        private void RestoreCanvas()
        {
            CanvasState state = originalCanvas;
            originalCanvas = null;
            if (state == null || state.canvas == null) return;
            RectTransform rect = state.canvas.GetComponent<RectTransform>();
            rect.SetParent(state.parent, false);
            rect.SetSiblingIndex(state.siblingIndex);
            state.canvas.renderMode = state.renderMode;
            state.canvas.worldCamera = state.camera;
            state.canvas.sortingOrder = state.sortingOrder;
            rect.anchorMin = state.anchorMin; rect.anchorMax = state.anchorMax;
            rect.pivot = state.pivot; rect.sizeDelta = state.sizeDelta;
            rect.localPosition = state.localPosition; rect.localRotation = state.localRotation;
            rect.localScale = state.localScale;
            if (state.scaler != null)
            {
                state.scaler.uiScaleMode = state.scaleMode;
                state.scaler.referenceResolution = state.referenceResolution;
                state.scaler.screenMatchMode = state.screenMatchMode;
                state.scaler.matchWidthOrHeight = state.matchWidthOrHeight;
                state.scaler.referencePixelsPerUnit = state.referencePixelsPerUnit;
                state.scaler.dynamicPixelsPerUnit = state.dynamicPixelsPerUnit;
                state.scaler.scaleFactor = state.scaleFactor;
                if (state.addedScaler) Destroy(state.scaler);
            }
            if (state.raycaster != null) state.raycaster.enabled = state.raycasterEnabled;
        }

        private void OnDestroy()
        {
            RestoreStaticScreen();
            RestoreCanvas();
        }
    }
}
