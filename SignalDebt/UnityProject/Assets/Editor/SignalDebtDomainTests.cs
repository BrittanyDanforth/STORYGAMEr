#if SIGNAL_DEBT_PORTABLE_TEST
using System;
using SignalDebt;

// Compile with SignalDebtSaveData.cs only. No Unity engine or test package required.
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
        Check(state.InspectRepair() != null, "inspect accepted repair");
        Check(state.ConnectRepair(true) != null, "restore correct service");
        Check(state.TestRepair() != null, "test restored service");
        Check(state.ClaimRepairPayment() != null, "claim verified fee");
        return state;
    }

    private static int Main()
    {
        try
        {
            var fresh = new SignalDebtSaveData();
            Check(fresh.Validate(), "initial save is valid");
            Check(fresh.balance == 80 && fresh.rentDue == 450, "opening economy");
            Check(fresh.InspectRepair() == null && fresh.ConnectRepair(true) == null
                && fresh.TestRepair() == null && fresh.ClaimRepairPayment() == null,
                "cannot skip authorization or service prerequisites");
            Check(fresh.revision == 0 && fresh.balance == 80 && fresh.clientMemories.Count == 0,
                "invalid actions do not change money, history, or memories");
            fresh.AcceptRepair(); fresh.InspectRepair();
            int beforeWrong = fresh.revision;
            Check(fresh.ConnectRepair(false) == null && !fresh.serviceConnected
                && fresh.revision == beforeWrong, "wrong service is harmless");
            Check(fresh.ClaimRepairPayment() == null && fresh.TestRepair() == null,
                "inspection alone cannot pass or pay");

            var state = CompleteRepair();
            Check(state.balance == 200 && state.jobPaid && state.Validate(), "120 credit fee settles");
            Check(state.ledger.Count == 5 && state.clientMemories.Count == 2, "job and two client memories recorded");
            for (int i = 0; i < 100; i++)
            {
                Check(state.ClaimRepairPayment() == null, "duplicate fee rejected");
                state.AcceptRepair(); state.InspectRepair(); state.ConnectRepair(true); state.TestRepair();
            }
            Check(state.balance == 200 && state.revision == 5 && state.ledger.Count == 5
                && state.clientMemories.Count == 2, "repeated callbacks cannot duplicate rewards or memories");
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
