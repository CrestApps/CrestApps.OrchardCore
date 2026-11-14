namespace CrestApps.OrchardCore.Invoicing;

/// <summary>
/// The shared constant values of the Invoicing modules.
/// </summary>
public static class InvoicingConstants
{
    /// <summary>
    /// The YesSql collection the invoicing documents are stored in.
    /// </summary>
    public const string CollectionName = "Invoicing";

    /// <summary>
    /// The value of <c>Transaction.ReferenceType</c> on the transaction an issued invoice posts.
    /// </summary>
    public const string InvoiceReferenceType = "Invoice";

    /// <summary>
    /// The value of a timeline entry's document kind for an adjustment a credit note posts.
    /// </summary>
    public const string CreditNoteDocumentKind = "CreditNote";

    /// <summary>
    /// The reference type of an invoice history entry copied from an entry of the invoice's transaction; its reference
    /// id is the transaction entry's id.
    /// </summary>
    public const string TransactionEventReferenceType = "TransactionEvent";

    /// <summary>
    /// The identifier of the site settings group of the invoicing settings.
    /// </summary>
    public const string SettingsGroupId = "invoicing";

    /// <summary>
    /// The display types an invoice is built for.
    /// </summary>
    public static class DisplayTypes
    {
        /// <summary>
        /// The invoice's page in the admin. Each display driver adds a card: the <c>Content</c> zone is the main column
        /// and the <c>Actions</c> zone the side column.
        /// </summary>
        public const string DetailAdmin = "DetailAdmin";
    }

    /// <summary>
    /// The feature identifiers of the Invoicing modules.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Invoices and credit notes: drafts, issue, void, payments, delivery and the customer pages.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Invoicing";

        /// <summary>
        /// Estimates and their conversion to invoices.
        /// </summary>
        public const string Estimates = "CrestApps.OrchardCore.Invoicing.Estimates";

        /// <summary>
        /// Recurring invoice plans and the task that generates their invoices.
        /// </summary>
        public const string Recurring = "CrestApps.OrchardCore.Invoicing.Recurring";

        /// <summary>
        /// Late fees, debit notes and statements.
        /// </summary>
        public const string Collections = "CrestApps.OrchardCore.Invoicing.Collections";

        /// <summary>
        /// Billable items from time, milestones and usage, progress invoicing and approvals.
        /// </summary>
        public const string Billing = "CrestApps.OrchardCore.Invoicing.Billing";

        /// <summary>
        /// The invoicing endpoints of the commerce API.
        /// </summary>
        public const string Api = "CrestApps.OrchardCore.Invoicing.Api";
    }
}
