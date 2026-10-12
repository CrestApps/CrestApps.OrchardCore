using CrestApps.OrchardCore.Subscriptions.Core.Workflows.Events;
using OrchardCore.Workflows.Display;

namespace CrestApps.OrchardCore.Subscriptions.Workflows.Drivers;

/// <summary>
/// Provides display shapes for the subscribed tenant failed setup workflow event.
/// </summary>
public sealed class SubscribedTenantFailedSetupEventDisplayDriver : ActivityDisplayDriver<SubscribedTenantFailedSetupEvent>
{
}
