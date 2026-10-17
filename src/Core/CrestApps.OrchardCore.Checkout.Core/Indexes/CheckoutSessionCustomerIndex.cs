using CrestApps.OrchardCore.Checkout;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// Who a <see cref="CheckoutSession"/> belongs to: its owning user and the email its buyer gave. It is what finds a
/// person's checkouts for an access or erasure request, including the guest checkouts only the email ties to them.
/// </summary>
public sealed class CheckoutSessionCustomerIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the checkout session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the owning user id, or <see langword="null"/> for a guest checkout.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the email the buyer gave, trimmed and upper-cased so it is matched without case sensitivity, or
    /// <see langword="null"/> when none was given.
    /// </summary>
    public string NormalizedEmail { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
