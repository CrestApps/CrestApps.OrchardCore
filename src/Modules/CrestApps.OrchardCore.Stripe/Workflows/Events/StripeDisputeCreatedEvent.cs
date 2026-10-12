using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Stripe.Workflows.Events;

/// <summary>
/// A workflow event that resumes when a Stripe dispute or chargeback is opened.
/// </summary>
public sealed class StripeDisputeCreatedEvent : StripeEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StripeDisputeCreatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    public StripeDisputeCreatedEvent(IStringLocalizer<StripeDisputeCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => StripeWorkflowEventNames.DisputeCreated;

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Stripe Dispute Created"];
}
