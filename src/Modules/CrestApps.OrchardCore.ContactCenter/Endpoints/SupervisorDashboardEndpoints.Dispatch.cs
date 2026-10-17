using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Endpoints;

internal static partial class SupervisorDashboardEndpoints
{
    // Diagnosing a campaign reads its pending records and the dispatcher's recent decisions, which costs far more than the
    // state poll; a supervisor's polls, and every supervisor's, share one reading for this long. The reading is kept in the
    // distributed cache, keyed by tenant, so every node serves the same one and tenants never see each other's.
    private static readonly JsonSerializerOptions _dispatchSerializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly DistributedCacheEntryOptions _dispatchCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(20),
    };

    internal static async Task<IResult> HandleDispatchAsync(
        IAuthorizationService authorizationService,
        IActivityQueueManager queueManager,
        IAgentProfileManager agentManager,
        ISupervisorQueueAuthorizationService supervisorQueueAuthorizationService,
        IDialerDispatchDiagnosticsService diagnosticsService,
        IStaleAgentWorkService staleWorkService,
        IDistributedCache cache,
        ShellSettings shellSettings,
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

        // What the dialer is doing for each paced campaign with work waiting, when Paced Dialing is on. A campaign the
        // dialer cannot pace is explained even when nobody is signed in to it and its campaign record cannot be read.
        var pacing = await ReadPacingHealthAsync(httpContext.RequestServices, cancellationToken);

        foreach (var queueId in pacing.Keys)
        {
            names.TryAdd(queueId, ContactCenterConstants.CampaignQueue.GetCampaignId(queueId) ?? queueId);
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
        var diagnoses = await DiagnoseCachedAsync(diagnosticsService, cache, shellSettings.Name, authorizedQueueIds, cancellationToken);
        var model = new SupervisorDispatchStateViewModel { ServerTimeUtc = now };

        var explained = diagnoses
            // Nothing pending and nothing waiting is nothing to explain.
            .Where(diagnosis => diagnosis.PendingCount != 0 || diagnosis.WaitingItemCount != 0 || pacing.ContainsKey(diagnosis.QueueId))
            .Select(diagnosis => (
                Diagnosis: diagnosis,
                SignedIn: agents
                    .Where(agent => ContactCenterConstants.IsCampaignQueue(diagnosis.QueueId)
                        ? CampaignQueueIds(agent).Contains(diagnosis.QueueId, StringComparer.OrdinalIgnoreCase)
                        : agent.QueueIds.Contains(diagnosis.QueueId, StringComparer.OrdinalIgnoreCase))
                    .ToArray()))
            .ToArray();

        // An agent who looks available or busy for a stale reason is a reason work is not going out, so the same
        // detection the agent board uses is applied to the agents signed in to the queues being explained.
        var staleWork = await staleWorkService.DetectAsync(
            explained.SelectMany(entry => entry.SignedIn).DistinctBy(agent => agent.ItemId).ToArray(),
            cancellationToken);

        foreach (var (diagnosis, signedIn) in explained)
        {
            var viewModel = ToViewModel(
                diagnosis,
                names.TryGetValue(diagnosis.QueueId, out var name) ? name : diagnosis.QueueId,
                signedIn.Length,
                signedIn.Count(agent => agent.PresenceStatus == AgentPresenceStatus.Available));

            viewModel.StaleAgentCount = signedIn.Count(agent =>
                agent.PresenceStatus is AgentPresenceStatus.Available or AgentPresenceStatus.Reserved or AgentPresenceStatus.Busy &&
                staleWork.ContainsKey(agent.ItemId));
            ApplyPacing(viewModel, pacing.GetValueOrDefault(diagnosis.QueueId));
            model.Queues.Add(viewModel);
        }

        model.Queues = model.Queues
            // A campaign the dialer cannot pace leads: nothing else on its card matters until that is fixed.
            .OrderByDescending(queue => queue.PacingLockNotAcquiredSinceUtc is not null)
            .ThenByDescending(queue => queue.HeldBackCount + queue.DialableNowCount)
            .ThenBy(queue => queue.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return TypedResults.Ok(model);
    }

    private static async Task<IReadOnlyDictionary<string, DialerPacingQueueHealth>> ReadPacingHealthAsync(
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var pacing = new Dictionary<string, DialerPacingQueueHealth>(StringComparer.OrdinalIgnoreCase);

        if (services.GetService<IDialerPacingHealthService>() is not { } healthService)
        {
            return pacing;
        }

        foreach (var queue in (await healthService.EvaluateAsync(cancellationToken)).Queues)
        {
            pacing.TryAdd(queue.QueueId, queue);
        }

        return pacing;
    }

    /// <summary>
    /// Copies what the dialer is doing for a paced campaign onto its card, so the card says when the dialer cannot pace it
    /// rather than leaving dialable records to look as though they wait for the next pass.
    /// </summary>
    /// <param name="viewModel">The campaign's card.</param>
    /// <param name="health">The dialer's state for the campaign's queue, or <see langword="null"/> when it is not paced.</param>
    internal static void ApplyPacing(SupervisorDispatchViewModel viewModel, DialerPacingQueueHealth health)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        if (health is null)
        {
            return;
        }

        viewModel.PacingState = health.State.ToString();
        viewModel.PacingLockNotAcquiredSinceUtc = health.State == DialerPacingHealthState.LockNotAcquired
            ? health.LockNotAcquiredSinceUtc
            : null;
        viewModel.PacingStalledMinutes = health.State == DialerPacingHealthState.Stalled
            ? (int)health.TimeWithoutCompletedCycle.TotalMinutes
            : null;
    }

    private static async Task<IReadOnlyList<QueueDispatchDiagnostics>> DiagnoseCachedAsync(
        IDialerDispatchDiagnosticsService diagnosticsService,
        IDistributedCache cache,
        string tenantName,
        IReadOnlyCollection<string> queueIds,
        CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, QueueDispatchDiagnostics>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();

        foreach (var queueId in queueIds)
        {
            var cached = await ReadCachedAsync(cache, DispatchCacheKey(tenantName, queueId), cancellationToken);

            if (cached is not null)
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
                await cache.SetAsync(
                    DispatchCacheKey(tenantName, diagnosis.QueueId),
                    JsonSerializer.SerializeToUtf8Bytes(diagnosis, _dispatchSerializerOptions),
                    _dispatchCacheOptions,
                    cancellationToken);
                found[diagnosis.QueueId] = diagnosis;
            }
        }

        return queueIds
            .Where(found.ContainsKey)
            .Select(queueId => found[queueId])
            .ToArray();
    }

    private static async Task<QueueDispatchDiagnostics> ReadCachedAsync(IDistributedCache cache, string key, CancellationToken cancellationToken)
    {
        var value = await cache.GetAsync(key, cancellationToken);

        if (value is null || value.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QueueDispatchDiagnostics>(value, _dispatchSerializerOptions);
        }
        catch (JsonException)
        {
            // An entry this release cannot read is read again from the source.
            return null;
        }
    }

    private static string DispatchCacheKey(string tenantName, string queueId)
        => $"ContactCenterDispatchDiagnostics:{tenantName}:{queueId}";

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
            WaitingItemCount = diagnosis.WaitingItemCount,
            ReservedItemCount = diagnosis.ReservedItemCount,
            AssignedItemCount = diagnosis.AssignedItemCount,
            Pacing = diagnosis.CampaignId is null ? null : diagnosis.Pacing.ToString(),
            ProfileMismatchCount = diagnosis.ProfileMismatchCount,
            ActivitiesWithoutProfileCount = diagnosis.ActivitiesWithoutProfileCount,
            ProfileStamps = diagnosis.ProfileStamps
                .Select(stamp => new SupervisorDispatchProfileViewModel
                {
                    ProfileId = stamp.ProfileId,
                    Name = stamp.ProfileName,
                    Mode = stamp.Mode,
                    Enabled = stamp.Enabled,
                    ItemCount = stamp.ItemCount,
                })
                .ToList(),
            ActivityMix = diagnosis.ActivityMix
                .Select(group => new SupervisorDispatchActivityGroupViewModel
                {
                    Source = group.Source,
                    InteractionType = group.InteractionType,
                    Channel = group.Channel,
                    Count = group.Count,
                })
                .ToList(),
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
