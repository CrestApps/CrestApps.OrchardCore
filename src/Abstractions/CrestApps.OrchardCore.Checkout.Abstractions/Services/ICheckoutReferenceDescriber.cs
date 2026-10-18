namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Names what a checkout paid for, such as an order, for the admin pages that list checkouts and what they used:
/// the text to show and the page it opens. A module implements it for its own reference type.
/// </summary>
public interface ICheckoutReferenceDescriber
{
    /// <summary>
    /// Describes a reference, or returns <see langword="null"/> when the type isn't this describer's or the record is
    /// gone.
    /// </summary>
    /// <param name="referenceType">The checkout's reference type, such as <c>Order</c>.</param>
    /// <param name="referenceId">The referenced record's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<CheckoutReferenceDescription> DescribeAsync(string referenceType, string referenceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// How a checkout's reference reads on an admin page.
/// </summary>
public sealed class CheckoutReferenceDescription
{
    /// <summary>
    /// Gets the text to show, such as <c>Order 261018-XTKM9Y</c>.
    /// </summary>
    public string Text { get; init; }

    /// <summary>
    /// Gets the admin page it opens, or <see langword="null"/> when there is none.
    /// </summary>
    public string Url { get; init; }
}
