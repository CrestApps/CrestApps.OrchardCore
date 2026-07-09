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
    private const int MaxAgents = 200;

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

        var queues = await queueManager.ListEnabledAsync(httpContext.RequestAborted);

        foreach (var queue in queues)
        {
            var waiting = await queueItemManager.ListWaitingAsync(queue.ItemId, httpContext.RequestAborted);
            var longestWaitSeconds = 0;
            var slaBreachCount = 0;

            foreach (var item in waiting)
            {
                var waitSeconds = (int)Math.Max(0, (now - item.EnqueuedUtc).TotalSeconds);
                longestWaitSeconds = Math.Max(longestWaitSeconds, waitSeconds);

                if (queue.SlaThresholdSeconds > 0 && waitSeconds > queue.SlaThresholdSeconds)
                {
                    slaBreachCount++;
                }
            }

            model.Queues.Add(new SupervisorQueueViewModel
            {
                Id = queue.ItemId,
                Name = queue.Name,
                WaitingCount = waiting.Count,
                LongestWaitSeconds = longestWaitSeconds,
                SlaBreachCount = slaBreachCount,
                SlaThresholdSeconds = queue.SlaThresholdSeconds,
            });

            model.TotalWaiting += waiting.Count;
        }

        var agents = await agentManager.PageAsync(1, MaxAgents, new QueryContext(), httpContext.RequestAborted);

        foreach (var agent in agents.Entries)
        {
            var activeInteractions = await interactionManager.CountActiveByAgentAsync(agent.ItemId, httpContext.RequestAborted);
            var activeInteraction = await interactionManager.FindActiveByAgentAsync(agent.ItemId, httpContext.RequestAborted);

            model.Agents.Add(new SupervisorAgentViewModel
            {
                AgentId = agent.ItemId,
                UserId = agent.UserId,
                DisplayName = await GetAgentDisplayNameAsync(agent, userManager, displayNameProvider, httpContext.RequestAborted),
                PresenceStatus = agent.PresenceStatus.ToString(),
                PresenceReason = agent.PresenceReason,
                QueueCount = agent.QueueIds.Count,
                ActiveInteractions = activeInteractions,
                ActiveInteractionId = activeInteraction?.ItemId,
            });

            if (agent.PresenceStatus == AgentPresenceStatus.Available)
            {
                model.AvailableAgents++;
            }
        }

        return TypedResults.Ok(model);
    }

    private static async Task<IResult> HandleEngageAsync(
        [FromForm] EngageRequest request,
        IAuthorizationService authorizationService,
        IAntiforgery antiforgery,
        IEnumerable<IContactCenterMonitoringService> monitoringServices,
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

        var result = await monitoringService.EngageAsync(request.InteractionId, supervisorId, request.Mode, httpContext.RequestAborted);

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

        return string.IsNullOrEmpty(agent.DisplayName) ? agent.UserName : agent.DisplayName;
    }

    private sealed class EngageRequest
    {
        public string InteractionId { get; set; }

        public MonitorMode Mode { get; set; }
    }
}
