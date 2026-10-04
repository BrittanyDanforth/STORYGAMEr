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
    }

    [Serializable]
    public sealed class SignalDebtSaveData
    {
        public int schemaVersion = 1;
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
            if (jobAccepted) return null;
            jobAccepted = true;
            return Record("job_accepted", "Accepted the copy shop's authorized order-folder repair.", 0, "repair-job-accepted");
        }

        public SignalDebtLedgerEvent InspectRepair()
        {
            if (!jobAccepted || serviceInspected) return null;
            serviceInspected = true;
            return Record("service_inspected", "Inspected Shop PC. Order Folder requires the Order Index service.", 0, "repair-service-inspected");
        }

        public SignalDebtLedgerEvent ConnectRepair(bool correct)
        {
            if (!jobAccepted || !serviceInspected || serviceConnected || !correct) return null;
            serviceConnected = true;
            return Record("service_connected", "Connected Order Folder to its permitted Order Index service.", 0, "repair-service-connected");
        }

        public SignalDebtLedgerEvent TestRepair()
        {
            if (!serviceConnected || serviceTestPassed) return null;
            serviceTestPassed = true;
            clientMemories.Add("copy_shop_client: the player restored our order folder and tested it with permission.");
            return Record("service_test_passed", "Client opened today's order list successfully and resumed work.", 0, "repair-service-test");
        }

        public SignalDebtLedgerEvent ClaimRepairPayment()
        {
            if (!serviceTestPassed || jobPaid || HasEvent("repair-job-payment")) return null;
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

        public bool Validate()
        {
            if (schemaVersion != 1 || balance < 0 || rentDue < 0 || day < 1 || rentDeadlineDay < 1) return false;
            if (string.IsNullOrEmpty(saveId)) saveId = Guid.NewGuid().ToString("N");
            if (ledger == null) ledger = new List<SignalDebtLedgerEvent>();
            if (clientMemories == null) clientMemories = new List<string>();
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
            return true;
        }
    }

}
