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
    /// The source type of an invoice made from an estimate, and the document kind of an estimate.
    /// </summary>
    public const string EstimateReferenceType = "Estimate";

    /// <summary>
    /// The source type of an invoice a recurring plan made.
    /// </summary>
    public const string RecurringPlanReferenceType = "RecurringPlan";

    /// <summary>
    /// The number series of estimates.
    /// </summary>
    public const string EstimateNumberSeries = "Invoicing.Estimate";

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
    /// The kinds of business document invoicing prints, each with its own document template.
    /// </summary>
    public static class BusinessDocumentKinds
    {
        /// <summary>
        /// An invoice.
        /// </summary>
        public const string Invoice = "Invoice";

        /// <summary>
        /// A credit note.
        /// </summary>
        public const string CreditNote = "CreditNote";

        /// <summary>
        /// An estimate.
        /// </summary>
        public const string Estimate = "Estimate";
    }

    /// <summary>
    /// The commerce e-mail kinds invoicing sends, each with a template a site can change.
    /// </summary>
    public static class EmailKinds
    {
        /// <summary>
        /// An invoice sent to its customer.
        /// </summary>
        public const string InvoiceIssued = "invoice-issued";

        /// <summary>
        /// A credit note sent to its customer.
        /// </summary>
        public const string CreditNoteIssued = "credit-note-issued";

        /// <summary>
        /// An estimate sent to its customer, with its PDF and a link to accept or decline it.
        /// </summary>
        public const string EstimateSent = "estimate-sent";

        /// <summary>
        /// The name the invoice or credit note has in an e-mail template.
        /// </summary>
        public const string DocumentModelName = "Document";

        /// <summary>
        /// The document's number.
        /// </summary>
        public const string NumberValueName = "Number";

        /// <summary>
        /// The document's total, with its currency.
        /// </summary>
        public const string TotalValueName = "Total";

        /// <summary>
        /// What is still due on an invoice, with its currency.
        /// </summary>
        public const string AmountDueValueName = "AmountDue";

        /// <summary>
        /// The day an invoice is due.
        /// </summary>
        public const string DueDateValueName = "DueDate";

        /// <summary>
        /// The link that pays an invoice.
        /// </summary>
        public const string PayUrlValueName = "PayUrl";

        /// <summary>
        /// The link that shows the document without signing in.
        /// </summary>
        public const string ViewUrlValueName = "ViewUrl";

        /// <summary>
        /// The name of who the document is for.
        /// </summary>
        public const string CustomerNameValueName = "CustomerName";

        /// <summary>
        /// The store's name.
        /// </summary>
        public const string StoreNameValueName = "StoreName";
    }

    /// <summary>
    /// The names of the routes other code links to.
    /// </summary>
    public static class RouteNames
    {
        /// <summary>
        /// The public page a signed share link opens: the invoice, without signing in.
        /// </summary>
        public const string View = "InvoicingView";

        /// <summary>
        /// The PDF of the invoice a signed share link opens.
        /// </summary>
        public const string ViewPdf = "InvoicingViewPdf";

        /// <summary>
        /// The signed-in customer's invoices, in their account.
        /// </summary>
        public const string MyInvoices = "InvoicingMyInvoices";

        /// <summary>
        /// The public page of an estimate, by signed link, where the customer accepts or declines it.
        /// </summary>
        public const string EstimateView = "InvoicingEstimateView";
    }

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
    /// The outbound webhook events Invoicing publishes when the Commerce Webhooks feature is on.
    /// </summary>
    public static class WebhookEvents
    {
        /// <summary>
        /// A draft invoice was created.
        /// </summary>
        public const string InvoiceCreated = "invoice.created";

        /// <summary>
        /// An invoice was issued with its number.
        /// </summary>
        public const string InvoiceIssued = "invoice.issued";

        /// <summary>
        /// An invoice was sent to its customer.
        /// </summary>
        public const string InvoiceSent = "invoice.sent";

        /// <summary>
        /// The customer opened an invoice's page for the first time.
        /// </summary>
        public const string InvoiceViewed = "invoice.viewed";

        /// <summary>
        /// A payment toward an invoice reached it.
        /// </summary>
        public const string InvoicePaymentReceived = "invoice.payment-received";

        /// <summary>
        /// An invoice was paid in full.
        /// </summary>
        public const string InvoicePaid = "invoice.paid";

        /// <summary>
        /// An open invoice was found unpaid after its due date, for the first time.
        /// </summary>
        public const string InvoiceOverdue = "invoice.overdue";

        /// <summary>
        /// An invoice was voided.
        /// </summary>
        public const string InvoiceVoided = "invoice.voided";

        /// <summary>
        /// A credit note for an invoice was issued.
        /// </summary>
        public const string CreditNoteIssued = "credit-note.issued";

        /// <summary>
        /// An estimate was sent.
        /// </summary>
        public const string EstimateSent = "estimate.sent";

        /// <summary>
        /// The customer accepted an estimate.
        /// </summary>
        public const string EstimateAccepted = "estimate.accepted";

        /// <summary>
        /// The customer declined an estimate.
        /// </summary>
        public const string EstimateDeclined = "estimate.declined";

        /// <summary>
        /// An estimate expired.
        /// </summary>
        public const string EstimateExpired = "estimate.expired";

        /// <summary>
        /// An estimate was turned into an invoice.
        /// </summary>
        public const string EstimateConverted = "estimate.converted";

        /// <summary>
        /// A recurring plan made an invoice.
        /// </summary>
        public const string RecurringInvoiceGenerated = "recurring-plan.invoice-generated";

        /// <summary>
        /// A run of a recurring plan failed.
        /// </summary>
        public const string RecurringRunFailed = "recurring-plan.run-failed";
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
