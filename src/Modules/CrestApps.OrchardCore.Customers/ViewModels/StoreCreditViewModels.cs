using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Customers.ViewModels;

public class CheckoutStoreCreditViewModel
{
    public string AccountId { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets what's on the buyer's store credit.
    /// </summary>
    public decimal Available { get; set; }

    /// <summary>
    /// Gets or sets what it pays toward this order.
    /// </summary>
    public decimal Used { get; set; }

    public bool IsApplied { get; set; }
}

public class CustomerStoreCreditViewModel
{
    public string CustomerId { get; set; }

    public bool CanManage { get; set; }

    public bool HasAccount { get; set; }

    public string DefaultCurrency { get; set; }

    public IReadOnlyList<StoreCreditAccount> Accounts { get; set; } = [];
}
