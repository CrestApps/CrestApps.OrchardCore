using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Stripe.Workflows.Events;

/// <summary>
/// A workflow event that resumes when a Stripe payment is canceled.
/// </summary>
public sealed class StripePaymentCanceledEvent : StripeEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StripePaymentCanceledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    public StripePaymentCanceledEvent(IStringLocalizer<StripePaymentCanceledEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => StripeWorkflowEventNames.PaymentCanceled;

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Stripe Payment Canceled"];
}
