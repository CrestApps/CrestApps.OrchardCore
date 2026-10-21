namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// How risky a gateway's fraud checks rated an order's payment.
/// </summary>
public sealed class OrderPaymentRisk
{
    /// <summary>
    /// Gets or sets the rating: <c>normal</c>, <c>elevated</c> or <c>highest</c>.
    /// </summary>
    public string Level { get; set; }

    /// <summary>
    /// Gets or sets the score, from 0 (safe) to 99, when the gateway gives one.
    /// </summary>
    public int? Score { get; set; }

    /// <summary>
    /// Gets or sets the gateway that rated it, such as <c>Stripe</c>.
    /// </summary>
    public string Gateway { get; set; }

    /// <summary>
    /// Gets whether the rating says to look at the order before fulfilling it.
    /// </summary>
    public bool IsElevated
        => Level is "elevated" or "highest";
}
