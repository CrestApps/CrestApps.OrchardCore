namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Has a say just before a checkout's payment begins, or before a checkout with nothing to pay completes: it can
/// hold something the purchase needs, such as a coupon's use, and stop the payment when it can't.
/// </summary>
public interface ICheckoutPaymentGate
{
    /// <summary>
    /// Called inside the checkout's lock, before any payment provider is contacted.
    /// </summary>
    /// <param name="context">The checkout and a way to refuse the payment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task BeginningAsync(CheckoutPaymentBeginningContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// The context of <see cref="ICheckoutPaymentGate.BeginningAsync"/>.
/// </summary>
public sealed class CheckoutPaymentBeginningContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutPaymentBeginningContext"/> class.
    /// </summary>
    /// <param name="flow">The checkout.</param>
    /// <param name="providerKey">The payment provider chosen, or <see langword="null"/> when nothing is paid.</param>
    public CheckoutPaymentBeginningContext(CheckoutFlow flow, string providerKey)
    {
        ArgumentNullException.ThrowIfNull(flow);

        Flow = flow;
        ProviderKey = providerKey;
    }

    /// <summary>
    /// Gets the checkout.
    /// </summary>
    public CheckoutFlow Flow { get; }

    /// <summary>
    /// Gets the payment provider chosen, or <see langword="null"/> when nothing is paid.
    /// </summary>
    public string ProviderKey { get; }

    /// <summary>
    /// Gets why the payment was refused, or <see langword="null"/> when it wasn't.
    /// </summary>
    public string ErrorMessage { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a gate refused the payment.
    /// </summary>
    public bool IsRefused
        => ErrorMessage is not null;

    /// <summary>
    /// Gets or sets a value indicating whether a gate changed what the checkout costs, so the buyer has to see the
    /// new total before paying.
    /// </summary>
    public bool InvoiceChanged { get; set; }

    /// <summary>
    /// Stops the payment, telling the buyer why.
    /// </summary>
    /// <param name="message">What the buyer is told.</param>
    public void Refuse(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);

        ErrorMessage = message;
    }
}
