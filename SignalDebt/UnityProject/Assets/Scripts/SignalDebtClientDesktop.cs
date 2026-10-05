using System;
using UnityEngine;
using UnityEngine.UI;

namespace SignalDebt
{
    /// <summary>
    /// One client workspace on the existing ASTER canvas. This component only reads
    /// detached observations and requests commands; its owner saves before publishing.
    /// No camera, cursor, campaign, task scheduler or file-system ownership lives here.
    /// </summary>
    public sealed class SignalDebtClientDesktop : MonoBehaviour
    {
        public enum Command
        {
            BeginSession, EndSession, OpenFolder, InspectConnection,
            ConnectEndpoint, UndoConnection, QueueOwnerCheck
        }

        private static readonly Color Background = new Color(.023f, .041f, .060f);
        private static readonly Color Panel = new Color(.043f, .070f, .091f);
        private static readonly Color Ink = new Color(.89f, .94f, .95f);
        private static readonly Color Muted = new Color(.58f, .70f, .74f);
        private static readonly Color Teal = new Color(.35f, .87f, .81f);
        private static readonly Color Amber = new Color(.97f, .72f, .40f);
        private static readonly string[] Endpoints =
        {
            SignalDebtClientDevice.OrderIndexOutputPortId,
            SignalDebtClientDevice.PrintQueueOutputPortId,
            SignalDebtClientDevice.AssetCatalogOutputPortId
        };

        private Font font;
        private Func<SignalDebtClientDevice> readSnapshot;
        private Func<Command, string, bool> commit;
        private Func<bool> isPaid, canCollectPayment;
        private Func<string> readFeedback;
        private Action collectPayment, openInbox;
        private SignalDebtClientDevice snapshot;
        private const string PortInstruction = "Select an output to inspect it, then select the folder input to connect.";
        private string selectedEndpoint, inspection = PortInstruction;
        private string feedback = "";
        private int hintLevel;
        private bool dispatching, feedbackIsError;
        private Text sessionLabel, folderTitle, folderNote, failureTitle, failureDetail;
        private Text inspector, ownerStatus, hintText, graphInstruction, inputCaption;
        private Text paymentCaption, sessionCaption;
        private Button sessionButton, orderTab, staffTab, inputButton, undoButton, disconnectButton;
        private Button checkButton, paymentButton, hintButton;
        private Button[] serviceButtons;
        private GameObject orderRows, failure, staffLabel, graphBody, graphClosed;
        private Text[] orderNames, orderNumbers, orderStates, orderVersions;
        private SignalDebtClientWire wire;

        /// <summary>
        /// Parent should retain this child while refreshing the Service application.
        /// Both Build and Refresh are read-only. Commands originate only in explicit
        /// button actions. Keep commit callbacks independent of page destruction.
        /// </summary>
        public static SignalDebtClientDesktop Build(RectTransform parent, Font typeface,
            Func<SignalDebtClientDevice> readSnapshot, Func<Command, string, bool> commit,
            Func<bool> isPaid, Func<bool> canCollectPayment, Action collectPayment,
            Action openInbox, Func<string> readFeedback = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (readSnapshot == null) throw new ArgumentNullException(nameof(readSnapshot));
            if (commit == null) throw new ArgumentNullException(nameof(commit));
            RectTransform root = Rect("Southbank client workspace", parent);
            var layout = root.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 500f;
            layout.minHeight = 500f;
            var desktop = root.gameObject.AddComponent<SignalDebtClientDesktop>();
            desktop.font = typeface != null ? typeface : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            desktop.readSnapshot = readSnapshot;
            desktop.commit = commit;
            desktop.isPaid = isPaid;
            desktop.canCollectPayment = canCollectPayment;
            desktop.collectPayment = collectPayment;
            desktop.openInbox = openInbox;
            desktop.readFeedback = readFeedback;
            desktop.CreateInterface();
            desktop.Refresh();
            return desktop;
        }

        private void CreateInterface()
        {
            RectTransform root = (RectTransform)transform;
            RectTransform header = Box("Client session", root, Panel);
            Top(header, 0f, 52f);
            Text title = Text("Client name", header, "SOUTHBANK  /  SHOP PC", 21, Ink);
            Top(title.rectTransform, 6f, 26f, 14f, 190f);
            sessionLabel = Text("Session permissions", header, "", 16, Muted);
            Top(sessionLabel.rectTransform, 31f, 20f, 14f, 190f);
            sessionButton = Button("Client connection", header, "", ToggleSession);
            Right(sessionButton.GetComponent<RectTransform>(), 10f, 9f, 164f, 34f);
            sessionCaption = sessionButton.GetComponentInChildren<Text>();

            RectTransform folders = Box("Client files", root, Background);
            Region(folders, 0f, .49f, 62f, 282f, 0f, 6f);
            orderTab = Button("Open Order Folder", folders, "Order Folder", () => OpenFolder(SignalDebtClientDevice.OrderFolderId));
            Region(orderTab.GetComponent<RectTransform>(), 0f, .53f, 0f, 36f, 0f, 4f);
            staffTab = Button("Read Staff Desk access label", folders, "Staff Desk  /  access", () => OpenFolder(SignalDebtClientDevice.StaffDeskId));
            Region(staffTab.GetComponent<RectTransform>(), .53f, 1f, 0f, 36f, 4f, 0f);
            folderTitle = Text("Folder location", folders, "", 17, Muted);
            Top(folderTitle.rectTransform, 47f, 24f, 12f, 12f);

            RectTransform rows = Rect("Actual client records", folders);
            Top(rows, 80f, 150f, 8f, 8f);
            orderRows = rows.gameObject;
            orderNames = new Text[2]; orderNumbers = new Text[2];
            orderStates = new Text[2]; orderVersions = new Text[2];
            for (int index = 0; index < 2; index++)
            {
                RectTransform row = Box("Order record " + index, rows, Panel);
                Top(row, index * 76f, 68f);
                orderNumbers[index] = Text("Order number", row, "", 21, Ink);
                Left(orderNumbers[index].rectTransform, 12f, 10f, 96f, 28f);
                orderVersions[index] = Text("Record version", row, "", 16, Muted);
                Left(orderVersions[index].rectTransform, 12f, 39f, 96f, 22f);
                orderNames[index] = Text("Order contents", row, "", 18, Ink);
                Top(orderNames[index].rectTransform, 8f, 54f, 118f, 130f);
                orderStates[index] = Text("Preparation state", row, "", 17, Muted);
                Right(orderStates[index].rectTransform, 12f, 11f, 110f, 46f);
                orderStates[index].alignment = TextAnchor.MiddleRight;
            }
            folderNote = Text("Folder result", folders, "", 16, Muted);
            Top(folderNote.rectTransform, 236f, 44f, 12f, 12f);

            RectTransform failed = Box("Folder read failure", folders, Panel);
            Top(failed, 80f, 150f, 8f, 8f);
            failure = failed.gameObject;
            failureTitle = Text("Read failure title", failed, "", 24, Amber);
            Top(failureTitle.rectTransform, 16f, 34f, 18f, 18f);
            failureDetail = Text("Read failure detail", failed, "", 19, Ink);
            Top(failureDetail.rectTransform, 61f, 73f, 18f, 18f);

            RectTransform restricted = Box("Staff Desk access", folders, Panel);
            Top(restricted, 80f, 190f, 8f, 8f);
            staffLabel = restricted.gameObject;
            Text staffTitle = Text("Staff workspace", restricted, "Staff Desk", 26, Ink);
            Top(staffTitle.rectTransform, 18f, 36f, 20f, 20f);
            Text staffScope = Text("Permission label", restricted, "Private workspace\nNo access granted", 21, Amber);
            Top(staffScope.rectTransform, 67f, 59f, 20f, 20f);
            Text staffExplanation = Text("Scope explanation", restricted, "Your repair covers the Order Folder. This label does not open any private files.", 17, Muted);
            Top(staffExplanation.rectTransform, 138f, 44f, 20f, 20f);

            RectTransform graph = Box("Connection board", root, Panel);
            Region(graph, .49f, 1f, 62f, 282f, 6f, 0f);
            Text graphTitle = Text("Connection title", graph, "CONNECTIONS", 18, Ink);
            Top(graphTitle.rectTransform, 10f, 28f, 14f, 14f);
            RectTransform board = Rect("Live port connections", graph);
            Stretch(board);
            graphBody = board.gameObject;
            wire = Rect("Current connection", board).gameObject.AddComponent<SignalDebtClientWire>();
            Stretch(wire.rectTransform);
            wire.raycastTarget = false;
            graphInstruction = Text("Patch instruction", board, "", 16, Muted);
            Top(graphInstruction.rectTransform, 42f, 42f, 14f, 14f);
            serviceButtons = new Button[3];
            for (int index = 0; index < Endpoints.Length; index++)
            {
                string endpoint = Endpoints[index];
                Button output = Button("Inspect " + EndpointName(endpoint) + " output", board,
                    EndpointName(endpoint) + "   ○", () => InspectOutput(endpoint));
                Region(output.GetComponent<RectTransform>(), 0f, .51f, 90f + index * 51f, 40f, 14f, 0f);
                serviceButtons[index] = output;
            }
            inputButton = Button("Order Folder records input", board, "", SelectInput);
            Region(inputButton.GetComponent<RectTransform>(), .70f, 1f, 131f, 60f, 0f, 14f);
            inputCaption = inputButton.GetComponentInChildren<Text>();
            inputCaption.fontSize = 17;
            inputCaption.text = "○  Order Folder\nInspect input";
            undoButton = Button("Undo connection", board, "Undo", () => Dispatch(Command.UndoConnection));
            Region(undoButton.GetComponent<RectTransform>(), 0f, .33f, 243f, 30f, 14f, 4f);
            disconnectButton = Button("Disconnect folder input", board, "Unplug", () => Dispatch(Command.ConnectEndpoint, ""));
            Region(disconnectButton.GetComponent<RectTransform>(), .33f, .66f, 243f, 30f, 4f, 4f);
            Button clear = Button("Clear port selection", board, "Clear selection", ClearSelection);
            Region(clear.GetComponent<RectTransform>(), .66f, 1f, 243f, 30f, 4f, 14f);
            clear.GetComponentInChildren<Text>().fontSize = 15;

            RectTransform closed = Rect("Unavailable connection board", graph);
            Top(closed, 68f, 184f, 20f, 20f);
            graphClosed = closed.gameObject;
            Text closedText = Text("Open workspace instruction", closed,
                "Open the client computer to inspect its connections.", 22, Muted);
            Stretch(closedText.rectTransform);
            closedText.alignment = TextAnchor.MiddleCenter;

            RectTransform inspectBar = Box("Port inspector", root, new Color(.035f, .080f, .096f));
            Top(inspectBar, 352f, 48f);
            inspector = Text("Inspected port or command result", inspectBar, "", 18, Ink);
            Stretch(inspector.rectTransform, 13f, 2f);

            RectTransform activity = Box("Client work and settlement", root, Panel);
            Top(activity, 408f, 44f);
            ownerStatus = Text("Observed client work", activity, "", 17, Muted);
            Top(ownerStatus.rectTransform, 2f, 40f, 12f, 329f);
            checkButton = Button("Request client folder check", activity, "Client check", () => Dispatch(Command.QueueOwnerCheck));
            Right(checkButton.GetComponent<RectTransform>(), 168f, 5f, 150f, 34f);
            checkButton.GetComponentInChildren<Text>().fontSize = 16;
            paymentButton = Button("Collect agreed repair fee", activity, "Collect 120 cr", Collect);
            Right(paymentButton.GetComponent<RectTransform>(), 8f, 5f, 152f, 34f);
            paymentCaption = paymentButton.GetComponentInChildren<Text>();
            paymentCaption.fontSize = 17;

            hintButton = Button("Tiered repair hint", root, "Hint", AdvanceHint);
            Left(hintButton.GetComponent<RectTransform>(), 0f, 462f, 90f, 32f);
            hintText = Text("Optional repair guidance", root, "", 17, Muted);
            Top(hintText.rectTransform, 460f, 40f, 104f, 4f);
        }

        public void Refresh()
        {
            if (readSnapshot == null || sessionLabel == null) return;
            SignalDebtClientDevice previous = snapshot;
            snapshot = readSnapshot();
            if (previous != null && snapshot != null && previous.ownerCheckCount != snapshot.ownerCheckCount)
                feedback = "";
            bool valid = snapshot != null && snapshot.Validate();
            bool permitted = valid && snapshot.accessGrant != SignalDebtDeviceGrant.None;
            bool open = permitted && snapshot.sessionOpen;
            bool editable = open && snapshot.powered && snapshot.accessGrant == SignalDebtDeviceGrant.Repair;
            bool paid = isPaid != null && isPaid();
            sessionLabel.text = !valid ? "Client computer unavailable"
                : !permitted ? "Permission required  ·  Order Folder repair"
                : !open ? "View disconnected  ·  client's work is retained"
                : !snapshot.powered ? "Powered off  ·  no live records"
                : snapshot.accessGrant == SignalDebtDeviceGrant.ReadOnlyAfterSettlement
                    ? "Live  ·  read-only after settlement" : "Live  ·  Order Folder repair only";
            sessionCaption.text = !permitted ? "Read offer" : open ? "Disconnect" : "Open computer";
            sessionButton.interactable = valid && (permitted || openInbox != null) && !dispatching;
            orderTab.interactable = staffTab.interactable = open && !dispatching;
            graphBody.SetActive(open);
            graphClosed.SetActive(!open);
            bool staff = open && snapshot.currentView == SignalDebtDeviceView.StaffDeskAccessLabel;
            Tint(orderTab, !staff && open); Tint(staffTab, staff);
            staffLabel.SetActive(staff);
            orderRows.SetActive(false);
            failure.SetActive(!staff);
            folderNote.text = "";
            folderTitle.text = staff ? "Shop PC  /  Staff Desk  /  access label" : "Shop PC  /  Order Folder";

            if (!valid || !permitted || !open)
            {
                failureTitle.text = !valid ? "Workspace unavailable" : !permitted ? "An invitation is waiting" : "Client view closed";
                failureDetail.text = !valid ? "The client computer could not be read. Reopen the saved campaign."
                    : !permitted ? "Read the client's offer before opening their repair workspace."
                    : "Open the computer to continue. Closing this view does not undo the client's work.";
            }
            else if (!staff)
            {
                SignalDebtFolderRead read = snapshot.ReadOrderFolder();
                failure.SetActive(!read.Success);
                orderRows.SetActive(read.Success);
                if (read.Success)
                {
                    for (int index = 0; index < read.Orders.Length && index < orderNames.Length; index++)
                    {
                        SignalDebtClientOrder order = read.Orders[index];
                        orderNumbers[index].text = "#" + OrderNumber(order.id);
                        orderVersions[index].text = "version " + order.version;
                        orderNames[index].text = order.description;
                        orderStates[index].text = order.prepared ? "Ready for\nthe counter" : "Awaiting\npreparation";
                        orderStates[index].color = order.prepared ? Teal : Muted;
                    }
                    folderNote.text = "2 order records";
                }
                else
                {
                    failureTitle.text = read.Result == SignalDebtFolderResult.PoweredOff ? "Computer powered off" : "Folder could not open";
                    failureDetail.text = read.Message;
                }
            }

            if (open)
            {
                string endpoint = snapshot.connectedEndpointId;
                int connection = Array.IndexOf(Endpoints, endpoint);
                wire.SetConnection(connection, snapshot.powered ? Teal : Muted);
                graphInstruction.text = "Current link: " + (string.IsNullOrEmpty(endpoint) ? "unplugged" : EndpointName(endpoint))
                    + (selectedEndpoint == null ? "" : "\nSelected: " + EndpointName(selectedEndpoint));
                for (int index = 0; index < serviceButtons.Length; index++)
                {
                    serviceButtons[index].interactable = snapshot.powered && !dispatching;
                    Tint(serviceButtons[index], selectedEndpoint == Endpoints[index]);
                }
                inputCaption.text = "○  Order Folder\n" + (selectedEndpoint != null && editable ? "Connect" : "Inspect input");
                inputButton.interactable = snapshot.powered && !dispatching;
                Tint(inputButton, selectedEndpoint != null && editable);
                undoButton.interactable = editable && snapshot.hasUndoConnection && !dispatching;
                disconnectButton.interactable = editable && !string.IsNullOrEmpty(endpoint) && !dispatching;
            }
            checkButton.interactable = permitted && snapshot.powered
                && !snapshot.CurrentOwnerVerified && !dispatching;
            // Re-requesting an existing task lets the owner retry after a failed
            // save. It never advances the task here or issues an automatic retry.
            checkButton.GetComponentInChildren<Text>().text = valid && snapshot.ownerCheckQueued ? "Retry check" : "Client check";
            paymentButton.interactable = permitted && !paid && collectPayment != null && !dispatching
                && (canCollectPayment != null ? canCollectPayment() : snapshot.CurrentOwnerVerified);
            paymentCaption.text = paid ? "120 cr received" : "Collect 120 cr";
            ownerStatus.text = !permitted ? "The repair offer is in Inbox." : OwnerObservation(snapshot);
            ownerStatus.color = permitted && snapshot.CurrentOwnerVerified ? Teal : Muted;
            inspector.transform.parent.gameObject.SetActive(open || !string.IsNullOrEmpty(feedback));
            inspector.text = !string.IsNullOrEmpty(feedback) ? feedback
                : inspection == PortInstruction && open && snapshot.accessGrant == SignalDebtDeviceGrant.ReadOnlyAfterSettlement
                    ? "Select a port to inspect it. Connections are read-only." : inspection;
            inspector.color = !string.IsNullOrEmpty(feedback) && feedbackIsError ? Amber : Ink;
            hintButton.interactable = permitted && snapshot.accessGrant == SignalDebtDeviceGrant.Repair;
            UpdateHint();
        }

        private void ToggleSession()
        {
            if (snapshot == null) return;
            if (snapshot.accessGrant == SignalDebtDeviceGrant.None) { openInbox?.Invoke(); return; }
            Dispatch(snapshot.sessionOpen ? Command.EndSession : Command.BeginSession);
        }

        private void OpenFolder(string folder)
        {
            if (snapshot == null || !snapshot.sessionOpen) return;
            bool alreadyOpen = folder == SignalDebtClientDevice.OrderFolderId
                ? snapshot.currentView == SignalDebtDeviceView.OrderFolder
                : folder == SignalDebtClientDevice.StaffDeskId
                    && snapshot.currentView == SignalDebtDeviceView.StaffDeskAccessLabel;
            if (alreadyOpen)
            {
                feedback = "";
                Refresh();
                return;
            }
            Dispatch(Command.OpenFolder, folder);
        }

        private void InspectOutput(string endpoint)
        {
            selectedEndpoint = endpoint;
            inspection = EndpointName(endpoint) + "  /  " + OutputDescription(endpoint)
                + (snapshot != null && snapshot.accessGrant == SignalDebtDeviceGrant.Repair
                    ? "  Select the folder input to connect it." : "  Connections are read-only.");
            Dispatch(Command.InspectConnection, endpoint, true);
        }

        private void SelectInput()
        {
            if (snapshot == null) return;
            if (selectedEndpoint != null && snapshot.accessGrant == SignalDebtDeviceGrant.Repair)
            {
                string endpoint = selectedEndpoint;
                if (endpoint == snapshot.connectedEndpointId)
                {
                    selectedEndpoint = null;
                    feedback = "";
                    inspection = EndpointName(endpoint) + " is already connected to the folder's records input.";
                    Refresh();
                    return;
                }
                if (Dispatch(Command.ConnectEndpoint, endpoint)) selectedEndpoint = null;
                Refresh();
                return;
            }
            inspection = "Order Folder  /  Records input. Expects order records with an order number, description and preparation status.";
            Dispatch(Command.InspectConnection, SignalDebtClientDevice.FolderInputPortId, true);
        }

        private bool Dispatch(Command command, string argument = null, bool inspectionOnly = false)
        {
            if (dispatching || commit == null) return false;
            dispatching = true;
            bool accepted;
            try { accepted = commit(command, argument); }
            finally { dispatching = false; }
            // An inspection is descriptive even if its already-recorded milestone
            // needs no second commit. It must not turn into an automatic patch.
            bool sessionChanged = command == Command.BeginSession || command == Command.EndSession;
            feedback = accepted && (inspectionOnly || sessionChanged) ? ""
                : readFeedback != null ? readFeedback() ?? "" : "";
            if (!accepted && string.IsNullOrEmpty(feedback))
                feedback = inspectionOnly ? "The inspection could not be recorded. Try inspecting the port again."
                    : "The change could not be saved. The client computer is unchanged.";
            feedbackIsError = !accepted;
            if (accepted && sessionChanged)
            {
                selectedEndpoint = null;
                inspection = PortInstruction;
            }
            if (this != null && isActiveAndEnabled) Refresh();
            return accepted;
        }

        private void Collect()
        {
            if (dispatching || collectPayment == null || isPaid != null && isPaid()) return;
            if (canCollectPayment != null ? !canCollectPayment() : snapshot == null || !snapshot.CurrentOwnerVerified) return;
            dispatching = true;
            try { collectPayment(); }
            finally { dispatching = false; }
            feedback = readFeedback != null ? readFeedback() ?? "" : "";
            feedbackIsError = isPaid != null && !isPaid();
            if (this != null && isActiveAndEnabled) Refresh();
        }

        private void ClearSelection()
        {
            selectedEndpoint = null;
            feedback = "";
            inspection = PortInstruction;
            Refresh();
        }

        private void AdvanceHint() { hintLevel = (hintLevel + 1) % 4; UpdateHint(); }

        private void UpdateHint()
        {
            hintButton.GetComponentInChildren<Text>().text = hintLevel == 0 ? "Hint" : "Hint " + hintLevel + "/3";
            bool available = snapshot != null && snapshot.sessionOpen
                && snapshot.accessGrant == SignalDebtDeviceGrant.Repair;
            hintButton.gameObject.SetActive(available);
            hintText.gameObject.SetActive(available && hintLevel > 0);
            hintText.text = hintLevel == 0 ? ""
                : hintLevel == 1 ? "Compare what the folder expects with what each service produces. An attached wire alone is not enough."
                : hintLevel == 2 ? "Inspect the outputs. Print jobs and images are different kinds of data from order records."
                : "Patch Order Index to the folder's records input. Open the folder, then ask the clerk to check the actual order.";
        }

        private static string OwnerObservation(SignalDebtClientDevice device)
        {
            if (device.ownerCheckQueued) return "Clerk's check is queued. You can leave this view while they try the folder.";
            if (device.CurrentOwnerVerified) return "Clerk opened the folder and prepared order #1042. The later order is unchanged.";
            if (device.ownerCheckCount == 0)
                return device.legacyRepairTestRecorded ? "A previous repair test is recorded. The clerk can check this folder again."
                    : "The clerk has not checked this connection yet.";
            if (device.lastOwnerObservedConfigurationRevision != device.configurationRevision)
                return "The connection changed after the clerk's last check. Ask them to try it again.";
            switch (device.lastOwnerReadResult)
            {
                case SignalDebtFolderResult.PoweredOff: return "The clerk could not read the folder: the computer was powered off.";
                case SignalDebtFolderResult.PrintService: return "The clerk received print jobs instead of order records.";
                case SignalDebtFolderResult.AssetService: return "The clerk received images instead of order records.";
                default: return "The clerk could not open the folder: its records input was disconnected.";
            }
        }

        private static string EndpointName(string endpoint)
        {
            if (endpoint == SignalDebtClientDevice.OrderIndexOutputPortId) return "Order Index";
            if (endpoint == SignalDebtClientDevice.PrintQueueOutputPortId) return "Print Queue";
            if (endpoint == SignalDebtClientDevice.AssetCatalogOutputPortId) return "Asset Catalog";
            return "Unknown output";
        }

        private static string OutputDescription(string endpoint)
        {
            if (endpoint == SignalDebtClientDevice.OrderIndexOutputPortId) return "Produces order records: numbers, descriptions and preparation state.";
            if (endpoint == SignalDebtClientDevice.PrintQueueOutputPortId) return "Produces print jobs: pages waiting for a printer.";
            return "Produces images: artwork and design assets.";
        }

        private static string OrderNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return "?";
            int separator = id.LastIndexOf('-');
            return separator >= 0 ? id.Substring(separator + 1) : id;
        }

        private Text Text(string name, Transform parent, string value, int size, Color color)
        {
            Text label = Rect(name, parent).gameObject.AddComponent<Text>();
            label.font = font; label.text = value; label.fontSize = size; label.color = color;
            label.supportRichText = false; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        private Button Button(string name, Transform parent, string caption, Action action)
        {
            RectTransform rect = Box(name, parent, new Color(.070f, .115f, .145f));
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.targetGraphic.raycastTarget = true;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = colors.selectedColor = new Color(1.3f, 1.45f, 1.45f);
            colors.pressedColor = new Color(.60f, .83f, .83f);
            colors.disabledColor = new Color(.44f, .49f, .51f, .72f);
            colors.fadeDuration = .10f;
            button.colors = colors;
            button.onClick.AddListener(() => action());
            Text label = Text("Label", rect, caption, 18, Ink);
            Stretch(label.rectTransform, 7f, 2f);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static void Tint(Button button, bool selected)
        { button.GetComponent<Image>().color = selected ? new Color(.055f, .26f, .27f) : new Color(.070f, .115f, .145f); }

        private static RectTransform Rect(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        private static RectTransform Box(string name, Transform parent, Color color)
        {
            RectTransform rect = Rect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return rect;
        }

        private static void Stretch(RectTransform rect, float horizontal = 0f, float vertical = 0f)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(horizontal, vertical); rect.offsetMax = new Vector2(-horizontal, -vertical);
        }

        private static void Top(RectTransform rect, float top, float height, float left = 0f, float right = 0f)
        { Region(rect, 0f, 1f, top, height, left, right); }

        private static void Region(RectTransform rect, float minX, float maxX, float top, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(minX, 1f); rect.anchorMax = new Vector2(maxX, 1f);
            rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Left(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.up; rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(left, -top); rect.sizeDelta = new Vector2(width, height);
        }

        private static void Right(RectTransform rect, float right, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.one; rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-right, -top); rect.sizeDelta = new Vector2(width, height);
        }
    }

    // Canvas geometry, so the connection appears on the world-mounted laptop too.
    // The wire reflects the saved endpoint, never the currently highlighted choice.
    public sealed class SignalDebtClientWire : MaskableGraphic
    {
        private int connection = -1;

        public void SetConnection(int index, Color tint)
        {
            if (connection == index && color == tint) return;
            connection = index; color = tint;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            if (connection < 0 || connection > 2) return;
            Rect bounds = rectTransform.rect;
            float startY = bounds.yMax - (110f + connection * 51f);
            float endY = bounds.yMax - 161f;
            float from = bounds.xMin + bounds.width * .51f;
            float to = bounds.xMin + bounds.width * .70f;
            float bend = Mathf.Lerp(from, to, .53f);
            Segment(vertices, new Vector2(from, startY), new Vector2(bend, startY));
            Segment(vertices, new Vector2(bend, startY), new Vector2(bend, endY));
            Segment(vertices, new Vector2(bend, endY), new Vector2(to, endY));
        }

        private void Segment(VertexHelper vertices, Vector2 from, Vector2 to)
        {
            Vector2 direction = to - from;
            if (direction.sqrMagnitude < .01f) return;
            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * 1.5f;
            int offset = vertices.currentVertCount;
            vertices.AddVert(from - normal, color, Vector2.zero);
            vertices.AddVert(from + normal, color, Vector2.zero);
            vertices.AddVert(to + normal, color, Vector2.zero);
            vertices.AddVert(to - normal, color, Vector2.zero);
            vertices.AddTriangle(offset, offset + 1, offset + 2);
            vertices.AddTriangle(offset, offset + 2, offset + 3);
        }
    }
}
