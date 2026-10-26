namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// One of the things a checkout pays for, when it pays for several at once (see <see cref="CheckoutSession.References"/>).
/// </summary>
public sealed class CheckoutReference
{
    /// <summary>
    /// Gets or sets the kind of thing being paid for, for example a transaction.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the thing being paid for.
    /// </summary>
    public string ReferenceId { get; set; }
}
