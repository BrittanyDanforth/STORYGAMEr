#if SIGNAL_DEBT_PORTABLE_TEST
using System;
using System.Text.Json;
using SignalDebt;

// Compiled with the production save/shop model. No Unity engine or test package required.
internal static class SignalDebtDomainTests
{
    private static int assertions;

    private static void Check(bool condition, string description)
    {
        assertions++;
        if (!condition) throw new Exception("FAIL: " + description);
    }

    private static SignalDebtSaveData CompleteRepair()
    {
        var state = new SignalDebtSaveData();
        Check(state.AcceptRepair() != null, "accept repair");
        Check(state.BeginClientSession() != null, "open the authorized client session");
        Check(state.InspectRepair() != null, "inspect accepted repair");
        Check(state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId) != null, "restore correct service");
        Check(state.TestRepair() != null && !state.CanClaimRepairPayment, "queue a real owner check without granting payment");
        Check(state.StepClientTask() != null && state.clientDevice.CurrentOwnerVerified, "owner reads the repaired device");
        Check(state.ClaimRepairPayment() != null, "claim verified fee");
        return state;
    }

    private static string Snapshot(SignalDebtSaveData state)
    { return JsonSerializer.Serialize(state, new JsonSerializerOptions { IncludeFields = true }); }

    private static void DeviceWorkAndReverification()
    {
        var state = new SignalDebtSaveData();
        string opening = Snapshot(state);
        Check(state.BeginClientSession() == null && state.InspectRepair() == null
            && state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId) == null
            && state.TestRepair() == null && state.StepClientTask() == null && state.ClaimRepairPayment() == null,
            "authorization and actual work cannot be skipped");
        Check(Snapshot(state) == opening, "unauthorized commands preserve the entire campaign");
        Check(state.AcceptRepair() != null && state.InspectRepair() == null,
            "accepting work does not inspect an unopened client session");
        Check(state.BeginClientSession() != null && state.InspectRepair() != null, "inspection requires an open scoped session");
        Check(state.OpenClientFolder(SignalDebtClientDevice.StaffDeskId) != null, "the neighboring workspace has an inspectable boundary");
        SignalDebtFolderRead restricted = state.clientDevice.ReadFolder(SignalDebtClientDevice.StaffDeskId);
        Check(restricted.Result == SignalDebtFolderResult.AccessLabelOnly && restricted.Orders.Length == 0
            && state.clientDevice.accessGrant == SignalDebtDeviceGrant.Repair,
            "reading the private access label reveals no records and grants no extra permission");
        Check(state.OpenClientFolder(SignalDebtClientDevice.OrderFolderId) != null, "return to the permitted folder");
        Check(state.ConnectRepair(SignalDebtClientDevice.PrintQueueOutputPortId) != null
            && state.clientDevice.ReadOrderFolder().Result == SignalDebtFolderResult.PrintService,
            "a wrong known endpoint becomes the actual diagnosable device connection");
        Check(state.TestRepair() != null && state.clientDevice.ownerCheckQueued && !state.serviceTestPassed,
            "requesting a check queues work rather than inventing success");
        Check(state.StepClientTask() != null && !state.serviceTestPassed && !state.CanClaimRepairPayment
            && state.clientMemories.Count == 0 && !state.clientDevice.OwnerHasCompletedFirstOrder,
            "the owner encounters the same bad connection without a reward or invented completed order");
        Check(state.UndoClientConnection() != null
            && state.clientDevice.ReadOrderFolder().Result == SignalDebtFolderResult.Disconnected,
            "undo restores the prior disconnected endpoint");
        string undone = Snapshot(state);
        Check(state.UndoClientConnection() == null && Snapshot(state) == undone, "connection undo is consumed once");
        Check(state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId) != null, "install the actual records connection");
        Check(state.TestRepair() != null && !state.CanClaimRepairPayment, "a good connection still requires the owner's check");
        Check(state.EndClientSession() != null && state.clientDevice.ownerCheckQueued, "owner work survives closing the remote view");
        Check(state.StepClientTask() != null && state.clientDevice.CurrentOwnerVerified
            && state.clientDevice.OwnerHasCompletedFirstOrder && state.CanClaimRepairPayment,
            "the owner opens the same folder and prepares an order after the player leaves");
        Check(state.clientDevice.orders[0].prepared && state.clientDevice.orders[0].version == 2
            && !state.clientDevice.orders[1].prepared && state.clientDevice.orders[1].version == 1,
            "one owner action changes only the first identified order");

        int completedAt = state.clientDevice.firstOrderCompletedAtCheck;
        int memories = state.clientMemories.Count;
        Check(state.BeginClientSession() != null
            && state.ConnectRepair(SignalDebtClientDevice.AssetCatalogOutputPortId) != null,
            "a later route can break the same device after historical success");
        Check(state.serviceConnected && state.serviceTestPassed && !state.clientDevice.CurrentOwnerVerified
            && !state.CanClaimRepairPayment && state.ClaimRepairPayment() == null,
            "historical repair milestones cannot pay a currently unverified native repair");
        Check(state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId) != null
            && !state.clientDevice.CurrentOwnerVerified && !state.CanClaimRepairPayment,
            "returning to the old endpoint cannot resurrect an old verification");
        Check(state.TestRepair() != null && state.StepClientTask() != null && state.clientDevice.CurrentOwnerVerified,
            "a completed order does not prevent a fresh verification after rerouting");
        Check(state.clientDevice.firstOrderCompletedAtCheck == completedAt && state.clientDevice.orders[0].version == 2
            && state.clientMemories.Count == memories, "reverification cannot repeat order completion or the milestone memory");
        string verified = Snapshot(state);
        Check(state.StepClientTask() == null && state.TestRepair() == null && Snapshot(state) == verified,
            "duplicate owner callbacks and an already verified check leave the campaign unchanged");

        SignalDebtSaveData copy = state.Copy();
        Check(!ReferenceEquals(copy.clientDevice, state.clientDevice)
            && !ReferenceEquals(copy.clientDevice.orders, state.clientDevice.orders)
            && !ReferenceEquals(copy.clientDevice.orders[0], state.clientDevice.orders[0]),
            "campaign copies detach the device, order list and individual records");
        copy.clientDevice.orders[0].description = "Changed detached order";
        copy.clientDevice.connectedEndpointId = SignalDebtClientDevice.PrintQueueOutputPortId;
        copy.ledger.Find(entry => entry.actorId == SignalDebtClientDevice.OwnerId).recordId = "Changed copied provenance";
        Check(Snapshot(state) == verified, "mutating copied device records or event provenance cannot alter the live campaign");
        SignalDebtFolderRead read = state.clientDevice.ReadOrderFolder();
        read.Orders[0].description = "Changed detached observation";
        read.Orders[1] = null;
        Check(Snapshot(state) == verified, "mutating a folder observation cannot change authoritative orders");
        Check(state.Validate(), "device work, undo and reverification retain valid campaign history");
    }

    private static void ConfigurationChangeCancelsQueuedWork()
    {
        var state = new SignalDebtSaveData();
        state.AcceptRepair(); state.BeginClientSession(); state.InspectRepair();
        state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId);
        Check(state.TestRepair() != null && state.clientDevice.ownerCheckQueued, "queue a check against the working configuration");
        int queuedRevision = state.clientDevice.queuedConfigurationRevision;
        Check(state.ConnectRepair(SignalDebtClientDevice.PrintQueueOutputPortId) != null
            && state.clientDevice.configurationRevision > queuedRevision && !state.clientDevice.ownerCheckQueued,
            "a real reroute cancels pending work tied to the earlier configuration");
        string rerouted = Snapshot(state);
        Check(state.StepClientTask() == null && Snapshot(state) == rerouted
            && state.clientDevice.ownerCheckCount == 0 && !state.serviceTestPassed && !state.CanClaimRepairPayment,
            "a delayed callback cannot use the old good route to invent success after rerouting");
        Check(state.TestRepair() != null && state.StepClientTask() != null
            && state.clientDevice.lastOwnerReadResult == SignalDebtFolderResult.PrintService
            && state.clientDevice.ownerCheckCount == 1 && state.clientMemories.Count == 0 && state.Validate(),
            "a fresh owner task observes the actual wrong connection without inventing a successful memory");
    }

    private static SignalDebtLedgerEvent LastOwnerObservation(SignalDebtSaveData state)
    {
        return state.ledger.FindLast(entry => entry.actorId == SignalDebtClientDevice.OwnerId
            && (entry.type == "service_test_passed" || entry.type == "client_check_failed"));
    }

    private static void RejectObservationCorruption(SignalDebtSaveData original,
        Action<SignalDebtSaveData> corrupt, string description)
    {
        string unchanged = Snapshot(original);
        SignalDebtSaveData invalid = original.Copy();
        corrupt(invalid);
        string damaged = Snapshot(invalid);
        Check(!invalid.Validate(), description);
        Check(Snapshot(invalid) == damaged && Snapshot(original) == unchanged,
            "observation validation neither repairs corrupted evidence nor mutates the source campaign");
    }

    private static void OwnerObservationProvenance()
    {
        var state = new SignalDebtSaveData();
        state.AcceptRepair(); state.BeginClientSession(); state.InspectRepair();
        state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId); state.TestRepair(); state.StepClientTask();
        Check(state.Validate() && state.clientDevice.CurrentOwnerVerified
            && LastOwnerObservation(state).type == "service_test_passed", "owner provenance fixture has a genuine successful observation");
        RejectObservationCorruption(state, invalid => LastOwnerObservation(invalid).type = "client_check_failed",
            "a successful device observation cannot be paired with a failed owner event");
        RejectObservationCorruption(state, invalid => LastOwnerObservation(invalid).deviceRevision--,
            "an owner event cannot claim an earlier configuration than the device actually observed");
        RejectObservationCorruption(state, invalid => LastOwnerObservation(invalid).recordId = SignalDebtClientDevice.SecondOrderId,
            "an observation of the first order cannot acquire another record's provenance");
        RejectObservationCorruption(state, invalid => LastOwnerObservation(invalid).actorId = "player",
            "changing an owner observation into player testimony cannot validate completed owner work");
        RejectObservationCorruption(state, invalid => LastOwnerObservation(invalid).deviceId = "another-workstation",
            "a different device's observation cannot substantiate this device's completed work");

        state.ConnectRepair(SignalDebtClientDevice.PrintQueueOutputPortId); state.TestRepair(); state.StepClientTask();
        Check(state.Validate() && !state.clientDevice.CurrentOwnerVerified && state.serviceTestPassed
            && LastOwnerObservation(state).type == "client_check_failed", "a later genuine failure coexists with historical success");
        RejectObservationCorruption(state, invalid => LastOwnerObservation(invalid).type = "service_test_passed",
            "historical success cannot disguise the latest failed owner read as a new successful event");
        RejectObservationCorruption(state, invalid => LastOwnerObservation(invalid).deviceRevision--,
            "the latest failed observation remains tied to its actual configuration revision");
    }

    private static int Main()
    {
        try
        {
            var fresh = new SignalDebtSaveData();
            Check(fresh.Validate(), "initial save is valid");
            Check(fresh.balance == 80 && fresh.rentDue == 450, "opening economy");
            DeviceWorkAndReverification();
            ConfigurationChangeCancelsQueuedWork();
            OwnerObservationProvenance();

            var state = CompleteRepair();
            Check(state.balance == 200 && state.jobPaid && state.Validate(), "120 credit fee settles");
            Check(state.clientMemories.Count == 2 && state.clientDevice.accessGrant == SignalDebtDeviceGrant.ReadOnlyAfterSettlement,
                "payment retains two client memories and closes repair write permission");
            string settled = Snapshot(state);
            for (int i = 0; i < 100; i++)
            {
                Check(state.ClaimRepairPayment() == null, "duplicate fee rejected");
                state.AcceptRepair(); state.InspectRepair();
                state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId); state.TestRepair(); state.StepClientTask();
            }
            Check(Snapshot(state) == settled, "repeated callbacks cannot duplicate rewards, orders or memories");
            int revision = state.revision;
            Check(state.ApplyRentPayment(-50) == null && state.ApplyRentPayment(0) == null
                && state.revision == revision, "negative and zero rent requests rejected");
            Check(state.ApplyRentPayment(50).amount == -50 && state.balance == 150
                && state.rentDue == 400, "partial rent deducts matching amounts");
            Check(state.ApplyRentPayment(int.MaxValue).amount == -150
                && state.balance == 0 && state.rentDue == 250, "rent capped at available funds");
            Check(state.ApplyRentPayment(50) == null, "cannot overdraw wallet");
            Check(state.ExtendRentDeadline() != null && state.rentDeadlineDay == 6,
                "one extension changes deadline by two days");
            Check(state.ExtendRentDeadline() == null && state.rentDeadlineDay == 6,
                "extension cannot repeat");
            Check(state.AdvanceDay().day == 2 && state.rentDue == 250 && state.balance == 0,
                "rest advances day without inventing funds or clearing debt");
            Check(state.Validate(), "full scenario remains valid");
            for (int i = 0; i < state.ledger.Count; i++)
                Check(state.ledger[i].worldRevision == i + 1, "ledger revisions are ordered");

            var smallDebt = new SignalDebtSaveData { rentDue = 30 };
            Check(smallDebt.ApplyRentPayment(80).amount == -30 && smallDebt.balance == 50
                && smallDebt.rentDue == 0, "rent capped at remaining debt");
            Check(smallDebt.ExtendRentDeadline() == null, "cleared debt needs no extension");
            var corrupt = new SignalDebtSaveData { jobPaid = true };
            Check(!corrupt.Validate(), "paid flag without receipt rejected");
            corrupt = CompleteRepair(); corrupt.serviceConnected = false;
            Check(!corrupt.Validate(), "broken prerequisite chain rejected");
            corrupt = new SignalDebtSaveData(); corrupt.ledger.Add(null);
            Check(!corrupt.Validate(), "null ledger record rejected");
            corrupt = CompleteRepair(); corrupt.ledger.Add(corrupt.ledger[0]); corrupt.revision++;
            Check(!corrupt.Validate(), "duplicate receipt record rejected");
            corrupt = new SignalDebtSaveData { revision = 1 };
            Check(!corrupt.Validate(), "revision without history rejected");
            SignalDebtShopTests.Run(Check);
            Console.WriteLine("PASS: " + assertions + " domain assertions; no Unity runtime exercised.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
#endif
