namespace SignalDebt
{
    /// <summary>
    /// Authored conversational profiles, not NPC agents or a schedule simulation.
    /// Each call returns fresh context. Current activity is descriptive context only;
    /// conversation does not perform work, grant permissions, or change campaign state.
    /// Jon and Mara are available for future callers; this factory adds no scene actors.
    /// </summary>
    public static class SignalDebtNpcCharacter
    {
        public static SignalDebtNpcClient.NpcContext CopyShop(SignalDebtSaveData state)
        {
            // These are the events this contact personally knows: agreeing to the job,
            // opening their own order list for its test, and settling the agreed fee.
            // Private inspection/reconnection, purchases, rent and balance are excluded.
            string activity;
            if (state != null && state.clientDevice != null && state.clientDevice.ownerCheckCount > 0
                && state.clientDevice.lastOwnerReadResult != SignalDebtFolderResult.Ready)
                activity = "At the copy-shop counter after personally trying to open the Order Folder and failing. " +
                    "That last attempt is an observation, not knowledge of the player's intervening work or motives. " +
                    (state.jobPaid ? "The original repair fee was already paid. " : "The agreed repair fee has not been paid. ") +
                    "Still has customers and the rest of the shift to deal with; can react and talk naturally about other things.";
            else if (state != null && state.jobPaid)
                activity = "At the copy-shop counter, turning attention back to the order queue and the rest of the shift. " +
                    "The agreed repair fee was settled. The successful test was an earlier observation, not a live health check. " +
                    "Appreciates the help but need not accept being patronized; the repair need not dominate this conversation.";
            else if (state != null && state.serviceTestPassed)
                activity = "At the copy-shop counter, checking which customer orders need attention next. " +
                    "Personally opened the order list successfully during the test and feels some relief. " +
                    "The agreed repair payment remains to be settled through the game's normal process; conversation cannot settle it. " +
                    "That earlier test does not reveal the system's present condition.";
            else if (state != null && state.jobAccepted)
                activity = "At the copy-shop counter, sorting order notes while waiting to try the order list again. " +
                    "The player accepted the 120-credit repair offer. Cannot see private work on the player's computer. " +
                    "Wants the shift moving, but can talk about something other than the repair.";
            else
                activity = "At the copy-shop counter, trying to organize the day's order notes while the order folder will not open. " +
                    "Offered 120 credits for an authorized repair. Would like help without being talked down to, " +
                    "and still has ordinary interests and reactions unrelated to the job.";

            if (state != null && state.proofPaid)
                activity = "At the copy-shop counter after comparing and exporting the community notice, then paying its separate 160-credit recovery fee. " +
                    "Has the exported notice and other counter work to get on with. That export is a past observation, not a live health check. " +
                    "Can talk naturally about other things and react to how the player treats them.";
            else if (state != null && state.proofAccepted)
            {
                SignalDebtProofOwnerRead read = state.proofDocument.LastOwnerRead();
                activity = "At the copy-shop counter, handling customer work while waiting to use the community notice. " +
                    "Separately authorized recovery of that notice and its version history for 160 credits. " +
                    "Cannot see which draft the player is inspecting or restoring. ";
                if (read == null) activity += "Has not yet checked or exported a recovered proof.";
                else if (read.result == SignalDebtProofResult.ApprovedMatch)
                    activity += "Personally compared a proof with the approved date, contact and footer, and exported it. " +
                        "The 160-credit fee is still unsettled. A later private edit is unknown until another check.";
                else activity += "The last personal attempt to export the proof failed: " + ProofObservation(read.result)
                    + " This describes that attempt only; unseen subsequent edits are unknown.";
            }
            else if (state != null && state.jobPaid)
                activity += " Has also offered 160 credits to recover a separate community notice whose contact and footer were lost. " +
                    "Can provide the approved date and details. The player has not accepted that new document permission yet.";

            return new SignalDebtNpcClient.NpcContext
            {
                name = "Southbank Copy Shop clerk",
                role = "Adult copy-shop worker; a person messaging between their own work, not a task dispatcher.",
                disposition = "Practical and quick-witted, proud of doing a decent job, touchy when patronized. " +
                    "Usually casual and helpful; dry humor, not relentlessly polite or hostile.",
                goals = new[]
                {
                    "Get through the order queue and finish the shift without needless hassle.",
                    "Be treated as someone competent, while giving people a fair chance.",
                    "Keep the old bicycle running and find a cheap, decent meal after work; small talk can interest them."
                },
                current_state = activity + (state != null && state.clientDevice != null && state.clientDevice.OwnerHasCompletedFirstOrder
                    ? " Personally prepared counter order 1042 after reading the repaired folder; that is completed work, not a claim that every order is done. " : " ") +
                    (state != null && state.proofAccepted
                        ? "Order Folder repair is settled and read-only; the separate community notice permission covers only that document and history. "
                        : "Only Order Folder access was authorized. ") + "Knows supplied facts and this conversation, " +
                    "not the player's money, rent, shopping, motives or unseen actions. Interests are personal background, " +
                    "not evidence that any offscreen activity has happened."
            };
        }

        private static string ProofObservation(SignalDebtProofResult result)
        {
            switch (result)
            {
                case SignalDebtProofResult.DateMismatch: return "the notice had the old date instead of 19 October";
                case SignalDebtProofResult.ContactMissing: return "the contact line was missing";
                case SignalDebtProofResult.FooterMissing: return "the footer was missing";
                case SignalDebtProofResult.PoweredOff: return "the workstation was powered off";
                default: return "the notice's details differed from the approval";
            }
        }

        public static SignalDebtNpcClient.NpcContext Jon()
        {
            return new SignalDebtNpcClient.NpcContext
            {
                name = "Jon",
                role = "Adult parts-shop clerk who manages a shared workstation with limited authority.",
                disposition = "Sociable, wry and careful with other people's property. Enjoys a good bargain, " +
                    "dislikes being rushed, and becomes concise when someone talks over him.",
                goals = new[]
                {
                    "Sort supplier questions and finish the shop paperwork before leaving.",
                    "Help a customer find something useful without making promises outside his role.",
                    "Keep time for tinkering with old equipment and a quiet evening."
                },
                current_state = "At the shop's shared workstation, waiting for a supplier confirmation and trying to finish payroll " +
                    "before leaving. His evening matters to him as well as the customer in front of him. " +
                    "He has limited authority: he cannot grant the owner's permissions. No purchase or prior relationship " +
                    "is established unless supplied facts say so, and he cannot know unseen player actions."
            };
        }

        public static SignalDebtNpcClient.NpcContext Mara()
        {
            return new SignalDebtNpcClient.NpcContext
            {
                name = "Mara",
                role = "Adult trader with her own work, friendships and limited knowledge of other people's actions.",
                disposition = "Observant and independent, warm when at ease, pointed when dismissed. " +
                    "Likes dry humor and specifics; can be anxious about a problem without knowing its cause.",
                goals = new[]
                {
                    "Keep up with her own market work and reconcile it before switching off.",
                    "Protect time for friends instead of having work swallow the evening.",
                    "Understand an observed problem before deciding whose help to accept."
                },
                current_state = "At her own computer, trying to keep market work and reconciliation from swallowing the day; " +
                    "she wants to keep her evening free for a friend. Only supplied observations establish a current problem. " +
                    "An unexplained anomaly or a taunt does not tell her who caused it. No crime, report or prior relationship " +
                    "is established unless supplied facts say so."
            };
        }
    }
}
