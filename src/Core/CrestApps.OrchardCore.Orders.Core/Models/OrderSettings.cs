namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// How the store handles orders, under Settings › Commerce › Orders.
/// </summary>
public sealed class OrderSettings
{
    /// <summary>
    /// Gets or sets whether a paid order's confirmation is emailed to the customer. It is sent only when the site has
    /// an email service.
    /// </summary>
    public bool SendConfirmations { get; set; } = true;

    /// <summary>
    /// Gets or sets whether signed-in buyers may ask for a quote from their cart instead of checking out.
    /// </summary>
    public bool AllowQuoteRequests { get; set; }

    /// <summary>
    /// Gets or sets whether a buyer who leaves a checkout without paying is emailed once, with what they chose and a
    /// link back to it. It is sent only when the site has an email service.
    /// </summary>
    public bool SendRecoveryEmails { get; set; }

    /// <summary>
    /// Gets or sets how many hours after the checkout was left the recovery email goes out.
    /// </summary>
    public int RecoveryDelayHours { get; set; } = 1;

    /// <summary>
    /// Gets or sets the product offered at checkout as an order bump: one tick adds it to the order before paying.
    /// </summary>
    public string OrderBumpProductId { get; set; }

    /// <summary>
    /// Gets or sets how much less the order bump costs than the product's price, in percent, or <see langword="null"/>.
    /// </summary>
    public decimal? OrderBumpPercentOff { get; set; }

    /// <summary>
    /// Gets or sets what the offer says above the product, such as "Add a matching cap?".
    /// </summary>
    public string OrderBumpHeadline { get; set; }
}
