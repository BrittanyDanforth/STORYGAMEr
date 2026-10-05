#if SIGNAL_DEBT_PORTABLE_TEST
using System;
using System.Text.Json;
using SignalDebt;

internal static class SignalDebtShopTests
{
    private static string Snapshot(SignalDebtSaveData state)
    { return JsonSerializer.Serialize(state, new JsonSerializerOptions { IncludeFields = true }); }

    private static SignalDebtSaveData Funded()
    {
        var state = new SignalDebtSaveData();
        state.AcceptRepair(); state.BeginClientSession(); state.InspectRepair();
        state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId);
        state.TestRepair(); state.StepClientTask(); state.ClaimRepairPayment();
        return state;
    }

    // These are the fields/events the delivered schema-one/two model wrote.
    // No device, session, queue or owner-order events are synthesized into them.
    private static SignalDebtSaveData LegacyRepaired(int schema, bool paid = true)
    {
        var state = new SignalDebtSaveData
        {
            schemaVersion = schema, clientDevice = null,
            jobAccepted = true, serviceInspected = true, serviceConnected = true,
            serviceTestPassed = true, jobPaid = paid, balance = paid ? 200 : 80
        };
        state.Record("job_accepted", "Accepted the authorized repair.", 0, "repair-job-accepted");
        state.Record("service_inspected", "Inspected the service.", 0, "repair-service-inspected");
        state.Record("service_connected", "Restored the permitted connection.", 0, "repair-service-connected");
        state.Record("service_test_passed", "The client opened the order list.", 0, "repair-service-test");
        state.clientMemories.Add("copy_shop_client: the repair test passed.");
        if (paid)
        {
            state.Record("job_paid", "Paid the agreed repair fee.", 120, "repair-job-payment");
            state.clientMemories.Add("copy_shop_client: paid the agreed 120 credits; the job is complete.");
        }
        return state;
    }

    public static void Run(Action<bool, string> check)
    {
        ExistingCommandsSaveBeforePublish(check);
        OwnerTaskAndPaymentRollback(check);
        var fresh = new SignalDebtSaveData();
        string opening = Snapshot(fresh);
        SignalDebtShopQuote quote = fresh.InspectUsedGpu();
        check(quote != null && quote.price == SignalDebtShopCatalog.Price, "inspection exposes the current price");
        check(Snapshot(fresh) == opening, "inspection changes no campaign data");
        check(!fresh.TryCreateUsedGpuPurchase(quote, out _, out _, out _), "opening funds cannot buy this card");
        check(Snapshot(fresh) == opening, "insufficient funds leave the entire snapshot unchanged");

        SignalDebtSaveData state = Funded();
        int repairRevision = state.revision;
        string before = Snapshot(state);
        quote = state.InspectUsedGpu();
        SignalDebtShopQuote[] rejected =
        {
            null,
            new SignalDebtShopQuote("another-campaign", quote.revision, quote.stockId, quote.itemId, quote.price),
            new SignalDebtShopQuote(quote.saveId, quote.revision - 1, quote.stockId, quote.itemId, quote.price),
            new SignalDebtShopQuote(quote.saveId, quote.revision, "other-stock", quote.itemId, quote.price),
            new SignalDebtShopQuote(quote.saveId, quote.revision, quote.stockId, "other-item", quote.price),
            new SignalDebtShopQuote(quote.saveId, quote.revision, quote.stockId, quote.itemId, 0),
            new SignalDebtShopQuote(quote.saveId, quote.revision, quote.stockId, quote.itemId, quote.price - 1)
        };
        foreach (SignalDebtShopQuote invalid in rejected)
        {
            check(!state.TryCreateUsedGpuPurchase(invalid, out var none, out var noEvent, out string error)
                && none == null && noEvent == null && !string.IsNullOrEmpty(error), "invalid quote has a specific refusal");
            check(Snapshot(state) == before, "refused quote cannot change money, stock, receipts or history");
        }

        int writes = 0;
        check(!state.ConfirmUsedGpuPurchase(quote, candidate =>
        {
            writes++;
            check(candidate.Validate() && candidate.balance == 110 && candidate.OwnsUsedGpu,
                "save candidate already contains the complete transaction");
            check(Snapshot(state) == before, "sale is unpublished while persistence runs");
            return false;
        }, out var failedEvent, out _), "failed persistence refuses the handoff");
        check(failedEvent == null && writes == 1 && Snapshot(state) == before,
            "failed save produces no event, debit, ownership or lost stock");

        SignalDebtSaveData persisted = null;
        check(state.ConfirmUsedGpuPurchase(quote, candidate =>
        {
            writes++;
            persisted = candidate.Copy();
            return true;
        }, out var sale, out _), "same offer can be retried after failed persistence");
        check(writes == 2 && state.balance == 110 && state.OwnsUsedGpu && !state.UsedGpuInStock,
            "one successful debit transfers the identified physical unit");
        check(state.Validate() && sale.amount == -90 && state.revision == repairRevision + 1 && state.ledger.Count == repairRevision + 1,
            "sale and receipt form one new world event");
        check(Snapshot(state) == Snapshot(persisted), "published world exactly matches the saved candidate");
        check(state.usedGpuPurchase.ledgerEventId == sale.id && state.usedGpuPurchase.worldRevision == sale.worldRevision,
            "owned item has its original receipt and ledger provenance");
        check(state.clientMemories.Count == 2, "purchase does not leak into the copy-shop client's memory");
        string paid = Snapshot(state);
        for (int attempt = 0; attempt < 100; attempt++)
            check(!state.ConfirmUsedGpuPurchase(quote, candidate => { writes++; return true; }, out _, out _),
                "repeated handoff callback cannot spend or award twice");
        check(writes == 2 && Snapshot(state) == paid && state.InspectUsedGpu() == null,
            "sold stock and receipt survive all duplicate confirmations");
        check(!persisted.ConfirmUsedGpuPurchase(quote, candidate => true, out _, out _)
            && Snapshot(persisted) == paid, "reloaded purchased snapshot cannot buy the same unit again");

        var stale = Funded();
        SignalDebtShopQuote old = stale.InspectUsedGpu();
        stale.ApplyRentPayment(50);
        string changed = Snapshot(stale);
        check(!stale.TryCreateUsedGpuPurchase(old, out _, out _, out _) && Snapshot(stale) == changed,
            "another real transaction invalidates a previously inspected offer");
        check(stale.TryCreateUsedGpuPurchase(stale.InspectUsedGpu(), out var refreshed, out _, out _)
            && refreshed.balance == 60, "fresh inspection reflects the current budget");

        Action<SignalDebtSaveData>[] corruptions =
        {
            s => s.shopPurchases = null,
            s => s.shopPurchases.Add(s.usedGpuPurchase.Copy()),
            s => s.shopPurchases[0] = null,
            s => s.usedGpuPurchase = null,
            s => s.usedGpuPurchase.receiptId = "",
            s => s.usedGpuPurchase.itemId = "another-card",
            s => s.usedGpuPurchase.stockId = "another-stock",
            s => s.usedGpuPurchase.sellerId = "another-seller",
            s => s.usedGpuPurchase.productId = "another-product",
            s => s.usedGpuPurchase.paidAmount = 0,
            s => s.usedGpuPurchase.paidAmount++,
            s => s.usedGpuPurchase.day++,
            s => s.usedGpuPurchase.worldRevision--,
            s => s.usedGpuPurchase.ledgerEventId = "another-event",
            s => s.ledger[s.ledger.Count - 1].type = "rent_paid",
            s => { s.ledger.RemoveAt(s.ledger.Count - 1); s.revision--; }
        };
        foreach (Action<SignalDebtSaveData> corrupt in corruptions)
        {
            SignalDebtSaveData invalid = state.Copy();
            corrupt(invalid);
            string damaged = Snapshot(invalid);
            check(!invalid.Validate(), "mismatched ownership, stock, money or receipt fails validation");
            check(Snapshot(invalid) == damaged, "validation never repairs or mutates an invalid transaction");
            check(Snapshot(state) == paid, "copied transaction has no mutable aliases into the live save");
        }
        var historicPrice = state.Copy();
        historicPrice.usedGpuPurchase.paidAmount = 75;
        historicPrice.ledger[historicPrice.ledger.Count - 1].amount = -75;
        historicPrice.balance = 125;
        check(historicPrice.Validate(), "historical receipt price is independent of a future catalog price");

        var legacy = LegacyRepaired(1);
        string legacyBefore = Snapshot(legacy);
        var upgraded = legacy.MigrateV1();
        check(upgraded != null && upgraded.Validate() && upgraded.schemaVersion == SignalDebtSaveData.CurrentSchemaVersion,
            "valid v1 has an explicit upgrade to the current device schema");
        check(Snapshot(legacy) == legacyBefore, "migration leaves the legacy object unchanged");
        check(upgraded.saveId == legacy.saveId && upgraded.balance == legacy.balance && upgraded.revision == legacy.revision
            && upgraded.rentDue == legacy.rentDue && upgraded.usedGpuPurchase == null, "upgrade preserves campaign identity and commitments");
        check(upgraded.clientDevice.legacyRepairTestRecorded && upgraded.clientDevice.legacyPaymentRecorded
            && !upgraded.clientDevice.OwnerHasCompletedFirstOrder && !upgraded.clientDevice.CurrentOwnerVerified
            && upgraded.clientDevice.ownerCheckCount == 0, "legacy facts do not invent a new owner task or current verification");
        upgraded.ledger[0].summary = "changed copy";
        upgraded.clientMemories.Clear();
        check(Snapshot(legacy) == legacyBefore, "migration deep-copies history and memory");
        var malformedLegacy = state.Copy(); malformedLegacy.schemaVersion = 1;
        check(malformedLegacy.MigrateV1() == null, "alleged v1 cannot contain a purchase");
        malformedLegacy.usedGpuPurchase = null;
        check(malformedLegacy.MigrateV1() == null, "legacy migration refuses an orphan sale event");
        check(state.MigrateV1() == null, "current schema is not silently reinterpreted as legacy");
        legacy.shopPurchases = null;
        legacyBefore = Snapshot(legacy);
        check(legacy.MigrateV1()?.Validate() == true && Snapshot(legacy) == legacyBefore,
            "explicit migration adds the absent legacy list without changing its source");
        var previous = LegacyRepaired(2, false);
        string previousBefore = Snapshot(previous);
        var migrated = SignalDebtSaveData.MigrateV2(previous);
        check(migrated != null && migrated.Validate() && migrated.CanClaimRepairPayment
            && !migrated.clientDevice.CurrentOwnerVerified && !migrated.clientDevice.OwnerHasCompletedFirstOrder,
            "a genuinely tested but unpaid v2 repair retains its historical payment entitlement");
        check(Snapshot(previous) == previousBefore && migrated.saveId == previous.saveId
            && migrated.revision == previous.revision && migrated.clientMemories.Count == previous.clientMemories.Count,
            "v2 migration preserves its source, identity, event history and memories");
        check(migrated.ClaimRepairPayment() != null && migrated.balance == 200 && migrated.Validate()
            && migrated.ClaimRepairPayment() == null && !migrated.clientDevice.OwnerHasCompletedFirstOrder,
            "historical v2 entitlement pays once without fabricating new device work");
        var withPurchase = SignalDebtSaveData.MigrateV2(LegacyRepaired(2));
        check(withPurchase.ConfirmUsedGpuPurchase(withPurchase.InspectUsedGpu(), candidate => true, out _, out _),
            "legacy-derived purchase fixture has a complete receipt");
        withPurchase.schemaVersion = 2; withPurchase.clientDevice = null;
        string purchaseBefore = Snapshot(withPurchase);
        var migratedPurchase = SignalDebtSaveData.MigrateV2(withPurchase);
        check(migratedPurchase != null && migratedPurchase.Validate()
            && migratedPurchase.usedGpuPurchase.receiptId == withPurchase.usedGpuPurchase.receiptId
            && migratedPurchase.balance == withPurchase.balance && migratedPurchase.revision == withPurchase.revision,
            "v2 migration retains purchased stock identity, receipt, funds and event revision");
        migratedPurchase.usedGpuPurchase.paidAmount++;
        migratedPurchase.clientDevice.orders[0].description = "Changed migrated device";
        check(Snapshot(withPurchase) == purchaseBefore, "v2 migration detaches purchases and initializes a separate device");
        check(SignalDebtSaveData.MigrateV2(state) == null, "v3 is not silently reinterpreted as v2");
        var missingCurrentList = new SignalDebtSaveData { shopPurchases = null };
        check(!missingCurrentList.Validate() && missingCurrentList.Copy().shopPurchases == null,
            "current missing list is invalid and copying cannot silently repair it");
        var emptyReceipt = new SignalDebtSaveData { usedGpuPurchase = new SignalDebtPurchase() };
        check(!emptyReceipt.Validate(), "an empty serialized receipt is never mistaken for unsold stock");
        var missingDevice = new SignalDebtSaveData { clientDevice = null };
        check(!missingDevice.Validate() && missingDevice.Copy().clientDevice == null,
            "current missing device is invalid and copying cannot silently create one");
    }

    private static void ExistingCommandsSaveBeforePublish(Action<bool, string> check)
    {
        var live = new SignalDebtSaveData();
        var identity = live;
        Func<SignalDebtSaveData, SignalDebtLedgerEvent>[] commands =
        {
            s => s.AcceptRepair(), s => s.BeginClientSession(), s => s.InspectRepair(),
            s => s.OpenClientFolder(SignalDebtClientDevice.StaffDeskId),
            s => s.OpenClientFolder(SignalDebtClientDevice.OrderFolderId),
            s => s.ConnectRepair(SignalDebtClientDevice.PrintQueueOutputPortId),
            s => s.TestRepair(), s => s.StepClientTask(), s => s.UndoClientConnection(),
            s => s.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId),
            s => s.TestRepair(), s => s.EndClientSession(), s => s.StepClientTask(),
            s => s.ClaimRepairPayment(), s => s.ApplyRentPayment(50),
            s => s.ExtendRentDeadline(), s => s.AdvanceDay()
        };
        foreach (var command in commands)
        {
            string before = Snapshot(live);
            int writes = 0;
            check(!live.TryCommitChange(command, candidate =>
            {
                writes++;
                check(candidate.Validate() && candidate.revision == live.revision + 1,
                    "each existing command presents a complete valid next snapshot");
                check(Snapshot(live) == before, "existing command stays unpublished during save");
                return false;
            }, out var rejected, out string error), "failed save rejects device, owner-task, repair, rent, extension and day changes");
            check(rejected == null && writes == 1 && !string.IsNullOrEmpty(error) && Snapshot(live) == before,
                "failed command leaves live money, flags, memory and history unchanged with no event");

            SignalDebtSaveData saved = null;
            check(live.TryCommitChange(command, candidate => { writes++; saved = candidate.Copy(); return true; },
                out var committed, out _), "existing command succeeds when retry is saved");
            check(writes == 2 && committed != null && ReferenceEquals(live, identity) && Snapshot(live) == Snapshot(saved),
                "successful command publishes exactly the saved snapshot without replacing campaign identity");
        }
        check(live.balance == 150 && live.rentDue == 400 && live.day == 2 && live.rentDeadlineDay == 6
            && live.clientMemories.Count == 2, "repair, rent, extension and rest retain their intended results");
        string unchanged = Snapshot(live);
        int unexpectedWrites = 0;
        check(!live.TryCommitChange(s => s.ClaimRepairPayment(), s => { unexpectedWrites++; return true; }, out _, out _)
            && unexpectedWrites == 0 && Snapshot(live) == unchanged, "duplicate reward command never writes or publishes");
        check(!live.TryCommitChange(s => { s.balance = -1; return s.AdvanceDay(); },
            s => { unexpectedWrites++; return true; }, out var invalid, out _)
            && invalid == null && unexpectedWrites == 0 && Snapshot(live) == unchanged,
            "invalid candidate cannot reach persistence or affect a live campaign");
        check(!live.TryCommitChange(s => { s.balance = 0; return null; },
            s => { unexpectedWrites++; return true; }, out _, out _)
            && unexpectedWrites == 0 && Snapshot(live) == unchanged,
            "a command without a committed event cannot leak its temporary mutation");
    }

    private static void OwnerTaskAndPaymentRollback(Action<bool, string> check)
    {
        var live = new SignalDebtSaveData();
        live.AcceptRepair(); live.BeginClientSession(); live.InspectRepair();
        live.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId); live.TestRepair();
        SignalDebtSaveData durable = live.Copy();
        string queued = Snapshot(live);
        check(live.clientDevice.ownerCheckQueued && !live.clientDevice.OwnerHasCompletedFirstOrder,
            "owner rollback fixture has pending real work");
        check(!live.TryCommitChange(candidate => candidate.StepClientTask(), candidate =>
        {
            check(candidate.clientDevice.CurrentOwnerVerified && candidate.clientDevice.OwnerHasCompletedFirstOrder
                && !candidate.clientDevice.ownerCheckQueued, "staged owner action contains observation and one completed order");
            check(Snapshot(live) == queued && Snapshot(durable) == queued,
                "neither live nor persisted snapshot sees owner work before a successful save");
            return false;
        }, out var rejected, out _), "failed owner-action save rejects publication");
        check(rejected == null && Snapshot(live) == queued && live.clientDevice.ownerCheckQueued
            && live.clientMemories.Count == 0 && !live.CanClaimRepairPayment,
            "failed owner action retains its queue with no completion, knowledge or payment eligibility");
        var restored = durable.Copy();
        check(restored.TryCommitChange(candidate => candidate.StepClientTask(), candidate => { durable = candidate.Copy(); return true; },
            out var observed, out _) && observed != null && restored.clientDevice.CurrentOwnerVerified,
            "a reloaded pending task can complete once after the write succeeds");
        string verified = Snapshot(restored);
        check(!restored.TryCommitChange(candidate => candidate.ClaimRepairPayment(), candidate =>
        {
            check(candidate.balance == 200 && candidate.jobPaid
                && candidate.clientDevice.accessGrant == SignalDebtDeviceGrant.ReadOnlyAfterSettlement,
                "staged payment includes the fee, receipt and settled permission together");
            return false;
        }, out var unpaid, out _) && unpaid == null && Snapshot(restored) == verified && Snapshot(durable) == verified,
            "failed payment preserves live and reloadable verified-but-unpaid state");
        check(restored.CanClaimRepairPayment && restored.balance == 80
            && restored.clientDevice.accessGrant == SignalDebtDeviceGrant.Repair,
            "failed payment keeps the original entitlement and permission for retry");
        check(restored.TryCommitChange(candidate => candidate.ClaimRepairPayment(), candidate => { durable = candidate.Copy(); return true; },
            out var payment, out _) && payment != null && durable.balance == 200 && durable.jobPaid,
            "successful retry durably settles exactly one payment");
        check(durable.StepClientTask() == null && durable.ClaimRepairPayment() == null
            && durable.clientDevice.orders[0].version == 2, "reload cannot repeat the consumed owner task, payout or order update");
    }
}
#endif
