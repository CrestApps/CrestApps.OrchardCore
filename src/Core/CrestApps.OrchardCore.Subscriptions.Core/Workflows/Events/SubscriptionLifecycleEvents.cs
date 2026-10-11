using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;

/// <summary>
/// The base for the workflow events raised as a subscription moves through its life.
/// </summary>
/// <remarks>
/// The reactions a site owner wants to a subscription changing state are exactly the ones that differ from
/// site to site: welcome the new subscriber, warn the one whose card failed, ask the one who cancelled why,
/// tell a Slack channel. Hard-coding any of them would be wrong, and adding a setting for each would never
/// end. Raising a workflow event instead lets the site owner build the reaction they actually want without
/// writing code.
///
/// Every event carries the subscription id, its title, its owner, its status, and the date access runs
/// through, and is correlated by the subscription id so a workflow can wait for a later stage of the same
/// agreement.
/// </remarks>
public abstract class SubscriptionEventBase : EventActivity
{
    /// <summary>
    /// Gets the localized workflow activity category.
    /// </summary>
    public override LocalizedString Category
        => Localizer["Subscriptions"];

    /// <summary>
    /// Gets the localizer used for the activity's display text.
    /// </summary>
    protected abstract IStringLocalizer Localizer { get; }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome(Localizer["Done"]);

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>
/// Raised when a subscription is first created from a completed checkout.
/// </summary>
public sealed class SubscriptionStartedEvent : SubscriptionEventBase
{
    /// <summary>
    /// The workflow event name.
    /// </summary>
    public const string EventName = "SubscriptionStartedEvent";

    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionStartedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionStartedEvent(IStringLocalizer<SubscriptionStartedEvent> stringLocalizer)
        => S = stringLocalizer;

    /// <inheritdoc/>
    protected override IStringLocalizer Localizer => S;

    /// <inheritdoc/>
    public override string Name => EventName;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Subscription Started Event"];
}

/// <summary>
/// Raised when a billing cycle is paid.
/// </summary>
public sealed class SubscriptionRenewedEvent : SubscriptionEventBase
{
    /// <summary>
    /// The workflow event name.
    /// </summary>
    public const string EventName = "SubscriptionRenewedEvent";

    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRenewedEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionRenewedEvent(IStringLocalizer<SubscriptionRenewedEvent> stringLocalizer)
        => S = stringLocalizer;

    /// <inheritdoc/>
    protected override IStringLocalizer Localizer => S;

    /// <inheritdoc/>
    public override string Name => EventName;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Subscription Renewed Event"];
}

/// <summary>
/// Raised when a renewal payment fails and the dunning window starts. It is the hook for the "your card
/// was declined" message that recovers a subscriber who would otherwise be lost to an expired card.
/// </summary>
public sealed class SubscriptionPastDueEvent : SubscriptionEventBase
{
    /// <summary>
    /// The workflow event name.
    /// </summary>
    public const string EventName = "SubscriptionPastDueEvent";

    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionPastDueEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionPastDueEvent(IStringLocalizer<SubscriptionPastDueEvent> stringLocalizer)
        => S = stringLocalizer;

    /// <inheritdoc/>
    protected override IStringLocalizer Localizer => S;

    /// <inheritdoc/>
    public override string Name => EventName;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Subscription Past Due Event"];
}

/// <summary>
/// Raised when a subscription is cancelled, by the customer, an administrator, or the gateway.
/// </summary>
public sealed class SubscriptionCanceledEvent : SubscriptionEventBase
{
    /// <summary>
    /// The workflow event name.
    /// </summary>
    public const string EventName = "SubscriptionCanceledEvent";

    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionCanceledEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionCanceledEvent(IStringLocalizer<SubscriptionCanceledEvent> stringLocalizer)
        => S = stringLocalizer;

    /// <inheritdoc/>
    protected override IStringLocalizer Localizer => S;

    /// <inheritdoc/>
    public override string Name => EventName;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Subscription Canceled Event"];
}

/// <summary>
/// Raised when a subscription ends, either because its grace window elapsed or because it billed every
/// cycle it was sold for. This is the moment access actually goes away.
/// </summary>
public sealed class SubscriptionExpiredEvent : SubscriptionEventBase
{
    /// <summary>
    /// The workflow event name.
    /// </summary>
    public const string EventName = "SubscriptionExpiredEvent";

    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionExpiredEvent"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionExpiredEvent(IStringLocalizer<SubscriptionExpiredEvent> stringLocalizer)
        => S = stringLocalizer;

    /// <inheritdoc/>
    protected override IStringLocalizer Localizer => S;

    /// <inheritdoc/>
    public override string Name => EventName;

    /// <inheritdoc/>
    public override LocalizedString DisplayText => S["Subscription Expired Event"];
}
