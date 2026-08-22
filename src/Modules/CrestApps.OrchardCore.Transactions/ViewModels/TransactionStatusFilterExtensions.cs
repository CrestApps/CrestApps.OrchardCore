using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// Helpers that translate a <see cref="TransactionStatusFilter"/> into a query filter and the toolbar
/// items rendered by both the administration report and the customer statement.
/// </summary>
public static class TransactionStatusFilterExtensions
{
    /// <summary>
    /// Applies the selected status filter to a transaction query.
    /// </summary>
    /// <param name="status">The selected status filter.</param>
    /// <param name="query">The query to configure.</param>
    public static void ApplyTo(this TransactionStatusFilter status, TransactionQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        switch (status)
        {
            case TransactionStatusFilter.All:
                break;
            case TransactionStatusFilter.Outstanding:
                query.OutstandingOnly = true;
                break;
            case TransactionStatusFilter.Pending:
                query.Status = TransactionStatus.Pending;
                break;
            case TransactionStatusFilter.PartiallyPaid:
                query.Status = TransactionStatus.PartiallyPaid;
                break;
            case TransactionStatusFilter.Paid:
                query.Status = TransactionStatus.Paid;
                break;
            case TransactionStatusFilter.Canceled:
                query.Status = TransactionStatus.Canceled;
                break;
            case TransactionStatusFilter.Failed:
                query.Status = TransactionStatus.Failed;
                break;
            case TransactionStatusFilter.Abandoned:
                query.Status = TransactionStatus.Abandoned;
                break;
            case TransactionStatusFilter.Refunded:
                query.Status = TransactionStatus.Refunded;
                break;
        }
    }

    /// <summary>
    /// Builds the status filter items rendered in a transactions toolbar.
    /// </summary>
    /// <param name="selected">The selected status filter.</param>
    /// <param name="S">The string localizer used to localize the item text.</param>
    /// <returns>The status filter items.</returns>
    public static List<SelectListItem> BuildFilterItems(TransactionStatusFilter selected, IStringLocalizer S)
    {
        return
        [
            new SelectListItem(S["All"], nameof(TransactionStatusFilter.All), selected == TransactionStatusFilter.All),
            new SelectListItem(S["Outstanding"], nameof(TransactionStatusFilter.Outstanding), selected == TransactionStatusFilter.Outstanding),
            new SelectListItem(S["Pending"], nameof(TransactionStatusFilter.Pending), selected == TransactionStatusFilter.Pending),
            new SelectListItem(S["Partially paid"], nameof(TransactionStatusFilter.PartiallyPaid), selected == TransactionStatusFilter.PartiallyPaid),
            new SelectListItem(S["Paid"], nameof(TransactionStatusFilter.Paid), selected == TransactionStatusFilter.Paid),
            new SelectListItem(S["Canceled"], nameof(TransactionStatusFilter.Canceled), selected == TransactionStatusFilter.Canceled),
            new SelectListItem(S["Failed"], nameof(TransactionStatusFilter.Failed), selected == TransactionStatusFilter.Failed),
            new SelectListItem(S["Abandoned"], nameof(TransactionStatusFilter.Abandoned), selected == TransactionStatusFilter.Abandoned),
            new SelectListItem(S["Refunded"], nameof(TransactionStatusFilter.Refunded), selected == TransactionStatusFilter.Refunded),
        ];
    }
}
