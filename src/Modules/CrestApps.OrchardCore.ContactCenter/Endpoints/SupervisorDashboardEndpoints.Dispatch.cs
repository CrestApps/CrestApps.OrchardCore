using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static partial class SupervisorDashboardEndpoints
{
    // Diagnosing a campaign reads its pending records and the dispatcher's recent decisions, which costs far more than the
    // state poll; a supervisor's polls, and every supervisor's, share one reading for this long.
    private static readonly TimeSpan _dispatchCacheDuration = TimeSpan.FromSeconds(20);

    internal static async Task<IResult> HandleDispatchAsync(
        IAuthorizationService authorizationService,
        IActivityQueueManager queueManager,
        IAgentProfileManager agentManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        IDialerDispatchDiagnosticsService diagnosticsService,
        IMemoryCache cache,
        IClock clock,
        HttpContext httpContext)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, ContactCenterPermissions.MonitorContactCenter))
        {
            return TypedResults.Forbid();
        }

        var supervisorId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(supervisorId))
        {
            return TypedResults.Forbid();
        }

        var cancellationToken = httpContext.RequestAborted;
        var agents = await ListAgentsAsync(agentManager, cancellationToken);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var queue in await queueManager.GetEnabledAsync(cancellationToken))
        {
            names[queue.ItemId] = queue.Name;
        }

        // A campaign is a queue of its own that is never stored. Every campaign is a candidate, not only those an agent is
        // signed in to: a campaign with work pending and nobody signed in is exactly the one to explain.
        if (httpContext.RequestServices.GetService<ICatalogManager<OmnichannelCampaign>>() is { } campaignManager)
        {
            foreach (var campaign in await campaignManager.GetAllAsync(cancellationToken))
            {
                if (campaign is not null && !string.IsNullOrEmpty(campaign.ItemId))
                {
                    names[ContactCenterConstants.CampaignQueue.CreateId(campaign.ItemId)] =
                        string.IsNullOrWhiteSpace(campaign.DisplayText) ? campaign.ItemId : campaign.DisplayText;
                }
            }
        }

        foreach (var campaignQueueId in agents.SelectMany(CampaignQueueIds))
        {
            names.TryAdd(campaignQueueId, ContactCenterConstants.CampaignQueue.GetCampaignId(campaignQueueId));
        }

        var authorizationCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var authorizedQueueIds = new List<string>();

        foreach (var queueId in names.Keys)
        {
            if (await IsQueueAuthorizedAsync(
                supervisorQueueAuthorizationService,
                authorizationCache,
                httpContext.User,
                supervisorId,
                queueId,
                cancellationToken))
            {
                authorizedQueueIds.Add(queueId);
            }
        }

        var now = clock.UtcNow;
        var diagnoses = await DiagnoseCachedAsync(diagnosticsService, cache, authorizedQueueIds, cancellationToken);
        var model = new SupervisorDispatchStateViewModel { ServerTimeUtc = now };

        foreach (var diagnosis in diagnoses)
        {
            // Nothing pending is nothing to explain.
            if (diagnosis.PendingCount == 0)
            {
                continue;
            }

            var signedIn = agents
                .Where(agent => ContactCenterConstants.IsCampaignQueue(diagnosis.QueueId)
                    ? CampaignQueueIds(agent).Contains(diagnosis.QueueId, StringComparer.OrdinalIgnoreCase)
                    : agent.QueueIds.Contains(diagnosis.QueueId, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            model.Queues.Add(ToViewModel(
                diagnosis,
                names.TryGetValue(diagnosis.QueueId, out var name) ? name : diagnosis.QueueId,
                signedIn.Length,
                signedIn.Count(agent => agent.PresenceStatus == AgentPresenceStatus.Available)));
        }

        model.Queues = model.Queues
            .OrderByDescending(queue => queue.HeldBackCount + queue.DialableNowCount)
            .ThenBy(queue => queue.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return TypedResults.Ok(model);
    }

    private static async Task<IReadOnlyList<QueueDispatchDiagnostics>> DiagnoseCachedAsync(
        IDialerDispatchDiagnosticsService diagnosticsService,
        IMemoryCache cache,
        IReadOnlyCollection<string> queueIds,
        CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, QueueDispatchDiagnostics>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        foreach (var queueId in queueIds)
        {
            if (cache.TryGetValue(DispatchCacheKey(queueId), out QueueDispatchDiagnostics cached))
            {
                found[queueId] = cached;
            }
            else
            {
                missing.Add(queueId);
            }
        }

        if (missing.Count > 0)
        {
            foreach (var diagnosis in await diagnosticsService.DiagnoseAsync(missing, cancellationToken))
            {
                cache.Set(DispatchCacheKey(diagnosis.QueueId), diagnosis, _dispatchCacheDuration);
                found[diagnosis.QueueId] = diagnosis;
            }
        }

        return queueIds
            .Where(found.ContainsKey)
            .Select(queueId => found[queueId])
            .ToArray();
    }

    private static string DispatchCacheKey(string queueId)
        => $"ContactCenterDispatchDiagnostics:{queueId}";

    internal static SupervisorDispatchViewModel ToViewModel(
        QueueDispatchDiagnostics diagnosis,
        string name,
        int signedInAgentCount,
        int availableAgentCount)
        => new()
        {
            QueueId = diagnosis.QueueId,
            CampaignId = diagnosis.CampaignId,
            Name = name,
            PendingCount = diagnosis.PendingCount,
            EvaluatedCount = diagnosis.EvaluatedCount,
            DialableNowCount = diagnosis.DialableNowCount,
            HeldBackCount = diagnosis.HeldBackCount,
            SignedInAgentCount = signedInAgentCount,
            AvailableAgentCount = availableAgentCount,
            QueueBlocker = diagnosis.QueueBlocker?.ToString(),
            Buckets = diagnosis.Buckets
                .Select(bucket => new SupervisorDispatchBucketViewModel
                {
                    Blocker = bucket.Blocker.ToString(),
                    Count = bucket.Count,
                    NextDueUtc = bucket.NextDueUtc,
                })
                .ToList(),
            Routing = diagnosis.RoutingFinding is null
                ? null
                : new SupervisorDispatchRoutingViewModel
                {
                    OccurredUtc = diagnosis.RoutingFinding.OccurredUtc,
                    Reason = diagnosis.RoutingFinding.Reason,
                    CandidateCount = diagnosis.RoutingFinding.CandidateCount,
                    CandidateReasons = diagnosis.RoutingFinding.CandidateReasons
                        .Select(reason => new SupervisorDispatchCandidateReasonViewModel
                        {
                            Reason = reason.Reason,
                            AgentCount = reason.AgentCount,
                        })
                        .ToList(),
                },
        };
}
