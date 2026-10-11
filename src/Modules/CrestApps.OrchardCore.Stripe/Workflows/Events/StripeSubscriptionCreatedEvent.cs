using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Stripe.Workflows.Events;

/// <summary>
/// A workflow event that resumes when a Stripe subscription is created.
/// </summary>
public sealed class StripeSubscriptionCreatedEvent : StripeEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StripeSubscriptionCreatedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    public StripeSubscriptionCreatedEvent(IStringLocalizer<StripeSubscriptionCreatedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => StripeWorkflowEventNames.SubscriptionCreated;

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Stripe Subscription Created"];
}
