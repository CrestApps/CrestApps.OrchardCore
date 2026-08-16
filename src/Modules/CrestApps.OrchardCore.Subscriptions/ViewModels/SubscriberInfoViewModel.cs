using CrestApps.OrchardCore.Subscriptions.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the subscriber account information displayed in subscriber views.
/// </summary>
public class SubscriberInfoViewModel
{
    /// <summary>
    /// Gets or sets the subscriber display name.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the subscriber user name.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the subscriber email address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the subscriber user identifier.
    /// </summary>
    public string UserId { get; set; }
}

/// <summary>
/// Represents the invoices displayed for a subscriber.
/// </summary>
public class SubscriberInvoicesViewModel
{
    /// <summary>
    /// Gets or sets the subscriber invoices to display.
    /// </summary>
    public IList<SubscriberInvoiceViewModel> Invoices { get; set; }
}

/// <summary>
/// Represents a single invoice displayed to a subscriber.
/// </summary>
public class SubscriberInvoiceViewModel
{
    /// <summary>
    /// Gets or sets the invoice date.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the title of the service plan billed by the invoice.
    /// </summary>
    public string ServicePlanTitle { get; set; }

    /// <summary>
    /// Gets or sets the invoice amount.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// Gets or sets the invoice payment status.
    /// </summary>
    public PaymentStatus Status { get; set; }
}
