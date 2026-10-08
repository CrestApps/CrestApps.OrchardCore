using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// The filters of the installment plans list.
/// </summary>
public class InstallmentPlansIndexOptions
{
    /// <summary>
    /// Gets or sets the status to show.
    /// </summary>
    public InstallmentPlanStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets text matched against the plan and the customer.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status choices.
    /// </summary>
    public IList<SelectListItem> Statuses { get; set; } = [];
}

/// <summary>
/// The installment plans list.
/// </summary>
public class InstallmentPlansIndexViewModel
{
    /// <summary>
    /// Gets or sets the plans on this page.
    /// </summary>
    public IList<InstallmentPlan> Plans { get; set; } = [];

    /// <summary>
    /// Gets or sets the filters.
    /// </summary>
    public InstallmentPlansIndexOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }
}

/// <summary>
/// The form an administrator fills in to create an installment plan.
/// </summary>
public class CreateInstallmentPlanViewModel
{
    /// <summary>
    /// Gets or sets what the plan pays for.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets whether the customer is a new one.
    /// </summary>
    public bool NewCustomer { get; set; }

    /// <summary>
    /// Gets or sets the existing customer.
    /// </summary>
    public string CustomerUserId { get; set; }

    /// <summary>
    /// Gets or sets the new customer's name.
    /// </summary>
    public string NewCustomerName { get; set; }

    /// <summary>
    /// Gets or sets the new customer's email.
    /// </summary>
    public string NewCustomerEmail { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the total, the down payment included.
    /// </summary>
    public decimal? TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the down payment.
    /// </summary>
    public decimal? DownPaymentAmount { get; set; }

    /// <summary>
    /// Gets or sets the number of payments after the down payment.
    /// </summary>
    public int? InstallmentCount { get; set; }

    /// <summary>
    /// Gets or sets how often those payments fall due.
    /// </summary>
    public InstallmentFrequency Frequency { get; set; } = InstallmentFrequency.Monthly;

    /// <summary>
    /// Gets or sets when the first of them falls due.
    /// </summary>
    public DateTime? FirstDueDate { get; set; }

    /// <summary>
    /// Gets or sets how they are collected.
    /// </summary>
    public InstallmentCollectionMethod CollectionMethod { get; set; }

    /// <summary>
    /// Gets or sets an internal note.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the currency choices.
    /// </summary>
    public IList<SelectListItem> Currencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the number of decimal places of each currency, for the schedule preview.
    /// </summary>
    public IDictionary<string, int> CurrencyDecimals { get; set; } = new Dictionary<string, int>();
}

/// <summary>
/// The page that takes an installment plan's down payment.
/// </summary>
public class InstallmentPlanPayViewModel
{
    /// <summary>
    /// Gets or sets the plan.
    /// </summary>
    public InstallmentPlan Plan { get; set; }

    /// <summary>
    /// Gets or sets the checkout that takes the down payment.
    /// </summary>
    public CheckoutFlow Flow { get; set; }

    /// <summary>
    /// Gets or sets the payment methods that can take the down payment.
    /// </summary>
    public IList<InstallmentPlanPaymentMethodViewModel> PaymentMethods { get; set; } = [];
}

/// <summary>
/// One payment method on the down payment page.
/// </summary>
public class InstallmentPlanPaymentMethodViewModel
{
    /// <summary>
    /// Gets or sets the provider key.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the name shown to the administrator.
    /// </summary>
    public string DisplayName { get; set; }
}

/// <summary>
/// The installment plan detail page.
/// </summary>
public class InstallmentPlanDetailViewModel
{
    /// <summary>
    /// Gets or sets the plan.
    /// </summary>
    public InstallmentPlan Plan { get; set; }

    /// <summary>
    /// Gets or sets each payment's transaction, keyed by transaction id.
    /// </summary>
    public IDictionary<string, Transaction> Transactions { get; set; } = new Dictionary<string, Transaction>();

    /// <summary>
    /// Gets or sets whether payment receipts are available.
    /// </summary>
    public bool ShowReceipts { get; set; }

    /// <summary>
    /// Gets or sets the current time.
    /// </summary>
    public DateTime UtcNow { get; set; }
}
