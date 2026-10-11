using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Stripe.Workflows.Events;

/// <summary>
/// A workflow event that resumes when a Stripe refund is observed.
/// </summary>
public sealed class StripePaymentRefundedEvent : StripeEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StripePaymentRefundedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    public StripePaymentRefundedEvent(IStringLocalizer<StripePaymentRefundedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => StripeWorkflowEventNames.PaymentRefunded;

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Stripe Payment Refunded"];
}
