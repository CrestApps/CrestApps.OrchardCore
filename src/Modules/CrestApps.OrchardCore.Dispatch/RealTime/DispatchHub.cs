using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Dispatch.RealTime;

/// <summary>
/// Tells open dispatch boards when visits change, so they refresh.
/// </summary>
[Authorize]
public sealed class DispatchHub : Hub
{
    private readonly IAuthorizationService _authorizationService;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchHub"/> class.
    /// </summary>
    public DispatchHub(IAuthorizationService authorizationService, ShellSettings shellSettings)
    {
        _authorizationService = authorizationService;
        _tenantName = shellSettings.Name;
    }

    /// <summary>
    /// Joins the board's updates, for users who can see the board.
    /// </summary>
    /// <returns>Whether the connection joined.</returns>
    public async Task<bool> Subscribe()
    {
        if (!await _authorizationService.AuthorizeAsync(Context.User, DispatchPermissions.ViewDispatch))
        {
            return false;
        }

        // A membership change must not stop half way, so it never takes the connection's token.
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(_tenantName), HubConnectionWork.MustComplete);

        return true;
    }

    /// <summary>
    /// Gives the tenant's board group.
    /// </summary>
    /// <param name="tenantName">The tenant.</param>
    /// <returns>The group's name.</returns>
    public static string Group(string tenantName) => TenantSignalRGroupName.ForGroup(tenantName, "dispatch-board");
}

/// <summary>
/// Sends every visit change to the open boards once the change is saved.
/// </summary>
public sealed class DispatchBoardNotifier : IVisitObserver
{
    /// <summary>
    /// The client method boards listen to.
    /// </summary>
    public const string ClientMethod = "VisitChanged";

    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchBoardNotifier"/> class.
    /// </summary>
    public DispatchBoardNotifier(ShellSettings shellSettings)
    {
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public Task OnEventAsync(ServiceVisit visit, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(visit);

        var tenantName = _tenantName;
        var message = new { visitId = visit.ItemId, number = visit.Number, eventName, status = visit.Status.ToString() };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IHubContext<DispatchHub>>().Clients.Group(DispatchHub.Group(tenantName)).SendAsync(ClientMethod, message));

        return Task.CompletedTask;
    }
}
