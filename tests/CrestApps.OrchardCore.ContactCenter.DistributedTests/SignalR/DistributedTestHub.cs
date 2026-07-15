using CrestApps.OrchardCore.SignalR;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.SignalR;

internal sealed class DistributedTestHub(ShellSettings shellSettings) : Hub<IDistributedTestClient>
{
    private readonly string _tenantName = shellSettings.Name;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();

        if (string.IsNullOrWhiteSpace(userId))
        {
            Context.Abort();

            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            TenantSignalRGroupName.ForUser(_tenantName, userId),
            Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public string Ready()
    {
        return _tenantName;
    }
}
