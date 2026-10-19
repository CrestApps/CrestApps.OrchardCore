using CrestApps.OrchardCore.Customers.Models;

namespace CrestApps.OrchardCore.Customers.ViewModels;

public class LoyaltySettingsViewModel
{
    public string ProgramName { get; set; }

    public decimal PointsPerUnit { get; set; }

    public decimal PointValue { get; set; }

    public int MinimumPointsToRedeem { get; set; }
}

public class CheckoutLoyaltyViewModel
{
    public string AccountId { get; set; }

    public string ProgramName { get; set; }

    public string Currency { get; set; }

    public int Points { get; set; }

    /// <summary>
    /// Gets or sets what the buyer's points are worth.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Gets or sets what they pay toward this order.
    /// </summary>
    public decimal Used { get; set; }

    public bool IsApplied { get; set; }

    /// <summary>
    /// Gets or sets the fewest points that can be spent, when the buyer has fewer.
    /// </summary>
    public int? PointsNeeded { get; set; }
}

public class CustomerLoyaltyViewModel
{
    public string CustomerId { get; set; }

    public bool CanManage { get; set; }

    public bool HasAccount { get; set; }

    public LoyaltyAccount Account { get; set; }

    public LoyaltySettings Settings { get; set; }

    public string Currency { get; set; }
}
