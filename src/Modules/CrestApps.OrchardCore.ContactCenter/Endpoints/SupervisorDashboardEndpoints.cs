using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
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
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static class SupervisorDashboardEndpoints
{
    private const int AgentPageSize = 200;

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
        UserManager<IUser> userManager,
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
        var queues = await queueManager.ListEnabledAsync(httpContext.RequestAborted);
        var monitoringService = monitoringServices.FirstOrDefault();
        var supervisorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var authorizedQueueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(supervisorId))
        {
            return TypedResults.Forbid();
        }

        foreach (var queue in queues)
        {
            if (!await supervisorQueueAuthorizationService.IsAuthorizedAsync(
                httpContext.User,
                supervisorId,
                queue.ItemId,
                httpContext.RequestAborted))
            {
                continue;
            }

            authorizedQueueIds.Add(queue.ItemId);

            var waitingCount = await queueItemManager.CountWaitingAsync(queue.ItemId, httpContext.RequestAborted);
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

        foreach (var agent in agents)
        {
            if (!agent.QueueIds.Any(authorizedQueueIds.Contains))
            {
                continue;
            }

            var activeInteraction = await interactionManager.FindActiveByAgentAsync(agent.ItemId, httpContext.RequestAborted);
            var canMonitorActiveInteraction = activeInteraction is not null &&
                await supervisorQueueAuthorizationService.IsAuthorizedAsync(
                    httpContext.User,
                    supervisorId,
                    activeInteraction.QueueId,
                    httpContext.RequestAborted);
            var activeInteractions = canMonitorActiveInteraction
                ? await interactionManager.CountActiveByAgentAsync(agent.ItemId, httpContext.RequestAborted)
                : 0;
            var availableMonitoringModes = activeInteraction is null || monitoringService is null || !canMonitorActiveInteraction
                ? []
                : await monitoringService.GetAvailableModesAsync(activeInteraction.ItemId, httpContext.RequestAborted);

            model.Agents.Add(new SupervisorAgentViewModel
            {
                AgentId = agent.ItemId,
                UserId = agent.UserId,
                DisplayName = await GetAgentDisplayNameAsync(agent, userManager, displayNameProvider, httpContext.RequestAborted),
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

    private static async Task<string> GetAgentDisplayNameAsync(
        AgentProfile agent,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(agent.UserId))
        {
            var user = await userManager.FindByIdAsync(agent.UserId);

            if (user is not null)
            {
                var displayName = await displayNameProvider.GetAsync(user, cancellationToken);

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }
        }

        return string.IsNullOrWhiteSpace(agent.DisplayName) ? "Unknown agent" : agent.DisplayName;
    }

    private sealed class EngageRequest
    {
        public string InteractionId { get; set; }

        public MonitorMode Mode { get; set; }
    }
}
