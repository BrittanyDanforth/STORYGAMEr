using System;
using System.Collections.Generic;

namespace SignalDebt
{
    public enum SignalDebtProofGrant { None, Recover, ReadOnlyAfterSettlement }
    public enum SignalDebtProofChange { Original, Restore, Undo }
    public enum SignalDebtProofResult
    {
        NotObserved, ApprovedMatch, DateMismatch, ContactMissing, ContactMismatch,
        FooterMissing, FooterMismatch, TitleMismatch, PoweredOff, PermissionRequired,
        SessionClosed, UnknownRevision, InvalidState
    }

    [Serializable]
    public sealed class SignalDebtProofContent
    {
        public string title, eventDate, contact, footer;
        public SignalDebtProofContent Copy() { return (SignalDebtProofContent)MemberwiseClone(); }
        public bool SameAs(SignalDebtProofContent other)
        {
            return other != null && title == other.title && eventDate == other.eventDate
                && contact == other.contact && footer == other.footer;
        }
    }

    [Serializable]
    public sealed class SignalDebtProofRevision
    {
        public string id, previousRevisionId, sourceRevisionId, createdBy;
        public int number;
        public SignalDebtProofChange change;
        public SignalDebtProofContent content;
        public SignalDebtProofRevision Copy()
        {
            var copy = (SignalDebtProofRevision)MemberwiseClone();
            copy.content = content == null ? null : content.Copy(); return copy;
        }
    }

    [Serializable]
    public sealed class SignalDebtProofApproval
    {
        public string id, deviceId, documentId, ownerId, sourceRevisionId;
        public SignalDebtProofContent content;
        public SignalDebtProofApproval Copy()
        {
            var copy = (SignalDebtProofApproval)MemberwiseClone();
            copy.content = content == null ? null : content.Copy(); return copy;
        }
    }

    [Serializable]
    public sealed class SignalDebtProofOwnerRead
    {
        public int number, deviceConfigurationRevision;
        public string actorId, deviceId, documentId, revisionId, exportId;
        public bool powered;
        public SignalDebtProofResult result;
        public SignalDebtProofOwnerRead Copy() { return (SignalDebtProofOwnerRead)MemberwiseClone(); }
    }

    // A real simulated output file. This records an export, never physical printing.
    [Serializable]
    public sealed class SignalDebtProofExport
    {
        public string id, actorId, deviceId, documentId, revisionId;
        public int ownerReadNumber, deviceConfigurationRevision;
        public SignalDebtProofContent content;
        public SignalDebtProofExport Copy()
        {
            var copy = (SignalDebtProofExport)MemberwiseClone();
            copy.content = content == null ? null : content.Copy(); return copy;
        }
    }

    // Content remains visible when its comparison fails. Access failures expose none.
    public sealed class SignalDebtProofRead
    {
        public readonly SignalDebtProofResult Result;
        public readonly string Message;
        public readonly SignalDebtProofRevision Revision;
        public readonly SignalDebtProofApproval Approval;
        public bool MatchesApproval { get { return Result == SignalDebtProofResult.ApprovedMatch; } }
        internal SignalDebtProofRead(SignalDebtProofResult result, string message,
            SignalDebtProofRevision revision, SignalDebtProofApproval approval)
        { Result = result; Message = message; Revision = revision; Approval = approval; }
    }

    /// <summary>
    /// One separate document on Southbank's existing workstation. All commands mutate
    /// a detached campaign candidate; the campaign must save before publishing it.
    /// No money, Order Folder changes, AI authority, or historical order completion.
    /// </summary>
    [Serializable]
    public sealed class SignalDebtProofDocument
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaxHistoryPageSize = 12;
        public const string DeviceId = SignalDebtClientDevice.DeviceId;
        public const string OwnerId = SignalDebtClientDevice.OwnerId;
        public const string DocumentId = "southbank-community-notice-proof";
        public const string ApprovalId = "southbank-community-notice-approval";
        public const string OwnerTaskId = "southbank-compare-and-export-proof";
        public const string RecoveryAccessLabel = "Community notice proof and its history only — recovery permitted";
        public const string SettledAccessLabel = "Community notice proof — recovery settled; read-only";

        public int schemaVersion;
        public string deviceId, ownerId, documentId, currentRevisionId;
        public SignalDebtProofGrant accessGrant;
        public SignalDebtProofApproval approval;
        public List<SignalDebtProofRevision> revisions;
        // The campaign ledger keeps each actual check. This app keeps bounded
        // observation state and all referenced document versions, without an
        // artificial attempt limit that could strand a recoverable draft.
        public int ownerCheckCount, successfulExportCount;
        // JsonUtility cannot preserve null inline classes. Zero-or-one lists
        // represent absent observations/exports faithfully in Unity save files.
        public List<SignalDebtProofOwnerRead> ownerReadSnapshots;
        public List<SignalDebtProofExport> exportSnapshots;
        private SignalDebtProofOwnerRead LastReadState
        {
            get { return ownerReadSnapshots != null && ownerReadSnapshots.Count == 1 ? ownerReadSnapshots[0] : null; }
            set { ownerReadSnapshots = value == null ? new List<SignalDebtProofOwnerRead>() : new List<SignalDebtProofOwnerRead> { value }; }
        }
        private SignalDebtProofExport LatestExportState
        {
            get { return exportSnapshots != null && exportSnapshots.Count == 1 ? exportSnapshots[0] : null; }
            set { exportSnapshots = value == null ? new List<SignalDebtProofExport>() : new List<SignalDebtProofExport> { value }; }
        }
        public bool ownerCheckQueued, queuedPowered;
        public string queuedRevisionId;
        public int queuedDeviceConfigurationRevision;

        public int OwnerCheckCount { get { return ownerCheckCount; } }
        public int RevisionCount { get { return revisions == null ? 0 : revisions.Count; } }
        public bool HasHistoricalExport { get { return Validate() && LatestExportState != null; } }
        public bool HasUndoRestore
        {
            get { return Validate() && accessGrant == SignalDebtProofGrant.Recover && revisions.Count < int.MaxValue
                && revisions[revisions.Count - 1].change == SignalDebtProofChange.Restore; }
        }

        public static string RevisionId(int number) { return DocumentId + ":r" + number; }
        public static SignalDebtProofDocument CreateInitial()
        {
            var document = new SignalDebtProofDocument
            {
                schemaVersion = CurrentSchemaVersion, deviceId = DeviceId, ownerId = OwnerId,
                documentId = DocumentId, currentRevisionId = RevisionId(3),
                accessGrant = SignalDebtProofGrant.None, queuedRevisionId = "",
                queuedDeviceConfigurationRevision = -1,
                revisions = new List<SignalDebtProofRevision>(),
                ownerReadSnapshots = new List<SignalDebtProofOwnerRead>(), exportSnapshots = new List<SignalDebtProofExport>(),
                approval = new SignalDebtProofApproval
                {
                    id = ApprovalId, deviceId = DeviceId, ownerId = OwnerId,
                    documentId = DocumentId, sourceRevisionId = RevisionId(2), content = OriginalContent(2)
                }
            };
            for (int n = 1; n <= 3; n++) document.revisions.Add(new SignalDebtProofRevision
            {
                id = RevisionId(n), number = n, previousRevisionId = n == 1 ? "" : RevisionId(n - 1),
                sourceRevisionId = "", createdBy = OwnerId, change = SignalDebtProofChange.Original,
                content = OriginalContent(n)
            });
            return document;
        }

        public SignalDebtProofDocument Copy()
        {
            var copy = (SignalDebtProofDocument)MemberwiseClone();
            copy.approval = approval == null ? null : approval.Copy();
            copy.revisions = revisions == null ? null : revisions.ConvertAll(r => r == null ? null : r.Copy());
            copy.ownerReadSnapshots = ownerReadSnapshots == null ? null : ownerReadSnapshots.ConvertAll(r => r == null ? null : r.Copy());
            copy.exportSnapshots = exportSnapshots == null ? null : exportSnapshots.ConvertAll(e => e == null ? null : e.Copy());
            return copy;
        }

        public bool Validate()
        {
            if (schemaVersion != CurrentSchemaVersion || deviceId != DeviceId || ownerId != OwnerId
                || documentId != DocumentId || !Enum.IsDefined(typeof(SignalDebtProofGrant), accessGrant)
                || ownerReadSnapshots == null || ownerReadSnapshots.Count > 1 || (ownerReadSnapshots.Count == 1 && ownerReadSnapshots[0] == null)
                || exportSnapshots == null || exportSnapshots.Count > 1 || (exportSnapshots.Count == 1 && exportSnapshots[0] == null)
                || revisions == null || revisions.Count < 3
                || currentRevisionId != RevisionId(revisions.Count)
                || ownerCheckCount < 0 || successfulExportCount < 0 || successfulExportCount > ownerCheckCount) return false;
            if (approval == null || approval.id != ApprovalId || approval.deviceId != DeviceId
                || approval.ownerId != OwnerId || approval.documentId != DocumentId
                || approval.sourceRevisionId != RevisionId(2) || !OriginalContent(2).SameAs(approval.content)) return false;
            var byId = new Dictionary<string, SignalDebtProofRevision>();
            for (int i = 0; i < revisions.Count; i++)
            {
                SignalDebtProofRevision r = revisions[i];
                if (r == null || r.number != i + 1 || r.id != RevisionId(i + 1) || r.content == null
                    || r.previousRevisionId != (i == 0 ? "" : RevisionId(i))) return false;
                if (i < 3)
                {
                    if (r.change != SignalDebtProofChange.Original || r.sourceRevisionId != ""
                        || r.createdBy != OwnerId || !OriginalContent(i + 1).SameAs(r.content)) return false;
                }
                else
                {
                    SignalDebtProofRevision source;
                    if (r.createdBy != "player" || r.sourceRevisionId == null
                        || !byId.TryGetValue(r.sourceRevisionId, out source) || !source.content.SameAs(r.content)) return false;
                    if (r.change == SignalDebtProofChange.Undo)
                    {
                        SignalDebtProofRevision previous = revisions[i - 1];
                        if (previous.change != SignalDebtProofChange.Restore
                            || r.sourceRevisionId != previous.previousRevisionId) return false;
                    }
                    else if (r.change != SignalDebtProofChange.Restore) return false;
                }
                byId.Add(r.id, r);
            }
            int priorDeviceRevision = -1;
            SignalDebtProofRevision observed = null;
            if (ownerCheckCount == 0)
            {
                if (LastReadState != null || LatestExportState != null || successfulExportCount != 0) return false;
            }
            else
            {
                SignalDebtProofOwnerRead read = LastReadState;
                if (read == null || read.number != ownerCheckCount || read.actorId != OwnerId || read.deviceId != DeviceId
                    || read.documentId != DocumentId || read.revisionId == null
                    || !byId.TryGetValue(read.revisionId, out observed) || observed.number < 3
                    || read.deviceConfigurationRevision < 0
                    || read.result != (read.powered ? Compare(observed.content) : SignalDebtProofResult.PoweredOff)) return false;
                priorDeviceRevision = read.deviceConfigurationRevision;
                if (read.result == SignalDebtProofResult.ApprovedMatch)
                {
                    if (LatestExportState == null || LatestExportState.ownerReadNumber != read.number
                        || read.exportId != LatestExportState.id || LatestExportState.revisionId != read.revisionId
                        || LatestExportState.deviceConfigurationRevision != read.deviceConfigurationRevision) return false;
                }
                else if (read.exportId != "" || (LatestExportState != null && LatestExportState.ownerReadNumber >= read.number)) return false;
            }
            if (successfulExportCount == 0)
            {
                if (LatestExportState != null) return false;
            }
            else
            {
                SignalDebtProofExport export = LatestExportState; SignalDebtProofRevision exported;
                if (export == null || export.ownerReadNumber < successfulExportCount || export.ownerReadNumber > ownerCheckCount
                    || export.id != ExportId(export.ownerReadNumber) || export.actorId != OwnerId
                    || export.deviceId != DeviceId || export.documentId != DocumentId || export.revisionId == null
                    || !byId.TryGetValue(export.revisionId, out exported) || exported.number < 3
                    || observed == null || exported.number > observed.number || !exported.content.SameAs(export.content)
                    || Compare(export.content) != SignalDebtProofResult.ApprovedMatch
                    || export.deviceConfigurationRevision < 0 || export.deviceConfigurationRevision > priorDeviceRevision
                    || (export.deviceConfigurationRevision == priorDeviceRevision && !LastReadState.powered)) return false;
            }
            if (accessGrant == SignalDebtProofGrant.None
                && (revisions.Count != 3 || ownerCheckCount != 0 || ownerCheckQueued)) return false;
            if (accessGrant == SignalDebtProofGrant.ReadOnlyAfterSettlement
                && (LatestExportState == null || LatestExportState.revisionId != currentRevisionId)) return false;
            if (ownerCheckQueued)
            {
                if (accessGrant == SignalDebtProofGrant.None || ownerCheckCount == int.MaxValue
                    || queuedRevisionId != currentRevisionId || queuedDeviceConfigurationRevision < 0
                    || queuedDeviceConfigurationRevision < priorDeviceRevision
                    || (LastReadState != null && queuedDeviceConfigurationRevision == priorDeviceRevision
                        && queuedPowered != LastReadState.powered)) return false;
            }
            else if (queuedRevisionId != "" || queuedDeviceConfigurationRevision != -1 || queuedPowered) return false;
            return true;
        }

        // Cross-object validation preserves one workstation authority. The campaign
        // additionally checks jobPaid and its once-only accepted/settled ledger events.
        public bool Validate(SignalDebtClientDevice device)
        {
            if (!Validate() || device == null || !device.Validate() || device.deviceId != deviceId
                || device.ownerId != ownerId) return false;
            if (accessGrant != SignalDebtProofGrant.None
                && device.accessGrant != SignalDebtDeviceGrant.ReadOnlyAfterSettlement) return false;
            if (LastReadState != null && (LastReadState.deviceConfigurationRevision > device.configurationRevision
                || (LastReadState.deviceConfigurationRevision == device.configurationRevision && LastReadState.powered != device.powered))) return false;
            return !ownerCheckQueued || (queuedDeviceConfigurationRevision <= device.configurationRevision
                && (queuedDeviceConfigurationRevision != device.configurationRevision || queuedPowered == device.powered));
        }

        public bool CurrentOwnerVerified(SignalDebtClientDevice device)
        {
            if (!Validate(device) || !device.powered || LastReadState == null) return false;
            SignalDebtProofOwnerRead read = LastReadState;
            return read.result == SignalDebtProofResult.ApprovedMatch && read.revisionId == currentRevisionId
                && read.deviceConfigurationRevision == device.configurationRevision && read.powered;
        }

        public SignalDebtProofOwnerRead LastOwnerRead()
        { return !Validate() || LastReadState == null ? null : LastReadState.Copy(); }

        public SignalDebtProofExport LatestExport()
        { return !Validate() || LatestExportState == null ? null : LatestExportState.Copy(); }

        public bool TryGrantRecoveryAccess(SignalDebtClientDevice device, out string message)
        {
            if (!ValidCommand(device, out message)) return false;
            if (device.accessGrant != SignalDebtDeviceGrant.ReadOnlyAfterSettlement)
            { message = "Finish and settle Southbank's Order Folder repair before accepting proof recovery."; return false; }
            if (accessGrant != SignalDebtProofGrant.None)
            { message = "This document's existing permission cannot be expanded by accepting again."; return false; }
            accessGrant = SignalDebtProofGrant.Recover;
            message = "Southbank permits recovery of the community notice proof and inspection of its history. Order Folder remains read-only.";
            return true;
        }

        public SignalDebtProofRead ReadCurrent(SignalDebtClientDevice device)
        { return ReadRevision(device, currentRevisionId); }

        public SignalDebtProofRead ReadRevision(SignalDebtClientDevice device, string revisionId)
        {
            SignalDebtProofResult access = ReadAccess(device, true);
            if (access != SignalDebtProofResult.ApprovedMatch) return FailedRead(access);
            SignalDebtProofRevision revision = FindRevision(revisionId);
            if (revision == null) return FailedRead(SignalDebtProofResult.UnknownRevision);
            SignalDebtProofResult comparison = Compare(revision.content);
            return new SignalDebtProofRead(comparison, Describe(comparison, revision.content), revision.Copy(), approval.Copy());
        }

        public SignalDebtProofApproval ReadApproval(SignalDebtClientDevice device)
        { return ReadAccess(device, true) == SignalDebtProofResult.ApprovedMatch ? approval.Copy() : null; }

        // Zero-based oldest-first pages: originals remain addressable, and no UI
        // request copies an unbounded document history into one screen.
        public SignalDebtProofRevision[] ReadHistory(SignalDebtClientDevice device, int startIndex = 0, int count = MaxHistoryPageSize)
        {
            if (ReadAccess(device, true) != SignalDebtProofResult.ApprovedMatch || startIndex < 0
                || startIndex >= revisions.Count || count <= 0) return new SignalDebtProofRevision[0];
            int length = Math.Min(Math.Min(count, MaxHistoryPageSize), revisions.Count - startIndex);
            return revisions.GetRange(startIndex, length).ConvertAll(r => r.Copy()).ToArray();
        }

        public bool TryRestoreRevision(SignalDebtClientDevice device, string revisionId, out string message)
        {
            if (!CanEdit(device, out message)) return false;
            SignalDebtProofRevision source = FindRevision(revisionId);
            if (source == null) { message = "That revision is not in this document's history."; return false; }
            AppendRevision(source, SignalDebtProofChange.Restore);
            message = "Created " + currentRevisionId + " from " + source.id
                + ". The previous draft and all source revisions remain in history. Ask Southbank to compare and export the current proof.";
            return true;
        }

        public bool TryUndoRestore(SignalDebtClientDevice device, out string message)
        {
            if (!CanEdit(device, out message)) return false;
            if (!HasUndoRestore) { message = "There is no unused restore to undo."; return false; }
            SignalDebtProofRevision current = revisions[revisions.Count - 1];
            AppendRevision(FindRevision(current.previousRevisionId), SignalDebtProofChange.Undo);
            message = "Restored the previous draft as " + currentRevisionId + ". The recovery attempt remains in history; a fresh owner check is required.";
            return true;
        }

        public bool TryQueueOwnerCheck(SignalDebtClientDevice device, out string message)
        {
            if (!ValidCommand(device, out message)) return false;
            if (accessGrant == SignalDebtProofGrant.None)
            { message = "Accept the separate proof-recovery permission first."; return false; }
            if (ownerCheckQueued) { message = "Southbank's proof check is already queued."; return false; }
            if (CurrentOwnerVerified(device)) { message = "Southbank already exported this verified revision."; return false; }
            if (ownerCheckCount == int.MaxValue)
            { message = "Another owner check cannot be numbered. No history or export was changed."; return false; }
            ownerCheckQueued = true; queuedRevisionId = currentRevisionId;
            queuedDeviceConfigurationRevision = device.configurationRevision; queuedPowered = device.powered;
            message = "Southbank will compare the current proof with the recorded approval and export it if all fields agree.";
            return true;
        }

        // True means processed (including failure/cancellation), never 'correct'.
        // Owner work continues with the player's session closed and needs no AI.
        public bool TryStepOwnerTask(SignalDebtClientDevice device, out string message)
        {
            if (!ValidCommand(device, out message)) return false;
            if (!ownerCheckQueued) { message = "There is no queued proof check."; return false; }
            bool stale = queuedRevisionId != currentRevisionId || queuedDeviceConfigurationRevision != device.configurationRevision
                || queuedPowered != device.powered;
            ClearQueue();
            if (stale) { message = "The workstation or proof changed. The old check was cancelled; request a fresh check."; return true; }
            SignalDebtProofRevision revision = revisions[revisions.Count - 1];
            SignalDebtProofResult result = device.powered ? Compare(revision.content) : SignalDebtProofResult.PoweredOff;
            var read = new SignalDebtProofOwnerRead
            {
                number = ownerCheckCount + 1, actorId = OwnerId, deviceId = DeviceId, documentId = DocumentId,
                revisionId = currentRevisionId, deviceConfigurationRevision = device.configurationRevision,
                powered = device.powered, result = result, exportId = ""
            };
            if (result == SignalDebtProofResult.ApprovedMatch)
            {
                read.exportId = ExportId(read.number);
                LatestExportState = new SignalDebtProofExport
                {
                    id = read.exportId, actorId = OwnerId, deviceId = DeviceId, documentId = DocumentId,
                    revisionId = currentRevisionId, ownerReadNumber = read.number, content = revision.content.Copy(),
                    deviceConfigurationRevision = device.configurationRevision
                };
                successfulExportCount++;
                message = "Southbank opened " + currentRevisionId + ", compared its date, contact and footer with the approval, and created export " + read.exportId + ".";
            }
            else message = "Southbank could not export " + currentRevisionId + ". " + Describe(result, revision.content);
            LastReadState = read; ownerCheckCount++;
            return true;
        }

        // Campaign settlement owns the once-only 160-credit ledger transaction.
        public bool TrySettleRecoveryAccess(SignalDebtClientDevice device, out string message)
        {
            if (!ValidCommand(device, out message)) return false;
            if (accessGrant != SignalDebtProofGrant.Recover || !CurrentOwnerVerified(device))
            { message = "Settlement requires Southbank's export of the current proof on the current workstation state."; return false; }
            accessGrant = SignalDebtProofGrant.ReadOnlyAfterSettlement;
            ClearQueue(); message = "Proof recovery is complete. The proof, preserved drafts and exported version remain read-only.";
            return true;
        }

        private static SignalDebtProofContent OriginalContent(int number)
        {
            return new SignalDebtProofContent
            {
                title = "Southbank Community Swap", eventDate = number == 1 ? "18 October, 10:00–14:00" : "19 October, 10:00–14:00",
                contact = number == 3 ? "" : "Ask at the Southbank counter",
                footer = number == 3 ? "" : "Free entry • Bring one item to exchange"
            };
        }

        private SignalDebtProofRevision FindRevision(string id)
        { return id == null ? null : revisions.Find(r => r.id == id); }
        private static string ExportId(int readNumber) { return DocumentId + ":export-" + readNumber; }

        private SignalDebtProofResult Compare(SignalDebtProofContent content)
        {
            if (content.title != approval.content.title) return SignalDebtProofResult.TitleMismatch;
            if (content.eventDate != approval.content.eventDate) return SignalDebtProofResult.DateMismatch;
            if (string.IsNullOrEmpty(content.contact)) return SignalDebtProofResult.ContactMissing;
            if (content.contact != approval.content.contact) return SignalDebtProofResult.ContactMismatch;
            if (string.IsNullOrEmpty(content.footer)) return SignalDebtProofResult.FooterMissing;
            if (content.footer != approval.content.footer) return SignalDebtProofResult.FooterMismatch;
            return SignalDebtProofResult.ApprovedMatch;
        }

        private bool ValidCommand(SignalDebtClientDevice device, out string message)
        {
            message = "";
            if (Validate(device)) return true;
            message = "The proof or workstation state could not be verified. No changes were made."; return false;
        }

        private SignalDebtProofResult ReadAccess(SignalDebtClientDevice device, bool requireSession)
        {
            if (!Validate(device)) return SignalDebtProofResult.InvalidState;
            if (accessGrant == SignalDebtProofGrant.None) return SignalDebtProofResult.PermissionRequired;
            if (!device.powered) return SignalDebtProofResult.PoweredOff;
            if (requireSession && !device.sessionOpen) return SignalDebtProofResult.SessionClosed;
            return SignalDebtProofResult.ApprovedMatch;
        }

        private bool CanEdit(SignalDebtClientDevice device, out string message)
        {
            if (!ValidCommand(device, out message)) return false;
            SignalDebtProofResult access = ReadAccess(device, true);
            if (access != SignalDebtProofResult.ApprovedMatch) { message = Describe(access, null); return false; }
            if (accessGrant != SignalDebtProofGrant.Recover)
            { message = "This settled proof is read-only. Its permission does not reopen Order Folder repair."; return false; }
            if (revisions.Count == int.MaxValue)
            { message = "Another document revision cannot be numbered. No original or draft was deleted."; return false; }
            return true;
        }

        private void AppendRevision(SignalDebtProofRevision source, SignalDebtProofChange change)
        {
            int next = revisions.Count + 1;
            revisions.Add(new SignalDebtProofRevision
            {
                id = RevisionId(next), number = next, previousRevisionId = currentRevisionId,
                sourceRevisionId = source.id, createdBy = "player", change = change, content = source.content.Copy()
            });
            currentRevisionId = RevisionId(next); ClearQueue();
        }

        private void ClearQueue()
        { ownerCheckQueued = false; queuedRevisionId = ""; queuedDeviceConfigurationRevision = -1; queuedPowered = false; }

        private SignalDebtProofRead FailedRead(SignalDebtProofResult result)
        { return new SignalDebtProofRead(result, Describe(result, null), null, null); }

        private string Describe(SignalDebtProofResult result, SignalDebtProofContent content)
        {
            switch (result)
            {
                case SignalDebtProofResult.ApprovedMatch: return "The title, date, contact and footer match the recorded approval.";
                case SignalDebtProofResult.DateMismatch: return "The proof says '" + content.eventDate + "'; the approval requires '" + approval.content.eventDate + "'.";
                case SignalDebtProofResult.ContactMissing: return "The contact line is missing. The approval requires '" + approval.content.contact + "'."
                    + (string.IsNullOrEmpty(content.footer) ? " The approved footer is also missing." : "");
                case SignalDebtProofResult.ContactMismatch: return "The contact line differs from the recorded approval.";
                case SignalDebtProofResult.FooterMissing: return "The footer is missing. The approval requires '" + approval.content.footer + "'.";
                case SignalDebtProofResult.FooterMismatch: return "The footer differs from the recorded approval.";
                case SignalDebtProofResult.TitleMismatch: return "The title differs from the recorded approval.";
                case SignalDebtProofResult.PoweredOff: return "Southbank's workstation is powered off; no live proof can be read or exported.";
                case SignalDebtProofResult.PermissionRequired: return "This document needs a separate proof-recovery grant. Order Folder permission does not cover it.";
                case SignalDebtProofResult.SessionClosed: return "Open Southbank's authorized workstation session to inspect the proof.";
                case SignalDebtProofResult.UnknownRevision: return "That revision is not in this proof's history.";
                default: return "The proof or workstation state could not be verified.";
            }
        }
    }
}
