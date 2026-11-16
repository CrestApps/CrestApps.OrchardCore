namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// What a business account's order holds of the account's monthly budget from the moment its payment began: the order
/// counts against the budget at this amount until it's paid, when its total counts instead, or canceled.
/// </summary>
public sealed class OrderBudgetHold
{
    /// <summary>
    /// Gets or sets the total the payment began for, in <see cref="Currency"/>.
    /// </summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets when the payment began and the budget was checked.
    /// </summary>
    public DateTime HeldUtc { get; set; }
}
