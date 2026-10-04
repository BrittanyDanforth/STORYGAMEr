using System;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalDebt
{
    public sealed class SignalDebtUI : MonoBehaviour
    {
        private SignalDebtBootstrap game;
        private Font font;
        private RectTransform desktop, content, navigation;
        private GameObject hud;
        private Text focus, objective, heading, subtitle, footer, toast;
        private GameObject focusPanel, toastPanel;
        private float toastUntil;
        private string page = "Inbox";
        private int hintLevel;
        private string lastLongMessage;
        private readonly Color background = new Color(0.022f, 0.035f, 0.055f, 0.99f);
        private readonly Color panel = new Color(0.042f, 0.065f, 0.092f, 1f);
        private readonly Color ink = new Color(0.87f, 0.91f, 0.96f);
        private readonly Color muted = new Color(0.55f, 0.64f, 0.72f);
        private readonly Color cyan = new Color(0.21f, 0.82f, 0.84f);
        private readonly Color amber = new Color(0.95f, 0.67f, 0.32f);

        public void Initialize(SignalDebtBootstrap owner)
        {
            game = owner;
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (ArgumentException) { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("Event System", typeof(EventSystem), typeof(StandaloneInputModule));
            BuildHud();
            BuildDesktop();
            BuildToast();
            Close();
        }

        private void BuildHud()
        {
            hud = Rect("World HUD", transform).gameObject;
            Stretch(hud.GetComponent<RectTransform>());
            objective = Label("Objective", hud.transform, "", 22, ink);
            Pin(objective.rectTransform, new Vector2(0f, 1f), new Vector2(42f, -35f), new Vector2(900f, 90f), new Vector2(0f, 1f));
            Text crosshair = Label("Crosshair", hud.transform, "·", 35, ink);
            crosshair.alignment = TextAnchor.MiddleCenter;
            Pin(crosshair.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f), new Vector2(0.5f, 0.5f));
            RectTransform bar = Box("Interaction prompt", hud.transform, new Color(0.02f, 0.04f, 0.055f, 0.92f));
            Pin(bar, new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(620f, 56f), new Vector2(0.5f, 0f));
            focusPanel = bar.gameObject;
            focus = Label("Prompt", bar, "", 24, cyan);
            Stretch(focus.rectTransform, 15f, 4f);
            focus.alignment = TextAnchor.MiddleCenter;
            Text controls = Label("Controls", hud.transform, "WASD  Move     Mouse  Look     E  Interact     Ctrl  Crouch     Shift  Walk faster     Esc  Pause", 17, muted);
            Pin(controls.rectTransform, new Vector2(0f, 0f), new Vector2(42f, 24f), new Vector2(1450f, 34f), new Vector2(0f, 0f));
        }

        private void BuildDesktop()
        {
            desktop = Box("Laptop desktop", transform, background);
            desktop.anchorMin = new Vector2(0.045f, 0.08f);
            desktop.anchorMax = new Vector2(0.955f, 0.91f);
            desktop.offsetMin = desktop.offsetMax = Vector2.zero;
            RectTransform header = Box("Title bar", desktop, panel);
            header.anchorMin = new Vector2(0f, 1f); header.anchorMax = Vector2.one;
            header.pivot = Vector2.up; header.sizeDelta = new Vector2(0f, 88f); header.anchoredPosition = Vector2.zero;
            heading = Label("Title", header, "ASTER /", 30, ink);
            Pin(heading.rectTransform, Vector2.up, new Vector2(28f, -14f), new Vector2(850f, 40f), Vector2.up);
            subtitle = Label("Status", header, "", 17, muted);
            Pin(subtitle.rectTransform, Vector2.up, new Vector2(30f, -55f), new Vector2(1050f, 28f), Vector2.up);
            Button close = Button("Close", header, "Leave  [Esc]", CloseFromButton, false);
            Pin(close.GetComponent<RectTransform>(), Vector2.one, new Vector2(-20f, -20f), new Vector2(170f, 46f), Vector2.one);

            navigation = Rect("Applications", desktop);
            navigation.anchorMin = new Vector2(0f, 0f); navigation.anchorMax = new Vector2(0.19f, 1f);
            navigation.offsetMin = new Vector2(20f, 60f); navigation.offsetMax = new Vector2(-10f, -110f);
            var navLayout = navigation.gameObject.AddComponent<VerticalLayoutGroup>();
            navLayout.spacing = 10f; navLayout.childControlHeight = true; navLayout.childControlWidth = true;
            navLayout.childForceExpandHeight = false;

            RectTransform scrollObject = Box("Workspace scroll", desktop, new Color(0.03f, 0.048f, 0.068f));
            scrollObject.anchorMin = new Vector2(0.21f, 0f); scrollObject.anchorMax = Vector2.one;
            scrollObject.offsetMin = new Vector2(0f, 62f); scrollObject.offsetMax = new Vector2(-24f, -110f);
            var scroll = scrollObject.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;
            RectTransform viewport = Rect("Viewport", scrollObject);
            Stretch(viewport, 16f, 16f);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Rect("Workspace content", viewport);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f); content.offsetMin = content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 4, 22); layout.spacing = 14f;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = viewport;
            footer = Label("Footer", desktop, "", 16, muted);
            footer.rectTransform.anchorMin = Vector2.zero; footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            footer.rectTransform.pivot = Vector2.zero;
            footer.rectTransform.offsetMin = new Vector2(28f, 16f); footer.rectTransform.offsetMax = new Vector2(-24f, 48f);
        }

        private void BuildToast()
        {
            RectTransform rectangle = Box("Notification", transform, new Color(0.028f, 0.065f, 0.085f, 0.98f));
            Pin(rectangle, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(1280f, 62f), new Vector2(0.5f, 1f));
            toastPanel = rectangle.gameObject;
            toast = Label("Notification text", rectangle, "", 20, cyan);
            Stretch(toast.rectTransform, 20f, 6f);
            toast.alignment = TextAnchor.MiddleCenter;
            toast.verticalOverflow = VerticalWrapMode.Truncate;
            toastPanel.SetActive(false);
        }

        private void Update()
        {
            if (game == null || game.State == null) return;
            if (toastPanel.activeSelf && Time.unscaledTime >= toastUntil) toastPanel.SetActive(false);
            objective.text = game.State.jobPaid
                ? "SIGNAL DEBT  /  FIRST JOB COMPLETE\n120 credits earned. Review EXODISS, pay some rent, or inspect your saved history."
                : game.State.jobAccepted
                    ? "SIGNAL DEBT  /  THE FIRST PAID REPAIR\nInspect the service, restore its connection, test the client's folder, then collect payment."
                    : "SIGNAL DEBT  /  A MESSAGE IN THE DARK\nFind the laptop. Southbank Copy Shop has a beginner repair that pays today.";
        }

        public void Open(string selectedPage)
        {
            page = selectedPage;
            desktop.gameObject.SetActive(true);
            hud.SetActive(false);
            Refresh();
        }

        public void Close()
        {
            desktop.gameObject.SetActive(false);
            hud.SetActive(true);
            SetFocus("");
        }

        public void SetFocus(string message)
        {
            focus.text = message;
            focusPanel.SetActive(!string.IsNullOrEmpty(message) && !game.Player.UiOpen);
        }

        public void ShowToast(string message)
        {
            message = message ?? "";
            if (message.Length > 220)
            {
                lastLongMessage = message;
                toast.text = message.Substring(0, 160) + "…  Full message in Inbox.";
                if (page == "Inbox" && desktop.gameObject.activeSelf) Refresh();
            }
            else toast.text = message;
            toastPanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            RectTransform rectangle = toastPanel.GetComponent<RectTransform>();
            rectangle.sizeDelta = new Vector2(1280f, Mathf.Clamp(toast.preferredHeight + 18f, 62f, 128f));
            toastPanel.transform.SetAsLastSibling();
            toastUntil = Time.unscaledTime + Mathf.Clamp(message.Length / 18f, 4f, 10f);
        }

        public void Refresh()
        {
            if (content == null || !desktop.gameObject.activeSelf) return;
            Clear(content); Clear(navigation);
            heading.text = "ASTER / " + page.ToUpperInvariant();
            subtitle.text = "Day " + game.State.day + "    ·    " + game.State.balance + " credits available    ·    " + game.State.rentDue + " overdue";
            footer.text = game.SaveStatus + "   /   Offline simulation   /   Scroll for more   /   Esc returns to the apartment";
            if (game.Player.IsSeated)
            {
                Nav("Inbox"); Nav("Service"); Nav("EXODISS"); Nav("History");
            }
            else
            {
                Nav(page == "Rest" ? "Rest" : page == "Pause" ? "Pause" : "EXODISS");
                Body(navigation, "Visit the desk to use the laptop's other applications.", muted, 18);
            }
            switch (page)
            {
                case "Service": Service(); break;
                case "EXODISS": Wallet(); break;
                case "History": History(); break;
                case "Rest": Rest(); break;
                case "Pause": Pause(); break;
                default: Inbox(); break;
            }
            content.anchoredPosition = Vector2.zero;
            Canvas.ForceUpdateCanvases();
        }

        private void Nav(string name)
        {
            Button button = Button(name, navigation, name, () => Open(name), page == name);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
        }

        private void Inbox()
        {
            if (!string.IsNullOrEmpty(lastLongMessage)) Card("LATEST MESSAGE", lastLongMessage);
            Title("01 / NEW MESSAGE");
            Body(content, "Southbank Copy Shop", cyan, 27);
            Body(content, "shop computer wont open the order folder. can pay today if u fix it.\n\nits the shared work folder. job folder only, dont touch the other stuff.");
            Card("PAID BEGINNER REPAIR", "Fee: 120 credits · Shop PC · Scope: Order Folder service\nA guided status check, one service connection, and a real test. No advanced tools required.");
            if (!game.State.jobAccepted) ActionButton("Accept the 120-credit repair", game.AcceptJob);
            else ActionButton(game.State.jobPaid ? "Review the completed repair" : "Resume the repair", () => Open("Service"));
            if (game.State.jobPaid)
            {
                Title("02 / FOLLOW-UP");
                Body(content, "Client: yeah its still working. appreciate it. payment sent.");
                Body(content, "A later chapter opens more jobs and questionable contacts. This playable slice ends with your first earned payment, a saved client memory, and a rent decision.", muted, 20);
                ActionButton("Ask the client how it looks", () =>
                {
                    game.ShowToast("CLIENT · order folder is working. thanks for checking.");
                    game.SendMessage("BeginClientChat", "Is the order folder still working?", SendMessageOptions.DontRequireReceiver);
                }, false);
            }
            ActionButton("Toggle optional local dialogue relay", () =>
            {
                game.ShowToast("Optional relay toggled if its adapter is installed. Authored dialogue remains available.");
                game.SendMessage("ToggleRelay", SendMessageOptions.DontRequireReceiver);
            }, false);
        }

        private void Service()
        {
            Title("SHOP PC / AUTHORIZED SESSION");
            if (!game.State.jobAccepted)
            {
                Body(content, "Accept the client's job before opening their repair workspace.");
                ActionButton("Read the job", () => Open("Inbox"));
                return;
            }
            Body(content, "A folder needs the service named in its status panel. Read the requirement, connect the matching service, then test the folder.", ink, 22);
            Card("1  INSPECT", game.State.serviceInspected
                ? "Order Folder · Required service: ORDER INDEX\nCurrent connection: " + (game.State.serviceConnected ? "Order Index / connected" : "none / unavailable")
                : "Shop PC is online. The shared order folder will not open. Inspect its status to learn why.");
            ActionButton(game.State.serviceInspected ? "Status inspected" : "Inspect Order Folder status", game.InspectService, false, !game.State.serviceInspected);
            if (game.State.serviceInspected)
            {
                Title("2  CONNECT THE MATCHING SERVICE");
                Body(content, game.State.serviceConnected ? "ORDER FOLDER  →  ORDER INDEX   /   connected" : "ORDER FOLDER  →  [ no service ]", game.State.serviceConnected ? cyan : amber, 24);
                RectTransform row = Row();
                RowButton(row, "Order Index", () => game.ConnectService(true), !game.State.serviceConnected);
                RowButton(row, "Print Queue", () => game.ConnectService(false), !game.State.serviceConnected);
                RowButton(row, "Asset Catalog", () => game.ConnectService(false), !game.State.serviceConnected);
            }
            Title("3  VERIFY AND CLOSE THE JOB");
            Body(content, game.State.serviceTestPassed ? "Test passed. The client opened today's orders and resumed work." : "A connection is only a proposed fix until the client's folder passes its test.", game.State.serviceTestPassed ? cyan : ink);
            RectTransform finish = Row();
            RowButton(finish, game.State.serviceTestPassed ? "Test passed" : "Run folder test", game.TestService, !game.State.serviceTestPassed);
            RowButton(finish, game.State.jobPaid ? "120 credits paid" : "Complete job · +120", game.ClaimPayment, game.State.serviceTestPassed && !game.State.jobPaid);
            ActionButton("Need a hint?", () =>
            {
                hintLevel = Mathf.Min(hintLevel + 1, 3);
                game.ShowToast(hintLevel == 1 ? "Read the required service in the Order Folder status. The names need to match."
                    : hintLevel == 2 ? "The correct resource is one of the three service buttons. Look for the word Index."
                    : "Inspect status, choose Order Index, then Run folder test. Finally complete the job to receive 120 credits.");
            }, false);
            ActionButton("PRIVATE WORKSPACE · inspect boundary", () => game.ShowToast("Outside the client's repair scope. This starter has no access, copying, or implant tools here."), false);
        }

        private void Wallet()
        {
            Title("EXODISS / WALLET");
            Body(content, game.State.balance + " CREDITS AVAILABLE", cyan, 36);
            Body(content, "All funds and transactions here belong to the fictional game economy.", muted, 18);
            Card("OVERDUE RENT", game.State.rentDue + " credits outstanding\nDeadline: day " + game.State.rentDeadlineDay + " · Current day: " + game.State.day
                + (game.State.day >= game.State.rentDeadlineDay && game.State.rentDue > 0 ? "\nThe deadline has arrived. Review an arrangement; the prototype does not evict you." : "\nPartial payments reduce the balance. They do not silently change the deadline."));
            Body(content, "Choose a stated payment. The amount is deducted once and receives a ledger entry. The available amount is capped at your balance and the debt.", muted, 20);
            RectTransform row = Row();
            RowButton(row, "Pay up to 50", () => game.PayRent(50), game.State.balance > 0 && game.State.rentDue > 0);
            RowButton(row, "Pay up to 120", () => game.PayRent(120), game.State.balance > 0 && game.State.rentDue > 0);
            RowButton(row, "Pay available", () => game.PayRent(game.State.balance), game.State.balance > 0 && game.State.rentDue > 0);
            ActionButton(game.State.extensionUsed ? "Two-day extension already used" : "Request one two-day hardship extension", game.RequestExtension, false, !game.State.extensionUsed && game.State.rentDue > 0);
            Title("RECENT TRANSACTIONS");
            int count = 0;
            for (int index = game.State.ledger.Count - 1; index >= 0 && count < 7; index--)
            {
                var entry = game.State.ledger[index];
                if (entry.amount == 0) continue;
                Body(content, "Day " + entry.day + "   " + (entry.amount > 0 ? "+" : "") + entry.amount + "   /   " + entry.summary, entry.amount > 0 ? cyan : ink, 20);
                count++;
            }
            if (count == 0) Body(content, "No transactions yet. Starting balance: 80 credits.", muted);
        }

        private void History()
        {
            Title("COMMITTED HISTORY");
            Body(content, "These entries record actions that actually changed the game. Dialogue does not grant money or create facts.", muted, 20);
            for (int index = game.State.ledger.Count - 1; index >= 0; index--)
            {
                var entry = game.State.ledger[index];
                Body(content, "DAY " + entry.day + " · " + entry.type + "\n" + entry.summary, ink, 20);
            }
            if (game.State.ledger.Count == 0) Body(content, "Your first job will start this history.");
            Title("CLIENT MEMORY");
            foreach (string memory in game.State.clientMemories) Body(content, memory, cyan, 20);
        }

        private void Rest()
        {
            Title("REST UNTIL TOMORROW");
            Body(content, "Day " + game.State.day + " → Day " + (game.State.day + 1), cyan, 32);
            Body(content, "Outstanding rent: " + game.State.rentDue + " credits. Deadline: day " + game.State.rentDeadlineDay + ". Your repair progress, money, receipts, and client memory remain saved.");
            if (game.State.rentDue > 0 && game.State.day + 1 >= game.State.rentDeadlineDay)
                Body(content, "Tomorrow reaches or passes the rent deadline. You can review the bill and use the one-time extension if it is still available.", amber);
            ActionButton("Rest · advance one day", game.Rest);
            ActionButton("Stay awake", CloseFromButton, false);
        }

        private void Pause()
        {
            Title("SIGNAL DEBT / APARTMENT PROTOTYPE");
            Body(content, "WASD move · Mouse look · E interact · Ctrl/C crouch · Shift move faster\nAt the laptop, use the mouse. Esc closes the interface and stands up.");
            Body(content, "Playable scope: apartment interaction, visible body, first repair, payment, rent decisions, and local save history. A larger city, criminal missions, and detailed hardware assembly are future work.", muted, 20);
            ActionButton("Return to the apartment", CloseFromButton);
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

        private RectTransform Row()
        {
            RectTransform row = Rect("Actions", content);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f; layout.childForceExpandWidth = true; layout.childControlWidth = true; layout.childControlHeight = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;
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

        private void CloseFromButton() { game.CloseUi(); }
    }
}
