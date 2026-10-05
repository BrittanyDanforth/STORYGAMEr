using System;
using System.Collections.Generic;

namespace SignalDebt
{
    [Serializable]
    public sealed class SignalDebtLedgerEvent
    {
        public string id;
        public string type;
        public string summary;
        public int amount;
        public int day;
        public int worldRevision;
        public string utc;
        public string actorId, deviceId, recordId;
        public int deviceRevision;
        public string documentRevisionId, exportId;
        public int ownerCheckNumber;
    }

    [Serializable]
    public sealed partial class SignalDebtSaveData
    {
        public const int CurrentSchemaVersion = 4;
        public int schemaVersion = CurrentSchemaVersion;
        public string saveId = Guid.NewGuid().ToString("N");
        public int revision;
        public int balance = 80;
        public int rentDue = 450;
        public int day = 1;
        public int rentDeadlineDay = 4;
        public bool extensionUsed;
        public bool jobAccepted;
        public bool serviceInspected;
        public bool serviceConnected;
        public bool serviceTestPassed;
        public bool jobPaid;
        public List<SignalDebtLedgerEvent> ledger = new List<SignalDebtLedgerEvent>();
        public List<string> clientMemories = new List<string>();

        public bool HasEvent(string id)
        {
            return ledger.Exists(item => item != null && item.id == id);
        }

        public SignalDebtLedgerEvent AcceptRepair()
        {
            if (jobAccepted || clientDevice == null || !clientDevice.TryGrantRepairAccess(out _)) return null;
            jobAccepted = true;
            return Record("job_accepted", "Accepted the copy shop's authorized order-folder repair.", 0, "repair-job-accepted");
        }

        public SignalDebtLedgerEvent InspectRepair()
        {
            if (!jobAccepted || serviceInspected || clientDevice == null || !clientDevice.sessionOpen) return null;
            serviceInspected = true;
            return DeviceEvent("service_inspected", "Inspected the Order Folder records input and the workstation's service outputs.", "repair-service-inspected");
        }

        public SignalDebtLedgerEvent ClaimRepairPayment()
        {
            if (!CanClaimRepairPayment || !clientDevice.TrySettleRepairAccess(out _)) return null;
            balance += 120;
            jobPaid = true;
            clientMemories.Add("copy_shop_client: paid the agreed 120 credits; the job is complete.");
            return Record("job_paid", "Southbank Copy Shop paid the agreed repair fee.", 120, "repair-job-payment");
        }

        public SignalDebtLedgerEvent ApplyRentPayment(int requested)
        {
            int amount = Math.Min(Math.Max(0, requested), Math.Min(balance, rentDue));
            if (amount == 0) return null;
            balance -= amount;
            rentDue -= amount;
            return Record("rent_paid", "Paid " + amount + " credits toward overdue rent.", -amount);
        }

        public SignalDebtLedgerEvent ExtendRentDeadline()
        {
            if (extensionUsed || rentDue == 0) return null;
            extensionUsed = true;
            rentDeadlineDay += 2;
            return Record("rent_extension", "Landlord agreed to one two-day hardship extension.", 0, "rent-extension");
        }

        public SignalDebtLedgerEvent AdvanceDay()
        {
            day++;
            return Record("rested", "Rested to day " + day + ". Existing bills remain.");
        }

        public SignalDebtLedgerEvent Record(string type, string summary, int amount = 0, string stableId = null)
        {
            string id = stableId ?? Guid.NewGuid().ToString("N");
            if (HasEvent(id)) return null;
            var entry = new SignalDebtLedgerEvent
            {
                id = id, type = type, summary = summary, amount = amount,
                day = day, worldRevision = revision + 1, utc = DateTime.UtcNow.ToString("o")
            };
            ledger.Add(entry);
            revision++;
            return entry;
        }

        // Gameplay commands use a detached candidate. Rejected writes must not
        // leave live rewards, obligations, memories or day changes behind.
        public bool TryCommitChange(Func<SignalDebtSaveData, SignalDebtLedgerEvent> change,
            Func<SignalDebtSaveData, bool> persist, out SignalDebtLedgerEvent committed, out string reason)
        {
            committed = null;
            reason = "";
            if (change == null || persist == null || !Validate())
            { reason = "This change could not be verified. Reload your saved game."; return false; }
            SignalDebtSaveData candidate = Copy();
            SignalDebtLedgerEvent entry = change(candidate);
            if (entry == null)
            { reason = "No changes were applied."; return false; }
            if (!candidate.Validate())
            { reason = "This change could not be recorded. Your saved commitments are unchanged."; return false; }
            if (!persist(candidate))
            { reason = "The change was not saved. Your money and commitments have not changed."; return false; }
            PublishSavedCopy(candidate);
            committed = entry;
            return true;
        }

        private void PublishSavedCopy(SignalDebtSaveData candidate)
        {
            // Keep this campaign object: replacing it would make the dialogue
            // client interpret each successful transaction as another load.
            schemaVersion = candidate.schemaVersion;
            saveId = candidate.saveId;
            revision = candidate.revision;
            balance = candidate.balance;
            rentDue = candidate.rentDue;
            day = candidate.day;
            rentDeadlineDay = candidate.rentDeadlineDay;
            extensionUsed = candidate.extensionUsed;
            jobAccepted = candidate.jobAccepted;
            serviceInspected = candidate.serviceInspected;
            serviceConnected = candidate.serviceConnected;
            serviceTestPassed = candidate.serviceTestPassed;
            jobPaid = candidate.jobPaid;
            ledger = candidate.ledger;
            clientMemories = candidate.clientMemories;
            shopPurchases = candidate.shopPurchases;
            clientDevice = candidate.clientDevice;
            proofDocument = candidate.proofDocument;
            proofAccepted = candidate.proofAccepted;
            proofPaid = candidate.proofPaid;
        }

        public bool Validate()
        {
            if (schemaVersion != CurrentSchemaVersion || balance < 0 || rentDue < 0 || day < 1 || rentDeadlineDay < 1) return false;
            if (string.IsNullOrEmpty(saveId) || ledger == null || clientMemories == null) return false;
            var ids = new HashSet<string>();
            int lastRevision = 0;
            foreach (SignalDebtLedgerEvent entry in ledger)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id) || !ids.Add(entry.id)
                    || entry.worldRevision != lastRevision + 1 || entry.day < 1) return false;
                lastRevision = entry.worldRevision;
            }
            if (revision != lastRevision) return false;
            // This slice writes one snapshot: a paid flag and its receipt must agree.
            if (jobPaid != HasEvent("repair-job-payment")) return false;
            if (jobPaid && !serviceTestPassed) return false;
            if (serviceTestPassed && !serviceConnected) return false;
            if (serviceConnected && !serviceInspected) return false;
            if (serviceInspected && !jobAccepted) return false;
            return ValidatePurchase() && ValidateClientRepair() && ValidateProofRecovery();
        }
    }

}
