using CrestApps.OrchardCore.Checkout;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="CheckoutSession"/>.
/// </summary>
public sealed class CheckoutSessionIndex : MapIndex
{
    /// <summary>
    /// The checkout session id.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The kind of thing being purchased.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// The identifier of the thing being purchased.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// The optional secondary identifier of the thing being purchased.
    /// </summary>
    public string ReferenceVersionId { get; set; }

    /// <summary>
    /// The owning user id, when the session belongs to an authenticated user.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// The lifecycle state of the checkout.
    /// </summary>
    public CheckoutSessionStatus Status { get; set; }

    /// <summary>
    /// The UTC time the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC time the session was last modified.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// The UTC time the session completed, when applicable.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}
