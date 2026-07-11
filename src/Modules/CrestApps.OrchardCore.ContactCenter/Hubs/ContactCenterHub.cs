using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
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

    /// <summary>
    /// Signs the current agent into the selected queues and campaigns without reloading the page.
    /// </summary>
    /// <param name="queueIds">The selected queues.</param>
    /// <param name="campaignIds">The selected campaigns.</param>
    /// <returns>The updated agent snapshot.</returns>
    public async Task<AgentDesktopSnapshot> SignIn(IList<string> queueIds, IList<string> campaignIds)
    {
        var userId = EnsureUserId();
        AgentDesktopSnapshot snapshot = null;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            await EnsureAuthorizedAsync(scope.ServiceProvider, ContactCenterPermissions.SignIntoQueues);

            var sessionService = scope.ServiceProvider.GetRequiredService<IAgentSessionService>();
            var presenceManager = scope.ServiceProvider.GetRequiredService<IAgentPresenceManager>();
            var previousSnapshot = await sessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
            var normalizedQueueIds = ContactCenterFormHelpers.NormalizeList(queueIds);
            var normalizedCampaignIds = ContactCenterFormHelpers.NormalizeList(campaignIds);

            if (normalizedQueueIds.Count == 0 && normalizedCampaignIds.Count == 0)
            {
                throw new HubException("Select at least one queue or campaign before signing in.");
            }

            await presenceManager.SignInAsync(userId, normalizedQueueIds, normalizedCampaignIds, Context.ConnectionAborted);

            snapshot = await sessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
            await UpdateQueueGroupsAsync(previousSnapshot?.QueueIds, snapshot?.QueueIds);
        });

        return snapshot;
    }

    /// <summary>
    /// Signs the current agent out without reloading the page.
    /// </summary>
    /// <returns>The updated agent snapshot.</returns>
    public async Task<AgentDesktopSnapshot> SignOut()
    {
        var userId = EnsureUserId();
        AgentDesktopSnapshot snapshot = null;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            await EnsureAuthorizedAsync(scope.ServiceProvider, ContactCenterPermissions.SignIntoQueues);

            var sessionService = scope.ServiceProvider.GetRequiredService<IAgentSessionService>();
            var presenceManager = scope.ServiceProvider.GetRequiredService<IAgentPresenceManager>();
            var previousSnapshot = await sessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);

            await presenceManager.SignOutAsync(userId, Context.ConnectionAborted);

            snapshot = await sessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
            await UpdateQueueGroupsAsync(previousSnapshot?.QueueIds, snapshot?.QueueIds);
        });

        return snapshot;
    }

    /// <summary>
    /// Updates the current agent's queue and campaign memberships without changing their presence or active work.
    /// </summary>
    /// <param name="queueIds">The queues to remain signed in to.</param>
    /// <param name="campaignIds">The campaigns to remain signed in to.</param>
    /// <returns>The updated agent snapshot.</returns>
    public async Task<AgentDesktopSnapshot> UpdateMemberships(IList<string> queueIds, IList<string> campaignIds)
    {
        var userId = EnsureUserId();
        AgentDesktopSnapshot snapshot = null;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            await EnsureAuthorizedAsync(scope.ServiceProvider, ContactCenterPermissions.SignIntoQueues);

            var sessionService = scope.ServiceProvider.GetRequiredService<IAgentSessionService>();
            var presenceManager = scope.ServiceProvider.GetRequiredService<IAgentPresenceManager>();
            var previousSnapshot = await sessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
            var normalizedQueueIds = ContactCenterFormHelpers.NormalizeList(queueIds);
            var normalizedCampaignIds = ContactCenterFormHelpers.NormalizeList(campaignIds);

            if (normalizedQueueIds.Count == 0 && normalizedCampaignIds.Count == 0)
            {
                throw new HubException("Use sign out to leave the final queue or campaign.");
            }

            var profile = await presenceManager.UpdateMembershipsAsync(
                userId,
                normalizedQueueIds,
                normalizedCampaignIds,
                Context.ConnectionAborted);

            if (profile is null)
            {
                throw new HubException("Sign in before changing queue or campaign memberships.");
            }

            snapshot = await sessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
            await UpdateQueueGroupsAsync(previousSnapshot?.QueueIds, snapshot?.QueueIds);
        });

        return snapshot;
    }

    /// <summary>
    /// Re-checks the signed-in queues for already-waiting inbound voice work.
    /// </summary>
    /// <returns>The number of offers attempted.</returns>
    public async Task<int> SyncQueuedVoiceWork()
    {
        var userId = EnsureUserId();
        var offered = 0;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            await EnsureAuthorizedAsync(scope.ServiceProvider, ContactCenterPermissions.SignIntoQueues);

            var queuedVoiceWorkOfferService = scope.ServiceProvider.GetServices<IQueuedVoiceWorkOfferService>().FirstOrDefault();

            if (queuedVoiceWorkOfferService is not null)
            {
                offered = await queuedVoiceWorkOfferService.OfferForUserAsync(userId, Context.ConnectionAborted);
            }
        });

        return offered;
    }

    /// <summary>
    /// Gets the current pending inbound offer so the soft-phone modal can restore it after reconnecting.
    /// </summary>
    /// <returns>The current pending inbound offer, or <see langword="null"/> when none exists.</returns>
    public async Task<PendingIncomingCallOffer> GetCurrentIncomingOffer()
    {
        var userId = EnsureUserId();
        PendingIncomingCallOffer offer = null;

        await ShellScope.UsingChildScopeAsync(async scope =>
        {
            await EnsureAuthorizedAsync(scope.ServiceProvider, ContactCenterPermissions.SignIntoQueues);

            var pendingIncomingCallOfferService = scope.ServiceProvider.GetServices<IPendingIncomingCallOfferService>().FirstOrDefault();

            if (pendingIncomingCallOfferService is not null)
            {
                offer = await pendingIncomingCallOfferService.GetForUserAsync(userId, Context.ConnectionAborted);
            }
        });

        return offer;
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

    private string EnsureUserId()
    {
        var userId = Context.UserIdentifier;

        if (string.IsNullOrEmpty(userId))
        {
            throw new HubException("The current connection is not associated with an authenticated Contact Center user.");
        }

        return userId;
    }

    private async Task EnsureAuthorizedAsync(IServiceProvider services, Permission permission)
    {
        if (!await AuthorizeAsync(services, permission))
        {
            throw new HubException($"The current user is not authorized for '{permission.Name}'.");
        }
    }

    private async Task UpdateQueueGroupsAsync(IEnumerable<string> previousQueueIds, IEnumerable<string> currentQueueIds)
    {
        var previous = new HashSet<string>(previousQueueIds ?? [], StringComparer.OrdinalIgnoreCase);
        var current = new HashSet<string>(currentQueueIds ?? [], StringComparer.OrdinalIgnoreCase);

        foreach (var queueId in current.Except(previous))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, QueueGroup(queueId), Context.ConnectionAborted);
        }

        foreach (var queueId in previous.Except(current))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, QueueGroup(queueId), Context.ConnectionAborted);
        }
    }
}
