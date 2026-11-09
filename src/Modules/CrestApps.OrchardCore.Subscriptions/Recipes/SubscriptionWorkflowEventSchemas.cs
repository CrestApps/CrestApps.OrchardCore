using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using Json.Schema;

namespace CrestApps.OrchardCore.Subscriptions.Recipes;

/// <summary>
/// Describes the subscription workflow events to CrestApps Recipes. They have no settings and one <c>Done</c> outcome.
/// </summary>
internal abstract class SubscriptionEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Subscriptions";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
        => [];
}

/// <summary>
/// Describes the <c>SubscriptionStartedEvent</c> workflow event.
/// </summary>
internal sealed class SubscriptionStartedEventSchema : SubscriptionEventSchemaBase
{
    public override string Name => SubscriptionStartedEvent.EventName;

    protected override string DisplayText => "Subscription Started Event";

    protected override string Description => "Starts when a subscription starts. The input carries the subscription id, title, owner, status and the date access runs through; the workflow is correlated by the subscription id.";
}

/// <summary>
/// Describes the <c>SubscriptionRenewedEvent</c> workflow event.
/// </summary>
internal sealed class SubscriptionRenewedEventSchema : SubscriptionEventSchemaBase
{
    public override string Name => SubscriptionRenewedEvent.EventName;

    protected override string DisplayText => "Subscription Renewed Event";

    protected override string Description => "Starts when a subscription renews. The input carries the subscription id, title, owner, status and the date access runs through; the workflow is correlated by the subscription id.";
}

/// <summary>
/// Describes the <c>SubscriptionPastDueEvent</c> workflow event.
/// </summary>
internal sealed class SubscriptionPastDueEventSchema : SubscriptionEventSchemaBase
{
    public override string Name => SubscriptionPastDueEvent.EventName;

    protected override string DisplayText => "Subscription Past Due Event";

    protected override string Description => "Starts when a subscription becomes past due. The input carries the subscription id, title, owner, status and the date access runs through; the workflow is correlated by the subscription id.";
}

/// <summary>
/// Describes the <c>SubscriptionCanceledEvent</c> workflow event.
/// </summary>
internal sealed class SubscriptionCanceledEventSchema : SubscriptionEventSchemaBase
{
    public override string Name => SubscriptionCanceledEvent.EventName;

    protected override string DisplayText => "Subscription Canceled Event";

    protected override string Description => "Starts when a subscription is canceled. The input carries the subscription id, title, owner, status and the date access runs through; the workflow is correlated by the subscription id.";
}

/// <summary>
/// Describes the <c>SubscriptionExpiredEvent</c> workflow event.
/// </summary>
internal sealed class SubscriptionExpiredEventSchema : SubscriptionEventSchemaBase
{
    public override string Name => SubscriptionExpiredEvent.EventName;

    protected override string DisplayText => "Subscription Expired Event";

    protected override string Description => "Starts when a subscription expires. The input carries the subscription id, title, owner, status and the date access runs through; the workflow is correlated by the subscription id.";
}

/// <summary>
/// Describes the <c>SubscribedTenantSetupSucceededEvent</c> workflow event.
/// </summary>
internal sealed class SubscribedTenantSetupSucceededEventSchema : SubscriptionEventSchemaBase
{
    public override string Name => SubscribedTenantSetupSucceededEvent.EventName;

    protected override string DisplayText => "Subscribed Tenant Setup Succeeded Event";

    protected override string Description => "Starts when the tenant provisioned for a subscription is set up.";
}

/// <summary>
/// Describes the <c>SubscribedTenantFailedSetupEvent</c> workflow event.
/// </summary>
internal sealed class SubscribedTenantFailedSetupEventSchema : SubscriptionEventSchemaBase
{
    public override string Name => SubscribedTenantFailedSetupEvent.EventName;

    protected override string DisplayText => "Subscribed Tenant Failed Setup Event";

    protected override string Description => "Starts when setting up the tenant provisioned for a subscription is abandoned.";
}
