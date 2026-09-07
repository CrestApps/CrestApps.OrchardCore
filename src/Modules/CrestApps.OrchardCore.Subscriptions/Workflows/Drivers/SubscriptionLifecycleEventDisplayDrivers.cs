using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Subscriptions.Workflows.Drivers;

/// <summary>
/// Renders the subscription started event in the workflow editor.
/// </summary>
public sealed class SubscriptionStartedEventDisplayDriver : ActivityDisplayDriver<SubscriptionStartedEvent>
{
}

/// <summary>
/// Renders the subscription renewed event in the workflow editor.
/// </summary>
public sealed class SubscriptionRenewedEventDisplayDriver : ActivityDisplayDriver<SubscriptionRenewedEvent>
{
}

/// <summary>
/// Renders the subscription past-due event in the workflow editor.
/// </summary>
public sealed class SubscriptionPastDueEventDisplayDriver : ActivityDisplayDriver<SubscriptionPastDueEvent>
{
}

/// <summary>
/// Renders the subscription canceled event in the workflow editor.
/// </summary>
public sealed class SubscriptionCanceledEventDisplayDriver : ActivityDisplayDriver<SubscriptionCanceledEvent>
{
}

/// <summary>
/// Renders the subscription expired event in the workflow editor.
/// </summary>
public sealed class SubscriptionExpiredEventDisplayDriver : ActivityDisplayDriver<SubscriptionExpiredEvent>
{
}
