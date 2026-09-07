using CrestApps.OrchardCore.Checkout.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Transactions.ViewModels;

/// <summary>
/// The filter applied to the administration payments ledger.
/// </summary>
public sealed class PaymentsAdminIndexOptions
{
    /// <summary>
    /// Gets or sets the provider key to filter by.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the attempt state to filter by, where an empty value means every state.
    /// </summary>
    public PaymentAttemptState? State { get; set; }

    /// <summary>
    /// Gets or sets the checkout session to filter by.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the state filter list.
    /// </summary>
    public IList<SelectListItem> States { get; set; } = [];

    /// <summary>
    /// Gets or sets the provider filter list.
    /// </summary>
    public IList<SelectListItem> Providers { get; set; } = [];
}

/// <summary>
/// The administration payments ledger.
/// </summary>
public sealed class PaymentsAdminIndexViewModel
{
    /// <summary>
    /// Gets or sets the attempts on this page.
    /// </summary>
    public IList<PaymentAttempt> Attempts { get; set; } = [];

    /// <summary>
    /// Gets or sets the filter options.
    /// </summary>
    public PaymentsAdminIndexOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the current user may issue refunds.
    /// </summary>
    public bool CanRefund { get; set; }
}

/// <summary>
/// The filter applied to the administration refunds ledger.
/// </summary>
public sealed class RefundsAdminIndexOptions
{
    /// <summary>
    /// Gets or sets the provider key to filter by.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the refund status to filter by, where an empty value means every status.
    /// </summary>
    public RefundStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the checkout session to filter by.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the status filter list.
    /// </summary>
    public IList<SelectListItem> Statuses { get; set; } = [];

    /// <summary>
    /// Gets or sets the provider filter list.
    /// </summary>
    public IList<SelectListItem> Providers { get; set; } = [];
}

/// <summary>
/// The administration refunds ledger.
/// </summary>
public sealed class RefundsAdminIndexViewModel
{
    /// <summary>
    /// Gets or sets the refunds on this page.
    /// </summary>
    public IList<PaymentRefund> Refunds { get; set; } = [];

    /// <summary>
    /// Gets or sets the filter options.
    /// </summary>
    public RefundsAdminIndexOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }
}

/// <summary>
/// The form used to refund a settled payment attempt.
/// </summary>
public sealed class RefundRequestViewModel
{
    /// <summary>
    /// Gets or sets the attempt being refunded.
    /// </summary>
    public PaymentAttempt Attempt { get; set; }

    /// <summary>
    /// Gets or sets the amount to refund, in major currency units, including tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the total already refunded against this payment, so the form can show what is left.
    /// </summary>
    public decimal AlreadyRefunded { get; set; }

    /// <summary>
    /// Gets or sets the reason recorded on the refund.
    /// </summary>
    public string Reason { get; set; }
}
