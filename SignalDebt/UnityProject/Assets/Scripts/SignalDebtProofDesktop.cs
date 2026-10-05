using System;
using UnityEngine;
using UnityEngine.UI;

namespace SignalDebt
{
    /// <summary>
    /// Read-only presentation of the notice and its detached history. Only explicit
    /// button clicks request saved commands from the owner; refresh never edits,
    /// grants access, advances owner work or settles the contract.
    /// </summary>
    public sealed class SignalDebtProofDesktop : MonoBehaviour
    {
        public enum Command { BeginSession, Restore, Undo, QueueCheck, Collect }
        private const int PageSize = 4;
        private static readonly Color Panel = new Color(.043f, .070f, .091f);
        private static readonly Color Ink = new Color(.89f, .94f, .95f);
        private static readonly Color Muted = new Color(.58f, .70f, .74f);
        private static readonly Color Teal = new Color(.35f, .87f, .81f);
        private static readonly Color Amber = new Color(.97f, .72f, .40f);
        private static readonly Color Paper = new Color(.91f, .92f, .87f);
        private static readonly Color Print = new Color(.10f, .17f, .19f);
        private Font font;
        private Func<SignalDebtSaveData> readSnapshot;
        private Func<Command, string, bool> commit;
        private Func<string> readFeedback;
        private SignalDebtSaveData snapshot;
        private string selectedRevisionId, campaignId, feedback = "", observedFeedback = "";
        private int historyStart, observedChecks = -1;
        private bool dispatching, feedbackIsError;
        private Text sessionLabel, previewLabel, currentLabel, pageLabel, status;
        private Text noticeTitle, noticeDate, noticeContact, noticeFooter;
        private Text unavailableTitle, unavailableDetail;
        private Text[] referenceValues, comparisonLabels;
        private Button openButton, currentButton, previousButton, nextButton;
        private Button restoreButton, undoButton, checkButton, collectButton;
        private Button[] revisionButtons;
        private string[] visibleRevisionIds;
        private GameObject preview, reference, history, unavailable;

        public string SelectedRevisionId { get { return selectedRevisionId; } }

        /// <summary>Retain this child across Refresh and window-size changes.</summary>
        public static SignalDebtProofDesktop Build(RectTransform parent, Font typeface,
            Func<SignalDebtSaveData> readSnapshot, Func<Command, string, bool> commit,
            Func<string> feedback)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (readSnapshot == null) throw new ArgumentNullException(nameof(readSnapshot));
            if (commit == null) throw new ArgumentNullException(nameof(commit));
            RectTransform root = Rect("Southbank notice recovery", parent);
            LayoutElement layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = layout.minHeight = 500f;
            var desktop = root.gameObject.AddComponent<SignalDebtProofDesktop>();
            desktop.font = typeface != null ? typeface : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            desktop.readSnapshot = readSnapshot;
            desktop.commit = commit;
            desktop.readFeedback = feedback;
            desktop.CreateInterface();
            desktop.Refresh();
            return desktop;
        }

        private void CreateInterface()
        {
            RectTransform root = (RectTransform)transform;
            RectTransform header = Box("Proof session", root, Panel);
            Top(header, 0f, 54f);
            Text title = Label("Workspace title", header, "SOUTHBANK  /  NOTICE PROOF", 20, Ink);
            Top(title.rectTransform, 5f, 26f, 14f, 190f);
            sessionLabel = Label("Recovery permission", header, "", 16, Muted);
            Top(sessionLabel.rectTransform, 31f, 21f, 14f, 190f);
            openButton = Button("Open client computer", header, "Open computer", () => Dispatch(Command.BeginSession));
            Right(openButton.GetComponent<RectTransform>(), 10f, 10f, 166f, 34f);

            RectTransform previewPanel = Box("Selected notice preview", root, Panel);
            Region(previewPanel, 0f, .55f, 62f, 264f, 0f, 6f);
            preview = previewPanel.gameObject;
            previewLabel = Label("Current or historical page", previewPanel, "", 18, Ink);
            Top(previewLabel.rectTransform, 7f, 27f, 14f, 142f);
            currentButton = Button("Preview current notice", previewPanel, "Show current", ShowCurrent);
            Right(currentButton.GetComponent<RectTransform>(), 12f, 7f, 124f, 27f);
            currentButton.GetComponentInChildren<Text>().fontSize = 15;
            RectTransform paper = Box("Actual notice page", previewPanel, Paper);
            Top(paper, 41f, 211f, 14f, 14f);
            Text masthead = Label("Notice masthead", paper, "COMMUNITY NOTICE", 12, new Color(.36f, .44f, .43f));
            Top(masthead.rectTransform, 11f, 18f, 24f, 24f);
            noticeTitle = Label("Actual notice title", paper, "", 24, Print);
            noticeTitle.fontStyle = FontStyle.Bold;
            Top(noticeTitle.rectTransform, 34f, 40f, 24f, 24f);
            RectTransform rule = Box("Notice divider", paper, new Color(.61f, .68f, .64f));
            Top(rule, 82f, 2f, 24f, 24f);
            noticeDate = Label("Actual event date", paper, "", 22, Print);
            Top(noticeDate.rectTransform, 98f, 31f, 24f, 24f);
            noticeContact = Label("Actual contact line", paper, "", 17, Print);
            Top(noticeContact.rectTransform, 143f, 25f, 24f, 24f);
            noticeFooter = Label("Actual footer", paper, "", 16, Print);
            Top(noticeFooter.rectTransform, 176f, 30f, 24f, 24f);

            RectTransform referencePanel = Box("Approved reference fields", root, Panel);
            Region(referencePanel, .55f, 1f, 62f, 264f, 6f, 0f);
            reference = referencePanel.gameObject;
            Text heading = Label("Reference heading", referencePanel, "APPROVED REFERENCE", 18, Teal);
            Top(heading.rectTransform, 9f, 26f, 16f, 16f);
            referenceValues = new Text[4]; comparisonLabels = new Text[4];
            string[] names = { "Title", "Event date", "Contact", "Footer" };
            for (int index = 0; index < names.Length; index++)
            {
                float top = 46f + index * 51f;
                Text name = Label(names[index] + " reference label", referencePanel, names[index].ToUpperInvariant(), 12, Muted);
                Top(name.rectTransform, top, 17f, 16f, 92f);
                comparisonLabels[index] = Label(names[index] + " comparison", referencePanel, "", 13, Muted);
                Right(comparisonLabels[index].rectTransform, 16f, top, 90f, 17f);
                comparisonLabels[index].alignment = TextAnchor.MiddleRight;
                referenceValues[index] = Label(names[index] + " approved content", referencePanel, "", 17, Ink);
                Top(referenceValues[index].rectTransform, top + 17f, 32f, 16f, 16f);
            }

            RectTransform blocked = Box("Unavailable proof", root, Panel);
            Top(blocked, 62f, 342f);
            unavailable = blocked.gameObject;
            unavailableTitle = Label("Unavailable title", blocked, "", 26, Ink);
            Top(unavailableTitle.rectTransform, 75f, 43f, 30f, 30f);
            unavailableTitle.alignment = TextAnchor.MiddleCenter;
            unavailableDetail = Label("Unavailable instruction", blocked, "", 20, Muted);
            Top(unavailableDetail.rectTransform, 134f, 90f, 60f, 60f);
            unavailableDetail.alignment = TextAnchor.UpperCenter;

            RectTransform historyPanel = Rect("Preserved version history", root);
            Top(historyPanel, 336f, 68f);
            history = historyPanel.gameObject;
            Text historyTitle = Label("History heading", historyPanel, "VERSION HISTORY", 16, Muted);
            Top(historyTitle.rectTransform, 0f, 23f, 0f, 300f);
            pageLabel = Label("History page range", historyPanel, "", 15, Muted);
            Right(pageLabel.rectTransform, 90f, 0f, 170f, 23f);
            pageLabel.alignment = TextAnchor.MiddleRight;
            previousButton = Button("Earlier versions", historyPanel, "‹", () => MovePage(-1));
            Right(previousButton.GetComponent<RectTransform>(), 43f, 0f, 36f, 25f);
            nextButton = Button("Later versions", historyPanel, "›", () => MovePage(1));
            Right(nextButton.GetComponent<RectTransform>(), 0f, 0f, 36f, 25f);
            revisionButtons = new Button[PageSize]; visibleRevisionIds = new string[PageSize];
            for (int index = 0; index < PageSize; index++)
            {
                int slot = index;
                revisionButtons[index] = Button("Preview history slot " + (index + 1), historyPanel, "", () => SelectSlot(slot));
                Region(revisionButtons[index].GetComponent<RectTransform>(), index / (float)PageSize,
                    (index + 1f) / PageSize, 30f, 38f, index == 0 ? 0f : 4f, index == PageSize - 1 ? 0f : 4f);
                revisionButtons[index].GetComponentInChildren<Text>().fontSize = 17;
            }

            RectTransform observation = Box("Owner observation and feedback", root, new Color(.035f, .080f, .096f));
            Top(observation, 412f, 42f);
            status = Label("Current owner result", observation, "", 17, Ink);
            Top(status.rectTransform, 2f, 38f, 12f, 12f);

            restoreButton = Button("Restore selected notice revision", root, "Restore selected", () => Dispatch(Command.Restore, selectedRevisionId));
            Region(restoreButton.GetComponent<RectTransform>(), 0f, .19f, 464f, 36f, 0f, 4f);
            undoButton = Button("Undo latest restore", root, "Undo", () => Dispatch(Command.Undo));
            Region(undoButton.GetComponent<RectTransform>(), .19f, .29f, 464f, 36f, 4f, 4f);
            currentLabel = Label("Owner checks current version", root, "", 15, Muted);
            Region(currentLabel.rectTransform, .29f, .55f, 464f, 36f, 12f, 8f);
            currentLabel.alignment = TextAnchor.MiddleRight;
            checkButton = Button("Request current notice export", root, "Ask owner to export", () => Dispatch(Command.QueueCheck));
            Region(checkButton.GetComponent<RectTransform>(), .55f, .81f, 464f, 36f, 4f, 4f);
            collectButton = Button("Collect proof recovery fee", root, "Collect 160 cr", () => Dispatch(Command.Collect));
            Region(collectButton.GetComponent<RectTransform>(), .81f, 1f, 464f, 36f, 4f, 0f);
        }

        public void Refresh()
        {
            if (readSnapshot == null || sessionLabel == null) return;
            snapshot = readSnapshot();
            SignalDebtProofDocument proof = snapshot == null ? null : snapshot.proofDocument;
            SignalDebtClientDevice device = snapshot == null ? null : snapshot.clientDevice;
            bool valid = proof != null && device != null && proof.Validate(device);
            bool permitted = valid && snapshot.proofAccepted && proof.accessGrant != SignalDebtProofGrant.None;
            bool paid = permitted && (snapshot.proofPaid || proof.accessGrant == SignalDebtProofGrant.ReadOnlyAfterSettlement);
            bool open = permitted && device.sessionOpen;
            bool readable = open && device.powered;
            bool editable = readable && !paid && proof.accessGrant == SignalDebtProofGrant.Recover;
            if (snapshot != null && campaignId != snapshot.saveId)
            {
                campaignId = snapshot.saveId;
                selectedRevisionId = null; historyStart = 0; observedChecks = -1; feedback = observedFeedback = "";
            }
            if (valid && observedChecks != proof.OwnerCheckCount)
            { observedChecks = proof.OwnerCheckCount; feedback = ""; }
            // Owner work can fail to save after the button callback has returned.
            // Surface that new failure without displaying raw ledger summaries.
            string externalFeedback = readFeedback == null ? "" : readFeedback() ?? "";
            if (externalFeedback != observedFeedback)
            {
                observedFeedback = externalFeedback;
                if (permitted && proof.ownerCheckQueued &&
                    (externalFeedback.IndexOf("not saved", StringComparison.OrdinalIgnoreCase) >= 0
                    || externalFeedback.IndexOf("could not", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    feedback = "Owner check was not saved. Your work is unchanged. Use Retry owner check.";
                    feedbackIsError = true;
                }
            }
            sessionLabel.text = !valid ? "Proof unavailable" : !permitted ? "Separate recovery permission required"
                : !open ? "Client view closed  ·  saved work retained"
                : !device.powered ? "Computer powered off" : paid ? "Recovery paid  ·  read-only" : "Notice recovery only  ·  160 cr agreed";
            openButton.GetComponentInChildren<Text>().text = open ? "Computer open" : "Open computer";
            openButton.interactable = permitted && !open && !dispatching;
            preview.SetActive(readable); reference.SetActive(readable); history.SetActive(readable);
            unavailable.SetActive(!readable);
            if (!readable)
            {
                unavailableTitle.text = !valid ? "Proof unavailable" : !permitted ? "A separate job needs your permission"
                    : !open ? "Client computer closed" : "Client computer powered off";
                unavailableDetail.text = !valid ? "Reopen the saved campaign to read this workspace."
                    : !permitted ? "Accept the notice-recovery offer in Inbox first."
                    : !open ? "Open the computer to inspect the notice and its preserved versions."
                    : "Turn the client computer on before reading or restoring its notice.";
                noticeTitle.text = noticeDate.text = noticeContact.text = noticeFooter.text = "";
                for (int index = 0; index < referenceValues.Length; index++)
                    referenceValues[index].text = comparisonLabels[index].text = "";
            }
            SignalDebtProofRevision selected = null;
            if (readable)
            {
                if (selectedRevisionId == null)
                {
                    selectedRevisionId = proof.currentRevisionId;
                    historyStart = ((proof.RevisionCount - 1) / PageSize) * PageSize;
                }
                SignalDebtProofRead read = proof.ReadRevision(device, selectedRevisionId);
                if (read.Revision == null)
                { read = proof.ReadCurrent(device); selectedRevisionId = read.Revision == null ? null : read.Revision.id; }
                selected = read.Revision;
                SignalDebtProofApproval approval = proof.ReadApproval(device);
                if (selected != null && approval != null)
                {
                    bool current = selected.id == proof.currentRevisionId;
                    previewLabel.text = (current ? "Current page" : "History preview") + "  ·  v" + selected.number;
                    previewLabel.color = current ? Teal : Amber;
                    currentButton.interactable = !current && !dispatching;
                    noticeTitle.text = selected.content.title;
                    noticeDate.text = selected.content.eventDate;
                    // Empty fields remain empty on the page. Their missing state
                    // is described alongside the actual approved reference.
                    noticeContact.text = selected.content.contact;
                    noticeFooter.text = selected.content.footer;
                    string[] shown = Fields(selected.content), approved = Fields(approval.content);
                    for (int index = 0; index < approved.Length; index++)
                    {
                        referenceValues[index].text = approved[index];
                        bool same = string.Equals(shown[index], approved[index], StringComparison.Ordinal);
                        comparisonLabels[index].text = same ? "Same" : string.IsNullOrEmpty(shown[index]) ? "Missing" : "Different";
                        comparisonLabels[index].color = same ? Muted : Amber;
                    }
                }
                RenderHistory(proof, device);
            }
            restoreButton.interactable = editable && selected != null && selected.id != proof.currentRevisionId && !dispatching;
            undoButton.interactable = editable && proof.HasUndoRestore && !dispatching;
            bool verified = permitted && proof.CurrentOwnerVerified(device);
            checkButton.interactable = editable && !verified && !dispatching;
            checkButton.GetComponentInChildren<Text>().text = permitted && proof.ownerCheckQueued ? "Retry owner check" : "Ask owner to export";
            collectButton.interactable = permitted && !paid && verified && snapshot.CanClaimProofPayment && !dispatching;
            collectButton.GetComponentInChildren<Text>().text = paid ? "160 cr received" : "Collect 160 cr";
            currentLabel.text = permitted ? "Owner checks current v" + proof.RevisionCount : "";
            status.text = !string.IsNullOrEmpty(feedback) ? feedback
                : !permitted ? "The recovery offer is in Inbox."
                : paid ? "160 credits received. The notice and its history are read-only."
                : OwnerStatus(proof, device);
            status.color = !string.IsNullOrEmpty(feedback) && feedbackIsError ? Amber : verified ? Teal : Ink;
        }

        private void RenderHistory(SignalDebtProofDocument proof, SignalDebtClientDevice device)
        {
            historyStart = Math.Max(0, Math.Min(historyStart, ((proof.RevisionCount - 1) / PageSize) * PageSize));
            SignalDebtProofRevision[] page = proof.ReadHistory(device, historyStart, PageSize);
            pageLabel.text = (historyStart + 1) + "–" + (historyStart + page.Length) + " of " + proof.RevisionCount;
            previousButton.interactable = historyStart > 0 && !dispatching;
            nextButton.interactable = historyStart < proof.RevisionCount - PageSize && !dispatching;
            for (int index = 0; index < PageSize; index++)
            {
                revisionButtons[index].gameObject.SetActive(index < page.Length);
                visibleRevisionIds[index] = index < page.Length ? page[index].id : null;
                if (index >= page.Length) continue;
                SignalDebtProofRevision revision = page[index];
                string kind = revision.id == proof.currentRevisionId ? "Current"
                    : revision.change == SignalDebtProofChange.Restore ? "Restored"
                    : revision.change == SignalDebtProofChange.Undo ? "Undo saved" : "Saved draft";
                revisionButtons[index].GetComponentInChildren<Text>().text = "v" + revision.number + "  ·  " + kind;
                revisionButtons[index].interactable = !dispatching;
                revisionButtons[index].GetComponent<Image>().color = revision.id == selectedRevisionId
                    ? new Color(.055f, .26f, .27f) : new Color(.070f, .115f, .145f);
            }
        }

        private void SelectSlot(int slot)
        {
            if (dispatching || visibleRevisionIds[slot] == null) return;
            selectedRevisionId = visibleRevisionIds[slot];
            Refresh();
        }
        private void MovePage(int direction)
        { if (dispatching) return; historyStart = Math.Max(0, historyStart + direction * PageSize); Refresh(); }
        private void ShowCurrent()
        {
            if (dispatching || snapshot == null || snapshot.proofDocument == null) return;
            selectedRevisionId = snapshot.proofDocument.currentRevisionId;
            historyStart = ((snapshot.proofDocument.RevisionCount - 1) / PageSize) * PageSize;
            Refresh();
        }
        private void Dispatch(Command command, string argument = null)
        {
            if (dispatching || commit == null) return;
            dispatching = true;
            bool accepted;
            try { accepted = commit(command, argument); }
            finally { dispatching = false; }
            feedbackIsError = !accepted;
            if (accepted)
                feedback = command == Command.Restore ? "Selected page restored. Ask the owner to export the current page."
                    : command == Command.Undo ? "Previous page restored. Ask for a fresh owner check."
                    : command == Command.QueueCheck ? "Owner check requested. You can leave this view."
                    : command == Command.Collect ? "160 credits received." : "Client computer open.";
            else
            {
                feedback = readFeedback == null ? "" : readFeedback() ?? "";
                if (string.IsNullOrWhiteSpace(feedback) || feedback.IndexOf("southbank-", StringComparison.OrdinalIgnoreCase) >= 0)
                    feedback = "The action could not be saved. Your last saved work is unchanged.";
                if (feedback.Length > 170) feedback = feedback.Substring(0, 167) + "…";
            }
            if (this != null && isActiveAndEnabled) Refresh();
        }

        private static string OwnerStatus(SignalDebtProofDocument proof, SignalDebtClientDevice device)
        {
            if (proof.ownerCheckQueued) return "Owner check queued. You can leave this view; retry if saving failed.";
            if (proof.CurrentOwnerVerified(device)) return "Owner exported the current notice. The 160-credit fee is ready.";
            SignalDebtProofOwnerRead last = proof.LastOwnerRead();
            if (last == null) return "Compare the page with the approved fields. The owner has not checked it yet.";
            if (last.revisionId != proof.currentRevisionId || last.deviceConfigurationRevision != device.configurationRevision)
                return proof.HasHistoricalExport ? "Earlier export retained. The current page needs a fresh owner check."
                    : "The page or computer changed. Ask the owner to check the current version.";
            switch (last.result)
            {
                case SignalDebtProofResult.DateMismatch: return "Owner could not export: the event date differs from the approval.";
                case SignalDebtProofResult.ContactMissing: return "Owner could not export: the contact line is missing.";
                case SignalDebtProofResult.ContactMismatch: return "Owner could not export: the contact line differs from the approval.";
                case SignalDebtProofResult.FooterMissing: return "Owner could not export: the footer is missing.";
                case SignalDebtProofResult.FooterMismatch: return "Owner could not export: the footer differs from the approval.";
                case SignalDebtProofResult.TitleMismatch: return "Owner could not export: the title differs from the approval.";
                case SignalDebtProofResult.PoweredOff: return "Owner could not export: the client computer was powered off.";
                default: return "The owner needs to check the current notice before payment.";
            }
        }
        private static string[] Fields(SignalDebtProofContent content)
        { return new[] { content.title, content.eventDate, content.contact, content.footer }; }
        private Text Label(string name, Transform parent, string value, int size, Color color)
        {
            Text text = Rect(name, parent).gameObject.AddComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color;
            text.supportRichText = false; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        private Button Button(string name, Transform parent, string caption, Action action)
        {
            RectTransform rect = Box(name, parent, new Color(.070f, .115f, .145f));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); button.targetGraphic.raycastTarget = true;
            ColorBlock colors = button.colors;
            colors.highlightedColor = colors.selectedColor = new Color(1.3f, 1.45f, 1.45f);
            colors.pressedColor = new Color(.60f, .83f, .83f);
            colors.disabledColor = new Color(.44f, .49f, .51f, .72f); colors.fadeDuration = .10f;
            button.colors = colors; button.onClick.AddListener(() => action());
            Text label = Label("Label", rect, caption, 17, Ink);
            Stretch(label.rectTransform, 6f, 2f); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }
        private static RectTransform Box(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent); Image image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false; return rect;
        }
        private static void Stretch(RectTransform rect, float x, float y)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(x, y); rect.offsetMax = new Vector2(-x, -y); }
        private static void Top(RectTransform rect, float top, float height, float left = 0f, float right = 0f)
        { Region(rect, 0f, 1f, top, height, left, right); }
        private static void Region(RectTransform rect, float minX, float maxX, float top, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(minX, 1f); rect.anchorMax = new Vector2(maxX, 1f);
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top);
        }
        private static void Right(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, -top); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
