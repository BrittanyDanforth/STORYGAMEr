using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalDebt
{
    /// <summary>Read-only recovery screen. It has no access to the campaign or its files.</summary>
    public sealed class SignalDebtStartupFailure : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.89f, 0.93f, 0.96f);
        private static readonly Color Muted = new Color(0.62f, 0.71f, 0.77f);
        private static readonly Color Accent = new Color(0.35f, 0.87f, 0.81f);

        public static GameObject Show(string message, string saveFolder)
        {
            var root = new GameObject("Signal Debt save recovery");
            root.AddComponent<SignalDebtStartupFailure>();
            Font font;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (ArgumentException) { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }

            var cameraObject = new GameObject("Recovery background camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform, false);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.016f, 0.029f, 0.043f);
            camera.cullingMask = 0;
            camera.depth = 1000f;
            if (FindFirstObjectByType<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();

            RectTransform canvasRect = Rect("Recovery interface", root.transform);
            Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 800f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Recovery input", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(root.transform, false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            RectTransform panel = Box("Save recovery panel", canvasRect, new Color(0.039f, 0.061f, 0.082f));
            panel.anchorMin = new Vector2(0.12f, 0.10f);
            panel.anchorMax = new Vector2(0.88f, 0.90f);
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            Text brand = Label("Game name", panel, "SIGNAL DEBT", font, 17, Accent);
            Anchor(brand.rectTransform, Vector2.up, Vector2.one, new Vector2(32f, -47f), new Vector2(-32f, -23f));
            Text heading = Label("Heading", panel, "Your save couldn't be opened", font, 31, Ink);
            Anchor(heading.rectTransform, Vector2.up, Vector2.one, new Vector2(32f, -99f), new Vector2(-32f, -53f));

            RectTransform scrollRect = Rect("Recovery details", panel);
            Anchor(scrollRect, Vector2.zero, Vector2.one, new Vector2(32f, 180f), new Vector2(-32f, -116f));
            // A transparent image makes the whole reading area respond to the mouse wheel.
            scrollRect.gameObject.AddComponent<Image>().color = Color.clear;
            ScrollRect scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
            RectTransform viewport = Rect("Viewport", scrollRect);
            Anchor(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect("Details", viewport);
            content.anchorMin = Vector2.up; content.anchorMax = Vector2.one;
            content.pivot = Vector2.up;
            content.offsetMin = content.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.padding = new RectOffset(0, 12, 0, 14);
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = viewport;
            string reason = string.IsNullOrWhiteSpace(message) ? "This save could not be loaded safely." : message.Trim();
            if (reason.Length > 1600) reason = reason.Substring(0, 1600) + "…";
            Paragraph(content, reason, font, 21, new Color(0.97f, 0.73f, 0.43f));
            Paragraph(content, "Your existing save files are unchanged. The game stopped before loading a campaign.", font, 21, Ink);
            Paragraph(content, "Close the game, then restore a known-good backup or open this save with a compatible game version.", font, 21, Ink);
            Paragraph(content, "Keep a copy of your current files before replacing them. Your save and backups are in the folder below.", font, 18, Muted);

            Text folderLabel = Label("Save folder label", panel, "SAVE FOLDER  ·  Select the path to copy it", font, 15, Muted);
            Anchor(folderLabel.rectTransform, Vector2.zero, Vector2.right, new Vector2(32f, 148f), new Vector2(-32f, 172f));
            RectTransform fieldRect = Box("Save folder", panel, new Color(0.025f, 0.042f, 0.059f));
            Anchor(fieldRect, Vector2.zero, Vector2.right, new Vector2(32f, 98f), new Vector2(-32f, 140f));
            fieldRect.gameObject.AddComponent<RectMask2D>();
            Text pathText = Label("Folder path", fieldRect, "", font, 17, Ink);
            Anchor(pathText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 7f), new Vector2(-12f, -7f));
            pathText.alignment = TextAnchor.MiddleLeft;
            pathText.horizontalOverflow = HorizontalWrapMode.Overflow;
            pathText.verticalOverflow = VerticalWrapMode.Truncate;
            InputField folderField = fieldRect.gameObject.AddComponent<InputField>();
            folderField.targetGraphic = fieldRect.GetComponent<Image>();
            folderField.textComponent = pathText;
            folderField.readOnly = true;
            folderField.lineType = InputField.LineType.SingleLine;
            folderField.selectionColor = new Color(0.15f, 0.43f, 0.45f, 0.8f);
            bool hasFolder = !string.IsNullOrWhiteSpace(saveFolder);
            folderField.text = hasFolder ? saveFolder : "Save folder unavailable";

            Text copyStatus = Label("Copy status", panel, "", font, 16, Accent);
            Anchor(copyStatus.rectTransform, Vector2.zero, Vector2.right, new Vector2(258f, 35f), new Vector2(-222f, 67f));
            Button copy = MakeButton("Copy folder path", panel, font, () =>
            {
                GUIUtility.systemCopyBuffer = saveFolder;
                copyStatus.text = "Folder path copied.";
            });
            Anchor(copy.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero, new Vector2(32f, 28f), new Vector2(238f, 76f));
            copy.interactable = hasFolder;
            Button close = MakeButton("Close game  [Esc]", panel, font, Quit);
            Anchor(close.GetComponent<RectTransform>(), Vector2.right, Vector2.right, new Vector2(-222f, 28f), new Vector2(-32f, 76f));
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
            return root;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) Quit();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void Paragraph(Transform parent, string message, Font font, int size, Color color)
        {
            Text text = Label("Paragraph", parent, message, font, size, color);
            text.gameObject.AddComponent<LayoutElement>().minHeight = size + 8f;
        }

        private static Button MakeButton(string label, Transform parent, Font font, UnityEngine.Events.UnityAction action)
        {
            RectTransform rect = Box(label, parent, new Color(0.07f, 0.24f, 0.27f));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(action);
            Text text = Label("Label", rect, label, font, 18, Ink);
            text.alignment = TextAnchor.MiddleCenter;
            Anchor(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            return button;
        }

        private static Text Label(string name, Transform parent, string text, Font font, int size, Color color)
        {
            Text label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        private static RectTransform Box(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 lower, Vector2 upper)
        {
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = lower; rect.offsetMax = upper;
        }
    }
}
