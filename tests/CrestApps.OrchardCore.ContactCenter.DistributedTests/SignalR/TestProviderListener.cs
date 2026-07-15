using CrestApps.OrchardCore.SignalR;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.SignalR;

internal sealed class TestProviderListener(
    IHubContext<DistributedTestHub, IDistributedTestClient> hubContext,
    ShellSettings shellSettings)
{
    private readonly IHubContext<DistributedTestHub, IDistributedTestClient> _hubContext = hubContext;
    private readonly string _tenantName = shellSettings.Name;

    public Task PublishAsync(string userId, string eventId)
    {
        return _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, userId))
            .ProviderEvent(eventId);
    }
}
