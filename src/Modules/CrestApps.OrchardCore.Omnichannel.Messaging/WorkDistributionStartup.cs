using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging;

/// <summary>
/// Supplies the real queue policy to the messaging workspace when Work Distribution is enabled. The workspace is deliberately
/// usable without queues — an agent can own a number and text from it with no departments configured at all — so
/// the queue lookup lives behind a seam with a null default, and this startup replaces it only where there are
/// queues to read.
/// </summary>
[RequireFeatures(ContactCenterConstants.Feature.Queues)]
public sealed class WorkDistributionStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IMessagingQueuePolicyReader, ActivityQueueMessagingQueuePolicyReader>());
    }
}
