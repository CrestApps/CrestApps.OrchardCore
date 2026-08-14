using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.SignalR;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.SignalR;

internal sealed class DistributedTestHub : Hub<IDistributedTestClient>
{
    private readonly string _tenantName;

    public DistributedTestHub(ShellSettings shellSettings)
    {
        _tenantName = shellSettings.Name;
    }

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
            HubConnectionWork.MustComplete);
        await base.OnConnectedAsync();
    }

    public string Ready()
    {
        return _tenantName;
    }
}
