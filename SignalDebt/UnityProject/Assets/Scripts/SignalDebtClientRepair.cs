using System;

namespace SignalDebt
{
    // Campaign commands wrap the shared workstation. The transaction owner saves a
    // detached candidate before publishing any connection, order, observation or fee.
    public sealed partial class SignalDebtSaveData
    {
        public SignalDebtClientDevice clientDevice = SignalDebtClientDevice.CreateInitial();

        public bool CanClaimRepairPayment
        {
            get
            {
                if (!jobAccepted || jobPaid || HasEvent("repair-job-payment") || clientDevice == null) return false;
                // A verified old save keeps its already-earned fee. Any subsequent
                // configuration change requires a new real owner observation.
                bool preservedEntitlement = clientDevice.legacyRepairTestRecorded
                    && clientDevice.configurationRevision == 0 && clientDevice.ReadOrderFolder().Success;
                return clientDevice.CurrentOwnerVerified || preservedEntitlement;
            }
        }

        public SignalDebtLedgerEvent BeginClientSession()
        {
            if (!jobAccepted || clientDevice == null || !clientDevice.TryBeginSession(out string message)) return null;
            return DeviceEvent("client_session_opened", message);
        }

        public SignalDebtLedgerEvent EndClientSession()
        {
            if (clientDevice == null || !clientDevice.TryEndSession(out string message)) return null;
            return DeviceEvent("client_session_closed", message);
        }

        public SignalDebtLedgerEvent OpenClientFolder(string folderId)
        {
            if (!jobAccepted || clientDevice == null || !clientDevice.TryOpenFolder(folderId, out string message)) return null;
            return DeviceEvent("client_workspace_viewed", message);
        }

        public SignalDebtLedgerEvent ConnectRepair(string endpointId)
        {
            if (!jobAccepted || !serviceInspected || clientDevice == null
                || !clientDevice.TryConnectEndpoint(endpointId, out string message)) return null;
            bool firstWorkingConnection = !serviceConnected && clientDevice.ReadOrderFolder().Success;
            if (firstWorkingConnection) serviceConnected = true;
            return DeviceEvent("service_connection_changed", "Changed the Order Folder connection. " + message,
                firstWorkingConnection ? "repair-service-connected" : null);
        }

        public SignalDebtLedgerEvent UndoClientConnection()
        {
            if (!jobAccepted || !serviceInspected || clientDevice == null
                || !clientDevice.TryUndoConnection(out string message)) return null;
            return DeviceEvent("service_connection_undone", message);
        }

        // Asking the owner is a saved request, not a successful test result.
        public SignalDebtLedgerEvent TestRepair()
        {
            if (!jobAccepted || clientDevice == null || !clientDevice.TryQueueOwnerCheck(out string message)) return null;
            return DeviceEvent("client_check_requested", message);
        }

        // The local owner task runs independently of the current UI and cloud dialogue.
        public SignalDebtLedgerEvent StepClientTask()
        {
            int previousCheckCount = clientDevice == null ? 0 : clientDevice.ownerCheckCount;
            if (!jobAccepted || clientDevice == null || !clientDevice.TryStepOwnerTask(out string message)) return null;
            if (previousCheckCount == clientDevice.ownerCheckCount)
                return DeviceEvent("client_check_cancelled", message, null, SignalDebtClientDevice.OwnerId);
            bool firstSuccess = clientDevice.CurrentOwnerVerified && !serviceTestPassed;
            if (firstSuccess)
            {
                serviceTestPassed = true;
                clientMemories.Add("copy_shop_client: opened the repaired order folder and prepared counter order 1042.");
            }
            SignalDebtLedgerEvent entry = DeviceEvent(clientDevice.CurrentOwnerVerified ? "service_test_passed" : "client_check_failed",
                message, firstSuccess ? "repair-service-test" : null, SignalDebtClientDevice.OwnerId);
            if (entry != null) entry.recordId = SignalDebtClientDevice.FirstOrderId;
            return entry;
        }

        private SignalDebtLedgerEvent DeviceEvent(string type, string summary, string stableId = null, string actorId = "player")
        {
            SignalDebtLedgerEvent entry = Record(type, summary, 0, stableId);
            if (entry == null) return null;
            entry.actorId = actorId;
            entry.deviceId = SignalDebtClientDevice.DeviceId;
            entry.deviceRevision = clientDevice.configurationRevision;
            return entry;
        }

        private bool ValidateClientRepair()
        {
            if (clientDevice == null || !clientDevice.Validate()) return false;
            SignalDebtDeviceGrant grant = jobPaid ? SignalDebtDeviceGrant.ReadOnlyAfterSettlement
                : jobAccepted ? SignalDebtDeviceGrant.Repair : SignalDebtDeviceGrant.None;
            if (clientDevice.accessGrant != grant) return false;
            if (clientDevice.legacyPaymentRecorded && !jobPaid) return false;
            if (serviceTestPassed != (clientDevice.legacyRepairTestRecorded || clientDevice.OwnerHasCompletedFirstOrder)) return false;
            if (clientDevice.ReadOrderFolder().Success && !serviceConnected) return false;
            if (serviceConnected && !HasEvent("repair-service-connected")) return false;
            if (serviceTestPassed && !HasEvent("repair-service-test")) return false;
            if (serviceInspected != HasEvent("repair-service-inspected") || jobAccepted != HasEvent("repair-job-accepted")) return false;
            int observedChecks = 0;
            SignalDebtLedgerEvent latestObservation = null;
            foreach (SignalDebtLedgerEvent entry in ledger)
            {
                if (entry.deviceId != SignalDebtClientDevice.DeviceId) continue;
                if (entry.deviceRevision < 0 || entry.deviceRevision > clientDevice.configurationRevision) return false;
                if (entry.actorId == SignalDebtClientDevice.OwnerId
                    && (entry.type == "service_test_passed" || entry.type == "client_check_failed"))
                {
                    if (entry.recordId != SignalDebtClientDevice.FirstOrderId) return false;
                    observedChecks++;
                    latestObservation = entry;
                }
            }
            if (observedChecks != clientDevice.ownerCheckCount) return false;
            if (latestObservation != null)
            {
                bool passed = clientDevice.lastOwnerReadResult == SignalDebtFolderResult.Ready;
                if (latestObservation.type != (passed ? "service_test_passed" : "client_check_failed")
                    || latestObservation.deviceRevision != clientDevice.lastOwnerObservedConfigurationRevision) return false;
            }
            return true;
        }
    }
}
