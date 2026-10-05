using System;
using System.Collections.Generic;

namespace SignalDebt
{
    // One physical unit for the first shop transaction. Its IDs survive buying,
    // carrying and future placement; stock/ownership derive from one receipt.
    public static class SignalDebtShopCatalog
    {
        public const string SellerId = "jon_parts_clerk";
        public const string StockId = "jon-used-gpu-001";
        public const string ItemId = "physical-used-gpu-001";
        public const string ProductId = "aster-display-s8";
        public const string ProductName = "Aster S8 graphics card";
        public const string PurchaseEventId = "purchase:jon-used-gpu-001";
        public const int Price = 90;
    }

    // A quote is transient. A later payment, day change or sale makes it stale;
    // the player must inspect the current offer before confirming again.
    public sealed class SignalDebtShopQuote
    {
        public readonly string saveId, stockId, itemId;
        public readonly int revision, price;

        public SignalDebtShopQuote(string campaign, int worldRevision, string stock, string item, int amount)
        { saveId = campaign; revision = worldRevision; stockId = stock; itemId = item; price = amount; }
    }

    [Serializable]
    public sealed class SignalDebtPurchase
    {
        public string receiptId, itemId, stockId, sellerId, productId, ledgerEventId;
        public int paidAmount, day, worldRevision;

        public SignalDebtPurchase Copy() { return (SignalDebtPurchase)MemberwiseClone(); }
    }

    public sealed partial class SignalDebtSaveData
    {
        // JsonUtility expands a null inline class into an empty object. A list
        // represents absence faithfully and gives stock one serialized source.
        public List<SignalDebtPurchase> shopPurchases = new List<SignalDebtPurchase>();

        public SignalDebtPurchase usedGpuPurchase
        {
            get { return shopPurchases != null && shopPurchases.Count == 1 ? shopPurchases[0] : null; }
            set { shopPurchases = value == null ? new List<SignalDebtPurchase>() : new List<SignalDebtPurchase> { value }; }
        }

        public bool UsedGpuInStock { get { return shopPurchases != null && shopPurchases.Count == 0; } }
        public bool OwnsUsedGpu { get { return usedGpuPurchase != null; } }

        public SignalDebtShopQuote InspectUsedGpu()
        {
            if (!Validate() || !UsedGpuInStock) return null;
            return new SignalDebtShopQuote(saveId, revision, SignalDebtShopCatalog.StockId,
                SignalDebtShopCatalog.ItemId, SignalDebtShopCatalog.Price);
        }

        // Build the whole next snapshot without touching the live campaign. The
        // caller must save successfully before publishing it or handing over art.
        public bool TryCreateUsedGpuPurchase(SignalDebtShopQuote quote,
            out SignalDebtSaveData candidate, out SignalDebtLedgerEvent entry, out string reason)
        {
            candidate = null; entry = null; reason = "";
            if (!Validate()) { reason = "This purchase could not be verified. Reload your saved game."; return false; }
            if (!UsedGpuInStock) { reason = "That card has already been sold."; return false; }
            if (quote == null) { reason = "Inspect the card before confirming the purchase."; return false; }
            if (quote.saveId != saveId || quote.revision != revision || quote.stockId != SignalDebtShopCatalog.StockId
                || quote.itemId != SignalDebtShopCatalog.ItemId || quote.price != SignalDebtShopCatalog.Price)
            { reason = "The offer changed. Inspect the card again."; return false; }
            if (balance < quote.price) { reason = "You need " + quote.price + " credits for this card."; return false; }
            if (revision == int.MaxValue) { reason = "This purchase could not be recorded."; return false; }

            candidate = Copy();
            entry = candidate.Record("shop_purchase", "Bought a used " + SignalDebtShopCatalog.ProductName
                + " from Jon for " + quote.price + " credits.", -quote.price, SignalDebtShopCatalog.PurchaseEventId);
            candidate.balance -= quote.price;
            candidate.usedGpuPurchase = new SignalDebtPurchase
            {
                receiptId = Guid.NewGuid().ToString("N"), itemId = SignalDebtShopCatalog.ItemId,
                stockId = SignalDebtShopCatalog.StockId, sellerId = SignalDebtShopCatalog.SellerId,
                productId = SignalDebtShopCatalog.ProductId, ledgerEventId = entry.id,
                paidAmount = quote.price, day = day, worldRevision = entry.worldRevision
            };
            return true;
        }

        public bool ConfirmUsedGpuPurchase(SignalDebtShopQuote quote, Func<SignalDebtSaveData, bool> persist,
            out SignalDebtLedgerEvent committed, out string reason)
        {
            committed = null;
            if (persist == null) { reason = "The purchase cannot be saved right now."; return false; }
            if (!TryCreateUsedGpuPurchase(quote, out SignalDebtSaveData candidate,
                out SignalDebtLedgerEvent sale, out reason)) return false;
            if (!persist(candidate))
            { reason = "The purchase was not saved. Your money and the card have not changed."; return false; }
            PublishSavedCopy(candidate);
            committed = sale;
            return true;
        }

        private bool ValidatePurchase()
        {
            if (shopPurchases == null || shopPurchases.Count > 1
                || (shopPurchases.Count == 1 && shopPurchases[0] == null)) return false;
            SignalDebtLedgerEvent sale = null;
            foreach (SignalDebtLedgerEvent item in ledger)
            {
                if (item.type != "shop_purchase" && item.id != SignalDebtShopCatalog.PurchaseEventId) continue;
                if (sale != null) return false;
                sale = item;
            }
            if (usedGpuPurchase == null) return sale == null;
            SignalDebtPurchase receipt = usedGpuPurchase;
            // Prices are historical facts. Future catalog repricing must not
            // invalidate a receipt; agreement with its original ledger is required.
            return sale != null && sale.type == "shop_purchase" && sale.id == SignalDebtShopCatalog.PurchaseEventId
                && !string.IsNullOrEmpty(receipt.receiptId) && receipt.itemId == SignalDebtShopCatalog.ItemId
                && receipt.stockId == SignalDebtShopCatalog.StockId && receipt.sellerId == SignalDebtShopCatalog.SellerId
                && receipt.productId == SignalDebtShopCatalog.ProductId && receipt.ledgerEventId == sale.id
                && receipt.paidAmount > 0 && sale.amount == -receipt.paidAmount
                && receipt.day >= 1 && receipt.day <= day && receipt.day == sale.day
                && receipt.worldRevision == sale.worldRevision;
        }

        public SignalDebtSaveData Copy()
        {
            var copy = (SignalDebtSaveData)MemberwiseClone();
            copy.clientDevice = clientDevice == null ? null : clientDevice.Copy();
            copy.proofDocument = proofDocument == null ? null : proofDocument.Copy();
            copy.clientMemories = clientMemories == null ? null : new List<string>(clientMemories);
            copy.shopPurchases = shopPurchases == null ? null
                : shopPurchases.ConvertAll(item => item == null ? null : item.Copy());
            copy.ledger = ledger == null ? null : ledger.ConvertAll(item => item == null ? null : new SignalDebtLedgerEvent
            {
                id = item.id, type = item.type, summary = item.summary, amount = item.amount,
                day = item.day, worldRevision = item.worldRevision, utc = item.utc,
                actorId = item.actorId, deviceId = item.deviceId, recordId = item.recordId, deviceRevision = item.deviceRevision,
                documentRevisionId = item.documentRevisionId, exportId = item.exportId, ownerCheckNumber = item.ownerCheckNumber
            });
            return copy;
        }

        public SignalDebtSaveData MigrateV1()
        {
            if (schemaVersion != 1 || (shopPurchases != null && shopPurchases.Count != 0)) return null;
            SignalDebtSaveData migrated = Copy();
            // Only the explicit legacy migration supplies a formerly absent
            // field; validation of current snapshots never repairs bad data.
            migrated.shopPurchases = new List<SignalDebtPurchase>();
            migrated.clientDevice = SignalDebtClientDevice.CreateFromLegacy(jobAccepted, serviceInspected, serviceConnected, serviceTestPassed, jobPaid);
            InitializeMigratedProof(migrated);
            migrated.schemaVersion = CurrentSchemaVersion;
            return migrated.Validate() ? migrated : null;
        }

        public static SignalDebtSaveData MigrateV2(SignalDebtSaveData legacy)
        {
            if (legacy == null || legacy.schemaVersion != 2 || legacy.shopPurchases == null) return null;
            SignalDebtSaveData migrated = legacy.Copy();
            migrated.clientDevice = SignalDebtClientDevice.CreateFromLegacy(legacy.jobAccepted, legacy.serviceInspected,
                legacy.serviceConnected, legacy.serviceTestPassed, legacy.jobPaid);
            InitializeMigratedProof(migrated);
            migrated.schemaVersion = CurrentSchemaVersion;
            return migrated.Validate() ? migrated : null;
        }

        public static SignalDebtSaveData MigrateV3(SignalDebtSaveData legacy)
        {
            if (legacy == null || legacy.schemaVersion != 3 || legacy.clientDevice == null) return null;
            SignalDebtSaveData migrated = legacy.Copy();
            // A schema-three device already records its actual connection,
            // queue, observations and order versions. Never rebuild it from
            // historical repair flags during this migration.
            InitializeMigratedProof(migrated);
            migrated.schemaVersion = CurrentSchemaVersion;
            return migrated.Validate() ? migrated : null;
        }

        private static void InitializeMigratedProof(SignalDebtSaveData migrated)
        {
            migrated.proofAccepted = false;
            migrated.proofPaid = false;
            migrated.proofDocument = SignalDebtProofDocument.CreateInitial();
        }
    }
}
