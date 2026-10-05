using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalDebt
{
    /// <summary>Owned settings overlay; the caller keeps control of pause/menu state.</summary>
    public sealed class SignalDebtAiSettings : MonoBehaviour
    {
        private static readonly Color Ink = new Color(0.89f, 0.93f, 0.96f);
        private static readonly Color Muted = new Color(0.61f, 0.70f, 0.76f);
        private static readonly Color Cyan = new Color(0.35f, 0.87f, 0.81f);
        private static readonly Color Amber = new Color(0.97f, 0.72f, 0.40f);
        private SignalDebtBootstrap game;
        private SignalDebtNpcClient client;
        private Action closed;
        private Font font;
        private InputField keyInput;
        private Toggle remember;
        private Button connect, remove, back;
        private Text storageStatus, result;
        private GameObject previousSelection;
        private bool keyAvailable, busy, closing, notified;
        private int requestGeneration;

        public static GameObject Show(SignalDebtBootstrap owner, Action onClosed)
        {
            var root = new GameObject("ASTER AI settings", typeof(RectTransform));
            var settings = root.AddComponent<SignalDebtAiSettings>();
            settings.game = owner;
            settings.closed = onClosed;
            settings.Build();
            return root;
        }

        private void Build()
        {
            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch (ArgumentException) { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("AI settings input", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
            if (EventSystem.current != null) previousSelection = EventSystem.current.currentSelectedGameObject;

            RectTransform shade = Box("Modal backdrop", transform, new Color(0.006f, 0.015f, 0.026f, 0.92f));
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
            shade.offsetMin = shade.offsetMax = Vector2.zero;
            RectTransform panel = Box("AI settings panel", shade, new Color(0.039f, 0.061f, 0.082f));
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero; panel.sizeDelta = new Vector2(940f, 690f);
            RectTransform accent = Box("Accent", panel, Cyan);
            Place(accent, 0f, 0f, 940f, 4f);
            Text brand = Label("Section", panel, "ASTER  /  SETTINGS", 17, Cyan);
            Place(brand.rectTransform, 38f, 30f, 864f, 28f);
            Text title = Label("Title", panel, "AI dialogue", 36, Ink);
            title.fontStyle = FontStyle.Bold;
            Place(title.rectTransform, 38f, 69f, 864f, 49f);
            Text description = Label("Description", panel,
                "Let clients answer in their own words.\nYou can keep playing without an API key.", 21, Muted);
            Place(description.rectTransform, 38f, 130f, 864f, 56f);
            storageStatus = Label("Key storage status", panel, "", 18, Muted);
            Place(storageStatus.rectTransform, 38f, 199f, 864f, 46f);
            Text keyLabel = Label("Key label", panel, "OpenAI API key", 20, Ink);
            Place(keyLabel.rectTransform, 38f, 251f, 864f, 29f);
            BuildKeyInput(panel);
            BuildRememberToggle(panel);
            Text privacy = Label("Local file note", panel,
                "This file contains your key. Keep it private.", 17, Muted);
            Place(privacy.rectTransform, 76f, 407f, 826f, 30f);
            result = Label("Connection result", panel, "", 20, Muted);
            Place(result.rectTransform, 38f, 461f, 864f, 58f);
            connect = MakeButton("Connect", panel, new Vector2(38f, 542f), Connect, true);
            remove = MakeButton("Remove saved key", panel, new Vector2(330f, 542f), RemoveKey, false);
            back = MakeButton("Back  [Esc]", panel, new Vector2(622f, 542f), Close, false);
            Text cost = Label("Connection test cost", panel,
                "Connect sends one small AI request. Provider charges may apply.", 17, Muted);
            Place(cost.rectTransform, 38f, 622f, 864f, 36f);
            SetNavigation(keyInput, back, remember, keyInput, keyInput);
            SetNavigation(remember, keyInput, connect, keyInput, connect);
            SetNavigation(connect, remember, keyInput, back, remove);
            SetNavigation(remove, remember, keyInput, connect, back);
            SetNavigation(back, remember, keyInput, remove, connect);
            RefreshStorage();
            RefreshControls();
            Canvas.ForceUpdateCanvases();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(keyInput.gameObject);
            keyInput.ActivateInputField();
        }

        private void BuildKeyInput(Transform panel)
        {
            RectTransform field = Box("API key input", panel, new Color(0.016f, 0.031f, 0.045f));
            Place(field, 38f, 286f, 864f, 62f);
            field.gameObject.AddComponent<RectMask2D>();
            Text value = Label("Masked key", field, "", 24, Ink);
            Stretch(value.rectTransform, 18f, 10f);
            value.alignment = TextAnchor.MiddleLeft;
            value.supportRichText = false;
            Text placeholder = Label("Key placeholder", field, "Paste a key to connect or replace it", 21, Muted);
            Stretch(placeholder.rectTransform, 18f, 10f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            keyInput = field.gameObject.AddComponent<InputField>();
            keyInput.textComponent = value;
            keyInput.placeholder = placeholder;
            keyInput.targetGraphic = field.GetComponent<Image>();
            keyInput.contentType = InputField.ContentType.Password;
            keyInput.inputType = InputField.InputType.Password;
            keyInput.lineType = InputField.LineType.SingleLine;
            keyInput.characterLimit = 1024;
            keyInput.asteriskChar = '•';
            keyInput.caretColor = Cyan;
            keyInput.customCaretColor = true;
            keyInput.selectionColor = new Color(0.15f, 0.43f, 0.45f, 0.8f);
            keyInput.text = ""; // Never load a stored credential into the editable field.
            keyInput.onValueChanged.AddListener(_ => RefreshControls());
        }

        private void BuildRememberToggle(Transform panel)
        {
            RectTransform row = Box("Save key choice", panel, Color.clear);
            Place(row, 38f, 367f, 864f, 35f);
            RectTransform square = Box("Checkbox", row, new Color(0.095f, 0.15f, 0.18f));
            Place(square, 0f, 3f, 27f, 27f);
            RectTransform check = Box("Check", square, Cyan);
            Place(check, 6f, 6f, 15f, 15f);
            check.GetComponent<Image>().raycastTarget = false;
            Text label = Label("Remember label", row, "Save key in UserData folder", 20, Ink);
            Place(label.rectTransform, 38f, 0f, 826f, 35f);
            label.alignment = TextAnchor.MiddleLeft;
            remember = row.gameObject.AddComponent<Toggle>();
            remember.targetGraphic = square.GetComponent<Image>();
            remember.graphic = check.GetComponent<Image>();
            remember.isOn = false;
            remember.toggleTransition = Toggle.ToggleTransition.None;
        }

        private void Connect()
        {
            if (closing || busy) return;
            string entered = keyInput.text.Trim();
            if (entered.Length > 0)
            {
                string message;
                bool accepted = remember.isOn
                    ? SignalDebtApiKeyStore.TrySave(entered, out message)
                    : SignalDebtApiKeyStore.SetSessionKey(entered, out message);
                if (!accepted)
                {
                    ShowResult(string.IsNullOrEmpty(message) ? "The key could not be saved. Try again." : message, false);
                    return;
                }
                ClearKeyInput();
            }
            entered = null;
            RefreshStorage();
            if (!keyAvailable)
            {
                ShowResult("Paste an API key before connecting.", false);
                RefreshControls();
                return;
            }
            client = game != null ? game.GetComponent<SignalDebtNpcClient>() : null;
            if (client == null)
            {
                ShowResult("AI connection is unavailable right now. Your key setting is kept.", false);
                RefreshControls();
                return;
            }
            busy = true;
            int generation = ++requestGeneration;
            result.text = "Testing connection…"; result.color = Muted;
            RefreshControls();
            // The transport returns fixed friendly statuses, never response bodies or keys.
            client.TestConnection((success, message) =>
            {
                if (this == null || closing || !isActiveAndEnabled || generation != requestGeneration) return;
                busy = false;
                ShowResult(string.IsNullOrEmpty(message)
                    ? success ? "Connected. AI dialogue is ready." : "Connection failed. Try again."
                    : message, success);
                RefreshStorage();
                RefreshControls();
            });
        }

        private void RemoveKey()
        {
            if (closing) return;
            CancelTest();
            ClearKeyInput();
            string message;
            bool removed = SignalDebtApiKeyStore.TryDelete(out message);
            RefreshStorage();
            ShowResult(removed ? keyAvailable
                ? "Saved key removed. Another key is ready for this session." : "Saved and session keys removed."
                : string.IsNullOrEmpty(message) ? "The saved key could not be removed. Try again." : message, removed);
            RefreshControls();
        }

        private void RefreshStorage()
        {
            keyAvailable = SignalDebtApiKeyStore.TryLoad(out _);
            storageStatus.text = keyAvailable
                ? SignalDebtApiKeyStore.CurrentKeyIsRemembered ? "Saved in UserData" : "Ready for this session"
                : "No key set";
            storageStatus.color = keyAvailable ? Cyan : Muted;
        }

        private void RefreshControls()
        {
            if (connect == null || keyInput == null || remember == null || remove == null) return;
            connect.interactable = !closing && !busy && (keyAvailable || !string.IsNullOrWhiteSpace(keyInput.text));
            keyInput.interactable = remember.interactable = !closing && !busy;
            // Removal also lets the player clear a malformed settings file whose key cannot be loaded.
            remove.interactable = !closing;
            // UGUI clears selection when Connect/input becomes disabled for a
            // request. Keep keyboard navigation available while it is pending.
            if (!closing && back != null && EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == null)
                back.Select();
        }

        private void ShowResult(string message, bool success)
        { result.text = message; result.color = success ? Cyan : Amber; }

        private void CancelTest()
        {
            requestGeneration++;
            bool wasBusy = busy;
            busy = false;
            if (wasBusy && client != null) client.CancelConnectionTest();
        }

        private void ClearKeyInput()
        {
            if (keyInput == null) return;
            keyInput.SetTextWithoutNotify("");
            keyInput.DeactivateInputField();
        }

        // Bootstrap routes Escape here, so one keypress cannot also close its parent menu.
        public void Close()
        {
            if (closing) return;
            closing = true;
            CancelTest();
            ClearKeyInput();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void OnDisable()
        {
            CancelTest();
            ClearKeyInput();
        }

        private void OnDestroy()
        {
            closing = true;
            CancelTest();
            ClearKeyInput();
            EventSystem events = EventSystem.current;
            if (events != null && (events.currentSelectedGameObject == null
                || events.currentSelectedGameObject.transform.IsChildOf(transform)))
                events.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                    ? previousSelection : null);
            if (notified) return;
            notified = true;
            Action callback = closed;
            closed = null;
            callback?.Invoke();
        }

        private Button MakeButton(string text, Transform parent, Vector2 position, Action action, bool primary)
        {
            RectTransform rect = Box(text, parent, primary ? Cyan : new Color(0.065f, 0.11f, 0.15f));
            Place(rect, position.x, position.y, 280f, 58f);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.18f, 1.18f);
            colors.pressedColor = new Color(0.6f, 0.8f, 0.82f);
            colors.disabledColor = new Color(0.43f, 0.48f, 0.5f, 0.65f);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            Text label = Label("Label", rect, text, 20, primary ? new Color(0.015f, 0.055f, 0.07f) : Ink);
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
            Stretch(label.rectTransform, 12f, 5f);
            return button;
        }

        private static void SetNavigation(Selectable target, Selectable up, Selectable down,
            Selectable left, Selectable right)
        {
            target.navigation = new Navigation { mode = Navigation.Mode.Explicit,
                selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
        }

        private Text Label(string name, Transform parent, string text, int size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            Text label = item.GetComponent<Text>();
            label.font = font; label.text = text; label.fontSize = size; label.color = color;
            label.supportRichText = false; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private static RectTransform Box(string name, Transform parent, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            item.GetComponent<Image>().color = color;
            return item.GetComponent<RectTransform>();
        }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect, float horizontal, float vertical)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical); rect.offsetMax = new Vector2(-horizontal, -vertical);
        }
    }
}
