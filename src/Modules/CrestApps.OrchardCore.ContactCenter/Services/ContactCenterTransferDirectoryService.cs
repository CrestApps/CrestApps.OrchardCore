using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Builds the soft phone's transfer directory from the Contact Center's own agents and queues.
/// </summary>
internal sealed class ContactCenterTransferDirectoryService : IContactCenterTransferDirectoryService
{
    private readonly IAgentProfileManager _agentManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly ITelephonyExtensionManager _extensionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ISiteService _siteService;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly ITelephonyProviderResolver _telephonyProviderResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTransferDirectoryService"/> class.
    /// </summary>
    public ContactCenterTransferDirectoryService(
        IAgentProfileManager agentManager,
        IInteractionManager interactionManager,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IEnumerable<ITelephonyExtensionManager> extensionManagers,
        IAuthorizationService authorizationService,
        ISiteService siteService,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        ITelephonyProviderResolver telephonyProviderResolver)
    {
        _telephonyProviderResolver = telephonyProviderResolver;
        _agentManager = agentManager;
        _interactionManager = interactionManager;
        _queueManager = queueManager;
        _queueItemManager = queueItemManager;
        _extensionManager = extensionManagers?.FirstOrDefault();
        _authorizationService = authorizationService;
        _siteService = siteService;
        _voiceProviderResolver = voiceProviderResolver;
    }

    /// <inheritdoc />
    public async Task<SoftPhoneTransferDirectory> GetAsync(string userId, ClaimsPrincipal principal, string providerName, CancellationToken cancellationToken = default)
    {
        // A consult needs both halves: the Contact Center adapter that places it, and a telephony provider that holds
        // the caller for it and reports the destination answering. With only the first, the agent would watch the
        // consult ring forever with Complete never enabled.
        var telephonyProvider = await _telephonyProviderResolver.GetAsync(providerName);
        var directory = new SoftPhoneTransferDirectory
        {
            SupportsConsult = _voiceProviderResolver.Get(providerName) is IContactCenterVoiceAttendedTransferProvider &&
                telephonyProvider is not null &&
                telephonyProvider.Capabilities.HasFlag(TelephonyCapabilities.AttendedTransfer),
        };

        await AddAgentsAsync(directory, userId, cancellationToken);
        await AddQueuesAsync(directory, cancellationToken);
        await AddExternalDestinationsAsync(directory, principal);

        return directory;
    }

    private async Task AddAgentsAsync(SoftPhoneTransferDirectory directory, string userId, CancellationToken cancellationToken)
    {
        var agents = (await _agentManager.GetAllAsync(cancellationToken))
            .Where(agent => agent is not null && !string.IsNullOrEmpty(agent.ItemId) && !string.Equals(agent.UserId, userId, StringComparison.Ordinal))
            .ToArray();

        if (agents.Length == 0)
        {
            return;
        }

        // One query for everybody's load rather than one per agent: the panel is opened mid-call, and the agent is
        // waiting on it with a customer on the line.
        var activeCounts = await _interactionManager.CountActiveByAgentIdsAsync(agents.Select(agent => agent.ItemId).ToArray(), cancellationToken);
        var extensions = await GetExtensionsByUserAsync(cancellationToken);

        foreach (var agent in agents.OrderBy(DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            var active = activeCounts is not null && activeCounts.TryGetValue(agent.ItemId, out var count) ? count : 0;

            directory.Agents.Add(new SoftPhoneTransferAgent
            {
                Id = agent.ItemId,
                Name = DisplayName(agent),
                Extension = !string.IsNullOrEmpty(agent.UserId) && extensions.TryGetValue(agent.UserId, out var extension) ? extension : null,
                Presence = agent.PresenceStatus,

                // What an offer would find: an agent who is Available with room for another call. The transfer
                // itself checks again, so this only decides what the panel shows.
                Available = agent.PresenceStatus == AgentPresenceStatus.Available &&
                    active < Math.Max(1, agent.MaxConcurrentInteractions),
            });
        }
    }

    private async Task<Dictionary<string, string>> GetExtensionsByUserAsync(CancellationToken cancellationToken)
    {
        var extensions = new Dictionary<string, string>(StringComparer.Ordinal);

        if (_extensionManager is null)
        {
            return extensions;
        }

        foreach (var extension in await _extensionManager.GetAllAsync(cancellationToken))
        {
            if (!string.IsNullOrEmpty(extension?.UserId) && !string.IsNullOrEmpty(extension.Number))
            {
                extensions.TryAdd(extension.UserId, extension.Number);
            }
        }

        return extensions;
    }

    private async Task AddQueuesAsync(SoftPhoneTransferDirectory directory, CancellationToken cancellationToken)
    {
        var queues = (await _queueManager.GetEnabledAsync(cancellationToken))
            .Where(queue => queue is not null && queue.Enabled && !ContactCenterConstants.IsDirectRoutingQueue(queue.ItemId))
            .ToArray();

        if (queues.Length == 0)
        {
            return;
        }

        var waiting = await _queueItemManager.CountWaitingByQueueIdsAsync(queues.Select(queue => queue.ItemId).ToArray(), cancellationToken);

        foreach (var queue in queues.OrderBy(queue => queue.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            directory.Queues.Add(new SoftPhoneTransferQueue
            {
                Id = queue.ItemId,
                Name = string.IsNullOrWhiteSpace(queue.Name) ? queue.ItemId : queue.Name,
                Waiting = waiting is not null && waiting.TryGetValue(queue.ItemId, out var count) ? count : 0,
            });
        }
    }

    private async Task AddExternalDestinationsAsync(SoftPhoneTransferDirectory directory, ClaimsPrincipal principal)
    {
        // The same permission the transfer checks, so the panel never offers a destination the transfer refuses.
        directory.CanTransferExternally = principal is not null &&
            await _authorizationService.AuthorizeAsync(principal, ContactCenterPermissions.TransferExternally);

        if (!directory.CanTransferExternally)
        {
            return;
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<ContactCenterExternalTransferSettings>();

        directory.AllowExternalNumbers = settings.AllowUnlistedNumbers;

        foreach (var destination in settings.Destinations.Where(destination => destination is not null && destination.Enabled))
        {
            directory.ExternalDestinations.Add(new SoftPhoneTransferExternalDestination
            {
                Id = destination.Id,
                Name = destination.DisplayName,
                Number = destination.E164Address,
            });
        }
    }

    private static string DisplayName(AgentProfile agent)
        => !string.IsNullOrWhiteSpace(agent.DisplayName)
            ? agent.DisplayName
            : !string.IsNullOrWhiteSpace(agent.UserName) ? agent.UserName : agent.Name ?? agent.ItemId;
}

/// <summary>
/// Lists where an agent can transfer the call they are on.
/// </summary>
internal interface IContactCenterTransferDirectoryService
{
    /// <summary>
    /// Builds the directory for the requesting agent.
    /// </summary>
    /// <param name="userId">The requesting user, who is left out of the agents listed.</param>
    /// <param name="principal">The requesting principal, whose permissions decide whether outside numbers are offered.</param>
    /// <param name="providerName">The provider carrying the call, which decides whether a warm transfer is possible.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The directory.</returns>
    Task<SoftPhoneTransferDirectory> GetAsync(string userId, ClaimsPrincipal principal, string providerName, CancellationToken cancellationToken = default);
}
