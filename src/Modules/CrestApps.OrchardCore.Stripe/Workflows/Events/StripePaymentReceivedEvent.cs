using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Stripe.Workflows.Events;

/// <summary>
/// A workflow event that resumes when a Stripe invoice payment succeeds.
/// </summary>
public sealed class StripePaymentReceivedEvent : StripeEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StripePaymentReceivedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    public StripePaymentReceivedEvent(IStringLocalizer<StripePaymentReceivedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => StripeWorkflowEventNames.PaymentReceived;

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Stripe Payment Received"];
}
