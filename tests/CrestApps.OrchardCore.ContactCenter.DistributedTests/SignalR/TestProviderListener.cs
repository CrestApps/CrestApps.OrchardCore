using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.SignalR;

internal sealed class TestProviderListener
{
    private readonly IHubContext<DistributedTestHub, IDistributedTestClient> _hubContext;
    private readonly string _tenantName;

    public TestProviderListener(
        IHubContext<DistributedTestHub, IDistributedTestClient> hubContext,
        ShellSettings shellSettings)
    {
        _hubContext = hubContext;
        _tenantName = shellSettings.Name;
    }

    public Task PublishAsync(string userId, string eventId)
    {
        return _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, userId))
            .ProviderEvent(eventId);
    }
}
