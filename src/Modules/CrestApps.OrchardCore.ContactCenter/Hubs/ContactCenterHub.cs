using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Hubs;

/// <summary>
/// SignalR hub that powers the real-time Contact Center experience. Agents connect to receive presence,
/// offer, and queue updates, while supervisors connect to monitor queues and agents. Each invocation runs
/// in its own OrchardCore shell scope and is authorized against Contact Center permissions.
/// </summary>
[Authorize]
public sealed class ContactCenterHub : Hub<IContactCenterHubClient>
{
    private const string WorkLeaseKey = "ContactCenterFeatureWorkLease";

    /// <summary>
    /// The name of the SignalR group that receives supervisor-wide updates.
    /// </summary>
    public const string SupervisorsGroup = "cc:supervisors";

    private readonly ILogger _logger;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IContactCenterFeatureWorkManager _workManager;
    private readonly ContactCenterHubConnectionRegistry _connectionRegistry;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterHub"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="scopeExecutor">The executor used to isolate hub operations in child shell scopes.</param>
    /// <param name="workManager">The feature work manager.</param>
    /// <param name="connectionRegistry">The tenant-local hub connection registry.</param>
    /// <param name="shellSettings">The current Orchard shell settings.</param>
    public ContactCenterHub(
        ILogger<ContactCenterHub> logger,
        IContactCenterScopeExecutor scopeExecutor,
        IContactCenterFeatureWorkManager workManager,
        ContactCenterHubConnectionRegistry connectionRegistry,
        ShellSettings shellSettings)
    {
        _logger = logger;
        _scopeExecutor = scopeExecutor;
        _workManager = workManager;
        _connectionRegistry = connectionRegistry;
        _tenantName = shellSettings.Name;
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
        var workLease = _workManager.TryEnter(ContactCenterConstants.Feature.RealTime);

        if (workLease is null)
        {
            Context.Abort();

            return;
        }

        Context.Items[WorkLeaseKey] = workLease;

        if (!_connectionRegistry.Register(Context))
        {
            ReleaseConnectionWork();

            return;
        }

        var userId = Context.UserIdentifier;

        if (string.IsNullOrEmpty(userId))
        {
            Context.Abort();

            return;
        }

        var authorized = false;

        await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            if (await AuthorizeAsync(services, ContactCenterPermissions.SignIntoQueues))
            {
                authorized = true;

                try
                {
                    var userName = Context.User?.Identity?.Name;
                    var displayName = await GetDisplayNameAsync(services, userName);

                    var session = await services.SessionService.ConnectAsync(
                        userId,
                        Context.ConnectionId,
                        userName,
                        displayName,
                        Context.ConnectionAborted);

                    foreach (var queueId in session.QueueIds)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, GetQueueGroup(queueId), Context.ConnectionAborted);
                    }

                    var snapshot = await services.SessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
                    await UpdateQueueGroupsAsync(session.QueueIds, snapshot.QueueIds);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        OperationalLogRedactor.RedactException(ex),
                        "An error occurred while registering the Contact Center connection for user '{UserId}'.",
                        OperationalLogRedactor.Pseudonymize(userId, OperationalLogIdentifierCategory.User));
                }
            }

            if (await AuthorizeAsync(services, ContactCenterPermissions.MonitorContactCenter))
            {
                authorized = true;

                await Groups.AddToGroupAsync(Context.ConnectionId, GetSupervisorsGroup(), Context.ConnectionAborted);
            }
        });

        if (!authorized)
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

    /// <inheritdoc/>
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        try
        {
            var userId = Context.UserIdentifier;

            if (!string.IsNullOrEmpty(userId))
            {
                await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
                {
                    try
                    {
                        await services.SessionService.DisconnectAsync(userId, Context.ConnectionId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            OperationalLogRedactor.RedactException(ex),
                            "An error occurred while removing the Contact Center connection for user '{UserId}'.",
                            OperationalLogRedactor.Pseudonymize(userId, OperationalLogIdentifierCategory.User));
                    }
                });
            }

            await base.OnDisconnectedAsync(exception);
        }
        finally
        {
            _connectionRegistry.Unregister(Context.ConnectionId);
            ReleaseConnectionWork();
        }
    }

    private void ReleaseConnectionWork()
    {
        if (Context.Items.Remove(WorkLeaseKey, out var workLease) &&
            workLease is IContactCenterFeatureWorkLease featureWorkLease)
        {
            featureWorkLease.Dispose();
        }
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

        return _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            await services.SessionService.HeartbeatAsync(userId, Context.ConnectionAborted);
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

        await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            snapshot = await services.SessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
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

        await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            if (await AuthorizeAsync(services, ContactCenterPermissions.MonitorContactCenter))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, GetQueueGroup(queueId), Context.ConnectionAborted);
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

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetQueueGroup(queueId), Context.ConnectionAborted);
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

        await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            await EnsureAuthorizedAsync(services, ContactCenterPermissions.SignIntoQueues);

            var previousSnapshot = await services.SessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
            var normalizedQueueIds = ContactCenterFormHelpers.NormalizeList(queueIds);
            var normalizedCampaignIds = ContactCenterFormHelpers.NormalizeList(campaignIds);

            if (normalizedQueueIds.Count == 0 && normalizedCampaignIds.Count == 0)
            {
                throw new HubException("Select at least one queue or campaign before signing in.");
            }

            try
            {
                await services.PresenceManager.SignInAsync(
                    userId,
                    normalizedQueueIds,
                    normalizedCampaignIds,
                    Context.ConnectionAborted);
            }
            catch (AgentEntitlementDeniedException exception)
            {
                throw new HubException(exception.Message);
            }

            snapshot = await services.SessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
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

        await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            await EnsureAuthorizedAsync(services, ContactCenterPermissions.SignIntoQueues);

            var previousSnapshot = await services.SessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);

            await services.PresenceManager.SignOutAsync(userId, Context.ConnectionAborted);

            snapshot = await services.SessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
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

        await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            await EnsureAuthorizedAsync(services, ContactCenterPermissions.SignIntoQueues);

            var previousSnapshot = await services.SessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
            var normalizedQueueIds = ContactCenterFormHelpers.NormalizeList(queueIds);
            var normalizedCampaignIds = ContactCenterFormHelpers.NormalizeList(campaignIds);

            if (normalizedQueueIds.Count == 0 && normalizedCampaignIds.Count == 0)
            {
                throw new HubException("Use sign out to leave the final queue or campaign.");
            }

            AgentProfile profile;

            try
            {
                profile = await services.PresenceManager.UpdateMembershipsAsync(
                    userId,
                    normalizedQueueIds,
                    normalizedCampaignIds,
                    Context.ConnectionAborted);
            }
            catch (AgentEntitlementDeniedException exception)
            {
                throw new HubException(exception.Message);
            }

            if (profile is null)
            {
                throw new HubException("Sign in before changing queue or campaign memberships.");
            }

            snapshot = await services.SessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
            await UpdateQueueGroupsAsync(previousSnapshot?.QueueIds, snapshot?.QueueIds);
        });

        return snapshot;
    }

    /// <summary>
    /// Changes the current agent's presence without reloading the page.
    /// </summary>
    /// <param name="status">The requested presence status.</param>
    /// <param name="reason">The optional reason code.</param>
    /// <returns>The updated agent snapshot.</returns>
    public async Task<AgentDesktopSnapshot> SetPresence(AgentPresenceStatus status, string reason)
    {
        var userId = EnsureUserId();
        AgentDesktopSnapshot snapshot = null;

        await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            await EnsureAuthorizedAsync(services, ContactCenterPermissions.SignIntoQueues);

            await services.PresenceManager.SetPresenceAsync(
                userId,
                status,
                reason,
                Context.ConnectionAborted);

            snapshot = await services.SessionService.BuildSnapshotAsync(userId, Context.ConnectionAborted);
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

        await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            await EnsureAuthorizedAsync(services, ContactCenterPermissions.SignIntoQueues);

            if (services.QueuedVoiceWorkOfferService is not null)
            {
                offered = await services.QueuedVoiceWorkOfferService.OfferForUserAsync(
                    userId,
                    Context.ConnectionAborted);
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

        await _scopeExecutor.ExecuteAsync<ContactCenterHubScopeContext>(async services =>
        {
            await EnsureAuthorizedAsync(services, ContactCenterPermissions.SignIntoQueues);

            if (services.PendingIncomingCallOfferService is not null)
            {
                offer = await services.PendingIncomingCallOfferService.GetForUserAsync(
                    userId,
                    Context.ConnectionAborted);
            }
        });

        return offer;
    }

    private async Task<bool> AuthorizeAsync(
        ContactCenterHubScopeContext services,
        Permission permission)
    {
        var httpContext = Context.GetHttpContext();

        if (httpContext?.User is null)
        {
            return false;
        }

        return await services.AuthorizationService.AuthorizeAsync(httpContext.User, permission);
    }

    private async Task<string> GetDisplayNameAsync(
        ContactCenterHubScopeContext services,
        string fallback)
    {
        var user = await services.UserManager.GetUserAsync(Context.User);

        if (user is not null)
        {
            var displayName = await services.DisplayNameProvider.GetAsync(user, Context.ConnectionAborted);

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

    private async Task EnsureAuthorizedAsync(
        ContactCenterHubScopeContext services,
        Permission permission)
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
            await Groups.AddToGroupAsync(Context.ConnectionId, GetQueueGroup(queueId), Context.ConnectionAborted);
        }

        foreach (var queueId in previous.Except(current))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetQueueGroup(queueId), Context.ConnectionAborted);
        }
    }

    private string GetQueueGroup(string queueId)
    {
        return TenantSignalRGroupName.ForGroup(_tenantName, QueueGroup(queueId));
    }

    private string GetSupervisorsGroup()
    {
        return TenantSignalRGroupName.ForGroup(_tenantName, SupervisorsGroup);
    }
}
