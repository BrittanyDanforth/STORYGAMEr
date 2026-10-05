#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SignalDebt.Editor
{
    // Run with -executeMethod SignalDebt.Editor.SignalDebtSaveStoreChecks.RunBatch
    // and optional -signalDebtSaveReport /absolute/output.json. Uses actual Unity
    // serialization and temporary filesystem fixtures; no player save is touched.
    public static class SignalDebtSaveStoreChecks
    {
        [Serializable] private sealed class Report
        {
            public bool passed;
            public int assertions, cases;
            public string unity_version, failure;
            public bool genuine_unity_json = true, real_temporary_files = true;
            public string scope = "Schema4 proof/device serialization, read-only v1/v2/v3 migration, payment rollback and filesystem recovery; not scene or gameplay acceptance.";
        }

        // The old on-disk format deliberately has no purchase member. Testing a
        // serialized modern object with a version number changed is insufficient.
        [Serializable] private sealed class LegacyV1Snapshot
        {
            public int schemaVersion = 1;
            public string saveId;
            public int revision, balance, rentDue, day, rentDeadlineDay;
            public bool extensionUsed, jobAccepted, serviceInspected, serviceConnected, serviceTestPassed, jobPaid;
            public List<LegacyLedgerEvent> ledger;
            public List<string> clientMemories;
        }

        [Serializable] private sealed class LegacyLedgerEvent
        {
            public string id, type, summary;
            public int amount, day, worldRevision;
            public string utc;
        }

        // Authentic schema2 adds purchases but has neither clientDevice nor the
        // later per-device provenance fields in its ledger entries.
        [Serializable] private sealed class LegacyV2Snapshot
        {
            public int schemaVersion = 2;
            public string saveId;
            public int revision, balance, rentDue, day, rentDeadlineDay;
            public bool extensionUsed, jobAccepted, serviceInspected, serviceConnected, serviceTestPassed, jobPaid;
            public List<LegacyLedgerEvent> ledger;
            public List<string> clientMemories;
            public List<SignalDebtPurchase> shopPurchases;
        }

        // Authentic schema3 has the persistent device and its ledger provenance,
        // but no proof job, document, export or proof-owner-check fields.
        [Serializable] private sealed class LegacyV3Snapshot
        {
            public int schemaVersion = 3;
            public string saveId;
            public int revision, balance, rentDue, day, rentDeadlineDay;
            public bool extensionUsed, jobAccepted, serviceInspected, serviceConnected, serviceTestPassed, jobPaid;
            public List<LegacyV3LedgerEvent> ledger;
            public List<string> clientMemories;
            public List<SignalDebtPurchase> shopPurchases;
            public SignalDebtClientDevice clientDevice;
        }

        [Serializable] private sealed class LegacyV3LedgerEvent
        {
            public string id, type, summary, utc, actorId, deviceId, recordId;
            public int amount, day, worldRevision, deviceRevision;
        }

        private static int assertions, cases;
        private static string root;

        [MenuItem("Signal Debt/Validation/Run Save Store Checks")]
        public static void Run()
        {
            Report result = Execute();
            if (!result.passed) throw new InvalidOperationException(result.failure);
        }

        public static void RunBatch()
        {
            Report result = Execute();
            EditorApplication.Exit(result.passed ? 0 : 1);
        }

        private static Report Execute()
        {
            assertions = cases = 0;
            root = Path.Combine(Path.GetTempPath(), "SignalDebtSaveStoreChecks-" + Guid.NewGuid().ToString("N"));
            var report = new Report { unity_version = Application.unityVersion, failure = "" };
            try
            {
                Directory.CreateDirectory(root);
                FreshAndPurchaseRoundTrip();
                LegacyMigration();
                LegacyRecovery();
                VersionTwoMigrationAndRecovery();
                VersionThreeMigrationAndRecovery();
                DeviceRoundTripAndFailedPublication();
                ProofRoundTripAndFailedPublication();
                CurrentPrecedence();
                CurrentRecovery();
                CorruptionBlocksReplacement();
                UnsupportedVersions();
                InvalidCandidates();
                MissingCurrentDevice();
                MissingCurrentProof();
                WriteFailures();
                RepairPaymentWriteFailure();
                ChangedFilesBetweenWrites();
                CampaignDiscovery();
                Check(assertions > 0 && cases > 0, "the suite actually ran");
                report.passed = true;
            }
            catch (Exception error) { report.failure = error.ToString(); }
            finally
            {
                report.assertions = assertions; report.cases = cases;
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception error)
                { report.passed = false; report.failure += "\nFixture cleanup failed: " + error.Message; }
            }
            try
            {
                string path = ReportPath();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonUtility.ToJson(report, true));
                Debug.Log((report.passed ? "PASS" : "FAIL") + ": " + report.assertions
                    + " real Unity save assertions across " + report.cases + " cases. Report: " + path);
            }
            catch (Exception error)
            { report.passed = false; report.failure += "\nReport write failed: " + error.Message; }
            if (!report.passed) Debug.LogError(report.failure);
            return report;
        }

        private static string ReportPath()
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length; index++)
                if (arguments[index] == "-signalDebtSaveReport")
                {
                    if (index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-", StringComparison.Ordinal))
                        throw new ArgumentException("-signalDebtSaveReport requires a file path.");
                    return Path.GetFullPath(arguments[index + 1]);
                }
            return Path.Combine(Application.temporaryCachePath, "SignalDebtSaveStoreChecks.json");
        }

        private static string Fixture(string name)
        {
            cases++;
            string directory = Path.Combine(root, name);
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static SignalDebtSaveData Repaired()
        {
            SignalDebtSaveData state = VerifiedUnpaid();
            state.ClaimRepairPayment(); state.ApplyRentPayment(50);
            state.ExtendRentDeadline(); state.AdvanceDay();
            Check(state.Validate(), "existing repair/rent scenario remains valid");
            return state;
        }

        private static SignalDebtSaveData VerifiedUnpaid()
        {
            var state = new SignalDebtSaveData();
            Check(state.AcceptRepair() != null && state.BeginClientSession() != null
                && state.InspectRepair() != null, "client grants an actual scoped device session");
            Check(state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId) != null
                && state.TestRepair() != null && !state.serviceTestPassed, "a folder check queues without granting success");
            Check(state.StepClientTask() != null && state.clientDevice.CurrentOwnerVerified
                && state.CanClaimRepairPayment && state.Validate(), "owner reads the real folder before payment eligibility");
            return state;
        }

        private static string Json(SignalDebtSaveData state) { return JsonUtility.ToJson(state, true); }
        private static void Write(string path, SignalDebtSaveData state) { File.WriteAllText(path, Json(state)); }
        private static void Check(bool condition, string description)
        { assertions++; if (!condition) throw new InvalidOperationException("FAIL: " + description); }

        private static void FreshAndPurchaseRoundTrip()
        {
            string directory = Fixture("fresh-and-purchase");
            var store = new SignalDebtSaveStore(directory);
            SignalDebtSaveData state = store.Load();
            Check(state != null && state.schemaVersion == SignalDebtSaveData.CurrentSchemaVersion && state.Validate(), "no files creates current schema");
            Check(!store.LoadBlocked && !store.HasSaveError && Directory.GetFiles(directory).Length == 0, "new-game load writes nothing");
            Check(state.usedGpuPurchase == null, "fresh game has no purchase");
            SignalDebtSaveData roundTrip = JsonUtility.FromJson<SignalDebtSaveData>(Json(state));
            Check(roundTrip != null && roundTrip.usedGpuPurchase == null && roundTrip.Validate(), "actual JsonUtility preserves absence of a purchase");
            Check(!roundTrip.proofAccepted && !roundTrip.proofPaid && roundTrip.proofDocument.LastOwnerRead() == null
                && roundTrip.proofDocument.LatestExport() == null, "fresh proof absence survives actual JsonUtility without an invented observation/export");
            Check(store.Save(state), "fresh current save succeeds");
            Check(Path.GetFileName(store.SavePath) == "signal-debt-save-v4.json", "current filename is isolated from all older players");
            Check(File.Exists(store.SavePath) && !File.Exists(store.PreviousSavePath)
                && !File.Exists(store.VersionTwoSavePath) && !File.Exists(store.LegacySavePath), "new save does not create older files");
            Check(Json(new SignalDebtSaveStore(directory).Load()) == Json(state), "current state reloads exactly");

            state.AcceptRepair(); state.BeginClientSession(); state.InspectRepair();
            state.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId); state.TestRepair(); state.StepClientTask(); state.ClaimRepairPayment();
            Check(store.Save(state), "earned funds persist before buying");
            SignalDebtShopQuote quote = state.InspectUsedGpu();
            Check(state.ConfirmUsedGpuPurchase(quote, store.Save, out SignalDebtLedgerEvent sale, out string reason), "confirmed purchase saves through real JsonUtility/filesystem");
            Check(sale != null && state.usedGpuPurchase != null && state.Validate(), "one receipt agrees with committed sale");
            SignalDebtSaveData bought = new SignalDebtSaveStore(directory).Load();
            Check(bought != null && Json(bought) == Json(state) && !bought.UsedGpuInStock && bought.OwnsUsedGpu, "item ownership, stock, money and receipt survive real save/load");
            string beforeDuplicate = Json(bought);
            Dictionary<string, byte[]> files = Files(directory);
            Check(!bought.ConfirmUsedGpuPurchase(quote, store.Save, out sale, out reason), "old purchase confirmation cannot execute after reload");
            Check(Json(bought) == beforeDuplicate && SameFiles(directory, files), "duplicate confirmation changes neither live state nor disk files");
        }

        private static LegacyV1Snapshot OldRepair()
        {
            var value = new LegacyV1Snapshot
            {
                saveId = "real-legacy-repair", balance = 150, rentDue = 400, day = 2,
                rentDeadlineDay = 6, extensionUsed = true, jobAccepted = true, serviceInspected = true,
                serviceConnected = true, serviceTestPassed = true, jobPaid = true,
                ledger = new List<LegacyLedgerEvent>(), clientMemories = new List<string>
                { "copy_shop_client: the player restored our order folder and tested it with permission.",
                  "copy_shop_client: paid the agreed 120 credits; the job is complete." }
            };
            OldEvent(value, "repair-job-accepted", "job_accepted", 0, 1);
            OldEvent(value, "repair-service-inspected", "service_inspected", 0, 1);
            OldEvent(value, "repair-service-connected", "service_connected", 0, 1);
            OldEvent(value, "repair-service-test", "service_test_passed", 0, 1);
            OldEvent(value, "repair-job-payment", "job_paid", 120, 1);
            OldEvent(value, "old-rent-payment", "rent_paid", -50, 1);
            OldEvent(value, "rent-extension", "rent_extension", 0, 1);
            OldEvent(value, "old-rest", "rested", 0, 2);
            return value;
        }

        private static void OldEvent(LegacyV1Snapshot state, string id, string type, int amount, int day)
        {
            state.ledger.Add(new LegacyLedgerEvent { id = id, type = type, amount = amount, day = day,
                worldRevision = ++state.revision, summary = "Recorded legacy " + type,
                utc = "2026-10-04T12:00:00.0000000Z" });
        }

        private static string LegacyJson() { return JsonUtility.ToJson(OldRepair(), true); }

        // Compare only original schema fields, without deriving expected device state
        // by calling the migration implementation under test.
        private static LegacyV1Snapshot OldProjection(SignalDebtSaveData state)
        {
            return new LegacyV1Snapshot
            {
                saveId = state.saveId, revision = state.revision, balance = state.balance, rentDue = state.rentDue,
                day = state.day, rentDeadlineDay = state.rentDeadlineDay, extensionUsed = state.extensionUsed,
                jobAccepted = state.jobAccepted, serviceInspected = state.serviceInspected,
                serviceConnected = state.serviceConnected, serviceTestPassed = state.serviceTestPassed,
                jobPaid = state.jobPaid, clientMemories = state.clientMemories,
                ledger = state.ledger.ConvertAll(entry => new LegacyLedgerEvent
                { id = entry.id, type = entry.type, summary = entry.summary, amount = entry.amount,
                  day = entry.day, worldRevision = entry.worldRevision, utc = entry.utc })
            };
        }

        private static LegacyV2Snapshot OldPurchase()
        {
            LegacyV1Snapshot old = OldRepair();
            old.saveId = "real-v2-purchase"; old.balance -= SignalDebtShopCatalog.Price;
            OldEvent(old, SignalDebtShopCatalog.PurchaseEventId, "shop_purchase", -SignalDebtShopCatalog.Price, old.day);
            return new LegacyV2Snapshot
            {
                saveId = old.saveId, revision = old.revision, balance = old.balance, rentDue = old.rentDue,
                day = old.day, rentDeadlineDay = old.rentDeadlineDay, extensionUsed = old.extensionUsed,
                jobAccepted = old.jobAccepted, serviceInspected = old.serviceInspected,
                serviceConnected = old.serviceConnected, serviceTestPassed = old.serviceTestPassed,
                jobPaid = old.jobPaid, ledger = old.ledger, clientMemories = old.clientMemories,
                shopPurchases = new List<SignalDebtPurchase> { new SignalDebtPurchase
                {
                    receiptId = "old-v2-receipt", stockId = SignalDebtShopCatalog.StockId,
                    itemId = SignalDebtShopCatalog.ItemId, sellerId = SignalDebtShopCatalog.SellerId,
                    productId = SignalDebtShopCatalog.ProductId, ledgerEventId = SignalDebtShopCatalog.PurchaseEventId,
                    paidAmount = SignalDebtShopCatalog.Price, day = old.day, worldRevision = old.revision
                } }
            };
        }

        private static string VersionTwoJson() { return JsonUtility.ToJson(OldPurchase(), true); }

        private static LegacyV3Snapshot VersionThreeProjection(SignalDebtSaveData state)
        {
            return new LegacyV3Snapshot
            {
                saveId = state.saveId, revision = state.revision, balance = state.balance, rentDue = state.rentDue,
                day = state.day, rentDeadlineDay = state.rentDeadlineDay, extensionUsed = state.extensionUsed,
                jobAccepted = state.jobAccepted, serviceInspected = state.serviceInspected,
                serviceConnected = state.serviceConnected, serviceTestPassed = state.serviceTestPassed,
                jobPaid = state.jobPaid, clientMemories = state.clientMemories, shopPurchases = state.shopPurchases,
                clientDevice = state.clientDevice,
                ledger = state.ledger.ConvertAll(entry => new LegacyV3LedgerEvent
                { id = entry.id, type = entry.type, summary = entry.summary, amount = entry.amount,
                  day = entry.day, worldRevision = entry.worldRevision, utc = entry.utc,
                  actorId = entry.actorId, deviceId = entry.deviceId, recordId = entry.recordId, deviceRevision = entry.deviceRevision })
            };
        }

        private static string VersionThreeJson(SignalDebtSaveData state)
        { return JsonUtility.ToJson(VersionThreeProjection(state), true); }

        private static void CheckFreshProof(SignalDebtSaveData state)
        {
            Check(!state.proofAccepted && !state.proofPaid && state.proofDocument != null
                && JsonUtility.ToJson(state.proofDocument) == JsonUtility.ToJson(SignalDebtProofDocument.CreateInitial()),
                "older campaign gains only a fresh unaccepted proof document with no fabricated work");
        }

        private static void CheckMigratedDevice(SignalDebtSaveData state)
        {
            Check(state != null && state.schemaVersion == 4 && state.Validate(), "old JSON becomes valid schema4");
            Check(state.clientDevice.legacyRepairTestRecorded && state.clientDevice.legacyPaymentRecorded
                && state.clientDevice.accessGrant == SignalDebtDeviceGrant.ReadOnlyAfterSettlement,
                "legacy tested/paid facts and read-only permission survive");
            Check(!state.clientDevice.OwnerHasCompletedFirstOrder && !state.clientDevice.CurrentOwnerVerified
                && state.clientDevice.ownerCheckCount == 0 && state.clientDevice.orders[0].version == 1,
                "migration does not invent an owner observation or completed specific order");
            CheckFreshProof(state);
        }

        private static void LegacyMigration()
        {
            string directory = Fixture("legacy-migration");
            var store = new SignalDebtSaveStore(directory);
            string legacy = LegacyJson();
            Check(!legacy.Contains("shopPurchases") && !legacy.Contains("clientDevice") && !legacy.Contains("deviceRevision"),
                "v1 fixture genuinely lacks later purchase/device/provenance fields");
            foreach (string suffix in new[] { "", ".bak", ".tmp" }) File.WriteAllText(store.LegacySavePath + suffix, legacy);
            Dictionary<string, byte[]> before = Files(directory);
            Dictionary<string, long> times = FileTimes(directory);
            SignalDebtSaveData loaded = store.Load();
            CheckMigratedDevice(loaded);
            Check(JsonUtility.ToJson(OldProjection(loaded), true) == legacy, "v1 identity, money, obligations, history and memories are unchanged");
            Check(loaded.usedGpuPurchase == null && SameFiles(directory, before) && SameTimes(directory, times),
                "v1 migration neither invents a sale nor modifies any source file");
            SignalDebtSaveData parsed = JsonUtility.FromJson<SignalDebtSaveData>(legacy);
            string unchanged = Json(parsed);
            SignalDebtSaveData copy = parsed.MigrateV1();
            Check(copy != null && Json(parsed) == unchanged, "v1 migration leaves parsed input unchanged");
            Check(!ReferenceEquals(copy.ledger, parsed.ledger) && !ReferenceEquals(copy.ledger[0], parsed.ledger[0])
                && !ReferenceEquals(copy.clientMemories, parsed.clientMemories), "v1 migration deep-copies records and memories");
            copy.ledger[0].summary = "changed copy"; copy.clientMemories[0] = "changed copy memory";
            Check(Json(parsed) == unchanged, "mutating migrated v1 cannot change its input");
            Check(store.Save(loaded), "v1 migration writes only current family");
            CheckOldFiles(directory, before, times);
            Check(File.Exists(store.SavePath) && !File.Exists(store.VersionTwoSavePath)
                && new SignalDebtSaveStore(directory).Load().saveId == loaded.saveId, "v1 import creates v4 directly with original identity");
        }

        private static void LegacyRecovery()
        {
            foreach (string suffix in new[] { ".bak", ".tmp" })
            {
                string directory = Fixture("legacy-recovery-" + suffix.Substring(1));
                var store = new SignalDebtSaveStore(directory);
                File.WriteAllText(store.LegacySavePath, "{broken");
                File.WriteAllText(store.LegacySavePath + suffix, LegacyJson());
                Dictionary<string, byte[]> before = Files(directory);
                SignalDebtSaveData loaded = store.Load(); CheckMigratedDevice(loaded);
                Check(JsonUtility.ToJson(OldProjection(loaded), true) == LegacyJson(), "v1 recovery retains original schema fields");
                Check(SameFiles(directory, before), "v1 recovery is read-only");
            }
        }

        private static void VersionTwoMigrationAndRecovery()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string directory = Fixture("v2-migration-" + (suffix.Length == 0 ? "primary" : suffix.Substring(1)));
                var store = new SignalDebtSaveStore(directory);
                string previous = VersionTwoJson();
                Check(previous.Contains("shopPurchases") && !previous.Contains("clientDevice") && !previous.Contains("deviceRevision"),
                    "v2 fixture contains actual old purchase fields without new device provenance");
                File.WriteAllText(store.LegacySavePath, LegacyJson());
                if (suffix.Length > 0) File.WriteAllText(store.VersionTwoSavePath, "{broken");
                File.WriteAllText(store.VersionTwoSavePath + suffix, previous);
                Dictionary<string, byte[]> before = Files(directory); Dictionary<string, long> times = FileTimes(directory);
                SignalDebtSaveData loaded = store.Load(); CheckMigratedDevice(loaded);
                Check(loaded.saveId == "real-v2-purchase" && loaded.balance == 60 && loaded.rentDue == 400
                    && loaded.revision == 9 && loaded.ledger.Count == 9, "v2 takes precedence and preserves financial/repair history");
                Check(loaded.OwnsUsedGpu && !loaded.UsedGpuInStock && loaded.usedGpuPurchase.receiptId == "old-v2-receipt"
                    && loaded.usedGpuPurchase.ledgerEventId == SignalDebtShopCatalog.PurchaseEventId,
                    "v2 item identity, stock and original receipt survive migration");
                Check(SameFiles(directory, before) && SameTimes(directory, times), "v2 load preserves every older byte and modification time");
                SignalDebtSaveData parsed = JsonUtility.FromJson<SignalDebtSaveData>(previous); string unchanged = Json(parsed);
                SignalDebtSaveData migrated = SignalDebtSaveData.MigrateV2(parsed);
                Check(migrated != null && !ReferenceEquals(migrated.shopPurchases, parsed.shopPurchases)
                    && !ReferenceEquals(migrated.usedGpuPurchase, parsed.usedGpuPurchase)
                    && !ReferenceEquals(migrated.ledger[0], parsed.ledger[0]), "v2 migration deeply copies receipts and ledger");
                migrated.usedGpuPurchase.receiptId = "mutated-copy"; migrated.ledger[0].summary = "mutated-copy";
                Check(Json(parsed) == unchanged, "v2 input stays unchanged after copy edits");
                Check(store.Save(loaded) && store.Save(loaded), "migrated v2 publishes and rotates v4 only");
                CheckOldFiles(directory, before, times);
                Check(Json(new SignalDebtSaveStore(directory).Load()) == Json(loaded), "migrated purchased campaign round-trips as v4");
            }
        }

        private static void VersionThreeMigrationAndRecovery()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string directory = Fixture("v3-migration-" + (suffix.Length == 0 ? "primary" : suffix.Substring(1)));
                var store = new SignalDebtSaveStore(directory);
                SignalDebtSaveData original;
                if (suffix == ".bak")
                {
                    original = VerifiedUnpaid(); original.ClaimRepairPayment();
                    Check(original.ConfirmUsedGpuPurchase(original.InspectUsedGpu(), candidate => true,
                        out _, out _), "paid v3 fixture retains an actual purchase receipt");
                }
                else
                {
                    original = suffix == ".tmp" ? VerifiedUnpaid() : new SignalDebtSaveData();
                    if (suffix.Length == 0) { original.AcceptRepair(); original.BeginClientSession(); original.InspectRepair(); }
                    original.ConnectRepair(SignalDebtClientDevice.PrintQueueOutputPortId);
                    original.OpenClientFolder(SignalDebtClientDevice.StaffDeskId); original.TestRepair();
                }
                Check(original.Validate(), "authentic v3 projection starts from consistent old-format state");
                string old = VersionThreeJson(original);
                Check(old.Contains("clientDevice") && old.Contains("deviceRevision")
                    && !old.Contains("proofDocument") && !old.Contains("proofAccepted")
                    && !old.Contains("documentRevisionId") && !old.Contains("ownerCheckNumber"),
                    "v3 fixture has old device/provenance fields but genuinely omits schema4 additions");
                File.WriteAllText(store.VersionTwoSavePath, VersionTwoJson());
                File.WriteAllText(store.LegacySavePath, LegacyJson());
                if (suffix.Length > 0) File.WriteAllText(store.PreviousSavePath, "{broken");
                File.WriteAllText(store.PreviousSavePath + suffix, old);
                Dictionary<string, byte[]> before = Files(directory); Dictionary<string, long> times = FileTimes(directory);
                SignalDebtSaveData loaded = store.Load();
                Check(loaded != null && loaded.schemaVersion == 4 && loaded.Validate(), "v3 primary/recovery imports into valid schema4");
                Check(VersionThreeJson(loaded) == old, "all v3 device, order, queue, history, receipt and financial values survive exactly");
                CheckFreshProof(loaded);
                Check(SameFiles(directory, before) && SameTimes(directory, times), "v3 import preserves all source bytes and modification times");
                SignalDebtSaveData parsed = JsonUtility.FromJson<SignalDebtSaveData>(old); string unchanged = Json(parsed);
                SignalDebtSaveData migrated = SignalDebtSaveData.MigrateV3(parsed);
                Check(migrated != null && Json(parsed) == unchanged && !ReferenceEquals(migrated.clientDevice, parsed.clientDevice)
                    && !ReferenceEquals(migrated.clientDevice.orders, parsed.clientDevice.orders)
                    && !ReferenceEquals(migrated.clientDevice.orders[0], parsed.clientDevice.orders[0])
                    && !ReferenceEquals(migrated.ledger[0], parsed.ledger[0]), "v3 migration deeply copies device/orders/provenance without mutating input");
                migrated.clientDevice.orders[0].description = "changed detached copy";
                migrated.ledger[0].summary = "changed detached history";
                Check(Json(parsed) == unchanged, "editing migrated v3 structures leaves parsed input unchanged");
                Check(store.Save(loaded) && store.Save(loaded), "v3 migration publishes and rotates only v4");
                CheckOldFiles(directory, before, times);
                Check(Json(new SignalDebtSaveStore(directory).Load()) == Json(loaded), "migrated v3 campaign reloads exactly as schema4");
            }
        }

        private static void DeviceRoundTripAndFailedPublication()
        {
            string directory = Fixture("v4-device-state"); var store = new SignalDebtSaveStore(directory);
            var state = new SignalDebtSaveData(); state.AcceptRepair(); state.BeginClientSession(); state.InspectRepair();
            state.ConnectRepair(SignalDebtClientDevice.PrintQueueOutputPortId); state.OpenClientFolder(SignalDebtClientDevice.StaffDeskId); state.TestRepair();
            Check(state.Validate() && store.Save(state), "v4 saves wrong endpoint, label view and pending owner task");
            SignalDebtSaveData restored = new SignalDebtSaveStore(directory).Load();
            Check(Json(restored) == Json(state) && restored.clientDevice.ownerCheckQueued
                && restored.clientDevice.currentView == SignalDebtDeviceView.StaffDeskAccessLabel,
                "actual JsonUtility retains live device/session/pending fields exactly");
            Check(restored.StepClientTask() != null && restored.clientDevice.lastOwnerReadResult == SignalDebtFolderResult.PrintService
                && !restored.CanClaimRepairPayment && restored.clientDevice.orders[0].version == 1,
                "restored queued task observes actual wrong service without order completion/payment");
            restored.ConnectRepair(SignalDebtClientDevice.OrderIndexOutputPortId); restored.TestRepair();
            Check(store.Save(restored), "corrected queued device saved before owner task");
            Dictionary<string, byte[]> before = Files(directory); string original = Json(restored);
            Directory.CreateDirectory(store.SavePath + ".bak-blocker");
            if (File.Exists(store.SavePath + ".bak")) File.Delete(store.SavePath + ".bak");
            Directory.Move(store.SavePath + ".bak-blocker", store.SavePath + ".bak");
            before = Files(directory);
            Check(!restored.TryCommitChange(candidate => candidate.StepClientTask(), store.Save,
                out SignalDebtLedgerEvent committed, out string reason), "failed filesystem publication rejects owner success");
            Check(committed == null && Json(restored) == original && SameFiles(directory, before),
                "failed owner step leaves live order, queue, observation and disk unchanged");
            SignalDebtSaveData unpaid = new SignalDebtSaveStore(directory).Load();
            Check(unpaid.clientDevice.ownerCheckQueued && unpaid.clientDevice.orders[0].version == 1
                && !unpaid.CanClaimRepairPayment, "reload keeps pending task and cannot resurrect rejected success");
            Directory.Delete(store.SavePath + ".bak");
            Check(restored.TryCommitChange(candidate => candidate.StepClientTask(), store.Save,
                out committed, out reason) && restored.clientDevice.CurrentOwnerVerified, "owner task commits once after obstruction removed");
            Check(new SignalDebtSaveStore(directory).Load().clientDevice.orders[0].version == 2,
                "real completed order version survives final reload");
        }

        private static void ProofRoundTripAndFailedPublication()
        {
            string directory = Fixture("proof-document-publication"); var store = new SignalDebtSaveStore(directory);
            SignalDebtSaveData live = VerifiedUnpaid();
            Check(live.ClaimRepairPayment() != null && live.AcceptProofRecovery() != null,
                "separate proof permission follows a recorded first repair payment");
            string firstRepairDevice = JsonUtility.ToJson(live.clientDevice);
            Check(live.RestoreProofRevision(SignalDebtProofDocument.RevisionId(1)) != null
                && live.RequestProofCheck() != null && live.Validate(), "wrong source revision becomes a real queued proof state");
            Check(store.Save(live), "wrong restored proof and pending owner task save through genuine JsonUtility");
            SignalDebtSaveData restored = new SignalDebtSaveStore(directory).Load();
            Check(Json(restored) == Json(live) && restored.proofDocument.ownerCheckQueued
                && restored.proofDocument.RevisionCount == 4, "proof history, separate grant and queued revision reload exactly");
            Check(restored.EndClientSession() != null, "player can close the session while the proof task remains queued");
            Check(restored.TryCommitChange(candidate => candidate.StepProofTask(), store.Save,
                out SignalDebtLedgerEvent observed, out string reason), "owner processes persisted proof task with player session closed");
            Check(observed != null && observed.documentRevisionId == restored.proofDocument.currentRevisionId
                && observed.ownerCheckNumber == 1 && observed.exportId == ""
                && restored.proofDocument.LastOwnerRead().result == SignalDebtProofResult.DateMismatch
                && restored.proofDocument.LatestExport() == null && !restored.CanClaimProofPayment,
                "wrong draft records its concrete failed comparison without export or payment");
            Check(Json(new SignalDebtSaveStore(directory).Load()) == Json(restored), "failed owner observation itself persists exactly");
            Check(restored.BeginClientSession() != null && restored.UndoProofRestore() != null
                && restored.RestoreProofRevision(SignalDebtProofDocument.RevisionId(2)) != null
                && restored.RequestProofCheck() != null && store.Save(restored), "Undo and approved-source restore retain history before a new saved owner task");

            File.Delete(store.SavePath + ".bak"); Directory.CreateDirectory(store.SavePath + ".bak");
            string unchanged = Json(restored); Dictionary<string, byte[]> before = Files(directory);
            Check(!restored.TryCommitChange(candidate => candidate.StepProofTask(), store.Save, out observed, out reason),
                "actual publication failure rejects a proof success/export candidate");
            Check(observed == null && Json(restored) == unchanged && SameFiles(directory, before),
                "rejected proof step leaves live revision/history/queue/read/export and all disk bytes unchanged");
            SignalDebtSaveData pending = new SignalDebtSaveStore(directory).Load();
            Check(pending != null && Json(pending) == unchanged && pending.proofDocument.ownerCheckQueued
                && pending.proofDocument.LatestExport() == null && !pending.CanClaimProofPayment,
                "reload cannot resurrect an export rejected by Save");
            Directory.Delete(store.SavePath + ".bak");
            Check(restored.TryCommitChange(candidate => candidate.StepProofTask(), store.Save, out observed, out reason)
                && restored.CanClaimProofPayment && restored.proofDocument.OwnerCheckCount == 2,
                "the same queued task commits one export after obstruction is removed");
            SignalDebtProofExport export = restored.proofDocument.LatestExport();
            Check(export != null && export.id == observed.exportId && export.revisionId == observed.documentRevisionId
                && export.ownerReadNumber == observed.ownerCheckNumber && export.content.SameAs(restored.proofDocument.approval.content),
                "saved owner observation and exported content share exact revision/check/export provenance");
            Check(Json(new SignalDebtSaveStore(directory).Load()) == Json(restored), "approved proof and immutable export round-trip exactly");
            SignalDebtSaveData detached = restored.Copy(); string sourceUnchanged = Json(restored);
            Check(!ReferenceEquals(detached.proofDocument, restored.proofDocument)
                && !ReferenceEquals(detached.proofDocument.revisions[0].content, restored.proofDocument.revisions[0].content)
                && !ReferenceEquals(detached.proofDocument.approval.content, restored.proofDocument.approval.content)
                && !ReferenceEquals(detached.proofDocument.exportSnapshots[0].content, restored.proofDocument.exportSnapshots[0].content),
                "candidate copies detach revision, approval and export content");
            detached.proofDocument.revisions[0].content.title = "detached edit";
            detached.proofDocument.exportSnapshots[0].content.footer = "detached export";
            detached.ledger[detached.ledger.Count - 1].exportId = "detached event";
            Check(Json(restored) == sourceUnchanged, "mutating nested candidate proof or provenance cannot affect live state");

            File.Delete(store.SavePath + ".bak"); Directory.CreateDirectory(store.SavePath + ".bak");
            unchanged = Json(restored); before = Files(directory);
            Check(!restored.TryCommitChange(candidate => candidate.ClaimProofPayment(), store.Save, out observed, out reason),
                "actual filesystem publication failure rejects the 160-credit proof settlement");
            Check(observed == null && Json(restored) == unchanged && SameFiles(directory, before),
                "rejected proof payment changes neither money, grant, receipt, live history nor disk");
            SignalDebtSaveData unpaid = new SignalDebtSaveStore(directory).Load();
            Check(unpaid != null && Json(unpaid) == unchanged && !unpaid.proofPaid && unpaid.balance == 200
                && !unpaid.HasEvent(SignalDebtSaveData.ProofPaymentEventId), "reload remains unpaid without a phantom proof receipt");
            Directory.Delete(store.SavePath + ".bak");
            SignalDebtSaveData sameIdentity = restored;
            Check(restored.TryCommitChange(candidate => candidate.ClaimProofPayment(), store.Save, out observed, out reason)
                && ReferenceEquals(restored, sameIdentity) && restored.balance == 360 && restored.proofPaid
                && restored.proofDocument.accessGrant == SignalDebtProofGrant.ReadOnlyAfterSettlement,
                "one saved 160-credit settlement publishes atomically and closes proof editing");
            Check(observed.exportId == export.id && observed.documentRevisionId == export.revisionId
                && observed.ownerCheckNumber == export.ownerReadNumber, "payment retains the exact accepted export provenance");
            unchanged = Json(restored); before = Files(directory);
            Check(!restored.TryCommitChange(candidate => candidate.ClaimProofPayment(), store.Save, out observed, out reason)
                && observed == null && Json(restored) == unchanged && SameFiles(directory, before),
                "duplicate settlement cannot pay again or write any file");
            Check(Json(new SignalDebtSaveStore(directory).Load()) == unchanged
                && JsonUtility.ToJson(restored.clientDevice) == firstRepairDevice,
                "paid proof reload preserves original repaired device and its separate read-only scope");
        }

        private static void CurrentPrecedence()
        {
            string directory = Fixture("current-precedence");
            var store = new SignalDebtSaveStore(directory);
            SignalDebtSaveData current = Repaired();
            Write(store.SavePath, current);
            File.WriteAllText(store.PreviousSavePath, VersionThreeJson(VerifiedUnpaid()));
            File.WriteAllText(store.VersionTwoSavePath, VersionTwoJson());
            File.WriteAllText(store.LegacySavePath, LegacyJson());
            Dictionary<string, byte[]> before = Files(directory);
            Check(Json(store.Load()) == Json(current), "current save wins over a different valid legacy campaign");
            Check(SameFiles(directory, before), "precedence selection changes no files");
            File.WriteAllText(store.PreviousSavePath, "{\"schemaVersion\":999}");
            File.WriteAllText(store.VersionTwoSavePath, "{\"schemaVersion\":999}");
            File.WriteAllText(store.LegacySavePath, "not json");
            Check(Json(new SignalDebtSaveStore(directory).Load()) == Json(current) && store.Save(current),
                "valid v4 ignores unsupported/corrupt lower families on both load and save");
        }

        private static void CurrentRecovery()
        {
            foreach (string suffix in new[] { ".bak", ".tmp" })
            {
                string directory = Fixture("current-recovery-" + suffix.Substring(1));
                var store = new SignalDebtSaveStore(directory);
                SignalDebtSaveData expected = Repaired();
                File.WriteAllText(store.SavePath, "{broken"); Write(store.SavePath + suffix, expected);
                Dictionary<string, byte[]> before = Files(directory);
                SignalDebtSaveData loaded = store.Load();
                Check(loaded != null && !store.LoadBlocked && Json(loaded) == Json(expected), "current recovery restores a supported snapshot");
                Check(SameFiles(directory, before), "recovery itself is read-only");
                Check(store.Save(loaded), "recovered snapshot can replace a known corrupt primary");
                Check(Json(new SignalDebtSaveStore(directory).Load()) == Json(expected), "recovery publication remains loadable");
                if (suffix == ".bak")
                    Check(EqualBytes(File.ReadAllBytes(store.SavePath + suffix), before[Path.GetFileName(store.SavePath + suffix)]), "corrupt primary never replaces the good backup");
            }
            string directoryOnly = Fixture("only-temporary");
            var storeOnly = new SignalDebtSaveStore(directoryOnly);
            SignalDebtSaveData interrupted = Repaired(); Write(storeOnly.SavePath + ".tmp", interrupted);
            Check(Json(storeOnly.Load()) == Json(interrupted), "a complete temporary file recovers without primary or backup");
        }

        private static void CorruptionBlocksReplacement()
        {
            string directory = Fixture("corrupt-current-with-legacy");
            var store = new SignalDebtSaveStore(directory);
            File.WriteAllText(store.SavePath, "{broken"); File.WriteAllText(store.SavePath + ".bak", "not json");
            File.WriteAllText(store.SavePath + ".tmp", "{}"); File.WriteAllText(store.LegacySavePath, LegacyJson());
            File.WriteAllText(store.PreviousSavePath, VersionThreeJson(new SignalDebtSaveData()));
            File.WriteAllText(store.VersionTwoSavePath, VersionTwoJson());
            Dictionary<string, byte[]> before = Files(directory);
            Check(store.Load() == null && store.LoadBlocked && store.HasSaveError, "corrupt current family blocks rollback to legacy");
            Check(!store.Save(new SignalDebtSaveData()) && SameFiles(directory, before), "blocked startup save creates no replacement or staging files");
            string legacyDirectory = Fixture("corrupt-legacy");
            var legacyStore = new SignalDebtSaveStore(legacyDirectory);
            File.WriteAllText(legacyStore.LegacySavePath, "not json");
            Dictionary<string, byte[]> legacyBefore = Files(legacyDirectory);
            Check(legacyStore.Load() == null && legacyStore.LoadBlocked, "corrupt legacy does not start a fresh campaign");
            Check(!legacyStore.Save(new SignalDebtSaveData()) && SameFiles(legacyDirectory, legacyBefore), "corrupt legacy remains untouched");
            foreach (bool directoryMember in new[] { false, true })
            {
                string previousDirectory = Fixture("corrupt-v2-" + directoryMember);
                var previousStore = new SignalDebtSaveStore(previousDirectory);
                if (directoryMember) Directory.CreateDirectory(previousStore.VersionTwoSavePath);
                else File.WriteAllText(previousStore.VersionTwoSavePath, "not json");
                File.WriteAllText(previousStore.LegacySavePath, LegacyJson());
                Dictionary<string, byte[]> previousBefore = Files(previousDirectory);
                Check(previousStore.Load() == null && previousStore.LoadBlocked, "present corrupt v2 blocks rollback to valid v1");
                var direct = new SignalDebtSaveStore(previousDirectory);
                Check(!direct.Save(new SignalDebtSaveData()) && direct.LoadBlocked
                    && SameFiles(previousDirectory, previousBefore) && !File.Exists(direct.SavePath),
                    "Save without Load cannot shadow corrupt v2 with a new v4");
                if (directoryMember) Check(Directory.Exists(previousStore.VersionTwoSavePath), "invalid v2 directory is preserved");
            }
            foreach (bool directoryMember in new[] { false, true })
            {
                string previousDirectory = Fixture("corrupt-v3-" + directoryMember);
                var previousStore = new SignalDebtSaveStore(previousDirectory);
                if (directoryMember) Directory.CreateDirectory(previousStore.PreviousSavePath);
                else File.WriteAllText(previousStore.PreviousSavePath, "not json");
                File.WriteAllText(previousStore.VersionTwoSavePath, VersionTwoJson());
                File.WriteAllText(previousStore.LegacySavePath, LegacyJson());
                Dictionary<string, byte[]> previousBefore = Files(previousDirectory);
                Check(previousStore.Load() == null && previousStore.LoadBlocked, "present corrupt v3 blocks rollback to valid v2/v1");
                var direct = new SignalDebtSaveStore(previousDirectory);
                Check(!direct.Save(new SignalDebtSaveData()) && direct.LoadBlocked
                    && SameFiles(previousDirectory, previousBefore) && !File.Exists(direct.SavePath),
                    "Save without Load cannot shadow corrupt v3 with a new v4");
                if (directoryMember) Check(Directory.Exists(previousStore.PreviousSavePath), "invalid v3 directory is preserved");
            }
        }

        private static void UnsupportedVersions()
        {
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string directory = Fixture("future-current-" + (suffix.Length == 0 ? "primary" : suffix.Substring(1)));
                var store = new SignalDebtSaveStore(directory);
                Write(store.SavePath, Repaired()); Write(store.SavePath + ".bak", new SignalDebtSaveData());
                File.WriteAllText(store.SavePath + suffix, "{\"schemaVersion\":" + (SignalDebtSaveData.CurrentSchemaVersion + 1) + ",\"futurePayload\":\"preserve\"}");
                Dictionary<string, byte[]> before = Files(directory);
                Check(store.Load() == null && store.LoadBlocked && store.HasSaveError, "future primary/backup/temp blocks fallback and replacement");
                Check(!store.Save(new SignalDebtSaveData()) && SameFiles(directory, before), "blocked future save stays byte-identical");
                var direct = new SignalDebtSaveStore(directory);
                Check(!direct.Save(new SignalDebtSaveData()) && direct.LoadBlocked && SameFiles(directory, before), "Save without Load also protects future files");
            }
            string legacyDirectory = Fixture("future-legacy");
            var legacyStore = new SignalDebtSaveStore(legacyDirectory);
            File.WriteAllText(legacyStore.LegacySavePath, "{\"schemaVersion\":999}");
            Dictionary<string, byte[]> legacyBefore = Files(legacyDirectory);
            Check(legacyStore.Load() == null && legacyStore.LoadBlocked, "future legacy format is preserved");
            Check(!legacyStore.Save(new SignalDebtSaveData()) && SameFiles(legacyDirectory, legacyBefore), "future legacy cannot be shadowed by a fresh campaign");
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string directory = Fixture("future-v2-" + (suffix.Length == 0 ? "primary" : suffix.Substring(1)));
                var store = new SignalDebtSaveStore(directory);
                File.WriteAllText(store.VersionTwoSavePath, VersionTwoJson());
                File.WriteAllText(store.VersionTwoSavePath + ".bak", VersionTwoJson());
                File.WriteAllText(store.VersionTwoSavePath + suffix, "{\"schemaVersion\":3,\"futurePayload\":\"preserve\"}");
                File.WriteAllText(store.LegacySavePath, LegacyJson());
                Dictionary<string, byte[]> before = Files(directory);
                Check(store.Load() == null && store.LoadBlocked, "newer v2 family member blocks even a valid sibling and v1");
                var direct = new SignalDebtSaveStore(directory);
                Check(!direct.Save(new SignalDebtSaveData()) && direct.LoadBlocked && SameFiles(directory, before),
                    "direct Save preserves newer v2 primary/backup/temp without a v4 output");
            }
            foreach (string suffix in new[] { "", ".bak", ".tmp" })
            {
                string directory = Fixture("future-v3-" + (suffix.Length == 0 ? "primary" : suffix.Substring(1)));
                var store = new SignalDebtSaveStore(directory);
                File.WriteAllText(store.PreviousSavePath, VersionThreeJson(new SignalDebtSaveData()));
                File.WriteAllText(store.PreviousSavePath + ".bak", VersionThreeJson(new SignalDebtSaveData()));
                // The newer payload shape deliberately cannot be a device or proof.
                // Its header must block rollback before any content is deserialized.
                File.WriteAllText(store.PreviousSavePath + suffix,
                    "{\"schemaVersion\":4,\"clientDevice\":23,\"proofDocument\":\"future shape\"}");
                File.WriteAllText(store.VersionTwoSavePath, VersionTwoJson());
                File.WriteAllText(store.LegacySavePath, LegacyJson());
                Dictionary<string, byte[]> before = Files(directory);
                Check(store.Load() == null && store.LoadBlocked, "future v3 primary/backup/temp blocks valid siblings and lower families");
                var direct = new SignalDebtSaveStore(directory);
                Check(!direct.Save(new SignalDebtSaveData()) && direct.LoadBlocked && SameFiles(directory, before),
                    "Save without Load preserves future v3 family without creating v4");
            }
        }

        private static void MissingCurrentDevice()
        {
            var noDevice = new LegacyV2Snapshot { schemaVersion = 4, saveId = "current-missing-device",
                balance = 80, rentDue = 450, day = 1, rentDeadlineDay = 4,
                ledger = new List<LegacyLedgerEvent>(), clientMemories = new List<string>(),
                shopPurchases = new List<SignalDebtPurchase>() };
            string missing = JsonUtility.ToJson(noDevice, true);
            missing = missing.Substring(0, missing.LastIndexOf('}')) + ",\"proofDocument\":"
                + JsonUtility.ToJson(SignalDebtProofDocument.CreateInitial()) + "}";
            foreach (bool explicitNull in new[] { false, true })
            {
                string directory = Fixture("current-device-" + (explicitNull ? "null" : "missing"));
                var store = new SignalDebtSaveStore(directory);
                string invalid = explicitNull ? missing.Substring(0, missing.LastIndexOf('}')) + ",\"clientDevice\":null}" : missing;
                File.WriteAllText(store.SavePath, invalid);
                File.WriteAllText(store.VersionTwoSavePath, VersionTwoJson());
                Dictionary<string, byte[]> before = Files(directory);
                Check(store.Load() == null && store.LoadBlocked, "current JSON must explicitly contain a valid client device");
                Check(!new SignalDebtSaveStore(directory).Save(new SignalDebtSaveData()) && SameFiles(directory, before),
                    "current missing/null device cannot be initialized or replaced by save defaults");
            }
        }

        private static void MissingCurrentProof()
        {
            LegacyV3Snapshot noProof = VersionThreeProjection(new SignalDebtSaveData()); noProof.schemaVersion = 4;
            string missing = JsonUtility.ToJson(noProof, true);
            foreach (bool explicitNull in new[] { false, true })
            {
                string directory = Fixture("current-proof-" + (explicitNull ? "null" : "missing"));
                var store = new SignalDebtSaveStore(directory);
                string invalid = explicitNull ? missing.Substring(0, missing.LastIndexOf('}')) + ",\"proofDocument\":null}" : missing;
                File.WriteAllText(store.SavePath, invalid);
                File.WriteAllText(store.PreviousSavePath, VersionThreeJson(new SignalDebtSaveData()));
                Dictionary<string, byte[]> before = Files(directory);
                Check(store.Load() == null && store.LoadBlocked, "current JSON must explicitly contain a valid proof document");
                Check(!new SignalDebtSaveStore(directory).Save(new SignalDebtSaveData()) && SameFiles(directory, before),
                    "missing/null schema4 proof cannot silently initialize or shadow valid v3");
            }
            foreach (bool explicitNull in new[] { false, true })
            {
                string directory = Fixture("v3-device-" + (explicitNull ? "null" : "missing"));
                var store = new SignalDebtSaveStore(directory);
                LegacyV2Snapshot noDevice = OldPurchase(); noDevice.schemaVersion = 3;
                string invalid = JsonUtility.ToJson(noDevice, true);
                if (explicitNull) invalid = invalid.Substring(0, invalid.LastIndexOf('}')) + ",\"clientDevice\":null}";
                File.WriteAllText(store.PreviousSavePath, invalid); File.WriteAllText(store.VersionTwoSavePath, VersionTwoJson());
                Dictionary<string, byte[]> before = Files(directory);
                Check(store.Load() == null && store.LoadBlocked, "schema3 migration still requires its original explicit device");
                Check(!new SignalDebtSaveStore(directory).Save(new SignalDebtSaveData()) && SameFiles(directory, before),
                    "missing/null v3 device is preserved without rebuilding it from historical flags");
            }
        }

        private static void InvalidCandidates()
        {
            string directory = Fixture("invalid-candidates");
            var store = new SignalDebtSaveStore(directory); store.Load();
            Check(!store.Save(null), "null save rejected");
            Check(!store.Save(new SignalDebtSaveData { balance = -1 }) && Directory.GetFiles(directory).Length == 0, "invalid candidate writes nothing");
            SignalDebtSaveData valid = Repaired();
            Check(store.Save(valid), "ordinary invalid candidate does not prevent a later valid save");
            Dictionary<string, byte[]> before = Files(directory);
            SignalDebtSaveData invalid = valid.Copy(); invalid.revision++;
            Check(!store.Save(invalid) && SameFiles(directory, before), "invalid candidate cannot touch existing good primary");
            invalid = valid.Copy(); invalid.proofDocument = null;
            Check(!store.Save(invalid) && SameFiles(directory, before), "null proof candidate cannot be default-initialized by serialization");
            invalid = valid.Copy(); invalid.proofDocument.revisions[0].content.title = "forged original";
            Check(!store.Save(invalid) && SameFiles(directory, before), "invalid original proof history cannot replace a good save");
            Check(!store.LoadBlocked && store.HasSaveError, "candidate errors are reported without permanently blocking valid state");
        }

        private static void WriteFailures()
        {
            string directory = Fixture("file-instead-of-parent");
            string blocker = Path.Combine(directory, "parent-is-a-file"); File.WriteAllText(blocker, "preserve this file");
            Dictionary<string, byte[]> before = Files(directory);
            var store = new SignalDebtSaveStore(Path.Combine(blocker, "save-directory"));
            Check(!store.Save(Repaired()) && store.HasSaveError, "file as parent prevents writes even for privileged test users");
            Check(SameFiles(directory, before), "failed staging changes no surrounding file");

            string publishDirectory = Fixture("blocked-publish");
            var publishStore = new SignalDebtSaveStore(publishDirectory);
            SignalDebtSaveData previous = Repaired(); Check(publishStore.Save(previous), "publication fixture has valid primary");
            byte[] primary = File.ReadAllBytes(publishStore.SavePath);
            Directory.CreateDirectory(publishStore.SavePath + ".bak");
            SignalDebtSaveData next = previous.Copy(); next.AdvanceDay();
            Check(!publishStore.Save(next) && publishStore.HasSaveError, "blocked backup destination reports failure");
            Check(EqualBytes(File.ReadAllBytes(publishStore.SavePath), primary), "publication failure preserves prior primary exactly");
            Check(Directory.Exists(publishStore.SavePath + ".bak"), "publication failure preserves obstructing directory");
            Check(Directory.GetFiles(publishDirectory, "*.stage-*").Length == 0, "only this attempt's staging artifact is cleaned");
            Check(Json(new SignalDebtSaveStore(publishDirectory).Load()) == Json(previous), "previous primary remains loadable after publication failure");

            string temporaryDirectory = Fixture("failed-purchase-with-only-temporary");
            var temporaryStore = new SignalDebtSaveStore(temporaryDirectory);
            Directory.CreateDirectory(temporaryStore.SavePath);
            SignalDebtSaveData recoverable = Repaired();
            Write(temporaryStore.SavePath + ".tmp", recoverable);
            SignalDebtSaveData live = temporaryStore.Load();
            Check(live != null && Json(live) == Json(recoverable), "temporary file recovers despite an obstructed primary path");
            Dictionary<string, byte[]> temporaryBefore = Files(temporaryDirectory);
            SignalDebtShopQuote quote = live.InspectUsedGpu();
            Check(!live.ConfirmUsedGpuPurchase(quote, temporaryStore.Save, out SignalDebtLedgerEvent sale, out string reason), "failed primary publication rejects purchase");
            Check(sale == null && Json(live) == Json(recoverable), "failed purchase leaves live money, ownership and receipt unchanged");
            Check(SameFiles(temporaryDirectory, temporaryBefore), "failed purchase cannot replace the only recoverable temporary snapshot");
            Check(Json(new SignalDebtSaveStore(temporaryDirectory).Load()) == Json(recoverable), "reloading cannot resurrect a purchase that Save rejected");
        }

        private static void RepairPaymentWriteFailure()
        {
            string directory = Fixture("failed-repair-payment-publication");
            var store = new SignalDebtSaveStore(directory);
            SignalDebtSaveData live = VerifiedUnpaid();
            Check(live.Validate() && live.serviceTestPassed && !live.jobPaid && live.balance == 80,
                "repair failure fixture is tested, unpaid and valid");
            Check(store.Save(live), "unpaid tested repair is saved before payment attempt");
            Directory.CreateDirectory(store.SavePath + ".bak");
            string unchanged = Json(live);
            Dictionary<string, byte[]> before = Files(directory);
            Check(!live.TryCommitChange(candidate => candidate.ClaimRepairPayment(), store.Save,
                out SignalDebtLedgerEvent committed, out string reason) && store.HasSaveError,
                "actual filesystem publication failure rejects repair payment");
            Check(committed == null && !string.IsNullOrEmpty(reason) && Json(live) == unchanged,
                "failed repair payment emits no committed event and leaves the whole live snapshot unchanged");
            Check(SameFiles(directory, before) && Directory.Exists(store.SavePath + ".bak"),
                "failed repair payment preserves prior save bytes and obstructing directory");
            SignalDebtSaveData restored = new SignalDebtSaveStore(directory).Load();
            Check(restored != null && Json(restored) == unchanged && !restored.jobPaid
                && !restored.HasEvent("repair-job-payment") && restored.balance == 80,
                "reload remains unpaid without a phantom receipt or payout");
        }

        private static void ChangedFilesBetweenWrites()
        {
            foreach (string change in new[] { "v2-corrupt-after-fresh-load", "v2-future-after-migration", "v4-corrupt-after-migration" })
            {
                string directory = Fixture(change); var store = new SignalDebtSaveStore(directory);
                if (change != "v2-corrupt-after-fresh-load") File.WriteAllText(store.VersionTwoSavePath, VersionTwoJson());
                SignalDebtSaveData state = store.Load(); Check(state != null, "earlier load was valid before external file change");
                if (change == "v2-corrupt-after-fresh-load") File.WriteAllText(store.VersionTwoSavePath, "not json");
                else if (change == "v2-future-after-migration") File.WriteAllText(store.VersionTwoSavePath + ".tmp", "{\"schemaVersion\":999}");
                else File.WriteAllText(store.SavePath, "not json");
                File.WriteAllText(store.LegacySavePath, LegacyJson());
                Dictionary<string, byte[]> before = Files(directory);
                Check(!store.Save(state) && store.LoadBlocked && SameFiles(directory, before),
                    "Save re-probes changed families instead of trusting its earlier Load");
            }
            foreach (string change in new[] { "v3-corrupt-after-fresh-load", "v3-future-after-migration", "v4-corrupt-after-v3-migration" })
            {
                string directory = Fixture(change); var store = new SignalDebtSaveStore(directory);
                if (change != "v3-corrupt-after-fresh-load")
                    File.WriteAllText(store.PreviousSavePath, VersionThreeJson(VerifiedUnpaid()));
                SignalDebtSaveData state = store.Load(); Check(state != null, "load precedes external schema3/schema4 family change");
                if (change == "v3-corrupt-after-fresh-load") File.WriteAllText(store.PreviousSavePath, "not json");
                else if (change == "v3-future-after-migration") File.WriteAllText(store.PreviousSavePath + ".tmp", "{\"schemaVersion\":999}");
                else File.WriteAllText(store.SavePath, "not json");
                File.WriteAllText(store.VersionTwoSavePath, VersionTwoJson());
                Dictionary<string, byte[]> before = Files(directory);
                Check(!store.Save(state) && store.LoadBlocked && SameFiles(directory, before),
                    "Save rechecks changed v3/v4 families and cannot shadow them with the earlier loaded state");
            }
            string currentDirectory = Fixture("v4-future-after-successful-save");
            var currentStore = new SignalDebtSaveStore(currentDirectory); SignalDebtSaveData current = Repaired();
            Check(currentStore.Save(current), "current write succeeds before later barrier");
            File.WriteAllText(currentStore.SavePath + ".tmp", "{\"schemaVersion\":999}");
            Dictionary<string, byte[]> currentBefore = Files(currentDirectory);
            Check(!currentStore.Save(current) && currentStore.LoadBlocked && SameFiles(currentDirectory, currentBefore),
                "subsequent Save detects a newer current recovery file and preserves every byte");
        }

        private static void CampaignDiscovery()
        {
            string directory = Fixture("campaign-discovery");
            Check(!SignalDebtLocalFiles.HasCampaign(directory), "empty campaign directory is absent");
            foreach (string name in new[] { "signal-debt-save-v4.json", "signal-debt-save-v3.json", "signal-debt-save-v2.json", "signal-debt-save.json" })
                foreach (string suffix in new[] { "", ".bak", ".tmp" })
                {
                    string path = Path.Combine(directory, name + suffix);
                    File.WriteAllText(path, "preserve even if corrupt");
                    Check(SignalDebtLocalFiles.HasCampaign(directory), "every save/recovery family is discoverable");
                    File.Delete(path); Directory.CreateDirectory(path);
                    Check(SignalDebtLocalFiles.HasCampaign(directory), "directory obstruction remains a campaign presence barrier");
                    Directory.Delete(path);
                }
        }

        private static Dictionary<string, long> FileTimes(string directory)
        {
            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                result[path.Substring(directory.Length + 1)] = File.GetLastWriteTimeUtc(path).Ticks;
            return result;
        }

        private static bool SameTimes(string directory, Dictionary<string, long> times)
        {
            Dictionary<string, long> current = FileTimes(directory);
            if (times.Count != current.Count) return false;
            foreach (var time in times) if (!current.TryGetValue(time.Key, out long ticks) || ticks != time.Value) return false;
            return true;
        }

        private static void CheckOldFiles(string directory, Dictionary<string, byte[]> before, Dictionary<string, long> times)
        {
            foreach (var file in before)
            {
                string path = Path.Combine(directory, file.Key);
                Check(EqualBytes(File.ReadAllBytes(path), file.Value) && File.GetLastWriteTimeUtc(path).Ticks == times[file.Key],
                    "migration and repeated v4 writes preserve original legacy bytes and modification times");
            }
        }

        private static Dictionary<string, byte[]> Files(string directory)
        {
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                result.Add(file.Substring(directory.Length + 1), File.ReadAllBytes(file));
            return result;
        }
        private static bool SameFiles(string directory, Dictionary<string, byte[]> expected)
        {
            Dictionary<string, byte[]> actual = Files(directory);
            if (actual.Count != expected.Count) return false;
            foreach (var item in expected)
                if (!actual.TryGetValue(item.Key, out byte[] bytes) || !EqualBytes(item.Value, bytes)) return false;
            return true;
        }
        private static bool EqualBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }
    }
}
#endif
