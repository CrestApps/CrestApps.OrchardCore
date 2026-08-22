using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Stripe.Workflows.Events;

/// <summary>
/// A workflow event that resumes when a Stripe payment fails.
/// </summary>
public sealed class StripePaymentFailedEvent : StripeEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StripePaymentFailedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    public StripePaymentFailedEvent(IStringLocalizer<StripePaymentFailedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => StripeWorkflowEventNames.PaymentFailed;

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Stripe Payment Failed"];
}
