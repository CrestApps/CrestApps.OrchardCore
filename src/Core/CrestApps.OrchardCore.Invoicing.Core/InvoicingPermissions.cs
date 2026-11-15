using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Invoicing.Core;

/// <summary>
/// The permissions of the Invoicing feature. Issuing, voiding, crediting and recording payments change what customers owe,
/// so they are security critical. Each of them lets a user see invoices too (<see cref="ViewInvoices"/> is implied by
/// them), and none implies another.
/// </summary>
public static class InvoicingPermissions
{
    /// <summary>
    /// Create invoices and change and delete drafts.
    /// </summary>
    public static readonly Permission ManageInvoices = new(
        "ManageInvoices",
        LocalizationSource.Create("Create invoices and change drafts", typeof(InvoicingPermissions)));

    /// <summary>
    /// Issue drafts, which numbers them and posts what is owed.
    /// </summary>
    public static readonly Permission IssueInvoices = new(
        "IssueInvoices",
        LocalizationSource.Create("Issue invoices", typeof(InvoicingPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Void, write off and reinstate issued invoices.
    /// </summary>
    public static readonly Permission VoidInvoices = new(
        "VoidInvoices",
        LocalizationSource.Create("Void and write off invoices", typeof(InvoicingPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Issue credit notes.
    /// </summary>
    public static readonly Permission ManageCreditNotes = new(
        "ManageCreditNotes",
        LocalizationSource.Create("Issue credit notes", typeof(InvoicingPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Record payments received outside the site against invoices.
    /// </summary>
    public static readonly Permission RecordInvoicePayments = new(
        "RecordInvoicePayments",
        LocalizationSource.Create("Record invoice payments", typeof(InvoicingPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Create, send, revise and cancel estimates, and turn them into draft invoices.
    /// </summary>
    public static readonly Permission ManageEstimates = new(
        "ManageEstimates",
        LocalizationSource.Create("Manage estimates", typeof(InvoicingPermissions)));

    /// <summary>
    /// Create, change, start, pause and cancel recurring plans, which make and issue invoices on their schedule.
    /// </summary>
    public static readonly Permission ManageRecurringInvoices = new(
        "ManageRecurringInvoices",
        LocalizationSource.Create("Manage recurring invoices", typeof(InvoicingPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Change the invoicing settings.
    /// </summary>
    public static readonly Permission ManageInvoicingSettings = new(
        "ManageInvoicingSettings",
        LocalizationSource.Create("Manage invoicing settings", typeof(InvoicingPermissions)));

    /// <summary>
    /// See every invoice and credit note. Implied by every permission that changes them.
    /// </summary>
    public static readonly Permission ViewInvoices = new(
        "ViewInvoices",
        LocalizationSource.Create("View invoices", typeof(InvoicingPermissions)),
        [ManageInvoices, IssueInvoices, VoidInvoices, ManageCreditNotes, RecordInvoicePayments, ManageEstimates, ManageRecurringInvoices]);

    /// <summary>
    /// See the invoices billed to the signed-in user and to the parties they have billing access to.
    /// </summary>
    public static readonly Permission ViewOwnInvoices = new(
        "ViewOwnInvoices",
        LocalizationSource.Create("View own invoices", typeof(InvoicingPermissions)));

    /// <summary>
    /// Every permission, in the order they are listed.
    /// </summary>
    public static IReadOnlyList<Permission> All { get; } =
    [
        ViewInvoices,
        ManageInvoices,
        IssueInvoices,
        VoidInvoices,
        ManageCreditNotes,
        RecordInvoicePayments,
        ManageEstimates,
        ManageRecurringInvoices,
        ManageInvoicingSettings,
        ViewOwnInvoices,
    ];
}
