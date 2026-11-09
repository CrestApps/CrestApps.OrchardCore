using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;
using CrestApps.OrchardCore.ContactCenter.Reports.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Lists what each campaign and queue holds: waiting, reserved, assigned, due now, held back and why, records in no queue
/// and records assigned to a user for manual work.
/// </summary>
internal sealed class QueueInventoryTool : ContactCenterAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "contactCenterQueueInventory";

    private const int MaxCampaigns = 20;

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "campaignOrQueue": { "type": "string", "description": "Optional. Only this queue or campaign, by name or id, for example 'Lead Generation'." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Shows what each contact center campaign and queue holds right now: records waiting, reserved and assigned, how many can be dialed now, how many are held back and why, the oldest waiting record, records that are in no queue, and records assigned to a user for manual work. Campaigns with no queue are called out. Returns findings and a verdict per campaign. Answer only from the result.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var filterText = Read(arguments, "campaignOrQueue");
        var names = await ContactCenterLookups.ReadQueueNamesAsync(services, cancellationToken);
        var candidateIds = filterText is null
            ? [.. names.Keys]
            : await ContactCenterLookups.FindQueueIdsAsync(services, filterText, cancellationToken);

        if (filterText is not null && candidateIds.Count == 0)
        {
            return Error($"No queue or campaign matches '{filterText}'. Use the configuration tool to list them.");
        }

        var overseen = await ContactCenterLookups.OverseenAsync(services, user, candidateIds, cancellationToken);
        var queueIds = candidateIds.Where(overseen.Contains).ToArray();

        if (queueIds.Length == 0)
        {
            return Error("You don't oversee any queue or campaign that matches, so you can't see their inventory.");
        }

        var agents = (await services.GetRequiredService<IAgentProfileManager>().GetAllAsync(cancellationToken) ?? []).ToArray();
        var campaignIds = queueIds
            .Select(ContactCenterConstants.CampaignQueue.GetCampaignId)
            .Where(id => id is not null)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var inventory = campaignIds.Length == 0
            ? new DialerQueueInventory()
            : await services.GetRequiredService<IDialerQueueInventoryService>().GetInventoryAsync(
                new DialerQueueInventoryRequest { CampaignIds = campaignIds },
                cancellationToken);
        var summaries = inventory.Campaigns.ToDictionary(summary => summary.CampaignId, StringComparer.Ordinal);
        var campaigns = new List<(string Name, string Verdict, object Result, bool Problem)>();

        foreach (var campaignId in campaignIds.Where(summaries.ContainsKey).OrderByDescending(id => summaries[id].PendingCount))
        {
            var queueId = ContactCenterConstants.CampaignQueue.CreateId(campaignId);
            var name = names.GetValueOrDefault(queueId) ?? campaignId;
            var (signedIn, available) = ContactCenterLookups.CountAgents(agents, queueId);
            var rows = inventory.Rows.Where(row => string.Equals(row.CampaignId, campaignId, StringComparison.Ordinal)).ToArray();
            var analysis = CampaignInventoryAnalyzer.Analyze(new CampaignInventoryFacts(campaignId, name, summaries[campaignId], rows, signedIn, available));
            var summary = summaries[campaignId];
            var queued = rows.Where(row => row.QueueId is not null).ToArray();

            campaigns.Add((name, analysis.Verdict, new
            {
                id = campaignId,
                name,
                verdict = analysis.Verdict,
                pending = summary.PendingCount,
                dialableNow = summary.DialableNowCount,
                heldBack = summary.HeldBackCount,
                scheduledForLater = summary.ScheduledForLaterCount,
                nextDueUtc = Utc(summary.NextDueUtc),
                notInAnyQueue = summary.NotInAnyQueueCount,
                assignedToUserForManualWork = summary.AssignedToUserCount,
                otherHeldBack = summary.OtherHeldBackCount,
                heldBackReasons = summary.OtherHeldBackReasons.Select(bucket => new { reason = BlockerWords.Describe(bucket.Blocker), bucket.Count, nextDueUtc = Utc(bucket.NextDueUtc) }),
                notExplained = summary.NotEvaluatedCount,
                queue = new
                {
                    waiting = queued.Sum(row => row.WaitingCount),
                    reserved = queued.Sum(row => row.ReservedCount),
                    assigned = queued.Sum(row => row.AssignedCount),
                    oldestWaitingUtc = Utc(queued.Where(row => row.OldestWaitingUtc.HasValue).Select(row => row.OldestWaitingUtc).DefaultIfEmpty().Min()),
                    lastDialedUtc = Utc(queued.Select(row => row.LastDialedUtc).DefaultIfEmpty().Max()),
                    profiles = queued.Select(row => new { profile = row.StampedProfileName, mode = row.StampedProfileMode, enabled = row.StampedProfileEnabled, row.WaitingCount }),
                },
                signedInAgents = signedIn,
                availableAgents = available,
                findings = analysis.Findings,
            }, analysis.Findings.Any(finding => finding.Likelihood is Likelihood.Certain or Likelihood.Likely)));
        }

        // A campaign with nothing pending is not in the inventory; it is listed by name so it is not mistaken for missing.
        var idle = campaignIds
            .Where(id => !summaries.ContainsKey(id))
            .Select(id => names.GetValueOrDefault(ContactCenterConstants.CampaignQueue.CreateId(id)) ?? id)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var storedQueueIds = queueIds.Where(id => !ContactCenterConstants.IsCampaignQueue(id)).ToArray();
        var queues = new List<object>();

        if (storedQueueIds.Length > 0)
        {
            var queueItems = services.GetRequiredService<IQueueItemManager>();
            var waiting = await queueItems.CountWaitingByQueueIdsAsync(storedQueueIds, cancellationToken);
            var now = services.GetRequiredService<IClock>().UtcNow;

            foreach (var queueId in storedQueueIds)
            {
                var count = waiting.GetValueOrDefault(queueId);
                var oldest = count > 0 ? await queueItems.FindLongestWaitingAsync(queueId, cancellationToken) : null;
                var (signedIn, available) = ContactCenterLookups.CountAgents(agents, queueId);

                queues.Add(new
                {
                    id = queueId,
                    name = names.GetValueOrDefault(queueId) ?? queueId,
                    waiting = count,
                    oldestWaitingUtc = Utc(oldest?.QueueEnteredUtc),
                    oldestWaitingMinutes = oldest is null ? (double?)null : Math.Round((now - oldest.QueueEnteredUtc).TotalMinutes, 1),
                    signedInAgents = signedIn,
                    availableAgents = available,
                    note = count > 0 && signedIn == 0 ? "Work is waiting but no agent is signed in to this queue." : null,
                });
            }
        }

        var problems = campaigns.Where(campaign => campaign.Problem).ToArray();

        return new
        {
            generatedUtc = Utc(inventory.GeneratedUtc == default ? services.GetRequiredService<IClock>().UtcNow : inventory.GeneratedUtc),
            verdict = campaigns.Count == 0 && queues.Count == 0
                ? "Nothing is pending in any campaign or waiting in any queue."
                : $"{campaigns.Count} campaign(s) have pending records; {problems.Length} of them have a problem." +
                    (problems.Length == 0 ? string.Empty : " " + string.Join(" ", problems.Take(3).Select(problem => problem.Verdict))),
            campaigns = Limit(campaigns.OrderByDescending(campaign => campaign.Problem).Select(campaign => campaign.Result), MaxCampaigns),
            campaignsWithNothingPending = Limit(idle),
            queues = Limit(queues),
        };
    }
}
