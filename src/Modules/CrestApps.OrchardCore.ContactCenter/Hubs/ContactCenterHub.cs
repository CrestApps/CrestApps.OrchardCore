using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Hubs;

/// <summary>
/// SignalR hub that powers the real-time Contact Center experience. Agents connect to receive presence,
/// offer, and queue updates, while supervisors connect to monitor queues and agents. Each invocation runs
/// in its own OrchardCore shell scope and is authorized against Contact Center permissions.
/// </summary>
[Authorize]
public sealed class ContactCenterHub : Hub<IContactCenterHubClient>
{
    /// <summary>
    /// The name of the SignalR group that receives supervisor-wide updates.
    /// </summary>
    public const string SupervisorsGroup = "cc:supervisors";

    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ContactCenterHub(ILogger<ContactCenterHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Builds the SignalR group name that receives updates for a single queue.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <returns>The queue group name.</returns>
    public static string QueueGroup(string queueId)
    {
        return $"cc:queue:{queueId}";
    }

    /// <inheritdoc/>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;

        if (string.IsNullOrEmpty(userId))
        {
            Context.Abort();

            return;
        }

        var authorized = false;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var services = scope.ServiceProvider;

            if (await AuthorizeAsync(services, ContactCenterPermissions.SignIntoQueues))
            {
                authorized = true;

                try
                {
                    var sessionService = services.GetRequiredService<IAgentSessionService>();
                    var userName = Context.User?.Identity?.Name;
                    var displayName = await GetDisplayNameAsync(services, userName);

                    var session = await sessionService.ConnectAsync(userId, Context.ConnectionId, userName, displayName, Context.ConnectionAborted);

                    foreach (var queueId in session.QueueIds)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, QueueGroup(queueId), Context.ConnectionAborted);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while registering the Contact Center connection for user '{UserId}'.", userId);
                }
            }

            if (await AuthorizeAsync(services, ContactCenterPermissions.MonitorContactCenter))
            {
                authorized = true;

                await Groups.AddToGroupAsync(Context.ConnectionId, SupervisorsGroup, Context.ConnectionAborted);
            }
        });

        if (!authorized)
        {
            Context.Abort();

            return;
        }

        await base.OnConnectedAsync();
    }

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var userId = Context.UserIdentifier;

        if (!string.IsNullOrEmpty(userId))
        {
            await ShellScope.UsingChildScopeAsync(async scope =>
            {
                var sessionService = scope.ServiceProvider.GetRequiredService<IAgentSessionService>();

                try
                {
                    await sessionService.DisconnectAsync(userId, Context.ConnectionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while removing the Contact Center connection for user '{UserId}'.", userId);
                }
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Records a heartbeat so the cleanup pass does not consider the agent session stale.
    /// </summary>
    public Task Heartbeat()
    {
        var userId = Context.UserIdentifier;

        if (string.IsNullOrEmpty(userId))
        {
            return Task.CompletedTask;
        }

        return ShellScope.UsingChildScopeAsync(async scope =>
        {
            var sessionService = scope.ServiceProvider.GetRequiredService<IAgentSessionService>();
            await sessionService.HeartbeatAsync(userId, Context.ConnectionAborted);
        });
    }

    /// <summary>
    /// Gets the reconnect snapshot the agent desktop needs to restore its state.
    /// </summary>
    /// <returns>The agent desktop snapshot.</returns>
    public async Task<AgentDesktopSnapshot> GetSnapshot()
    {
        var userId = Context.UserIdentifier;

        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        AgentDesktopSnapshot snapshot = null;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            var sessionService = scope.ServiceProvider.GetRequiredService<IAgentSessionService>();
            snapshot = await sessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
        });

        return snapshot;
    }

    /// <summary>
    /// Subscribes a supervisor connection to live updates for a single queue.
    /// </summary>
    /// <param name="queueId">The queue identifier to watch.</param>
    public async Task WatchQueue(string queueId)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return;
        }

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            if (await AuthorizeAsync(scope.ServiceProvider, ContactCenterPermissions.MonitorContactCenter))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, QueueGroup(queueId), Context.ConnectionAborted);
            }
        });
    }

    /// <summary>
    /// Unsubscribes a supervisor connection from live updates for a single queue.
    /// </summary>
    /// <param name="queueId">The queue identifier to stop watching.</param>
    public async Task UnwatchQueue(string queueId)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, QueueGroup(queueId), Context.ConnectionAborted);
    }

    private async Task<bool> AuthorizeAsync(IServiceProvider services, Permission permission)
    {
        var httpContext = Context.GetHttpContext();

        if (httpContext?.User is null)
        {
            return false;
        }

        var authorizationService = services.GetRequiredService<IAuthorizationService>();

        return await authorizationService.AuthorizeAsync(httpContext.User, permission);
    }

    private async Task<string> GetDisplayNameAsync(IServiceProvider services, string fallback)
    {
        var userManager = services.GetRequiredService<UserManager<IUser>>();
        var displayNameProvider = services.GetRequiredService<IDisplayNameProvider>();
        var user = await userManager.GetUserAsync(Context.User);

        if (user is not null)
        {
            var displayName = await displayNameProvider.GetAsync(user, Context.ConnectionAborted);

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName;
            }
        }

        return fallback;
    }
}
