namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// The context raised while a new checkout session is being activated. Handlers add their steps and
/// billing items here.
/// </summary>
public sealed class CheckoutFlowActivatingContext
{
    /// <summary>
    /// The session being activated.
    /// </summary>
    public CheckoutSession Session { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutFlowActivatingContext"/> class.
    /// </summary>
    /// <param name="session">The session being activated.</param>
    public CheckoutFlowActivatingContext(CheckoutSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Session = session;
    }
}
