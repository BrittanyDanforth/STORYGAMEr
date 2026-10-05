using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SignalDebt
{
    /// <summary>Bounded checkpoint browser. Bootstrap owns persistence, pause and campaign activation.</summary>
    public sealed class SignalDebtSaveMenu : MonoBehaviour
    {
        private const int PageSize = 3;
        private static readonly Color Ink = new Color(.89f, .94f, .95f);
        private static readonly Color Muted = new Color(.60f, .70f, .75f);
        private static readonly Color Accent = new Color(.35f, .87f, .81f);
        private static readonly Color Amber = new Color(.97f, .72f, .40f);
        private SignalDebtBootstrap game;
        private Action closed;
        private Font font;
        private RectTransform cards;
        private CanvasGroup navigation;
        private GameObject confirmation, previousSelection;
        private Text summary, status, pageLabel, confirmationText, confirmationDetails, confirmationProgress, confirmationStatus;
        private InputField checkpointName;
        private Button save, previous, next, refresh, back, confirmLoad, cancelLoad, selectedLoad;
        private readonly List<Button> cardButtons = new List<Button>();
        private readonly List<Selectable> keyboardButtons = new List<Selectable>();
        private SignalDebtSaveLibrary.Entry pendingEntry;
        private int page, total;
        private bool busy, closing, loading, notified, restoreSelection;

        public static GameObject Show(SignalDebtBootstrap owner, Action onClosed)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var root = new GameObject("Save and load menu", typeof(RectTransform));
            var menu = root.AddComponent<SignalDebtSaveMenu>();
            menu.game = owner;
            menu.closed = onClosed;
            menu.Build();
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
                var events = new GameObject("Save menu input", typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }
            if (EventSystem.current != null) previousSelection = EventSystem.current.currentSelectedGameObject;

            RectTransform shade = Box("Save menu backdrop", transform, new Color(.006f, .015f, .026f, .94f));
            Stretch(shade);
            RectTransform panel = Box("Save menu panel", shade, new Color(.039f, .061f, .082f));
            Center(panel, 1100f, 780f);
            Place(Box("Accent", panel, Accent), 0f, 0f, 1100f, 4f);
            Put("Section", panel, "ASTER  /  CAMPAIGN", 17, Accent, 32f, 24f, 1036f, 26f);
            Put("Title", panel, "Save / Load", 38, Ink, 32f, 62f, 680f, 49f, true);
            if (!game.CanSaveCheckpoint)
                Put("Description", panel, "Checkpoints and previous campaigns", 21, Muted, 32f, 120f, 1036f, 32f);
            summary = Put("Current campaign", panel, "", 19, Muted, 32f, 171f, 1036f, 32f);

            RectTransform controls = Rect("Save navigation", panel);
            Stretch(controls);
            navigation = controls.gameObject.AddComponent<CanvasGroup>();
            if (game.CanSaveCheckpoint) BuildNameInput(controls);
            save = MakeButton("Save new checkpoint", controls, SaveCheckpoint, true, 760f, 118f, 308f, 42f);
            if (!game.CanSaveCheckpoint)
            {
                save.gameObject.SetActive(false);
                Put("Checkpoint hint", panel, "Make a checkpoint from the\npause menu while playing.", 18, Muted,
                    760f, 76f, 308f, 64f);
            }
            cards = Rect("Saved campaign cards", controls);
            Place(cards, 32f, 215f, 1036f, 390f);
            status = Put("Save result", panel, "", 18, Muted, 32f, 615f, 1036f, 52f);
            previous = MakeButton("Previous", controls, () => ChangePage(-1), false, 32f, 686f, 160f, 54f);
            pageLabel = Put("Page", panel, "", 19, Muted, 208f, 686f, 190f, 54f);
            pageLabel.alignment = TextAnchor.MiddleCenter;
            next = MakeButton("Next", controls, () => ChangePage(1), false, 414f, 686f, 160f, 54f);
            refresh = MakeButton("Refresh", controls, RefreshList, false, 598f, 686f, 170f, 54f);
            back = MakeButton("Back  [Esc]", controls, Close, false, 792f, 686f, 276f, 54f);
            Put("Keyboard hint", panel, "Arrow keys / Tab  Select     Enter  Confirm     Esc  Back", 16, Muted,
                32f, 746f, 1036f, 24f);
            BuildConfirmation(shade);
            RefreshPage();
            Select(DefaultButton());
        }

        private void BuildNameInput(Transform parent)
        {
            RectTransform field = Box("Optional checkpoint name", parent, new Color(.016f, .031f, .045f));
            Place(field, 32f, 118f, 690f, 42f);
            field.gameObject.AddComponent<RectMask2D>();
            Text value = Put("Checkpoint name", field, "", 20, Ink, 14f, 5f, 662f, 32f);
            value.alignment = TextAnchor.MiddleLeft;
            Text placeholder = Put("Name placeholder", field, "Checkpoint name (optional)", 20, Muted, 14f, 5f, 662f, 32f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            checkpointName = field.gameObject.AddComponent<InputField>();
            checkpointName.textComponent = value;
            checkpointName.placeholder = placeholder;
            checkpointName.targetGraphic = field.GetComponent<Image>();
            checkpointName.contentType = InputField.ContentType.Standard;
            checkpointName.lineType = InputField.LineType.SingleLine;
            checkpointName.characterLimit = 48;
            checkpointName.caretColor = Accent;
            checkpointName.customCaretColor = true;
            checkpointName.selectionColor = new Color(.15f, .43f, .45f, .8f);
            // Saving is an explicit button action; editing, Enter and blur never write a checkpoint.
        }

        private void BuildConfirmation(Transform parent)
        {
            RectTransform blocker = Box("Load confirmation", parent, new Color(.006f, .014f, .024f, .96f));
            Stretch(blocker);
            confirmation = blocker.gameObject;
            RectTransform panel = Box("Load confirmation panel", blocker, new Color(.047f, .076f, .098f));
            Center(panel, 880f, 440f);
            Put("Load title", panel, "Load this save?", 33, Ink, 34f, 30f, 812f, 48f, true);
            confirmationText = Put("Selected save", panel, "", 21, Ink, 34f, 92f, 812f, 30f, true);
            confirmationDetails = Put("Selected save details", panel, "", 19, Ink, 34f, 126f, 812f, 27f);
            confirmationProgress = Put("Selected save progress", panel, "", 19, Ink, 34f, 157f, 812f, 27f);
            Put("Preservation notice", panel,
                "Your current campaign will be preserved before loading.\nThe selected save will stay available.",
                20, Muted, 34f, 197f, 812f, 61f);
            confirmationStatus = Put("Load result", panel, "", 18, Amber, 34f, 271f, 812f, 70f);
            cancelLoad = MakeButton("Keep playing / Back", panel, DismissConfirmation, false, 34f, 354f, 386f, 56f);
            confirmLoad = MakeButton("Load save", panel, ConfirmLoad, true, 438f, 354f, 408f, 56f);
            confirmation.SetActive(false);
        }

        private void RefreshPage(string message = null, bool success = true)
        {
            ClearCards();
            SignalDebtSaveLibrary.Page result = game.SaveLibrary.List(page, PageSize);
            total = result.Total;
            int lastPage = Mathf.Max(0, (total - 1) / PageSize);
            if (page > lastPage)
            {
                page = lastPage;
                result = game.SaveLibrary.List(page, PageSize);
                total = result.Total;
            }
            SignalDebtSaveData current = game.State;
            summary.text = game.CanSaveCheckpoint && current != null
                ? "Current campaign  ·  Day " + current.day + "  ·  " + current.balance + " credits"
                : "Choose a saved campaign to continue.";
            int index = 0;
            foreach (SignalDebtSaveLibrary.Entry entry in result.Entries)
            {
                if (index >= PageSize) break;
                BuildCard(entry, index++);
            }
            if (index == 0)
            {
                RectTransform empty = Box("No saved campaigns", cards, new Color(.027f, .047f, .063f));
                Place(empty, 0f, 0f, 1036f, 256f);
                Put("Empty title", empty, "No saves here yet", 27, Ink, 28f, 57f, 980f, 44f, true);
                Put("Empty explanation", empty, game.CanSaveCheckpoint
                    ? "Save a checkpoint to keep this moment available."
                    : "Start a new game, then use Save / Load from the pause menu.",
                    21, Muted, 28f, 114f, 980f, 65f);
            }
            previous.interactable = page > 0;
            next.interactable = (page + 1) * PageSize < total;
            save.interactable = game.CanSaveCheckpoint;
            pageLabel.text = "Page " + (page + 1) + " of " + Mathf.Max(1, (total + PageSize - 1) / PageSize);
            string error = result.Error;
            bool actionFailed = !success && !string.IsNullOrWhiteSpace(message);
            status.text = Limited(actionFailed ? message : !string.IsNullOrWhiteSpace(error) ? error : message, 260);
            status.color = !string.IsNullOrWhiteSpace(error) || !success ? Amber : Accent;
            RebuildNavigation();
        }

        private void BuildCard(SignalDebtSaveLibrary.Entry entry, int index)
        {
            RectTransform card = Box("Save card " + (index + 1), cards, new Color(.027f, .047f, .063f));
            Place(card, 0f, index * 134f, 1036f, 122f);
            string kind = entry.Kind == SignalDebtSaveLibrary.EntryKind.Archive ? "PREVIOUS CAMPAIGN"
                : entry.Kind == SignalDebtSaveLibrary.EntryKind.Checkpoint ? "CHECKPOINT" : "CURRENT SAVE";
            string recovery = RecoveryLabel(entry.Status);
            if (entry.CanLoad && recovery.Length > 0) kind += "  ·  " + recovery;
            Put("Save kind", card, kind, 14, !entry.CanLoad ? Muted : recovery.Length > 0 ? Amber : Accent,
                20f, 10f, 754f, 20f);
            Text label = Put("Save label", card, "", 22, Ink, 20f, 32f, 754f, 30f, true);
            SetSingleLine(label, entry.Label);
            if (entry.CanLoad)
            {
                Put("Save details", card, "Day " + entry.Day + "  ·  " + entry.Balance + " credits  ·  " + SavedDate(entry.SavedUtc),
                    17, Muted, 20f, 67f, 754f, 24f);
                Put("Save progress", card, Limited(entry.ProgressSummary, 100), 17, Muted, 20f, 94f, 754f, 24f);
            }
            else Put("Save unavailable reason", card, Limited(entry.Status, 160), 17, Amber, 20f, 67f, 754f, 49f);
            Button load = null;
            load = MakeButton(entry.CanLoad ? "Load" : "Unavailable", card,
                () => RequestLoad(entry, load), false, 810f, 35f, 206f, 54f);
            load.interactable = entry.CanLoad;
            cardButtons.Add(load);
        }

        private static string SavedDate(DateTime utc)
        {
            return utc == DateTime.MinValue ? "Date unavailable"
                : utc.ToLocalTime().ToString("dd MMM yyyy  HH:mm:ss", CultureInfo.InvariantCulture) + " local";
        }

        private static string RecoveryLabel(string value)
        {
            if (value == "Recovered last good backup") return "RECOVERED BACKUP";
            if (value == "Recovered interrupted save") return "RECOVERED SAVE";
            return "";
        }

        private void SaveCheckpoint()
        {
            if (busy || closing || loading || !game.CanSaveCheckpoint) return;
            busy = true;
            bool success = game.SaveCheckpoint(checkpointName != null ? checkpointName.text : "", out string message);
            busy = false;
            if (success)
            {
                page = 0;
                if (checkpointName != null) checkpointName.SetTextWithoutNotify("");
            }
            RefreshPage(message, success);
            Select(save.interactable ? save : DefaultButton());
        }

        private void ChangePage(int direction)
        {
            if (busy || closing || loading) return;
            page = Mathf.Clamp(page + direction, 0, Mathf.Max(0, (total - 1) / PageSize));
            RefreshPage();
            Select(cardButtons.Find(item => item.interactable) ?? DefaultButton());
        }

        private void RefreshList()
        {
            if (busy || closing || loading) return;
            RefreshPage();
            Select(refresh);
        }

        private void RequestLoad(SignalDebtSaveLibrary.Entry entry, Button source)
        {
            if (busy || closing || loading || !entry.CanLoad) return;
            pendingEntry = entry;
            selectedLoad = source;
            SetSingleLine(confirmationText, entry.Label);
            confirmationDetails.text = "Day " + entry.Day + "  ·  " + entry.Balance + " credits  ·  " + SavedDate(entry.SavedUtc);
            confirmationProgress.text = Limited(entry.ProgressSummary, 100);
            confirmationStatus.text = RecoveryLabel(entry.Status).Length > 0
                ? "This is recovered progress. Check the day and job status before loading." : "";
            cancelLoad.GetComponentInChildren<Text>().text = game.CanSaveCheckpoint ? "Keep playing" : "Back";
            navigation.interactable = navigation.blocksRaycasts = false;
            confirmation.SetActive(true);
            RebuildNavigation();
            Select(cancelLoad);
        }

        private void ConfirmLoad()
        {
            if (busy || closing || loading || pendingEntry == null) return;
            busy = true;
            bool success = game.LoadSavedGame(pendingEntry, out string message);
            busy = false;
            if (success)
            {
                // Activation is queued for the next frame. Keep the pointer blocker alive
                // while the previous campaign is destroyed, without restoring its selection.
                loading = true;
                restoreSelection = false;
                confirmationStatus.text = "Loading…";
                confirmationStatus.color = Accent;
                confirmLoad.interactable = cancelLoad.interactable = false;
                keyboardButtons.Clear();
                Select(null);
                return;
            }
            confirmationStatus.text = Limited(message, 260);
            confirmationStatus.color = Amber;
            Select(cancelLoad);
        }

        private void DismissConfirmation()
        {
            if (busy || closing || loading) return;
            pendingEntry = null;
            confirmation.SetActive(false);
            navigation.interactable = navigation.blocksRaycasts = true;
            RebuildNavigation();
            Select(selectedLoad != null && selectedLoad.interactable ? selectedLoad : DefaultButton());
        }

        // Bootstrap owns Escape routing so the same keypress cannot also dismiss its pause menu.
        public void Close()
        {
            if (busy || closing || loading) return;
            if (confirmation != null && confirmation.activeSelf) { DismissConfirmation(); return; }
            closing = true;
            restoreSelection = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private void Update()
        {
            if (closing || busy || loading || keyboardButtons.Count == 0 || !Input.GetKeyDown(KeyCode.Tab)) return;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int index = keyboardButtons.FindIndex(button => button.gameObject == selected);
            int step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -1 : 1;
            if (index < 0) index = step < 0 ? 0 : -1;
            Select(keyboardButtons[(index + step + keyboardButtons.Count) % keyboardButtons.Count]);
        }

        private void RebuildNavigation()
        {
            keyboardButtons.Clear();
            if (confirmation != null && confirmation.activeSelf)
            { keyboardButtons.Add(cancelLoad); keyboardButtons.Add(confirmLoad); }
            else
            {
                AddNavigable(checkpointName);
                AddNavigable(save);
                foreach (Button button in cardButtons) AddNavigable(button);
                AddNavigable(previous); AddNavigable(next); AddNavigable(refresh); AddNavigable(back);
            }
            for (int index = 0; index < keyboardButtons.Count; index++)
            {
                Selectable before = keyboardButtons[(index + keyboardButtons.Count - 1) % keyboardButtons.Count];
                Selectable after = keyboardButtons[(index + 1) % keyboardButtons.Count];
                keyboardButtons[index].navigation = new Navigation { mode = Navigation.Mode.Explicit,
                    selectOnUp = before, selectOnLeft = keyboardButtons[index] == checkpointName ? checkpointName : before,
                    selectOnDown = after, selectOnRight = keyboardButtons[index] == checkpointName ? checkpointName : after };
            }
        }

        private void AddNavigable(Selectable button)
        { if (button != null && button.gameObject.activeInHierarchy && button.interactable) keyboardButtons.Add(button); }

        private Button DefaultButton()
        { return save.gameObject.activeSelf && save.interactable ? save : cardButtons.Find(button => button.interactable) ?? back; }

        private static void Select(Selectable button)
        { if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button != null ? button.gameObject : null); }

        private void ClearCards()
        {
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(cards)) Select(null);
            cardButtons.Clear();
            for (int index = cards.childCount - 1; index >= 0; index--)
            { GameObject item = cards.GetChild(index).gameObject; item.SetActive(false); Destroy(item); }
        }

        private void OnDestroy()
        {
            if (loading || notified) return;
            notified = true;
            if (restoreSelection && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(previousSelection != null && previousSelection.activeInHierarchy
                    ? previousSelection : null);
            Action callback = closed;
            closed = null;
            callback?.Invoke();
        }

        private static string Limited(string value, int length)
        {
            value = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            return value.Length <= length ? value : value.Substring(0, length - 1) + "…";
        }

        private static void SetSingleLine(Text label, string value)
        {
            // Measure the actual font and weight in UI units. Only presentation is
            // shortened; the entry's full name remains in its immutable metadata.
            value = (value ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            TextGenerationSettings settings = label.GetGenerationSettings(Vector2.zero);
            float width = Mathf.Max(0f, label.rectTransform.rect.width - 2f) * label.pixelsPerUnit;
            TextGenerator measure = label.cachedTextGeneratorForLayout;
            if (measure.GetPreferredWidth(value, settings) <= width) { label.text = value; return; }
            int[] elements = StringInfo.ParseCombiningCharacters(value);
            int low = 0, high = elements.Length;
            while (low < high)
            {
                int count = (low + high + 1) / 2;
                int end = count < elements.Length ? elements[count] : value.Length;
                string candidate = value.Substring(0, end) + "…";
                if (measure.GetPreferredWidth(candidate, settings) <= width) low = count;
                else high = count - 1;
            }
            int length = low < elements.Length ? elements[low] : value.Length;
            label.text = value.Substring(0, length) + "…";
        }

        private Button MakeButton(string label, Transform parent, Action action, bool primary,
            float x, float y, float width, float height)
        {
            RectTransform rect = Box(label, parent, primary ? Accent : new Color(.065f, .11f, .15f));
            Place(rect, x, y, width, height);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.1f, 1.18f, 1.18f);
            colors.pressedColor = new Color(.6f, .8f, .82f);
            colors.disabledColor = new Color(.43f, .48f, .5f, .65f);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            Text text = Put("Label", rect, label, 20, primary ? new Color(.015f, .055f, .07f) : Ink,
                12f, 5f, width - 24f, height - 10f, primary);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private Text Put(string name, Transform parent, string value, int size, Color color,
            float x, float y, float width, float height, bool bold = false)
        {
            RectTransform rect = Rect(name, parent);
            Place(rect, x, y, width, height);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.supportRichText = false; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return item.GetComponent<RectTransform>();
        }

        private static RectTransform Box(string name, Transform parent, Color color)
        { RectTransform rect = Rect(name, parent); rect.gameObject.AddComponent<Image>().color = color; return rect; }

        private static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }

        private static void Center(RectTransform rect, float width, float height)
        { rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(width, height); }

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
