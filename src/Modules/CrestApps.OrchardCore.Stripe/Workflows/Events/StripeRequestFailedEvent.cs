using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Stripe.Workflows.Events;

/// <summary>
/// A workflow event that resumes when a request to Stripe fails, typically because the connection is no
/// longer valid or the API key has been revoked. This lets operators build alerting workflows.
/// </summary>
public sealed class StripeRequestFailedEvent : StripeEventActivity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StripeRequestFailedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for workflow display text.</param>
    public StripeRequestFailedEvent(IStringLocalizer<StripeRequestFailedEvent> stringLocalizer)
        : base(stringLocalizer)
    {
    }

    /// <inheritdoc/>
    public override string Name
        => StripeWorkflowEventNames.RequestFailed;

    /// <inheritdoc/>
    public override LocalizedString DisplayText
        => S["Stripe Request Failed"];
}
