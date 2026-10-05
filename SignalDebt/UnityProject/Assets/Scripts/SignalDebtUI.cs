using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalDebt
{
    public sealed class SignalDebtUI : MonoBehaviour
    {
        private SignalDebtBootstrap game;
        private Font font;
        private Canvas computerCanvas;
        private CanvasGroup computerInput;
        private RectTransform desktop, window, content, appContent, modalContent, modal;
        private ScrollRect appScroll, modalScroll;
        private SignalDebtDesktopWindow windowController;
        private GameObject hud, launcher;
        private Text focus, objective, heading, subtitle, footer, toast, clock, desktopStatus, desktopBalance, taskTitle;
        private GameObject focusPanel, toastPanel;
        private readonly Dictionary<string, float> scrollPositions = new Dictionary<string, float>();
        private readonly Dictionary<string, Button> appButtons = new Dictionary<string, Button>();
        private float toastUntil, nextClockUpdate;
        private string screenPage, modalPage;
        private bool computerFocused, minimized, preferencesReady, restoringPreferences;
        private string lastSavedPreferences;
        private const string DesktopPreferenceFile = "desktop.json";
        private const int DesktopPreferenceVersion = 1, MaxDesktopPreferenceLength = 4096;
        private int lastObjectiveRevision = -1, clientMilestone, historyPage;
        private string clientDraft = "", clientStatus = "", selectedHistoryId;
        private bool clientBusy, historyReceiptsOnly, walletReceipts;
        private InputField clientInput;
        private Button clientSendButton;
        private Text clientStatusLabel;
        private SignalDebtClientDesktop clientDesktop;
        private SignalDebtProofDesktop proofDesktop;
        private int clientOwnerObservationSeen;
        private int proofMilestone, proofOwnerObservationSeen;
        private ScrollRect conversationScroll;
        private RectTransform conversationContent;
        private readonly List<ClientLine> conversation = new List<ClientLine>();
        private sealed class ClientLine { public string speaker, text; public bool fromPlayer; }
        private readonly Color background = new Color(0.023f, 0.041f, 0.06f, 1f);
        private readonly Color panel = new Color(0.048f, 0.073f, 0.098f, 1f);
        private readonly Color ink = new Color(0.89f, 0.93f, 0.96f);
        private readonly Color muted = new Color(0.61f, 0.70f, 0.76f);
        private readonly Color cyan = new Color(0.35f, 0.87f, 0.81f);
        private readonly Color amber = new Color(0.97f, 0.72f, 0.40f);
        private static readonly string[] Applications = { "Inbox", "Service", "Proofs", "EXODISS", "History" };

        [Serializable]
        private sealed class DesktopPreferences
        {
            public int version;
            public string application;
            public bool minimized;
            public AppScrollPreference[] scroll;
            public SignalDebtDesktopWindow.LayoutState layout;
        }

        [Serializable]
        private sealed class AppScrollPreference
        {
            public string application;
            public float position;
        }
        public Canvas ComputerCanvas { get { return computerCanvas; } }
        public bool ComputerFocused { get { return computerFocused; } }
        public string ActiveApplication { get { return screenPage; } }
        public bool HasWorldModal { get { return modal != null && modal.gameObject.activeSelf; } }
        public bool IsPauseOpen { get { return modalPage == "Pause" && HasWorldModal; } }

        public void Initialize(SignalDebtBootstrap owner)
        {
            game = owner;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (ArgumentException) { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            ConfigureCanvas(gameObject, 150);
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            BuildHud();
            // Independent roots keep apartment prompts and bills off the physical screen.
            var computerObject = new GameObject("ASTER computer display", typeof(RectTransform));
            computerCanvas = ConfigureCanvas(computerObject, 100);
            computerInput = computerObject.AddComponent<CanvasGroup>();
            BuildDesktop();
            BuildModal();
            BuildToast();
            Canvas.ForceUpdateCanvases();
            restoringPreferences = true;
            RestoreDesktopPreferences();
            Refresh();
            restoringPreferences = false;
            preferencesReady = true;
            Close();
        }

        private static Canvas ConfigureCanvas(GameObject target, int order)
        {
            Canvas canvas = target.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = target.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            // Expand retains a readable 1600 x 900 workspace at narrow aspect ratios.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            target.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private void BuildHud()
        {
            hud = Rect("World HUD", transform).gameObject;
            Stretch(hud.GetComponent<RectTransform>());
            objective = Label("Objective", hud.transform, "", 22, ink);
            objective.rectTransform.anchorMin = new Vector2(0f, 1f);
            objective.rectTransform.anchorMax = Vector2.one;
            objective.rectTransform.pivot = Vector2.up;
            objective.rectTransform.offsetMin = new Vector2(42f, -132f);
            objective.rectTransform.offsetMax = new Vector2(-42f, -35f);
            Text crosshair = Label("Crosshair", hud.transform, "·", 35, ink);
            crosshair.alignment = TextAnchor.MiddleCenter;
            Pin(crosshair.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f), new Vector2(0.5f, 0.5f));
            RectTransform bar = Box("Interaction prompt", hud.transform, new Color(0.02f, 0.04f, 0.055f, 0.92f));
            Pin(bar, new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(760f, 56f), new Vector2(0.5f, 0f));
            focusPanel = bar.gameObject;
            focus = Label("Prompt", bar, "", 24, cyan);
            Stretch(focus.rectTransform, 15f, 4f);
            focus.alignment = TextAnchor.MiddleCenter;
            Text controls = Label("Controls", hud.transform, "WASD  Move     Mouse  Look     E  Interact     Ctrl  Crouch     Shift  Walk faster     Esc  Pause", 17, muted);
            controls.rectTransform.anchorMin = Vector2.zero;
            controls.rectTransform.anchorMax = Vector2.right;
            controls.rectTransform.offsetMin = new Vector2(42f, 24f);
            controls.rectTransform.offsetMax = new Vector2(-42f, 60f);
        }

        private void BuildDesktop()
        {
            desktop = Box("Desktop", computerCanvas.transform, background);
            Stretch(desktop);
            desktop.gameObject.AddComponent<RectMask2D>();
            BuildWallpaper();
            RectTransform top = Box("System bar", desktop, new Color(0.027f, 0.049f, 0.067f));
            top.anchorMin = Vector2.up; top.anchorMax = Vector2.one;
            top.offsetMin = new Vector2(0f, -46f); top.offsetMax = Vector2.zero;
            Text brand = Label("System name", top, "ASTER", 20, cyan);
            Pin(brand.rectTransform, Vector2.up, new Vector2(26f, -11f), new Vector2(100f, 28f), Vector2.up);
            brand.fontStyle = FontStyle.Bold;
            Text owner = Label("Workspace name", top, "Personal workspace", 17, muted);
            Pin(owner.rectTransform, Vector2.up, new Vector2(140f, -12f), new Vector2(330f, 28f), Vector2.up);
            clock = Label("Clock", top, "", 17, ink);
            clock.alignment = TextAnchor.MiddleRight;
            Pin(clock.rectTransform, Vector2.one, new Vector2(-24f, -10f), new Vector2(390f, 28f), Vector2.one);

            for (int index = 0; index < Applications.Length; index++)
            {
                string app = Applications[index];
                Button icon = Button("Desktop " + app, desktop, "", () => Open(app), false);
                icon.GetComponent<Image>().color = new Color(0.04f, 0.08f, 0.105f, 0.68f);
                Pin(icon.GetComponent<RectTransform>(), Vector2.up, new Vector2(26f, -88f - index * 130f), new Vector2(142f, 112f), Vector2.up);
                AddAppGlyph(icon.transform, app, new Vector2(71f, -35f));
                Text label = Label("App name", icon.transform, AppName(app), 20, ink);
                label.alignment = TextAnchor.MiddleCenter;
                Pin(label.rectTransform, Vector2.up, new Vector2(8f, -72f), new Vector2(126f, 30f), Vector2.up);
            }
            BuildWindow();
            BuildTaskbar();
            window.gameObject.SetActive(false);
        }

        private void BuildWallpaper()
        {
            // Quiet, procedural artwork stays sharp on the real laptop and any resolution.
            RectTransform art = Rect("Wallpaper geometry", desktop);
            Stretch(art);
            for (int index = 0; index < 7; index++)
            {
                RectTransform beam = Box("Signal line", art, new Color(0.08f, 0.23f, 0.27f, 0.18f + index * 0.025f));
                Pin(beam, new Vector2(0.78f, 0.54f), new Vector2(index * 73f - 190f, index * 40f - 40f), new Vector2(5f + index, 1200f), new Vector2(0.5f, 0.5f));
                beam.localRotation = Quaternion.Euler(0f, 0f, -38f);
                beam.GetComponent<Image>().raycastTarget = false;
            }
            Text wordmark = Label("Wallpaper wordmark", art, "ASTER", 82, new Color(0.44f, 0.75f, 0.76f, 0.48f));
            wordmark.fontStyle = FontStyle.Bold;
            Pin(wordmark.rectTransform, new Vector2(0.66f, 0.68f), Vector2.zero, new Vector2(500f, 115f), new Vector2(0.5f, 0.5f));
            Text tagline = Label("Wallpaper caption", art, "A little space to make things work.", 23, muted);
            Pin(tagline.rectTransform, new Vector2(0.66f, 0.68f), new Vector2(4f, -60f), new Vector2(500f, 40f), new Vector2(0.5f, 0.5f));
            RectTransform summary = Box("Work overview", desktop, new Color(0.035f, 0.064f, 0.084f, 0.94f));
            summary.anchorMin = new Vector2(0.43f, 0.19f); summary.anchorMax = new Vector2(0.94f, 0.42f);
            summary.offsetMin = summary.offsetMax = Vector2.zero;
            Text caption = Label("Overview title", summary, "YOUR WORKSPACE", 17, cyan);
            Pin(caption.rectTransform, Vector2.up, new Vector2(24f, -20f), new Vector2(370f, 26f), Vector2.up);
            desktopStatus = Label("Work status", summary, "", 24, ink);
            desktopStatus.rectTransform.anchorMin = new Vector2(0f, 0.5f); desktopStatus.rectTransform.anchorMax = Vector2.one;
            desktopStatus.rectTransform.offsetMin = new Vector2(24f, -10f); desktopStatus.rectTransform.offsetMax = new Vector2(-24f, -57f);
            desktopBalance = Label("Balance", summary, "", 18, muted);
            Pin(desktopBalance.rectTransform, Vector2.zero, new Vector2(24f, 22f), new Vector2(500f, 32f), Vector2.zero);
            Button resume = Button("Open inbox", summary, "Open Inbox", () => Open("Inbox"), true);
            Pin(resume.GetComponent<RectTransform>(), Vector2.right, new Vector2(-20f, 20f), new Vector2(160f, 42f), Vector2.right);
        }

        private void BuildWindow()
        {
            window = Box("Application window", desktop, new Color(0.029f, 0.047f, 0.066f));
            window.anchorMin = new Vector2(0.145f, 0.14f); window.anchorMax = new Vector2(0.97f, 0.92f);
            window.offsetMin = window.offsetMax = Vector2.zero;
            window.gameObject.AddComponent<RectMask2D>();
            var border = window.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(0.23f, 0.46f, 0.48f, 0.6f); border.effectDistance = new Vector2(1f, -1f);
            RectTransform titlebar = Box("Window title bar", window, new Color(0.068f, 0.10f, 0.129f));
            titlebar.anchorMin = Vector2.up; titlebar.anchorMax = Vector2.one;
            titlebar.offsetMin = new Vector2(0f, -56f); titlebar.offsetMax = Vector2.zero;
            windowController = titlebar.gameObject.AddComponent<SignalDebtDesktopWindow>();
            windowController.Initialize(window, desktop, () => computerFocused);
            heading = Label("Application title", titlebar, "", 22, ink);
            Pin(heading.rectTransform, Vector2.up, new Vector2(24f, -13f), new Vector2(540f, 32f), Vector2.up);
            Button minimize = Button("Minimize window", titlebar, "—", MinimizeWindow, false);
            Pin(minimize.GetComponent<RectTransform>(), Vector2.one, new Vector2(-122f, -8f), new Vector2(46f, 40f), Vector2.one);
            Button maximize = Button("Maximize or restore window", titlebar, "□", () => windowController.ToggleMaximize(), false);
            Pin(maximize.GetComponent<RectTransform>(), Vector2.one, new Vector2(-66f, -8f), new Vector2(46f, 40f), Vector2.one);
            Button close = Button("Close application", titlebar, "×", CloseApplication, false);
            Pin(close.GetComponent<RectTransform>(), Vector2.one, new Vector2(-10f, -8f), new Vector2(46f, 40f), Vector2.one);
            close.GetComponent<Image>().color = new Color(0.24f, 0.12f, 0.14f);

            RectTransform sidebar = Box("App switcher", window, new Color(0.039f, 0.061f, 0.083f));
            sidebar.anchorMin = Vector2.zero; sidebar.anchorMax = Vector2.up;
            sidebar.offsetMin = new Vector2(0f, 0f); sidebar.offsetMax = new Vector2(176f, -56f);
            Text apps = Label("App heading", sidebar, "APPLICATIONS", 15, muted);
            Pin(apps.rectTransform, Vector2.up, new Vector2(18f, -25f), new Vector2(144f, 25f), Vector2.up);
            for (int index = 0; index < Applications.Length; index++)
            {
                string app = Applications[index];
                Button nav = Button("Switch to " + app, sidebar, AppName(app), () => Open(app), false);
                Pin(nav.GetComponent<RectTransform>(), Vector2.up, new Vector2(10f, -65f - index * 60f), new Vector2(156f, 49f), Vector2.up);
                nav.GetComponentInChildren<Text>().fontSize = 20;
                appButtons.Add(app, nav);
            }
            Text session = Label("Session label", sidebar, "PERSONAL\nWORKSTATION", 14, muted);
            Pin(session.rectTransform, Vector2.zero, new Vector2(20f, 18f), new Vector2(145f, 50f), Vector2.zero);
            subtitle = Label("App status", window, "", 18, muted);
            subtitle.rectTransform.anchorMin = Vector2.up; subtitle.rectTransform.anchorMax = Vector2.one;
            subtitle.rectTransform.offsetMin = new Vector2(200f, -100f); subtitle.rectTransform.offsetMax = new Vector2(-24f, -70f);
            appScroll = BuildScroll("Application workspace", window, out appContent);
            var scrollRect = appScroll.GetComponent<RectTransform>();
            Stretch(scrollRect);
            scrollRect.offsetMin = new Vector2(190f, 54f); scrollRect.offsetMax = new Vector2(-15f, -110f);
            footer = Label("Save status", window, "", 16, muted);
            footer.rectTransform.anchorMin = Vector2.zero; footer.rectTransform.anchorMax = Vector2.right;
            footer.rectTransform.offsetMin = new Vector2(207f, 14f); footer.rectTransform.offsetMax = new Vector2(-24f, 44f);
        }

        private void BuildTaskbar()
        {
            RectTransform taskbar = Box("Taskbar", desktop, new Color(0.039f, 0.066f, 0.087f));
            taskbar.anchorMin = Vector2.zero; taskbar.anchorMax = Vector2.right;
            taskbar.offsetMin = Vector2.zero; taskbar.offsetMax = new Vector2(0f, 70f);
            Button menu = Button("Applications menu", taskbar, "ASTER  /  Apps", ToggleLauncher, false);
            Pin(menu.GetComponent<RectTransform>(), Vector2.zero, new Vector2(14f, 12f), new Vector2(178f, 46f), Vector2.zero);
            Button showDesktop = Button("Show desktop", taskbar, "Desktop", MinimizeWindow, false);
            Pin(showDesktop.GetComponent<RectTransform>(), Vector2.zero, new Vector2(204f, 12f), new Vector2(116f, 46f), Vector2.zero);
            Button task = Button("Current application", taskbar, "No app open", RestoreWindow, false);
            Pin(task.GetComponent<RectTransform>(), Vector2.zero, new Vector2(332f, 12f), new Vector2(240f, 46f), Vector2.zero);
            taskTitle = task.GetComponentInChildren<Text>();
            Button leave = Button("Leave computer", taskbar, "Stand up", CloseFromButton, false);
            Pin(leave.GetComponent<RectTransform>(), Vector2.right, new Vector2(-14f, 12f), new Vector2(202f, 46f), Vector2.right);
            Text saved = Label("Local session", taskbar, "Local session  ·  Auto-save", 17, muted);
            saved.alignment = TextAnchor.MiddleRight;
            Pin(saved.rectTransform, Vector2.right, new Vector2(-238f, 18f), new Vector2(350f, 32f), Vector2.right);

            RectTransform menuRect = Box("Application launcher", desktop, new Color(0.055f, 0.086f, 0.111f));
            Pin(menuRect, Vector2.zero, new Vector2(14f, 82f), new Vector2(315f, 352f), Vector2.zero);
            launcher = menuRect.gameObject;
            Text title = Label("Launcher title", menuRect, "APPLICATIONS", 17, cyan);
            Pin(title.rectTransform, Vector2.up, new Vector2(20f, -20f), new Vector2(270f, 30f), Vector2.up);
            for (int index = 0; index < Applications.Length; index++)
            {
                string app = Applications[index];
                Button choice = Button("Launch " + app, menuRect, AppName(app), () => Open(app), false);
                Pin(choice.GetComponent<RectTransform>(), Vector2.up, new Vector2(16f, -67f - index * 66f), new Vector2(283f, 54f), Vector2.up);
            }
            launcher.SetActive(false);
        }

        private void AddAppGlyph(Transform parent, string app, Vector2 position)
        {
            RectTransform glyph = Rect("App symbol", parent);
            Pin(glyph, Vector2.up, position, new Vector2(44f, 38f), new Vector2(0.5f, 0.5f));
            Color tint = app == "EXODISS" ? amber : cyan;
            if (app == "Inbox")
            {
                GlyphLine(glyph, new Vector2(0f, -12f), new Vector2(40f, 3f), 0f, tint);
                GlyphLine(glyph, new Vector2(-19f, 0f), new Vector2(3f, 24f), 0f, tint);
                GlyphLine(glyph, new Vector2(19f, 0f), new Vector2(3f, 24f), 0f, tint);
                GlyphLine(glyph, new Vector2(-10f, 5f), new Vector2(24f, 3f), -31f, tint);
                GlyphLine(glyph, new Vector2(10f, 5f), new Vector2(24f, 3f), 31f, tint);
            }
            else if (app == "Service")
            {
                for (int index = 0; index < 3; index++)
                {
                    GlyphLine(glyph, new Vector2(0f, 12f - index * 12f), new Vector2(42f, 3f), 0f, tint);
                    GlyphLine(glyph, new Vector2(index == 1 ? 9f : -9f, 12f - index * 12f), new Vector2(5f, 9f), 0f, tint);
                }
            }
            else
            {
                for (int index = 0; index < 3; index++)
                    GlyphLine(glyph, new Vector2(-14f + index * 14f, app == "History" ? -index * 3f : 0f), new Vector2(8f, 18f + index * 9f), 0f, tint);
            }
        }

        private static void GlyphLine(Transform parent, Vector2 at, Vector2 size, float rotation, Color color)
        {
            RectTransform line = Box("Stroke", parent, color);
            Pin(line, new Vector2(0.5f, 0.5f), at, size, new Vector2(0.5f, 0.5f));
            line.localRotation = Quaternion.Euler(0f, 0f, rotation);
            line.GetComponent<Image>().raycastTarget = false;
        }

        private void BuildModal()
        {
            modal = Box("Apartment dialog", transform, new Color(0.008f, 0.016f, 0.025f, 0.82f));
            Stretch(modal);
            RectTransform sheet = Box("Dialog", modal, background);
            sheet.anchorMin = new Vector2(0.15f, 0.16f); sheet.anchorMax = new Vector2(0.85f, 0.86f);
            sheet.offsetMin = sheet.offsetMax = Vector2.zero;
            RectTransform titlebar = Box("Dialog title bar", sheet, panel);
            titlebar.anchorMin = Vector2.up; titlebar.anchorMax = Vector2.one;
            titlebar.offsetMin = new Vector2(0f, -60f); titlebar.offsetMax = Vector2.zero;
            Text title = Label("Dialog title", titlebar, "SIGNAL DEBT", 22, cyan);
            Pin(title.rectTransform, Vector2.up, new Vector2(24f, -17f), new Vector2(500f, 34f), Vector2.up);
            Button close = Button("Return", titlebar, "Return", CloseFromButton, false);
            Pin(close.GetComponent<RectTransform>(), Vector2.one, new Vector2(-12f, -9f), new Vector2(180f, 42f), Vector2.one);
            modalScroll = BuildScroll("Dialog content", sheet, out modalContent);
            RectTransform rect = modalScroll.GetComponent<RectTransform>();
            Stretch(rect); rect.offsetMin = new Vector2(24f, 20f); rect.offsetMax = new Vector2(-24f, -78f);
            modal.gameObject.SetActive(false);
        }

        private ScrollRect BuildScroll(string name, Transform parent, out RectTransform body)
        {
            RectTransform root = Box(name, parent, new Color(0.031f, 0.051f, 0.071f));
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;
            RectTransform viewport = Rect("Viewport", root);
            Stretch(viewport); viewport.offsetMax = new Vector2(-16f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            body = Rect("Content", viewport);
            body.anchorMin = Vector2.up; body.anchorMax = Vector2.one;
            body.pivot = Vector2.up; body.offsetMin = body.offsetMax = Vector2.zero;
            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 8, 16); layout.spacing = 12f;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = body; scroll.viewport = viewport;
            RectTransform track = Box("Scroll track", root, new Color(0.04f, 0.075f, 0.095f));
            track.anchorMin = Vector2.right; track.anchorMax = Vector2.one;
            track.offsetMin = new Vector2(-9f, 4f); track.offsetMax = new Vector2(-3f, -4f);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            RectTransform handle = Box("Scroll handle", track, new Color(0.24f, 0.41f, 0.44f));
            Stretch(handle);
            scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        private void BuildToast()
        {
            RectTransform rectangle = Box("Notification", transform, new Color(0.028f, 0.065f, 0.085f, 0.98f));
            rectangle.anchorMin = new Vector2(0.1f, 1f); rectangle.anchorMax = new Vector2(0.9f, 1f);
            rectangle.pivot = new Vector2(0.5f, 1f);
            rectangle.offsetMin = new Vector2(0f, -85f); rectangle.offsetMax = new Vector2(0f, -14f);
            rectangle.GetComponent<Image>().raycastTarget = false;
            toastPanel = rectangle.gameObject;
            toast = Label("Notification text", rectangle, "", 21, cyan);
            Stretch(toast.rectTransform, 20f, 9f);
            toast.alignment = TextAnchor.MiddleCenter;
            toast.verticalOverflow = VerticalWrapMode.Truncate;
            toastPanel.SetActive(false);
        }

        private void Update()
        {
            if (game == null || game.State == null) return;
            if (computerFocused && !game.Player.CanUseComputer) Close();
            if (toastPanel.activeSelf && Time.unscaledTime >= toastUntil) toastPanel.SetActive(false);
            if (lastObjectiveRevision != game.State.revision)
            {
                lastObjectiveRevision = game.State.revision;
                objective.text = game.State.proofPaid
                    ? "SIGNAL DEBT  /  NOTICE RECOVERED\nSouthbank exported the notice. Your 160-credit receipt is in History."
                    : game.State.proofAccepted
                        ? "SIGNAL DEBT  /  RECOVER THE NOTICE\nCompare the saved drafts, restore the notice, and ask Southbank to export it."
                    : game.State.jobPaid
                    ? "SIGNAL DEBT  /  MORE WORK FROM SOUTHBANK\nThe shop has a notice to recover. Read the 160-credit offer in Inbox."
                    : game.State.jobAccepted
                        ? "SIGNAL DEBT  /  THE FIRST PAID REPAIR\nRestore the service, test the client's folder, then collect payment."
                        : "SIGNAL DEBT  /  A MESSAGE IN THE DARK\nSouthbank Copy Shop has a repair that pays today. Read it at your laptop.";
            }
            if (Time.unscaledTime >= nextClockUpdate)
            {
                // The wall clock is explicitly local; the campaign day only changes after rest.
                clock.text = "Day " + game.State.day + "     ·     " + DateTime.Now.ToString("HH:mm") + " local";
                nextClockUpdate = Time.unscaledTime + 1f;
            }
        }

        // Called before the sit animation. The physical display remains live and non-interactive.
        public void PrepareComputer()
        {
            modal.gameObject.SetActive(false);
            modalPage = null;
            hud.SetActive(false);
            SetFocus("");
            computerFocused = false;
            computerInput.interactable = false;
            computerInput.blocksRaycasts = false;
            game.ComputerDisplay.Mount(computerCanvas);
        }

        // Only called after the camera reaches the screen; cursor ownership stays with Player.
        public void FocusComputer()
        {
            if (!game.Player.CanUseComputer) return;
            game.ComputerDisplay.Focus(computerCanvas);
            computerFocused = true;
            computerInput.interactable = true;
            computerInput.blocksRaycasts = true;
            hud.SetActive(false);
            Canvas.ForceUpdateCanvases();
            windowController.ClampToDesktop();
            RestoreScroll();
        }

        public void ComputerExited()
        {
            Close();
            if (hud != null) hud.SetActive(!HasWorldModal);
        }

        public void Open(string selectedPage)
        {
            if (selectedPage == "Rest" || selectedPage == "Pause" || !game.Player.IsSeated)
            {
                modalPage = selectedPage;
                modal.gameObject.SetActive(true);
                hud.SetActive(false);
                computerInput.interactable = false;
                EventSystem.current?.SetSelectedGameObject(null);
                RenderModal(false);
                return;
            }
            if (!computerFocused || !game.Player.CanUseComputer || Array.IndexOf(Applications, selectedPage) < 0) return;
            SaveScroll();
            bool changed = screenPage != selectedPage;
            screenPage = selectedPage;
            minimized = false;
            launcher.SetActive(false);
            window.gameObject.SetActive(true);
            if (changed) RenderApplication();
            else { Canvas.ForceUpdateCanvases(); RestoreScroll(); }
            UpdateDesktopStatus();
            windowController.ClampToDesktop();
        }

        public void Close()
        {
            SaveScroll();
            if (computerFocused) SaveDesktopPreferences();
            computerFocused = false;
            if (computerInput != null)
            {
                computerInput.interactable = false;
                computerInput.blocksRaycasts = false;
            }
            if (launcher != null) launcher.SetActive(false);
            if (modal != null) modal.gameObject.SetActive(false);
            modalPage = null;
            if (game != null && game.ComputerDisplay != null && computerCanvas != null) game.ComputerDisplay.Mount(computerCanvas);
            if (hud != null) hud.SetActive(game == null || game.Player == null || !game.Player.IsSeated);
            SetFocus("");
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void CloseWorldModal()
        {
            if (modal != null) modal.gameObject.SetActive(false);
            modalPage = null;
            bool seated = game != null && game.Player != null && game.Player.IsSeated;
            if (hud != null) hud.SetActive(!seated);
            if (computerInput != null)
            {
                computerInput.interactable = computerFocused && game != null && game.Player != null && game.Player.CanUseComputer;
                computerInput.blocksRaycasts = computerInput.interactable;
            }
            SetFocus("");
            EventSystem.current?.SetSelectedGameObject(null);
        }

        public void SetFocus(string message)
        {
            if (focus != null) focus.text = message;
            if (focusPanel != null) focusPanel.SetActive(!string.IsNullOrEmpty(message)
                && game != null && game.Player != null && !game.Player.UiOpen && !game.Player.IsSeated);
        }

        public void ShowToast(string message)
        {
            message = message ?? "";
            if (message.Length > 220)
            {
                toast.text = message.Substring(0, 160) + "…  More in Inbox.";
                AppendClientLine("Notice", message, false);
            }
            else toast.text = message;
            toastPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            RectTransform rectangle = toastPanel.GetComponent<RectTransform>();
            rectangle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(toast.preferredHeight + 24f, 68f, 134f));
            toastPanel.transform.SetAsLastSibling();
            toastUntil = Time.unscaledTime + Mathf.Clamp(message.Length / 18f, 4f, 10f);
        }

        public void Refresh()
        {
            if (desktop == null || game.State == null) return;
            SaveScroll();
            UpdateDesktopStatus();
            if (screenPage == "Service" && clientDesktop != null) clientDesktop.Refresh();
            else if (screenPage == "Proofs" && proofDesktop != null) proofDesktop.Refresh();
            else if (screenPage != null) RenderApplication();
            if (HasWorldModal) RenderModal(true);
            nextClockUpdate = 0f;
        }

        private void UpdateDesktopStatus()
        {
            desktopStatus.text = game.State.proofPaid ? "Notice recovered. Payment received."
                : game.State.proofAccepted ? "Southbank notice · recovery in progress"
                : game.State.jobPaid ? "New offer · recover Southbank's notice"
                : game.State.jobAccepted ? "Southbank repair · in progress" : "1 new message · Southbank Copy Shop";
            desktopBalance.text = game.State.balance + " credits     /     " + game.State.rentDue + " rent outstanding";
            taskTitle.text = screenPage == null ? "Open an application" : AppName(screenPage) + (minimized ? "  ·  minimized" : "  ·  open");
            heading.text = AppName(screenPage);
            subtitle.text = game.State.jobPaid ? "Southbank Copy Shop  /  Repair completed"
                : game.State.jobAccepted ? "Southbank Copy Shop  /  Active repair" : "Personal workspace  /  New work available";
            if (screenPage == "EXODISS") subtitle.text = "Personal account  /  " + game.State.balance + " credits available";
            if (screenPage == "History") subtitle.text = "Activity and receipts  /  Day " + game.State.day;
            if (screenPage == "Proofs") subtitle.text = "Southbank Copy Shop  /  Community notice";
            footer.text = game.SaveStatus;
            foreach (var entry in appButtons)
                entry.Value.GetComponent<Image>().color = entry.Key == screenPage ? new Color(0.06f, 0.26f, 0.27f) : new Color(0.065f, 0.095f, 0.127f);
        }

        private void SaveScroll()
        {
            // Remounting the screen changes viewport dimensions, not the user's chosen scroll position.
            if (computerFocused && !restoringPreferences && screenPage != null && appScroll != null && window != null && window.gameObject.activeSelf)
                scrollPositions[screenPage] = SafeScrollPosition(screenPage == "Inbox" && conversationScroll != null
                    ? conversationScroll.verticalNormalizedPosition : appScroll.verticalNormalizedPosition);
        }

        private static float SafeScrollPosition(float position)
        {
            return float.IsNaN(position) || float.IsInfinity(position) ? 1f : Mathf.Clamp01(position);
        }

        private void RestoreScroll()
        {
            if (screenPage == null) return;
            appScroll.StopMovement();
            float position = scrollPositions.TryGetValue(screenPage, out float saved) ? saved : screenPage == "Inbox" ? 0f : 1f;
            appScroll.verticalNormalizedPosition = screenPage == "Inbox" ? 1f : position;
            if (screenPage == "Inbox" && conversationScroll != null)
            {
                conversationScroll.StopMovement();
                conversationScroll.verticalNormalizedPosition = position;
            }
        }

        private void RenderApplication()
        {
            clientInput = null; clientSendButton = null; clientStatusLabel = null;
            conversationScroll = null; conversationContent = null; clientDesktop = null; proofDesktop = null;
            content = appContent;
            Clear(content);
            DrawPage(screenPage);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            RestoreScroll();
            UpdateDesktopStatus();
        }

        private void RenderModal(bool preserveScroll)
        {
            float position = preserveScroll ? modalScroll.verticalNormalizedPosition : 1f;
            content = modalContent;
            Clear(content);
            DrawPage(modalPage);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            modalScroll.StopMovement();
            modalScroll.verticalNormalizedPosition = position;
        }

        private void DrawPage(string selectedPage)
        {
            switch (selectedPage)
            {
                case "Service": Service(); break;
                case "Proofs": Proofs(); break;
                case "EXODISS": Wallet(); break;
                case "History": History(); break;
                case "Rest": Rest(); break;
                case "Pause": Pause(); break;
                default: Inbox(); break;
            }
        }

        private void MinimizeWindow()
        {
            if (!computerFocused) return;
            SaveScroll(); minimized = screenPage != null;
            window.gameObject.SetActive(false);
            launcher.SetActive(false);
            UpdateDesktopStatus();
        }

        private void RestoreWindow()
        {
            if (!computerFocused) return;
            if (screenPage == null) { ToggleLauncher(); return; }
            Open(screenPage);
        }

        private void CloseApplication()
        {
            if (!computerFocused) return;
            SaveScroll(); screenPage = null; minimized = false;
            window.gameObject.SetActive(false);
            launcher.SetActive(false);
            UpdateDesktopStatus();
            EventSystem.current?.SetSelectedGameObject(null);
            SaveDesktopPreferences();
        }

        private void ToggleLauncher()
        {
            if (computerFocused) launcher.SetActive(!launcher.activeSelf);
        }

        private void OnDestroy()
        {
            SaveScroll();
            SaveDesktopPreferences();
            if (computerCanvas != null) Destroy(computerCanvas.gameObject);
        }

        private void OnApplicationQuit()
        {
            // Unity can destroy the separate display root before this component's OnDestroy.
            SaveScroll();
            SaveDesktopPreferences();
        }

        private void RestoreDesktopPreferences()
        {
            try
            {
                if (!SignalDebtLocalFiles.TryReadText(DesktopPreferenceFile, MaxDesktopPreferenceLength, out string json)) return;
                if (string.IsNullOrEmpty(json) || json.Length > MaxDesktopPreferenceLength) return;
                DesktopPreferences saved = JsonUtility.FromJson<DesktopPreferences>(json);
                if (saved == null || saved.version != DesktopPreferenceVersion) return;
                screenPage = Array.IndexOf(Applications, saved.application) >= 0 ? saved.application : null;
                minimized = screenPage != null && saved.minimized;
                if (saved.scroll != null && saved.scroll.Length <= Applications.Length)
                {
                    foreach (AppScrollPreference entry in saved.scroll)
                    {
                        // Chat is rebuilt from campaign observations on load. An
                        // old scroll ratio refers to a different conversation and
                        // can hide the latest offer; reopen that thread at its end.
                        if (entry != null && entry.application != "Inbox"
                            && Array.IndexOf(Applications, entry.application) >= 0)
                            scrollPositions[entry.application] = SafeScrollPosition(entry.position);
                    }
                }
                windowController.RestoreLayout(saved.layout);
                window.gameObject.SetActive(screenPage != null && !minimized);
                lastSavedPreferences = json;
            }
            catch (Exception)
            {
                // Preferences are cosmetic. Invalid or unavailable preferences never touch the campaign save.
                Debug.LogWarning("ASTER desktop preferences could not be restored; using the default desktop.");
            }
        }

        private void SaveDesktopPreferences()
        {
            if (!preferencesReady || windowController == null) return;
            try
            {
                var scroll = new AppScrollPreference[Applications.Length];
                for (int index = 0; index < Applications.Length; index++)
                {
                    string app = Applications[index];
                    scroll[index] = new AppScrollPreference
                    {
                        application = app,
                        position = app == "Inbox" ? 0f
                            : scrollPositions.TryGetValue(app, out float position) ? SafeScrollPosition(position) : 1f
                    };
                }
                var saved = new DesktopPreferences
                {
                    version = DesktopPreferenceVersion,
                    application = Array.IndexOf(Applications, screenPage) >= 0 ? screenPage : null,
                    minimized = screenPage != null && minimized,
                    scroll = scroll,
                    layout = windowController.CaptureLayout()
                };
                string json = JsonUtility.ToJson(saved);
                if (json.Length > MaxDesktopPreferenceLength || json == lastSavedPreferences) return;
                if (!SignalDebtLocalFiles.TryWriteText(DesktopPreferenceFile, json, out string error))
                {
                    Debug.LogWarning("ASTER desktop preferences could not be saved. Game progress uses its separate campaign save.");
                    return;
                }
                lastSavedPreferences = json;
            }
            catch (Exception)
            {
                Debug.LogWarning("ASTER desktop preferences could not be saved. Game progress uses its separate campaign save.");
            }
        }

        private static string AppName(string name) { return name == "Service" ? "Client PC" : name ?? ""; }

        private void Inbox()
        {
            EnsureConversation();
            RectTransform contact = FixedBlock("Client", content, 48f, panel);
            Text initials = Label("Client initials", contact, "SB", 20, cyan);
            Pin(initials.rectTransform, Vector2.up, new Vector2(16f, -10f), new Vector2(44f, 30f), Vector2.up);
            TopText(contact, "Southbank Copy Shop", 24, ink, 8f, 34f, 72f, 160f);
            Text fee = TopText(contact, game.State.proofPaid ? "PAID  ·  160 cr"
                : game.State.jobPaid ? "RECOVERY  ·  160 cr" : "REPAIR  ·  120 cr", 17, cyan, 13f, 27f);
            fee.alignment = TextAnchor.MiddleRight;

            conversationScroll = BuildScroll("Conversation", content, out conversationContent);
            conversationScroll.gameObject.AddComponent<LayoutElement>().preferredHeight = 188f;
            conversationContent.GetComponent<VerticalLayoutGroup>().spacing = 8f;
            RenderConversation();
            if (!game.State.jobAccepted) ActionButton("Accept repair  ·  120 credits", game.AcceptJob);
            else if (!game.State.jobPaid) ActionButton("Continue repair", () => Open("Service"));
            else if (!game.State.proofAccepted) ActionButton("Accept notice recovery  ·  160 credits", game.AcceptProofJob);
            else if (!game.State.proofPaid) ActionButton("Continue notice recovery", () => Open("Proofs"));
            else ActionButton("View payment receipt", () =>
            {
                selectedHistoryId = "southbank-proof-payment";
                historyReceiptsOnly = true;
                Open("History");
            });
            BuildComposer();
            clientStatusLabel = Body(content, clientStatus, muted, 17);
            clientStatusLabel.verticalOverflow = VerticalWrapMode.Truncate;
            SetClientStatus(clientStatus, clientBusy);
        }

        private void EnsureConversation()
        {
            if (conversation.Count == 0)
                conversation.Add(new ClientLine
                {
                    speaker = "Southbank", text = "hey, our order folder won't open. can you fix it? 120 credits. just the work folder."
                });
            SignalDebtClientDevice device = game.State.clientDevice;
            if (device != null && device.ownerCheckCount > clientOwnerObservationSeen)
            {
                string observation;
                switch (device.lastOwnerReadResult)
                {
                    case SignalDebtFolderResult.Ready:
                        observation = device.firstOrderCompletedAtCheck == device.ownerCheckCount
                            ? "yeah, orders are back. got 1042 ready for the customer. that's a relief."
                            : "opened it again. the orders are still there.";
                        clientMilestone = Mathf.Max(clientMilestone, 1);
                        break;
                    case SignalDebtFolderResult.PrintService:
                        observation = "still won't open. it's getting print jobs where the order list should be."; break;
                    case SignalDebtFolderResult.AssetService:
                        observation = "no luck. it's pulling images, not the customer orders."; break;
                    case SignalDebtFolderResult.PoweredOff:
                        observation = "computer's off at the moment. couldn't check the folder."; break;
                    default:
                        observation = "tried it. the folder still has no source for its order list."; break;
                }
                conversation.Add(new ClientLine { speaker = "Southbank", text = observation });
                clientOwnerObservationSeen = device.ownerCheckCount;
            }
            else if (game.State.serviceTestPassed && !game.State.jobPaid && clientMilestone < 1
                && device != null && device.legacyRepairTestRecorded && device.ownerCheckCount == 0)
            {
                conversation.Add(new ClientLine { speaker = "Southbank", text = "opened when i tested it. the orders were all there." });
                clientMilestone = 1;
            }
            if (game.State.jobPaid && clientMilestone < 2)
            {
                conversation.Add(new ClientLine { speaker = "Southbank", text = "sent the 120. thanks for getting that folder open." });
                clientMilestone = 2;
            }
            if (game.State.jobPaid && proofMilestone < 1)
            {
                conversation.Add(new ClientLine { speaker = "Southbank", text = "one more thing, if you've got time. the community notice lost its contact line and footer. older drafts are on this PC. 160 to recover it — i'll send you what was approved." });
                proofMilestone = 1;
            }
            if (game.State.proofAccepted && proofMilestone < 2)
            {
                conversation.Add(new ClientLine { speaker = "Southbank", text = "you can restore the notice from its history. just that document. i'll check the date and details before i export it." });
                proofMilestone = 2;
            }
            SignalDebtProofOwnerRead proofRead = game.State.proofDocument.LastOwnerRead();
            if (proofRead != null && proofRead.number > proofOwnerObservationSeen)
            {
                string observation = proofRead.result == SignalDebtProofResult.ApprovedMatch
                    ? "that's the one. date and details all match — exported it."
                    : proofRead.result == SignalDebtProofResult.DateMismatch
                        ? "hang on, that's the old date. the swap is on the 19th."
                    : proofRead.result == SignalDebtProofResult.ContactMissing
                        ? "contact line's still missing. can't send it out like that."
                    : proofRead.result == SignalDebtProofResult.PoweredOff
                        ? "PC's off. haven't been able to open the notice."
                        : "the notice still differs from the approval. i've left the result in Proofs.";
                conversation.Add(new ClientLine { speaker = "Southbank", text = observation });
                proofOwnerObservationSeen = proofRead.number;
            }
            if (game.State.proofPaid && proofMilestone < 3)
            {
                conversation.Add(new ClientLine { speaker = "Southbank", text = "160 sent. glad we still had those drafts." });
                proofMilestone = 3;
            }
            while (conversation.Count > 12) conversation.RemoveAt(1);
        }

        // Send the conversation the player actually sees, including the client's
        // authored messages. History is conversational context, not new world facts.
        public SignalDebtAiProvider.ChatLine[] CaptureClientHistoryForAi(string currentMessage)
        {
            if (game == null || game.State == null) return new SignalDebtAiProvider.ChatLine[0];
            EnsureConversation();
            int end = conversation.Count;
            // Send already appended this turn to the screen; the request carries
            // it separately as player_message, so include it only once.
            if (end > 0 && conversation[end - 1].fromPlayer
                && string.Equals(conversation[end - 1].text, currentMessage, StringComparison.Ordinal)) end--;
            var history = new List<SignalDebtAiProvider.ChatLine>();
            for (int index = Mathf.Max(0, end - 12); index < end; index++)
            {
                ClientLine line = conversation[index];
                if (string.IsNullOrWhiteSpace(line.text)) continue;
                // Notices can contain private wallet or technical information.
                // Only actual dialogue was spoken to or by this contact.
                if (!line.fromPlayer && line.speaker != "Southbank") continue;
                history.Add(new SignalDebtAiProvider.ChatLine
                {
                    role = line.fromPlayer ? "user" : "assistant",
                    text = line.text.Length <= 1000 ? line.text : line.text.Substring(0, 1000)
                });
            }
            return history.ToArray();
        }

        private void BuildComposer()
        {
            RectTransform composer = Row(48f);
            composer.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            RectTransform inputRect = Box("Message input", composer, new Color(0.025f, 0.043f, 0.062f));
            inputRect.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            inputRect.gameObject.AddComponent<RectMask2D>();
            Text text = Label("Message text", inputRect, "", 20, ink);
            Stretch(text.rectTransform, 14f, 10f);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            Text placeholder = Label("Message placeholder", inputRect, "Message Southbank…", 20, muted);
            Stretch(placeholder.rectTransform, 14f, 10f);
            clientInput = inputRect.gameObject.AddComponent<InputField>();
            clientInput.targetGraphic = inputRect.GetComponent<Image>();
            clientInput.textComponent = text; clientInput.placeholder = placeholder;
            clientInput.lineType = InputField.LineType.SingleLine;
            clientInput.characterLimit = 500;
            clientInput.selectionColor = new Color(0.14f, 0.43f, 0.43f, 0.8f);
            clientInput.text = clientDraft;
            clientInput.onValueChanged.AddListener(value =>
            {
                clientDraft = value;
                if (clientSendButton != null) clientSendButton.interactable = !clientBusy && !string.IsNullOrWhiteSpace(value);
            });
            clientInput.onEndEdit.AddListener(value =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) SendClientMessage();
            });
            clientSendButton = Button("Send message", composer, "Send", SendClientMessage, true);
            LayoutElement sendSize = clientSendButton.gameObject.AddComponent<LayoutElement>();
            sendSize.preferredWidth = 116f; sendSize.flexibleWidth = 0f;
            clientSendButton.interactable = !clientBusy && !string.IsNullOrWhiteSpace(clientDraft);
        }

        private void SendClientMessage()
        {
            if (!computerFocused || clientBusy || string.IsNullOrWhiteSpace(clientDraft)) return;
            if (game.GetComponent<SignalDebtNpcClient>() == null)
            { SetClientStatus("Replies are unavailable right now."); return; }
            string message = clientDraft.Trim();
            clientDraft = "";
            if (clientInput != null) clientInput.text = "";
            AppendClientLine("You", message, true);
            SetClientStatus("Waiting for a reply…", true);
            game.SendMessage("BeginClientChat", message, SendMessageOptions.DontRequireReceiver);
        }

        public void ShowClientReply(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) { SetClientStatus("No reply received. Try again."); return; }
            SetClientStatus("");
            AppendClientLine("Southbank", message, false);
        }

        public void ShowLongMessage(string message) { ShowClientReply(message); }

        public void SetClientStatus(string message, bool busy = false)
        {
            clientStatus = message ?? "";
            if (clientStatus.Length > 100) clientStatus = clientStatus.Substring(0, 99) + "…";
            clientBusy = busy;
            if (clientStatusLabel != null) clientStatusLabel.text = clientStatus;
            if (clientSendButton != null) clientSendButton.interactable = !busy && !string.IsNullOrWhiteSpace(clientDraft);
        }

        private void AppendClientLine(string speaker, string message, bool fromPlayer)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            EnsureConversation();
            message = message.Trim();
            if (message.Length > 2000) message = message.Substring(0, 1999) + "…";
            conversation.Add(new ClientLine { speaker = speaker, text = message, fromPlayer = fromPlayer });
            while (conversation.Count > 12) conversation.RemoveAt(1);
            scrollPositions["Inbox"] = 0f;
            RenderConversation();
        }

        private void RenderConversation()
        {
            if (conversationContent == null || conversationScroll == null) return;
            Clear(conversationContent);
            foreach (ClientLine line in conversation)
            {
                RectTransform bubble = Box(line.speaker + " message", conversationContent,
                    line.fromPlayer ? new Color(0.045f, 0.18f, 0.19f) : panel);
                VerticalLayoutGroup layout = bubble.gameObject.AddComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(14, 14, 10, 10); layout.spacing = 3f;
                layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
                Body(bubble, line.speaker, line.fromPlayer ? cyan : muted, 14, true);
                Body(bubble, line.text, ink, 20);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(conversationContent);
            Canvas.ForceUpdateCanvases();
            conversationScroll.StopMovement();
            conversationScroll.verticalNormalizedPosition = 0f;
        }

        private void Service()
        {
            if (!game.State.jobAccepted)
            {
                Title("A repair is waiting");
                FolderCard("Order Folder", "Southbank Copy Shop", "Won't open", false);
                ActionButton("Read the client's message", () => Open("Inbox"));
                return;
            }
            clientDesktop = SignalDebtClientDesktop.Build(content, font,
                () => game.State.clientDevice.Copy(), CommitClientCommand,
                () => game.State.jobPaid, () => game.State.CanClaimRepairPayment,
                game.ClaimPayment, () => Open("Inbox"), () => game.ClientWorkspaceFeedback);
        }

        private bool CommitClientCommand(SignalDebtClientDesktop.Command command, string argument)
        {
            switch (command)
            {
                case SignalDebtClientDesktop.Command.BeginSession: return game.BeginClientWorkspace();
                case SignalDebtClientDesktop.Command.EndSession: return game.EndClientWorkspace();
                case SignalDebtClientDesktop.Command.OpenFolder: return game.OpenClientFolder(argument);
                case SignalDebtClientDesktop.Command.InspectConnection: return game.InspectClientConnection();
                case SignalDebtClientDesktop.Command.ConnectEndpoint: return game.ConnectClientEndpoint(argument);
                case SignalDebtClientDesktop.Command.UndoConnection: return game.UndoClientConnection();
                case SignalDebtClientDesktop.Command.QueueOwnerCheck: return game.RequestClientCheck();
                default: return false;
            }
        }

        private void Proofs()
        {
            if (!game.State.proofAccepted)
            {
                Title(game.State.jobPaid ? "Recover the community notice" : "Southbank's document workspace");
                Body(content, game.State.jobPaid
                    ? "Compare preserved drafts with the owner's reference. Recover the notice for 160 credits."
                    : "Finish the Order Folder repair to receive the next offer.", muted);
                ActionButton("Open Inbox", () => Open("Inbox"));
                return;
            }
            proofDesktop = SignalDebtProofDesktop.Build(content, font, () => game.State.Copy(),
                CommitProofCommand, () => game.ProofWorkspaceFeedback);
        }

        private bool CommitProofCommand(SignalDebtProofDesktop.Command command, string argument)
        {
            switch (command)
            {
                case SignalDebtProofDesktop.Command.BeginSession: return game.BeginProofWorkspace();
                case SignalDebtProofDesktop.Command.Restore: return game.RestoreProofRevision(argument);
                case SignalDebtProofDesktop.Command.Undo: return game.UndoProofRestore();
                case SignalDebtProofDesktop.Command.QueueCheck: return game.RequestProofCheck();
                case SignalDebtProofDesktop.Command.Collect: return game.ClaimProofPayment();
                default: return false;
            }
        }

        private void FolderCard(string name, string detail, string status, bool ready)
        {
            RectTransform card = FixedBlock("Folder status", content, 108f, panel);
            RectTransform tab = Box("Folder tab", card, ready ? cyan : amber);
            Pin(tab, Vector2.up, new Vector2(24f, -29f), new Vector2(25f, 13f), Vector2.up);
            RectTransform folder = Box("Folder icon", card, ready ? new Color(.12f, .43f, .42f) : new Color(.45f, .31f, .15f));
            Pin(folder, Vector2.up, new Vector2(24f, -38f), new Vector2(56f, 40f), Vector2.up);
            TopText(card, name, 25, ink, 23f, 34f, 102f, 190f);
            TopText(card, detail, 18, muted, 62f, 28f, 102f, 190f);
            Text state = TopText(card, status, 19, ready ? cyan : amber, 38f, 34f);
            state.alignment = TextAnchor.MiddleRight;
        }

        private void Wallet()
        {
            if (walletReceipts && !game.Player.IsSeated)
            {
                Title("Recent payments");
                int shown = 0;
                for (int index = game.State.ledger.Count - 1; index >= 0 && shown < 3; index--)
                {
                    SignalDebtLedgerEvent entry = game.State.ledger[index];
                    if (entry.amount == 0) continue;
                    ReceiptRow(entry, null); shown++;
                }
                if (shown == 0) Body(content, "No payments yet.", muted);
                ActionButton("Back to balance", () => { walletReceipts = false; RenderModal(false); }, false);
                return;
            }
            Title("Your account");
            RectTransform balances = Row(126f);
            StatCard(balances, "AVAILABLE", game.State.balance + " cr", "Personal balance", cyan);
            StatCard(balances, "RENT DUE", game.State.rentDue + " cr", "Due day " + game.State.rentDeadlineDay + " · Today " + game.State.day,
                game.State.rentDue > 0 ? amber : cyan);
            int available = Mathf.Min(game.State.balance, game.State.rentDue);
            if (available > 0)
            {
                Body(content, "Pay rent", ink, 22, true);
                RectTransform payments = Row(49f);
                var amounts = new HashSet<int>();
                foreach (int requested in new[] { 50, 120, available })
                {
                    int amount = Mathf.Min(requested, available);
                    if (amounts.Add(amount)) RowButton(payments, "Pay " + amount + " credits", () => game.PayRent(amount), true);
                }
            }
            else Body(content, game.State.rentDue == 0 ? "Rent cleared." : "No funds available to pay rent.", game.State.rentDue == 0 ? cyan : muted, 21);
            if (!game.State.extensionUsed && game.State.rentDue > 0)
                ActionButton("Request 2 extra days", game.RequestExtension, false);
            else if (game.State.extensionUsed && game.State.rentDue > 0)
                Body(content, "Your 2-day extension is already included.", muted, 18);
            ActionButton("View receipts", () =>
            {
                if (game.Player.IsSeated)
                {
                    selectedHistoryId = null; historyReceiptsOnly = true; historyPage = 0; Open("History");
                }
                else { walletReceipts = true; RenderModal(false); }
            }, false);
        }

        private void StatCard(Transform parent, string caption, string value, string detail, Color accent)
        {
            RectTransform card = Box(caption, parent, panel);
            card.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TopText(card, caption, 16, muted, 14f, 26f);
            TopText(card, value, 36, accent, 45f, 48f);
            TopText(card, detail, 17, muted, 96f, 26f);
        }

        private void History()
        {
            if (!string.IsNullOrEmpty(selectedHistoryId))
            {
                SignalDebtLedgerEvent selected = game.State.ledger.Find(entry => entry.id == selectedHistoryId);
                if (selected != null)
                {
                    Title(EntryTitle(selected));
                    Card("DAY " + selected.day + (selected.amount != 0 ? "   ·   " + Amount(selected.amount) + " credits" : ""), selected.summary);
                    ActionButton("Back to history", () => { selectedHistoryId = null; RebuildHistory(); }, false);
                    return;
                }
                selectedHistoryId = null;
            }
            RectTransform filters = Row(40f);
            Button all = Button("All activity", filters, "All activity", () => { historyReceiptsOnly = false; historyPage = 0; RebuildHistory(); }, !historyReceiptsOnly);
            all.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Button receipts = Button("Receipts", filters, "Receipts", () => { historyReceiptsOnly = true; historyPage = 0; RebuildHistory(); }, historyReceiptsOnly);
            receipts.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var entries = game.State.ledger.FindAll(entry => !historyReceiptsOnly || entry.amount != 0);
            const int perPage = 4;
            int pages = Mathf.Max(1, (entries.Count + perPage - 1) / perPage);
            historyPage = Mathf.Clamp(historyPage, 0, pages - 1);
            int first = entries.Count - 1 - historyPage * perPage;
            if (entries.Count == 0) Body(content, historyReceiptsOnly ? "No receipts yet." : "Your activity will appear here.", muted);
            for (int offset = 0; offset < perPage && first - offset >= 0; offset++)
            {
                SignalDebtLedgerEvent entry = entries[first - offset];
                ReceiptRow(entry, () => { selectedHistoryId = entry.id; RebuildHistory(); });
            }
            RectTransform paging = Row(44f);
            RowButton(paging, "Previous", () => { historyPage--; RebuildHistory(); }, historyPage > 0);
            Text page = Body(paging, "Page " + (historyPage + 1) + " of " + pages, muted, 18);
            page.alignment = TextAnchor.MiddleCenter;
            page.GetComponent<LayoutElement>().flexibleWidth = 1f;
            RowButton(paging, "Next", () => { historyPage++; RebuildHistory(); }, historyPage + 1 < pages);
        }

        private void RebuildHistory()
        {
            scrollPositions["History"] = 1f;
            RenderApplication();
        }

        private void ReceiptRow(SignalDebtLedgerEvent entry, Action open)
        {
            RectTransform row;
            if (open == null) row = FixedBlock("Receipt", content, 64f, panel);
            else
            {
                Button button = Button("Read receipt", content, "", open, false);
                row = button.GetComponent<RectTransform>();
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 64f;
            }
            TopText(row, EntryTitle(entry), 21, ink, 8f, 29f, 16f, 180f);
            TopText(row, "Day " + entry.day, 16, muted, 37f, 23f, 16f, 180f);
            Text amount = TopText(row, entry.amount == 0 ? "View" : Amount(entry.amount) + " cr", 21, entry.amount > 0 ? cyan : muted, 20f, 31f);
            amount.alignment = TextAnchor.MiddleRight;
        }

        private static string Amount(int amount) { return (amount > 0 ? "+" : "") + amount; }

        private static string EntryTitle(SignalDebtLedgerEvent entry)
        {
            switch (entry.type)
            {
                case "job_accepted": return "Repair accepted";
                case "service_inspected": return "Folder inspected";
                case "service_connected": return "Connection restored";
                case "service_test_passed": return "Folder test passed";
                case "job_paid": return "Repair payment";
                case "rent_paid": return "Rent payment";
                case "rent_extension": return "Rent extension";
                case "rested": return "Rested";
                case "shop_purchase": return "Parts purchase";
                default: return "Saved activity";
            }
        }

        private void Rest()
        {
            Title("Rest until tomorrow?");
            Body(content, "Day " + game.State.day + " → " + (game.State.day + 1), cyan, 34);
            Body(content, "Rent: " + game.State.rentDue + " credits · Due day " + game.State.rentDeadlineDay, muted, 21);
            if (game.State.rentDue > 0 && game.State.day + 1 >= game.State.rentDeadlineDay)
                Body(content, "Your rent deadline will have arrived.", amber, 21);
            RectTransform actions = Row();
            RowButton(actions, "Rest", game.Rest, true);
            RowButton(actions, "Stay awake", CloseFromButton, true);
        }

        private void Pause()
        {
            Title("Paused");
            Card("CONTROLS", "WASD  Move     Mouse  Look     E  Interact\nCtrl  Crouch     Shift  Walk faster     Esc  Pause / resume");
            ActionButton("Return to game", CloseFromButton);
            ActionButton("Save / Load", game.OpenSaveMenu, false);
            ActionButton("Settings", game.OpenAiSettings, false);
            ActionButton("Main menu", game.ReturnToMainMenu, false);
            ActionButton("Quit game", game.QuitGame, false);
        }

        private RectTransform FixedBlock(string name, Transform parent, float height, Color color)
        {
            RectTransform block = Box(name, parent, color);
            block.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return block;
        }

        private Text TopText(Transform parent, string message, int size, Color color, float top, float height, float left = 18f, float right = 18f)
        {
            Text text = Label("Text", parent, message, size, color);
            text.rectTransform.anchorMin = Vector2.up; text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(left, -top - height);
            text.rectTransform.offsetMax = new Vector2(-right, -top);
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private void Title(string message) { Body(content, message, cyan, 25, true); }

        private void Card(string title, string body)
        {
            RectTransform card = Box(title, content, panel);
            var layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 14, 14); layout.spacing = 7f;
            layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
            Body(card, title, muted, 18, true); Body(card, body, ink, 23);
        }

        private Text Body(Transform parent, string text, Color? color = null, int size = 22, bool bold = false)
        {
            Text result = Label("Text", parent, text, size, color ?? ink);
            result.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            var layout = result.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = size + 9f;
            return result;
        }

        private void ActionButton(string label, Action action, bool primary = true, bool enabled = true)
        {
            Button button = Button(label, content, label, action, primary);
            button.interactable = enabled;
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 49f;
        }

        private RectTransform Row(float height = 52f)
        {
            RectTransform row = Rect("Actions", content);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f; layout.childForceExpandWidth = true; layout.childControlWidth = true; layout.childControlHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        private void RowButton(Transform row, string label, Action action, bool enabled)
        {
            var button = Button(label, row, label, action, true);
            button.interactable = enabled;
            button.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }

        private Button Button(string name, Transform parent, string text, Action callback, bool primary)
        {
            var rect = Box(name, parent, primary ? new Color(0.055f, 0.29f, 0.31f) : new Color(0.075f, 0.11f, 0.15f));
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.6f, 0.8f, 0.82f);
            colors.disabledColor = new Color(0.42f, 0.46f, 0.5f, 0.8f);
            button.colors = colors;
            button.onClick.AddListener(() => callback());
            Text label = Label("Label", rect, text, 21, ink);
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform, 10f, 4f);
            return button;
        }

        private Text Label(string name, Transform parent, string text, int size, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Text label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.supportRichText = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            return result;
        }

        private static RectTransform Box(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical); rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static void Pin(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private static void Clear(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false); Destroy(child);
            }
        }

        private void CloseFromButton()
        {
            if (IsPauseOpen) game.ResumeGame();
            else game.CloseUi();
        }
    }
}
