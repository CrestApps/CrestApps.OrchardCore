using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Stripe.Workflows.Events;

/// <summary>
/// A workflow event that resumes when a Stripe payment intent succeeds.
/// </summary>
public sealed class StripePaymentIntentSucceededEvent : StripeEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StripePaymentIntentSucceededEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    public StripePaymentIntentSucceededEvent(IStringLocalizer<StripePaymentIntentSucceededEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => StripeWorkflowEventNames.PaymentIntentSucceeded;

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Stripe Payment Intent Succeeded"];
}
