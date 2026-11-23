namespace CrestApps.OrchardCore.MailingLists.Services;

/// <summary>
/// A welcome coupon issued to a member, as pages and e-mails show it.
/// </summary>
public sealed class MailingListIssuedCoupon
{
    /// <summary>
    /// Gets the code the person types at checkout.
    /// </summary>
    public string Code { get; init; }

    /// <summary>
    /// Gets the absolute link that applies the code at checkout.
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    /// Gets the discount in words, such as "15% off".
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets when the code stops working (UTC), when it has an end. Convert it with <c>ILocalClock</c> before showing it.
    /// </summary>
    public DateTime? ExpiresUtc { get; init; }
}
