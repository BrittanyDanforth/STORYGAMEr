using System;
using System.Collections.Generic;

namespace SignalDebt
{
    public enum SignalDebtDeviceGrant { None, Repair, ReadOnlyAfterSettlement }
    public enum SignalDebtDeviceView { Closed, OrderFolder, StaffDeskAccessLabel }
    public enum SignalDebtFolderResult { NotObserved, Ready, PoweredOff, Disconnected, PrintService, AssetService, AccessLabelOnly, UnknownFolder }

    [Serializable]
    public sealed class SignalDebtClientOrder
    {
        public string id;
        public string description;
        public int version;
        public bool prepared;

        public SignalDebtClientOrder Copy() { return (SignalDebtClientOrder)MemberwiseClone(); }
    }

    // A detached observation. The returned order objects never belong to the device.
    public sealed class SignalDebtFolderRead
    {
        public readonly SignalDebtFolderResult Result;
        public readonly string FolderId, Message, AccessLabel;
        public readonly int ConfigurationRevision;
        public readonly SignalDebtClientOrder[] Orders;
        public bool Success { get { return Result == SignalDebtFolderResult.Ready; } }

        internal SignalDebtFolderRead(SignalDebtFolderResult result, string folderId, string message,
            string label, int revision, SignalDebtClientOrder[] orders)
        {
            Result = result; FolderId = folderId; Message = message; AccessLabel = label;
            ConfigurationRevision = revision; Orders = orders;
        }
    }

    /// <summary>
    /// One persistent client workstation. Commands mutate a detached campaign candidate;
    /// the caller must save it before publishing observations, payment or UI success.
    /// This is a bounded device/task model, not a population scheduler or a real computer.
    /// </summary>
    [Serializable]
    public sealed class SignalDebtClientDevice
    {
        public const int CurrentSchemaVersion = 1;
        public const string DeviceId = "southbank-workstation-01";
        public const string OwnerId = "copy_shop_client";
        public const string OrderFolderId = "southbank-order-folder";
        public const string StaffDeskId = "southbank-staff-desk";
        public const string FolderInputPortId = "southbank-order-folder:records-in";
        public const string OrderIndexServiceId = "southbank-order-index";
        public const string PrintQueueServiceId = "southbank-print-queue";
        public const string AssetCatalogServiceId = "southbank-asset-catalog";
        public const string OrderIndexOutputPortId = "southbank-order-index:records-out";
        public const string PrintQueueOutputPortId = "southbank-print-queue:print-jobs-out";
        public const string AssetCatalogOutputPortId = "southbank-asset-catalog:images-out";
        public const string FirstOrderId = "southbank-order-1042";
        public const string SecondOrderId = "southbank-order-1043";
        public const string OwnerTaskId = "southbank-open-and-prepare-1042";
        public const string RepairAccessLabel = "Order Folder only — authorized repair workspace";
        public const string StaffAccessLabel = "Staff Desk — private workspace; no access granted";

        public int schemaVersion;
        public string deviceId, ownerId, folderInputPortId;
        public bool powered;
        public SignalDebtDeviceGrant accessGrant;
        public bool sessionOpen;
        public SignalDebtDeviceView currentView;
        // Empty means physically disconnected; every nonempty value names a real output port.
        public string connectedEndpointId;
        public int configurationRevision;
        public bool hasUndoConnection;
        public string undoEndpointId;
        public List<SignalDebtClientOrder> orders;

        public bool ownerCheckQueued;
        public int queuedConfigurationRevision;
        public int ownerCheckCount;
        public SignalDebtFolderResult lastOwnerReadResult;
        public int lastOwnerObservedConfigurationRevision;
        public int lastOwnerObservedOrderVersion;
        public int lastSuccessfulConfigurationRevision;
        public int firstOrderCompletedAtCheck;

        // These retain old campaign facts, never a newly invented order/task observation.
        public bool legacyRepairTestRecorded;
        public bool legacyPaymentRecorded;

        public bool CurrentOwnerVerified
        {
            get
            {
                return Validate() && lastOwnerReadResult == SignalDebtFolderResult.Ready
                    && lastOwnerObservedConfigurationRevision == configurationRevision
                    && lastOwnerObservedOrderVersion == orders[0].version
                    && Readiness() == SignalDebtFolderResult.Ready;
            }
        }

        public bool OwnerHasCompletedFirstOrder
        {
            get { return Validate() && firstOrderCompletedAtCheck > 0; }
        }

        public static SignalDebtClientDevice CreateInitial()
        {
            return new SignalDebtClientDevice
            {
                schemaVersion = CurrentSchemaVersion, deviceId = DeviceId, ownerId = OwnerId,
                folderInputPortId = FolderInputPortId, powered = true,
                accessGrant = SignalDebtDeviceGrant.None, currentView = SignalDebtDeviceView.Closed,
                connectedEndpointId = "", undoEndpointId = "", configurationRevision = 0,
                queuedConfigurationRevision = -1, lastOwnerObservedConfigurationRevision = -1,
                lastSuccessfulConfigurationRevision = -1,
                orders = new List<SignalDebtClientOrder>
                {
                    new SignalDebtClientOrder { id = FirstOrderId, description = "Counter order: twenty bound handouts", version = 1 },
                    new SignalDebtClientOrder { id = SecondOrderId, description = "Later order: six window notices", version = 1 }
                }
            };
        }

        // The accepted flag matters even when the old repair had not been inspected yet.
        // Invalid legacy progression is rejected rather than silently repaired.
        public static SignalDebtClientDevice CreateFromLegacy(bool accepted, bool inspected,
            bool connected, bool tested, bool paid)
        {
            if ((inspected && !accepted) || (connected && !inspected)
                || (tested && !connected) || (paid && !tested)) return null;
            SignalDebtClientDevice device = CreateInitial();
            device.accessGrant = paid ? SignalDebtDeviceGrant.ReadOnlyAfterSettlement
                : accepted ? SignalDebtDeviceGrant.Repair : SignalDebtDeviceGrant.None;
            device.connectedEndpointId = connected ? OrderIndexOutputPortId : "";
            device.legacyRepairTestRecorded = tested;
            device.legacyPaymentRecorded = paid;
            return device;
        }

        public SignalDebtClientDevice Copy()
        {
            var copy = (SignalDebtClientDevice)MemberwiseClone();
            copy.orders = orders == null ? null : orders.ConvertAll(order => order == null ? null : order.Copy());
            return copy;
        }

        public bool Validate()
        {
            if (schemaVersion != CurrentSchemaVersion || deviceId != DeviceId || ownerId != OwnerId
                || folderInputPortId != FolderInputPortId || !KnownEndpoint(connectedEndpointId)
                || !KnownEndpoint(undoEndpointId) || configurationRevision < 0
                || (hasUndoConnection && (configurationRevision == 0 || undoEndpointId == connectedEndpointId))
                || (!hasUndoConnection && undoEndpointId != "")) return false;
            if (!Enum.IsDefined(typeof(SignalDebtDeviceGrant), accessGrant)
                || !Enum.IsDefined(typeof(SignalDebtDeviceView), currentView)
                || (sessionOpen && accessGrant == SignalDebtDeviceGrant.None)
                || (sessionOpen && currentView == SignalDebtDeviceView.Closed)
                || (!sessionOpen && currentView != SignalDebtDeviceView.Closed)) return false;
            if (legacyPaymentRecorded && (!legacyRepairTestRecorded
                || accessGrant != SignalDebtDeviceGrant.ReadOnlyAfterSettlement)) return false;
            if (legacyRepairTestRecorded && accessGrant == SignalDebtDeviceGrant.None) return false;
            if (orders == null || orders.Count != 2 || orders[0] == null || orders[1] == null) return false;
            if (orders[0].id != FirstOrderId || orders[0].description != "Counter order: twenty bound handouts"
                || orders[0].version != (orders[0].prepared ? 2 : 1)
                || orders[1].id != SecondOrderId || orders[1].description != "Later order: six window notices"
                || orders[1].version != 1 || orders[1].prepared) return false;
            if (ownerCheckCount < 0 || (ownerCheckCount > 0 && accessGrant == SignalDebtDeviceGrant.None)
                || firstOrderCompletedAtCheck < 0 || firstOrderCompletedAtCheck > ownerCheckCount
                || orders[0].prepared != (firstOrderCompletedAtCheck > 0)) return false;
            if (ownerCheckQueued)
            {
                if (accessGrant == SignalDebtDeviceGrant.None || ownerCheckCount == int.MaxValue
                    || queuedConfigurationRevision < 0 || queuedConfigurationRevision > configurationRevision) return false;
            }
            else if (queuedConfigurationRevision != -1) return false;
            if (ownerCheckCount == 0)
            {
                if (lastOwnerReadResult != SignalDebtFolderResult.NotObserved
                    || lastOwnerObservedConfigurationRevision != -1 || lastOwnerObservedOrderVersion != 0
                    || lastSuccessfulConfigurationRevision != -1) return false;
            }
            else
            {
                if (!OwnerObservationResult(lastOwnerReadResult) || lastOwnerObservedConfigurationRevision < 0
                    || lastOwnerObservedConfigurationRevision > configurationRevision
                    || lastOwnerObservedOrderVersion != orders[0].version) return false;
                if (lastOwnerReadResult == SignalDebtFolderResult.Ready
                    && (!orders[0].prepared || lastSuccessfulConfigurationRevision != lastOwnerObservedConfigurationRevision)) return false;
                if (lastOwnerReadResult != SignalDebtFolderResult.Ready
                    && (firstOrderCompletedAtCheck == ownerCheckCount
                        || lastSuccessfulConfigurationRevision == lastOwnerObservedConfigurationRevision)) return false;
                // At the current generation the actual endpoint/power must agree with the observation.
                if (lastOwnerObservedConfigurationRevision == configurationRevision
                    && lastOwnerReadResult != Readiness()) return false;
            }
            if (lastSuccessfulConfigurationRevision < -1
                || lastSuccessfulConfigurationRevision > lastOwnerObservedConfigurationRevision
                || orders[0].prepared != (lastSuccessfulConfigurationRevision >= 0)) return false;
            return true;
        }

        public bool TryGrantRepairAccess(out string message)
        {
            if (!ValidCommand(out message)) return false;
            if (accessGrant != SignalDebtDeviceGrant.None)
            { message = "The existing permission cannot be expanded by reopening the job."; return false; }
            accessGrant = SignalDebtDeviceGrant.Repair;
            message = "The client permits inspection and repair of the Order Folder connection.";
            return true;
        }

        // Called by the transaction that settles the job. A queued owner action remains valid.
        public bool TrySettleRepairAccess(out string message)
        {
            if (!ValidCommand(out message)) return false;
            if (accessGrant != SignalDebtDeviceGrant.Repair)
            { message = "No active repair permission is available to settle."; return false; }
            accessGrant = SignalDebtDeviceGrant.ReadOnlyAfterSettlement;
            hasUndoConnection = false; undoEndpointId = "";
            message = "Repair access is now read-only. The client keeps control of the workstation.";
            return true;
        }

        public bool TryBeginSession(out string message)
        {
            if (!ValidCommand(out message)) return false;
            if (accessGrant == SignalDebtDeviceGrant.None)
            { message = "Accept the scoped repair before opening the client workspace."; return false; }
            if (sessionOpen) { message = "The client workspace is already open."; return false; }
            sessionOpen = true; currentView = SignalDebtDeviceView.OrderFolder;
            message = powered ? "Connected to Southbank's permitted workspace." : "The workstation is powered off; its folder is unavailable.";
            return true;
        }

        public bool TryEndSession(out string message)
        {
            if (!ValidCommand(out message)) return false;
            if (!sessionOpen) { message = "The client workspace is already closed."; return false; }
            sessionOpen = false; currentView = SignalDebtDeviceView.Closed;
            message = "Remote view closed. The client's queued work continues.";
            return true;
        }

        public bool TryOpenFolder(string folderId, out string message)
        {
            if (!ValidCommand(out message)) return false;
            if (!sessionOpen) { message = "Open the authorized client workspace first."; return false; }
            SignalDebtDeviceView view;
            if (folderId == OrderFolderId) view = SignalDebtDeviceView.OrderFolder;
            else if (folderId == StaffDeskId) view = SignalDebtDeviceView.StaffDeskAccessLabel;
            else { message = "That folder is not part of this workspace."; return false; }
            if (currentView == view) { message = ReadFolder(folderId).Message; return false; }
            currentView = view; message = ReadFolder(folderId).Message;
            return true;
        }

        public SignalDebtFolderRead ReadOrderFolder() { return ReadFolder(OrderFolderId); }

        public SignalDebtFolderRead ReadFolder(string folderId)
        {
            var empty = new SignalDebtClientOrder[0];
            if (folderId == StaffDeskId)
                return new SignalDebtFolderRead(SignalDebtFolderResult.AccessLabelOnly, folderId,
                    StaffAccessLabel, StaffAccessLabel, configurationRevision, empty);
            if (folderId != OrderFolderId || !Validate())
                return new SignalDebtFolderRead(SignalDebtFolderResult.UnknownFolder, folderId,
                    "This folder cannot be read from the verified device state.", "Unavailable", configurationRevision, empty);
            SignalDebtFolderResult result = Readiness();
            SignalDebtClientOrder[] snapshot = result == SignalDebtFolderResult.Ready
                ? orders.ConvertAll(order => order.Copy()).ToArray() : empty;
            return new SignalDebtFolderRead(result, folderId, Describe(result), RepairAccessLabel,
                configurationRevision, snapshot);
        }

        public bool TryConnectEndpoint(string endpointId, out string message)
        {
            if (!CanEditConnection(out message)) return false;
            if (!KnownEndpoint(endpointId)) { message = "That output port is not present on this workstation."; return false; }
            if (endpointId == connectedEndpointId) { message = "That connection is already in place."; return false; }
            if (!CanAdvanceConfiguration(out message)) return false;
            undoEndpointId = connectedEndpointId; hasUndoConnection = true;
            connectedEndpointId = endpointId; AdvanceConfiguration();
            message = Describe(Readiness());
            return true;
        }

        public bool TryUndoConnection(out string message)
        {
            if (!CanEditConnection(out message)) return false;
            if (!hasUndoConnection) { message = "There is no previous connection to restore."; return false; }
            if (!CanAdvanceConfiguration(out message)) return false;
            connectedEndpointId = undoEndpointId; undoEndpointId = ""; hasUndoConnection = false;
            AdvanceConfiguration(); message = "Previous connection restored. " + Describe(Readiness());
            return true;
        }

        // OWNER/SYSTEM action only. Repair permission never authorizes player control
        // of the whole machine's power; presentation must not expose this as a repair tool.
        public bool TrySetPower(bool on, out string message)
        {
            if (!ValidCommand(out message)) return false;
            if (powered == on) { message = "The workstation's power state is unchanged."; return false; }
            if (!CanAdvanceConfiguration(out message)) return false;
            powered = on; AdvanceConfiguration();
            message = on ? "The owner powered on the workstation. A fresh check is required."
                : "The owner powered off the workstation. Pending checks were cancelled.";
            return true;
        }

        // The caller authorizes requesting the owner's check. It is independent of
        // the player's screen and may finish after leaving or settling the job.
        public bool TryQueueOwnerCheck(out string message)
        {
            if (!ValidCommand(out message)) return false;
            if (accessGrant == SignalDebtDeviceGrant.None)
            { message = "The client has not accepted this repair session."; return false; }
            if (ownerCheckQueued) { message = "The client's folder check is already queued."; return false; }
            if (CurrentOwnerVerified) { message = "The client already verified this configuration."; return false; }
            if (ownerCheckCount == int.MaxValue) { message = "Another check cannot be recorded."; return false; }
            ownerCheckQueued = true; queuedConfigurationRevision = configurationRevision;
            message = "The client will open the Order Folder and try the queued counter order.";
            return true;
        }

        // True means a queued task was processed, including a real failed read.
        // Success is represented by the resulting observation, never this return value.
        public bool TryStepOwnerTask(out string message)
        {
            if (!ValidCommand(out message)) return false;
            if (!ownerCheckQueued) { message = "There is no queued client action."; return false; }
            int queued = queuedConfigurationRevision;
            ownerCheckQueued = false; queuedConfigurationRevision = -1;
            if (queued != configurationRevision)
            { message = "The queued check refers to an old connection and was cancelled."; return true; }
            SignalDebtFolderRead read = ReadOrderFolder();
            ownerCheckCount++;
            lastOwnerReadResult = read.Result;
            lastOwnerObservedConfigurationRevision = configurationRevision;
            if (read.Success)
            {
                lastSuccessfulConfigurationRevision = configurationRevision;
                if (!orders[0].prepared)
                {
                    orders[0].prepared = true; orders[0].version = 2;
                    firstOrderCompletedAtCheck = ownerCheckCount;
                }
                message = "The client opened the Order Folder. Counter order 1042 is prepared; the later order is unchanged.";
            }
            else message = "The client could not open the Order Folder. " + read.Message;
            lastOwnerObservedOrderVersion = orders[0].version;
            return true;
        }

        private bool ValidCommand(out string message)
        {
            message = "";
            if (Validate()) return true;
            message = "The client device state could not be verified. No changes were made.";
            return false;
        }

        private bool CanEditConnection(out string message)
        {
            if (!ValidCommand(out message)) return false;
            if (!sessionOpen || accessGrant != SignalDebtDeviceGrant.Repair)
            { message = "Changing connections requires the active scoped repair permission."; return false; }
            if (!powered) { message = "The workstation is powered off; its connection tools are unavailable."; return false; }
            return true;
        }

        private bool CanAdvanceConfiguration(out string message)
        {
            message = "";
            if (configurationRevision != int.MaxValue) return true;
            message = "Another configuration change cannot be recorded.";
            return false;
        }

        private void AdvanceConfiguration()
        {
            configurationRevision++;
            ownerCheckQueued = false; queuedConfigurationRevision = -1;
        }

        private SignalDebtFolderResult Readiness()
        {
            if (!powered) return SignalDebtFolderResult.PoweredOff;
            if (connectedEndpointId == "") return SignalDebtFolderResult.Disconnected;
            if (connectedEndpointId == OrderIndexOutputPortId) return SignalDebtFolderResult.Ready;
            if (connectedEndpointId == PrintQueueOutputPortId) return SignalDebtFolderResult.PrintService;
            if (connectedEndpointId == AssetCatalogOutputPortId) return SignalDebtFolderResult.AssetService;
            return SignalDebtFolderResult.UnknownFolder;
        }

        private static bool KnownEndpoint(string endpoint)
        {
            return endpoint == "" || endpoint == OrderIndexOutputPortId
                || endpoint == PrintQueueOutputPortId || endpoint == AssetCatalogOutputPortId;
        }

        private static bool OwnerObservationResult(SignalDebtFolderResult result)
        {
            return result == SignalDebtFolderResult.Ready || result == SignalDebtFolderResult.PoweredOff
                || result == SignalDebtFolderResult.Disconnected || result == SignalDebtFolderResult.PrintService
                || result == SignalDebtFolderResult.AssetService;
        }

        private static string Describe(SignalDebtFolderResult result)
        {
            switch (result)
            {
                case SignalDebtFolderResult.Ready: return "The Order Index supplies order records. The Order Folder can open.";
                case SignalDebtFolderResult.PoweredOff: return "The workstation is powered off; no live order records are available.";
                case SignalDebtFolderResult.Disconnected: return "The Order Folder has no source connected to its records input.";
                case SignalDebtFolderResult.PrintService: return "Print Queue supplies print jobs, not order records. The Order Folder cannot read this connection.";
                case SignalDebtFolderResult.AssetService: return "Asset Catalog supplies images, not order records. The Order Folder cannot read this connection.";
                default: return "The Order Folder is unavailable.";
            }
        }
    }
}
