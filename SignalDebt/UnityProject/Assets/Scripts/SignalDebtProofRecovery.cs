using System;
using System.Collections.Generic;

namespace SignalDebt
{
    // The campaign saves a detached candidate before publishing document changes,
    // owner observations, exports or the once-only 160-credit settlement.
    public sealed partial class SignalDebtSaveData
    {
        public const string ProofAcceptanceEventId = "southbank-proof-accepted";
        public const string ProofPaymentEventId = "southbank-proof-payment";
        public const int ProofRecoveryFee = 160;
        public bool proofAccepted, proofPaid;
        public SignalDebtProofDocument proofDocument = SignalDebtProofDocument.CreateInitial();

        public bool CanClaimProofPayment
        {
            get
            {
                return Validate() && proofAccepted && !proofPaid && !HasEvent(ProofPaymentEventId)
                    && balance <= int.MaxValue - ProofRecoveryFee && revision < int.MaxValue
                    && proofDocument.CurrentOwnerVerified(clientDevice);
            }
        }

        public SignalDebtLedgerEvent AcceptProofRecovery()
        {
            if (!CanRecordProofChange() || !jobPaid || !HasEvent("repair-job-payment") || proofAccepted
                || HasEvent(ProofAcceptanceEventId) || !proofDocument.TryGrantRecoveryAccess(clientDevice, out string message)) return null;
            proofAccepted = true;
            return ProofEvent("proof_recovery_accepted", message, ProofAcceptanceEventId);
        }

        public SignalDebtLedgerEvent RestoreProofRevision(string revisionId)
        {
            if (!CanRecordProofChange() || !proofAccepted
                || !proofDocument.TryRestoreRevision(clientDevice, revisionId, out string message)) return null;
            return ProofEvent("proof_revision_restored", message);
        }

        public SignalDebtLedgerEvent UndoProofRestore()
        {
            if (!CanRecordProofChange() || !proofAccepted
                || !proofDocument.TryUndoRestore(clientDevice, out string message)) return null;
            return ProofEvent("proof_restore_undone", message);
        }

        public SignalDebtLedgerEvent RequestProofCheck()
        {
            if (!CanRecordProofChange() || !proofAccepted
                || !proofDocument.TryQueueOwnerCheck(clientDevice, out string message)) return null;
            return ProofEvent("proof_check_requested", message);
        }

        // A single owner event retains the exact read outcome and, when present,
        // export identity. Its document revision resolves to immutable content.
        public SignalDebtLedgerEvent StepProofTask()
        {
            if (!CanRecordProofChange() || !proofAccepted) return null;
            int previousCheckCount = proofDocument.OwnerCheckCount;
            bool hadExport = proofDocument.HasHistoricalExport;
            if (!proofDocument.TryStepOwnerTask(clientDevice, out string message)) return null;
            if (previousCheckCount == proofDocument.OwnerCheckCount)
                return ProofEvent("proof_check_cancelled", message, null, SignalDebtProofDocument.OwnerId);
            SignalDebtProofOwnerRead read = proofDocument.LastOwnerRead();
            SignalDebtLedgerEvent entry = ProofEvent("proof_owner_read_" + read.result, message,
                null, SignalDebtProofDocument.OwnerId, 0, read.number, read.exportId);
            if (!hadExport && read.result == SignalDebtProofResult.ApprovedMatch)
                clientMemories.Add("copy_shop_client: compared the recovered community notice with its approval and exported " + read.revisionId + ".");
            return entry;
        }

        public SignalDebtLedgerEvent ClaimProofPayment()
        {
            if (!CanClaimProofPayment || !proofDocument.TrySettleRecoveryAccess(clientDevice, out _)) return null;
            SignalDebtProofExport export = proofDocument.LatestExport();
            balance += ProofRecoveryFee; proofPaid = true;
            clientMemories.Add("copy_shop_client: paid the agreed 160 credits for the recovered proof; its original drafts remain in history.");
            return ProofEvent("proof_recovery_paid", "Southbank Copy Shop paid 160 credits for recovery of the approved community notice proof.",
                ProofPaymentEventId, SignalDebtProofDocument.OwnerId, ProofRecoveryFee, export.ownerReadNumber, export.id);
        }

        private bool CanRecordProofChange()
        { return revision < int.MaxValue && Validate(); }

        private SignalDebtLedgerEvent ProofEvent(string type, string message, string stableId = null,
            string actorId = "player", int amount = 0, int ownerCheckNumber = 0, string exportId = "")
        {
            SignalDebtLedgerEvent entry = Record(type, message, amount, stableId);
            if (entry == null) return null;
            entry.actorId = actorId; entry.deviceId = SignalDebtProofDocument.DeviceId;
            entry.recordId = SignalDebtProofDocument.DocumentId; entry.deviceRevision = clientDevice.configurationRevision;
            entry.documentRevisionId = proofDocument.currentRevisionId;
            entry.ownerCheckNumber = ownerCheckNumber; entry.exportId = exportId;
            return entry;
        }

        private bool ValidateProofRecovery()
        {
            if (proofDocument == null || !proofDocument.Validate(clientDevice)) return false;
            SignalDebtProofGrant expectedGrant = proofPaid ? SignalDebtProofGrant.ReadOnlyAfterSettlement
                : proofAccepted ? SignalDebtProofGrant.Recover : SignalDebtProofGrant.None;
            if (proofDocument.accessGrant != expectedGrant || (proofAccepted && !jobPaid) || (proofPaid && !proofAccepted)) return false;
            int currentDocumentNumber = 3, ownerChecks = 0, exportCount = 0, priorDeviceRevision = -1;
            bool accepted = false, paid = false, firstRepairPaid = false, queued = false;
            int queuedDeviceRevision = -1;
            string queuedDocumentRevision = "";
            SignalDebtLedgerEvent latestObservation = null, latestExport = null;
            var observedPower = new Dictionary<int, bool>();
            foreach (SignalDebtLedgerEvent entry in ledger)
            {
                if (entry.id == "repair-job-payment") firstRepairPaid = true;
                bool proofEvent = (entry.type != null && entry.type.StartsWith("proof_", StringComparison.Ordinal))
                    || entry.id == ProofAcceptanceEventId || entry.id == ProofPaymentEventId
                    || entry.recordId == SignalDebtProofDocument.DocumentId;
                if (!proofEvent) continue;
                if ((entry.id == ProofAcceptanceEventId && entry.type != "proof_recovery_accepted")
                    || (entry.id == ProofPaymentEventId && entry.type != "proof_recovery_paid")) return false;
                if (entry.deviceId != SignalDebtProofDocument.DeviceId || entry.recordId != SignalDebtProofDocument.DocumentId
                    || entry.deviceRevision < 0 || entry.deviceRevision > clientDevice.configurationRevision
                    || entry.deviceRevision < priorDeviceRevision || entry.documentRevisionId == null
                    || entry.exportId == null) return false;
                priorDeviceRevision = entry.deviceRevision;
                if (entry.type == "proof_recovery_accepted")
                {
                    if (accepted || !firstRepairPaid || entry.id != ProofAcceptanceEventId || entry.actorId != "player"
                        || entry.amount != 0 || entry.ownerCheckNumber != 0 || entry.exportId != ""
                        || entry.documentRevisionId != SignalDebtProofDocument.RevisionId(3)) return false;
                    accepted = true; continue;
                }
                if (!accepted || entry.id == ProofAcceptanceEventId) return false;
                if (entry.type == "proof_revision_restored" || entry.type == "proof_restore_undone")
                {
                    if (paid || currentDocumentNumber >= proofDocument.RevisionCount || entry.actorId != "player"
                        || entry.amount != 0 || entry.ownerCheckNumber != 0 || entry.exportId != "") return false;
                    SignalDebtProofRevision documentRevision = proofDocument.revisions[currentDocumentNumber++];
                    SignalDebtProofChange change = entry.type == "proof_revision_restored" ? SignalDebtProofChange.Restore : SignalDebtProofChange.Undo;
                    if (documentRevision.id != entry.documentRevisionId || documentRevision.change != change) return false;
                    queued = false; queuedDeviceRevision = -1; queuedDocumentRevision = "";
                    continue;
                }
                if (entry.documentRevisionId != SignalDebtProofDocument.RevisionId(currentDocumentNumber)) return false;
                if (entry.type == "proof_check_requested")
                {
                    if (queued || entry.actorId != "player" || entry.amount != 0 || entry.ownerCheckNumber != 0 || entry.exportId != ""
                        || (latestObservation != null && latestObservation.type == "proof_owner_read_ApprovedMatch"
                            && latestObservation.documentRevisionId == entry.documentRevisionId
                            && latestObservation.deviceRevision == entry.deviceRevision)) return false;
                    queued = true; queuedDeviceRevision = entry.deviceRevision; queuedDocumentRevision = entry.documentRevisionId;
                    continue;
                }
                if (entry.type == "proof_check_cancelled")
                {
                    if (!queued || entry.actorId != SignalDebtProofDocument.OwnerId || entry.amount != 0
                        || entry.ownerCheckNumber != 0 || entry.exportId != "" || queuedDeviceRevision == entry.deviceRevision) return false;
                    queued = false; queuedDeviceRevision = -1; queuedDocumentRevision = "";
                    continue;
                }
                const string readPrefix = "proof_owner_read_";
                if (entry.type != null && entry.type.StartsWith(readPrefix, StringComparison.Ordinal))
                {
                    SignalDebtProofResult result;
                    if (!queued || queuedDeviceRevision != entry.deviceRevision || queuedDocumentRevision != entry.documentRevisionId
                        || entry.actorId != SignalDebtProofDocument.OwnerId || entry.amount != 0 || ownerChecks == int.MaxValue
                        || entry.ownerCheckNumber != ownerChecks + 1
                        || !Enum.TryParse(entry.type.Substring(readPrefix.Length), out result)
                        || entry.type != readPrefix + result) return false;
                    bool powered = result != SignalDebtProofResult.PoweredOff;
                    if (powered && result != ProofContentResult(proofDocument.revisions[currentDocumentNumber - 1].content)) return false;
                    bool previousPower;
                    if (observedPower.TryGetValue(entry.deviceRevision, out previousPower) && previousPower != powered) return false;
                    observedPower[entry.deviceRevision] = powered;
                    if (result == SignalDebtProofResult.ApprovedMatch)
                    {
                        if (entry.exportId != SignalDebtProofDocument.DocumentId + ":export-" + entry.ownerCheckNumber) return false;
                        latestExport = entry; exportCount++;
                    }
                    else if (entry.exportId != "") return false;
                    ownerChecks++; latestObservation = entry;
                    queued = false; queuedDeviceRevision = -1; queuedDocumentRevision = "";
                    continue;
                }
                if (entry.type == "proof_recovery_paid")
                {
                    if (paid || queued || entry.id != ProofPaymentEventId || entry.actorId != SignalDebtProofDocument.OwnerId
                        || entry.amount != ProofRecoveryFee || latestObservation == null || latestExport != latestObservation
                        || entry.documentRevisionId != latestExport.documentRevisionId || entry.deviceRevision != latestExport.deviceRevision
                        || entry.ownerCheckNumber != latestExport.ownerCheckNumber || entry.exportId != latestExport.exportId) return false;
                    paid = true; continue;
                }
                return false;
            }
            if (accepted != proofAccepted || paid != proofPaid || currentDocumentNumber != proofDocument.RevisionCount
                || ownerChecks != proofDocument.OwnerCheckCount || exportCount != proofDocument.successfulExportCount
                || queued != proofDocument.ownerCheckQueued) return false;
            if (queued && (queuedDeviceRevision != proofDocument.queuedDeviceConfigurationRevision
                || queuedDocumentRevision != proofDocument.queuedRevisionId)) return false;
            SignalDebtProofOwnerRead readSnapshot = proofDocument.LastOwnerRead();
            if (latestObservation != null && (readSnapshot == null || latestObservation.type != "proof_owner_read_" + readSnapshot.result
                || latestObservation.documentRevisionId != readSnapshot.revisionId || latestObservation.ownerCheckNumber != readSnapshot.number
                || latestObservation.deviceRevision != readSnapshot.deviceConfigurationRevision || latestObservation.exportId != readSnapshot.exportId)) return false;
            SignalDebtProofExport exportSnapshot = proofDocument.LatestExport();
            if (latestExport != null && (exportSnapshot == null || latestExport.exportId != exportSnapshot.id
                || latestExport.documentRevisionId != exportSnapshot.revisionId || latestExport.ownerCheckNumber != exportSnapshot.ownerReadNumber
                || latestExport.deviceRevision != exportSnapshot.deviceConfigurationRevision)) return false;
            return true;
        }

        private SignalDebtProofResult ProofContentResult(SignalDebtProofContent content)
        {
            SignalDebtProofContent reference = proofDocument.approval.content;
            if (content.title != reference.title) return SignalDebtProofResult.TitleMismatch;
            if (content.eventDate != reference.eventDate) return SignalDebtProofResult.DateMismatch;
            if (string.IsNullOrEmpty(content.contact)) return SignalDebtProofResult.ContactMissing;
            if (content.contact != reference.contact) return SignalDebtProofResult.ContactMismatch;
            if (string.IsNullOrEmpty(content.footer)) return SignalDebtProofResult.FooterMissing;
            if (content.footer != reference.footer) return SignalDebtProofResult.FooterMismatch;
            return SignalDebtProofResult.ApprovedMatch;
        }
    }
}
