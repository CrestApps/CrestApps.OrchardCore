namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// What the client needs in order to finish a payment the engine has begun.
/// </summary>
/// <remarks>
/// A checkout can owe money for several obligations at once (an up-front fee plus one or more recurring
/// intervals), and a provider may need the customer to act on more than one of them. The outcome therefore
/// carries one <see cref="PaymentBeginStep"/> per obligation rather than a single client secret, so a client
/// can confirm each in turn instead of the framework silently settling only the first.
/// </remarks>
public sealed class PaymentBeginOutcome
{
    /// <summary>
    /// Gets or sets a value indicating whether every obligation was begun successfully.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the error shown to the customer when <see cref="Succeeded"/> is <see langword="false"/>.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets the per-obligation steps the client must complete.
    /// </summary>
    public IList<PaymentBeginStep> Steps { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether any step still needs the customer to act (for example confirming a
    /// card or completing a Strong Customer Authentication challenge). When this is <see langword="false"/>
    /// the client can go straight to completing the checkout.
    /// </summary>
    public bool RequiresAction
        => Steps.Any(step => step.RequiresAction);

    /// <summary>
    /// Creates a failed outcome.
    /// </summary>
    /// <param name="errorMessage">The error to show the customer.</param>
    public static PaymentBeginOutcome Failure(string errorMessage)
        => new() { Succeeded = false, ErrorMessage = errorMessage };
}

/// <summary>
/// One obligation the client must complete with the provider.
/// </summary>
public sealed class PaymentBeginStep
{
    /// <summary>
    /// Gets or sets the obligation this step settles.
    /// </summary>
    public string ObligationId { get; set; }

    /// <summary>
    /// Gets or sets the durable attempt backing the step, so a client callback can be correlated to the
    /// ledger entry it belongs to.
    /// </summary>
    public string AttemptId { get; set; }

    /// <summary>
    /// Gets or sets the client secret or token an embedded provider needs to confirm the payment.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the absolute URL a hosted provider requires the customer to be redirected to.
    /// </summary>
    public string RedirectUrl { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the customer must act before this obligation can settle.
    /// </summary>
    public bool RequiresAction { get; set; }
}
