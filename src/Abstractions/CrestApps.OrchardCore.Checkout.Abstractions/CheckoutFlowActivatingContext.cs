namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised while a new checkout session is being activated. Handlers add their steps and
/// billing items here, or refuse the checkout with <see cref="Reject(string)"/>.
/// </summary>
public sealed class CheckoutFlowActivatingContext
{
    private readonly List<string> _errors = [];

    /// <summary>
    /// The session being activated.
    /// </summary>
    public CheckoutSession Session { get; }

    /// <summary>
    /// Gets why the checkout was refused, when a handler refused it. When it has any entry, the checkout is not started
    /// and the caller gets a <see cref="CheckoutRejectedException"/> carrying these reasons.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowActivatingContext"/> class.
    /// </summary>
    /// <param name="session">The session being activated.</param>
    public CheckoutFlowActivatingContext(CheckoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
    }

    /// <summary>
    /// Refuses the checkout, for example because what it was asked to pay for can't be paid together. A handler calls
    /// it instead of throwing, because an exception a handler throws is logged and the next handler still runs.
    /// </summary>
    /// <param name="error">Why the checkout is refused, in words a customer can read.</param>
    public void Reject(string error)
    {
        ArgumentException.ThrowIfNullOrEmpty(error);

        _errors.Add(error);
    }
}
