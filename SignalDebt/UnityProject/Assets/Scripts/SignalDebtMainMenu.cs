using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalDebt
{
    /// <summary>Launch navigation only. Campaign files and settings belong to the bootstrap.</summary>
    public sealed class SignalDebtMainMenu : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.89f, 0.94f, 0.95f);
        private static readonly Color Muted = new Color(0.56f, 0.68f, 0.73f);
        private static readonly Color Accent = new Color(0.35f, 0.87f, 0.81f);
        private static readonly Color ButtonFill = new Color(0.065f, 0.112f, 0.139f);
        private SignalDebtBootstrap game;
        private Font font;
        private CanvasGroup navigation;
        private GameObject confirmation;
        private Button newGameButton, backButton, defaultButton;
        private bool hasCampaign, restoreSelection;

        public static GameObject Show(SignalDebtBootstrap game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            var root = new GameObject("Signal Debt main menu");
            var menu = root.AddComponent<SignalDebtMainMenu>();
            menu.game = game;
            menu.hasCampaign = game.HasSavedCampaign;
            menu.Build(game.MenuStatus);
            return root;
        }

        private void Build(string status)
        {
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (ArgumentException) { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }

            var cameraObject = new GameObject("Menu background camera", typeof(Camera));
            cameraObject.transform.SetParent(transform, false);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.029f, 0.040f);
            camera.cullingMask = 0;
            camera.depth = 1000f;
            if (FindFirstObjectByType<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();

            RectTransform canvasRect = Rect("Launch interface", transform);
            Canvas canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Menu input", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }

            // The full-screen image consumes pointer input outside the controls too.
            RectTransform background = Box("Night background", canvasRect, camera.backgroundColor, true);
            Stretch(background);
            RectTransform frame = Rect("Menu composition", canvasRect);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(1600f, 900f);
            BuildNightRoom(frame);

            Block("Signal mark", frame, 132f, 142f, 42f, 4f, Accent);
            Text title = Label("Title", frame, "SIGNAL DEBT", 74, Ink);
            Place(title.rectTransform, 126f, 167f, 630f, 102f);
            Text introduction = Label("Introduction", frame,
                "A quiet room. An unpaid bill.\nA message that could change tomorrow.", 23, Muted);
            Place(introduction.rectTransform, 132f, 280f, 550f, 76f);
            Block("Menu divider", frame, 132f, 364f, 468f, 1f, new Color(0.14f, 0.24f, 0.27f));

            RectTransform controls = Rect("Main navigation", frame);
            Stretch(controls);
            navigation = controls.gameObject.AddComponent<CanvasGroup>();
            Button resume = MakeButton("Continue", controls, Continue, true);
            Place(resume.GetComponent<RectTransform>(), 132f, 390f, 468f, 62f);
            resume.interactable = hasCampaign;
            if (!hasCampaign) resume.GetComponentInChildren<Text>().color = new Color(0.38f, 0.47f, 0.51f);
            newGameButton = MakeButton("New Game", controls, RequestNewGame, !hasCampaign);
            Place(newGameButton.GetComponent<RectTransform>(), 132f, 466f, 468f, 62f);
            Button settings = MakeButton("Settings", controls, Settings, false);
            Place(settings.GetComponent<RectTransform>(), 132f, 542f, 468f, 62f);
            Button quit = MakeButton("Quit", controls, Quit, false);
            Place(quit.GetComponent<RectTransform>(), 132f, 618f, 468f, 62f);
            ConnectNavigation(resume, newGameButton, settings, quit);

            string message = string.IsNullOrWhiteSpace(status)
                ? hasCampaign ? "Continue your story." : "No saved campaign yet."
                : status.Trim();
            if (message.Length > 240) message = message.Substring(0, 237) + "…";
            Text statusLabel = Label("Campaign status", frame, message, 18, Muted);
            Place(statusLabel.rectTransform, 132f, 703f, 530f, 92f);
            Text controlsHint = Label("Controls", frame, "WASD  Move     Mouse  Look     E  Interact", 16, Muted);
            Place(controlsHint.rectTransform, 132f, 822f, 560f, 32f);

            BuildConfirmation(canvasRect);
            Canvas.ForceUpdateCanvases();
            defaultButton = hasCampaign ? resume : newGameButton;
            Select(defaultButton);
        }

        private void BuildNightRoom(RectTransform parent)
        {
            // Quiet geometric illustration, independent of the playable room and its camera.
            Color shadow = new Color(0.023f, 0.044f, 0.059f);
            Color edge = new Color(0.095f, 0.19f, 0.22f);
            Block("Room wall", parent, 760f, 126f, 752f, 620f, shadow);
            Block("Window casing", parent, 922f, 179f, 485f, 360f, edge);
            RectTransform window = Block("Night window", parent, 930f, 187f, 469f, 344f,
                new Color(0.037f, 0.087f, 0.113f));
            window.gameObject.AddComponent<RectMask2D>();
            BuildBuilding(window, 22f, 156f, 102f, 230f, 3, 5);
            BuildBuilding(window, 145f, 89f, 121f, 280f, 4, 7);
            BuildBuilding(window, 284f, 194f, 74f, 180f, 2, 4);
            BuildBuilding(window, 374f, 119f, 106f, 245f, 3, 6);
            Block("Window center frame", parent, 1159f, 179f, 8f, 360f, edge);
            Block("Window horizontal frame", parent, 922f, 346f, 485f, 6f, edge);
            Block("Window sill", parent, 903f, 534f, 523f, 10f, new Color(0.12f, 0.21f, 0.23f));
            Block("Desk shadow", parent, 789f, 631f, 703f, 24f, new Color(0.012f, 0.025f, 0.033f));
            Block("Desk surface", parent, 785f, 611f, 711f, 16f, new Color(0.18f, 0.21f, 0.21f));
            Block("Desk front", parent, 802f, 627f, 675f, 19f, new Color(0.065f, 0.10f, 0.115f));
            Block("Desk left leg", parent, 830f, 646f, 17f, 100f, edge);
            Block("Desk right leg", parent, 1429f, 646f, 17f, 100f, edge);
            Block("Laptop lid", parent, 985f, 449f, 264f, 158f, new Color(0.12f, 0.22f, 0.25f));
            Block("Laptop bezel", parent, 990f, 454f, 254f, 148f, new Color(0.009f, 0.022f, 0.030f));
            Block("Laptop light", parent, 1001f, 465f, 232f, 124f, new Color(0.033f, 0.12f, 0.14f));
            Block("Screen signal", parent, 1020f, 485f, 21f, 3f, Accent);
            Block("Screen line one", parent, 1020f, 508f, 148f, 3f, new Color(0.19f, 0.42f, 0.44f));
            Block("Screen line two", parent, 1020f, 519f, 103f, 3f, new Color(0.13f, 0.30f, 0.34f));
            Block("Screen line three", parent, 1020f, 530f, 127f, 3f, new Color(0.13f, 0.30f, 0.34f));
            Block("Screen notification", parent, 1179f, 565f, 33f, 5f, Accent);
            Block("Laptop base", parent, 965f, 603f, 304f, 8f, new Color(0.22f, 0.34f, 0.37f));
            Block("Lamp stem", parent, 1358f, 497f, 6f, 111f, edge);
            Block("Lamp shade", parent, 1321f, 481f, 79f, 25f, new Color(0.18f, 0.23f, 0.24f));
            Block("Lamp light", parent, 1328f, 506f, 65f, 3f, new Color(0.80f, 0.62f, 0.39f));
            Block("Lamp foot", parent, 1337f, 604f, 49f, 7f, edge);
            Text sceneCaption = Label("Room caption", parent, "ONE MORE NIGHT. ONE MORE CHANCE.", 16, Muted);
            Place(sceneCaption.rectTransform, 805f, 779f, 690f, 34f);
            sceneCaption.alignment = TextAnchor.MiddleRight;
        }

        private void BuildBuilding(RectTransform window, float x, float y, float width, float height, int columns, int rows)
        {
            RectTransform building = Block("Building silhouette", window, x, y, width, height,
                new Color(0.022f, 0.049f, 0.066f));
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                if ((row * 3 + column * 2) % 5 > 1) continue;
                Color light = (row + column) % 3 == 0
                    ? new Color(0.42f, 0.34f, 0.24f) : new Color(0.10f, 0.25f, 0.29f);
                Block("Distant window", building, 13f + column * 25f, 17f + row * 29f, 7f, 12f, light);
            }
        }

        private void BuildConfirmation(RectTransform parent)
        {
            RectTransform blocker = Box("New game confirmation", parent, new Color(0.005f, 0.012f, 0.02f, 0.92f), true);
            Stretch(blocker);
            confirmation = blocker.gameObject;
            RectTransform panel = Box("Confirmation panel", blocker, new Color(0.039f, 0.068f, 0.089f), true);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(700f, 340f);
            Block("Confirmation accent", panel, 38f, 34f, 35f, 3f, Accent);
            Text heading = Label("Confirmation heading", panel, "Start a new game?", 34, Ink);
            Place(heading.rectTransform, 38f, 60f, 624f, 53f);
            Text description = Label("Confirmation description", panel,
                "Your current save will be preserved.", 23, Muted);
            Place(description.rectTransform, 38f, 127f, 624f, 77f);
            backButton = MakeButton("Back", panel, CancelNewGame, false);
            Place(backButton.GetComponent<RectTransform>(), 38f, 242f, 183f, 60f);
            Button start = MakeButton("Start new game", panel, StartNewGame, true);
            Place(start.GetComponent<RectTransform>(), 237f, 242f, 425f, 60f);
            ConnectNavigation(backButton, start);
            confirmation.SetActive(false);
        }

        private void Continue() { if (game != null && hasCampaign) game.ContinueGame(); }
        private void Settings() { if (game != null) game.OpenAiSettings(); }
        private void Quit() { if (game != null) game.QuitGame(); }
        private void StartNewGame() { if (game != null) game.StartNewGame(); }

        private void RequestNewGame()
        {
            if (!hasCampaign) { StartNewGame(); return; }
            navigation.interactable = false;
            navigation.blocksRaycasts = false;
            confirmation.SetActive(true);
            Select(backButton);
        }

        private void CancelNewGame()
        {
            confirmation.SetActive(false);
            navigation.interactable = true;
            navigation.blocksRaycasts = true;
            Select(newGameButton);
        }

        private void Update()
        {
            // A child EventSystem may enable after this component, so restore
            // keyboard focus on the first update with an available input owner.
            if (restoreSelection && defaultButton != null && EventSystem.current != null)
            {
                restoreSelection = false;
                if (EventSystem.current.currentSelectedGameObject == null)
                    Select(confirmation != null && confirmation.activeSelf ? backButton : defaultButton);
            }
            if (confirmation != null && confirmation.activeSelf && Input.GetKeyDown(KeyCode.Escape)) CancelNewGame();
        }

        private void OnEnable()
        {
            restoreSelection = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDisable()
        {
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null
                && events.currentSelectedGameObject.transform.IsChildOf(transform)) events.SetSelectedGameObject(null);
        }

        private Button MakeButton(string label, Transform parent, UnityEngine.Events.UnityAction action, bool primary)
        {
            RectTransform rect = Box(label, parent, primary ? new Color(0.065f, 0.28f, 0.29f) : ButtonFill, true);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(1.45f, 1.45f, 1.45f);
            colors.pressedColor = new Color(0.75f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.34f, 0.41f, 0.44f, 0.65f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;
            button.onClick.AddListener(action);
            Text text = Label("Label", rect, label, 25, Ink);
            Stretch(text.rectTransform, 23f, 8f);
            text.alignment = TextAnchor.MiddleLeft;
            return button;
        }

        private static void ConnectNavigation(params Button[] candidates)
        {
            var active = new List<Button>();
            foreach (Button button in candidates) if (button.interactable) active.Add(button);
            for (int i = 0; i < active.Count; i++)
            {
                Button previous = active[(i + active.Count - 1) % active.Count];
                Button next = active[(i + 1) % active.Count];
                active[i].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = previous, selectOnDown = next, selectOnLeft = previous, selectOnRight = next };
            }
        }

        private static void Select(Button button)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        private Text Label(string name, Transform parent, string value, int size, Color color)
        {
            Text text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color;
            text.raycastTarget = false; text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static RectTransform Block(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            RectTransform rect = Box(name, parent, color, false);
            Place(rect, x, y, width, height);
            return rect;
        }

        private static RectTransform Box(string name, Transform parent, Color color, bool raycast)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = raycast;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical);
            rect.offsetMax = new Vector2(-horizontal, -vertical);
        }
    }
}
