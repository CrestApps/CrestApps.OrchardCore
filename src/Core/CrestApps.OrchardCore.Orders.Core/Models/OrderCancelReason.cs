namespace CrestApps.OrchardCore.Orders.Core.Models;

/// <summary>
/// Why an unpaid order was canceled.
/// </summary>
public enum OrderCancelReason
{
    /// <summary>
    /// The customer checked out the same cart again, which made a newer order.
    /// </summary>
    Replaced = 1,

    /// <summary>
    /// The payment was declined.
    /// </summary>
    PaymentFailed = 2,

    /// <summary>
    /// The checkout was canceled.
    /// </summary>
    CheckoutCanceled = 3,

    /// <summary>
    /// Nobody finished the checkout within its lifetime.
    /// </summary>
    CheckoutExpired = 4,

    /// <summary>
    /// The order had no checkout left that could pay for it.
    /// </summary>
    Abandoned = 5,

    /// <summary>
    /// Something the order needed couldn't be held for it when it was made, such as the last units of a product.
    /// </summary>
    Unavailable = 6,

    /// <summary>
    /// The store canceled it from the order's page.
    /// </summary>
    CanceledByStore = 7,

    /// <summary>
    /// The buyer turned down the quote it was.
    /// </summary>
    Declined = 8,

    /// <summary>
    /// The person who had to approve it, such as a company admin, rejected it.
    /// </summary>
    Rejected = 9,
}
