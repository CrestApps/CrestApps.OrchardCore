using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class SupervisorDashboardEndpoints
{
    private const int AgentPageSize = 200;
    private const int UserQueryBatchSize = 500;

    public const string StateRouteName = "ContactCenterSupervisorDashboardState";
    public const string EngageRouteName = "ContactCenterSupervisorDashboardEngage";

    public static IEndpointRouteBuilder AddSupervisorDashboardEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("Admin/contact-center/dashboard/state", HandleStateAsync)
            .WithName(StateRouteName);

        builder.MapPost("Admin/contact-center/dashboard/engage", HandleEngageAsync)
            .WithName(EngageRouteName);

        return builder;
    }

    private static async Task<IResult> HandleStateAsync(
        IAuthorizationService authorizationService,
        IActivityQueueManager queueManager,
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IInteractionManager interactionManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        IEnumerable<IContactCenterMonitoringService> monitoringServices,
        ISession session,
        IDisplayNameProvider displayNameProvider,
        IClock clock,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
        {
            return TypedResults.Forbid();
        }

        var now = clock.UtcNow;
        var model = new SupervisorDashboardStateViewModel
        {
            ServerTimeUtc = now,
        };

        var agents = await ListAgentsAsync(agentManager, httpContext.RequestAborted);
        var queues = await queueManager.GetEnabledAsync(httpContext.RequestAborted);
        var monitoringService = monitoringServices.FirstOrDefault();
        var supervisorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var authorizedQueueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queueAuthorizationCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(supervisorId))
        {
            return TypedResults.Forbid();
        }

        foreach (var queue in queues)
        {
            if (!await IsQueueAuthorizedAsync(
                supervisorQueueAuthorizationService,
                queueAuthorizationCache,
                httpContext.User,
                supervisorId,
                queue.ItemId,
                httpContext.RequestAborted))
            {
                continue;
            }

            authorizedQueueIds.Add(queue.ItemId);
        }

        // Waiting depth is read for every authorized queue on every poll, so it is loaded in a single batched
        // query rather than one query per queue.
        var waitingCounts = authorizedQueueIds.Count == 0
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : await queueItemManager.CountWaitingByQueueIdsAsync(authorizedQueueIds, httpContext.RequestAborted);

        foreach (var queue in queues)
        {
            if (!authorizedQueueIds.Contains(queue.ItemId))
            {
                continue;
            }

            var waitingCount = waitingCounts.TryGetValue(queue.ItemId, out var count) ? count : 0;
            var longestWaiting = await queueItemManager.FindLongestWaitingAsync(queue.ItemId, httpContext.RequestAborted);
            var signedInAgents = agents
                .Where(agent => agent.QueueIds.Contains(queue.ItemId, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            var longestWaitSeconds = longestWaiting is null
                ? 0
                : (int)Math.Max(0, (now - longestWaiting.EnqueuedUtc).TotalSeconds);
            var slaBreachCount = queue.SlaThresholdSeconds > 0
                ? await queueItemManager.CountWaitingOlderThanAsync(
                    queue.ItemId,
                    now.AddSeconds(-queue.SlaThresholdSeconds),
                    httpContext.RequestAborted)
                : 0;

            model.Queues.Add(new SupervisorQueueViewModel
            {
                Id = queue.ItemId,
                Name = queue.Name,
                WaitingCount = waitingCount,
                SignedInAgentCount = signedInAgents.Length,
                AvailableAgentCount = signedInAgents.Count(agent => agent.PresenceStatus == AgentPresenceStatus.Available),
                BusyAgentCount = signedInAgents.Count(agent => agent.PresenceStatus is AgentPresenceStatus.Reserved or AgentPresenceStatus.Busy or AgentPresenceStatus.WrapUp),
                NotReadyAgentCount = signedInAgents.Count(agent => agent.PresenceStatus is not AgentPresenceStatus.Available and not AgentPresenceStatus.Reserved and not AgentPresenceStatus.Busy and not AgentPresenceStatus.WrapUp),
                LongestWaitSeconds = longestWaitSeconds,
                SlaBreachCount = slaBreachCount,
                SlaThresholdSeconds = queue.SlaThresholdSeconds,
            });

            model.TotalWaiting += waitingCount;
        }

        var scopedAgents = agents
            .Where(agent => agent.QueueIds.Any(authorizedQueueIds.Contains))
            .ToArray();
        var scopedAgentIds = scopedAgents
            .Select(agent => agent.ItemId)
            .ToArray();

        // The agent grid previously issued three queries per agent (active interaction, active count, and a
        // user lookup for the display name). Each is now resolved once for the whole scoped set so a poll's
        // cost no longer scales with the number of agents on watch.
        var activeInteractionsByAgent = await ResolveActiveInteractionsAsync(
            interactionManager,
            scopedAgentIds,
            httpContext.RequestAborted);
        var activeInteractionCounts = scopedAgentIds.Length == 0
            ? new Dictionary<string, int>(StringComparer.Ordinal)
            : await interactionManager.CountActiveByAgentIdsAsync(scopedAgentIds, httpContext.RequestAborted);
        var agentDisplayNames = await ResolveAgentDisplayNamesAsync(
            scopedAgents,
            session,
            displayNameProvider,
            httpContext.RequestAborted);

        foreach (var agent in scopedAgents)
        {
            activeInteractionsByAgent.TryGetValue(agent.ItemId, out var activeInteraction);
            var canMonitorActiveInteraction = activeInteraction is not null &&
                await IsQueueAuthorizedAsync(
                    supervisorQueueAuthorizationService,
                    queueAuthorizationCache,
                    httpContext.User,
                    supervisorId,
                    activeInteraction.QueueId,
                    httpContext.RequestAborted);
            var activeInteractions = canMonitorActiveInteraction
                && activeInteractionCounts.TryGetValue(agent.ItemId, out var activeCount)
                ? activeCount
                : 0;
            var availableMonitoringModes = activeInteraction is null || monitoringService is null || !canMonitorActiveInteraction
                ? []
                : await monitoringService.GetAvailableModesAsync(activeInteraction, httpContext.RequestAborted);

            model.Agents.Add(new SupervisorAgentViewModel
            {
                AgentId = agent.ItemId,
                UserId = agent.UserId,
                DisplayName = agentDisplayNames.TryGetValue(agent.ItemId, out var displayName) ? displayName : "Unknown agent",
                PresenceStatus = agent.PresenceStatus.ToString(),
                PresenceReason = agent.PresenceReason,
                QueueCount = agent.QueueIds.Count,
                ActiveInteractions = activeInteractions,
                ActiveInteractionId = canMonitorActiveInteraction ? activeInteraction?.ItemId : null,
                AvailableMonitoringModes = availableMonitoringModes
                    .Select(mode => mode.ToString())
                    .ToArray(),
            });

            if (agent.PresenceStatus == AgentPresenceStatus.Available)
            {
                model.AvailableAgents++;
            }
        }

        return TypedResults.Ok(model);
    }

    private static async Task<bool> IsQueueAuthorizedAsync(
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        Dictionary<string, bool> cache,
        ClaimsPrincipal principal,
        string supervisorId,
        string queueId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return false;
        }

        if (cache.TryGetValue(queueId, out var authorized))
        {
            return authorized;
        }

        // Every authorization check resolves the same supervisor profile, so the result is memoized per queue
        // for the request. This keeps the per-agent monitoring gate from reissuing the supervisor lookup once
        // for each busy agent on every poll.
        authorized = await supervisorQueueAuthorizationService.IsAuthorizedAsync(
            principal,
            supervisorId,
            queueId,
            cancellationToken);
        cache[queueId] = authorized;

        return authorized;
    }

    private static async Task<IReadOnlyDictionary<string, Interaction>> ResolveActiveInteractionsAsync(
        IInteractionManager interactionManager,
        string[] agentIds,
        CancellationToken cancellationToken)
    {
        var activeInteractionsByAgent = new Dictionary<string, Interaction>(StringComparer.Ordinal);

        if (agentIds.Length == 0)
        {
            return activeInteractionsByAgent;
        }

        var interactions = await interactionManager.GetActiveByAgentIdsAsync(agentIds, cancellationToken);

        foreach (var interaction in interactions)
        {
            if (string.IsNullOrEmpty(interaction.AgentId))
            {
                continue;
            }

            if (!activeInteractionsByAgent.TryGetValue(interaction.AgentId, out var existing)
                || interaction.CreatedUtc > existing.CreatedUtc)
            {
                activeInteractionsByAgent[interaction.AgentId] = interaction;
            }
        }

        return activeInteractionsByAgent;
    }

    private static async Task<IReadOnlyDictionary<string, string>> ResolveAgentDisplayNamesAsync(
        AgentProfile[] agents,
        ISession session,
        IDisplayNameProvider displayNameProvider,
        CancellationToken cancellationToken)
    {
        var displayNames = new Dictionary<string, string>(StringComparer.Ordinal);

        if (agents.Length == 0)
        {
            return displayNames;
        }

        var userIds = agents
            .Where(agent => !string.IsNullOrEmpty(agent.UserId))
            .Select(agent => agent.UserId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var usersById = new Dictionary<string, IUser>(StringComparer.Ordinal);

        // Resolve every agent's user in bounded batches rather than one lookup per agent; the display-name
        // provider then works from the already-materialized user without touching the database again.
        foreach (var userIdBatch in userIds.Chunk(UserQueryBatchSize))
        {
            var users = await session.Query<User, UserIndex>(index => index.UserId.IsIn(userIdBatch))
                .ListAsync(cancellationToken);

            foreach (var user in users)
            {
                usersById[user.UserId] = user;
            }
        }

        foreach (var agent in agents)
        {
            string displayName = null;

            if (!string.IsNullOrEmpty(agent.UserId) && usersById.TryGetValue(agent.UserId, out var user))
            {
                displayName = await displayNameProvider.GetAsync(user, cancellationToken);
            }

            displayNames[agent.ItemId] = string.IsNullOrWhiteSpace(displayName)
                ? (string.IsNullOrWhiteSpace(agent.DisplayName) ? "Unknown agent" : agent.DisplayName)
                : displayName;
        }

        return displayNames;
    }

    private static async Task<IReadOnlyCollection<AgentProfile>> ListAgentsAsync(
        IAgentProfileManager agentManager,
        CancellationToken cancellationToken)
    {
        var agents = new List<AgentProfile>();
        var page = 1;

        while (true)
        {
            var result = await agentManager.PageAsync(page, AgentPageSize, new QueryContext(), cancellationToken);
            agents.AddRange(result.Entries);

            if (result.Entries.Count < AgentPageSize)
            {
                return agents;
            }

            page++;
        }
    }

    private static async Task<IResult> HandleEngageAsync(
        [FromForm] EngageRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IEnumerable<IContactCenterMonitoringService> monitoringServices,
        IInteractionManager interactionManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
        {
            return TypedResults.Forbid();
        }

        if (!await ContactCenterEndpointAntiforgery.ValidateRequestAsync(antiforgery, httpContext))
        {
            return TypedResults.BadRequest();
        }

        if (string.IsNullOrEmpty(request.InteractionId))
        {
            return TypedResults.BadRequest();
        }

        var monitoringService = monitoringServices.FirstOrDefault();

        if (monitoringService is null)
        {
            return TypedResults.BadRequest();
        }

        var supervisorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(supervisorId))
        {
            return TypedResults.Forbid();
        }

        var interaction = await interactionManager.FindByIdAsync(request.InteractionId, httpContext.RequestAborted);

        if (interaction is null ||
            !await supervisorQueueAuthorizationService.IsAuthorizedAsync(
                httpContext.User,
                supervisorId,
                interaction.QueueId,
                httpContext.RequestAborted))
        {
            return TypedResults.NotFound();
        }

        var result = await monitoringService.EngageAsync(
            request.InteractionId,
            supervisorId,
            httpContext.User,
            request.Mode,
            httpContext.RequestAborted);

        return TypedResults.Ok(new
        {
            result.Succeeded,
            ErrorMessage = result.Reason,
        });
    }

    private sealed class EngageRequest
    {
        public string InteractionId { get; set; }

        public MonitorMode Mode { get; set; }
    }
}
