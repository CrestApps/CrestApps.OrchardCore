using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Lists the campaigns, dialer profiles and queues with the settings that decide how work is offered and dialed.
/// </summary>
internal sealed class ListConfigurationTool : ContactCenterAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "contactCenterListConfiguration";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "section": { "type": "string", "enum": ["all", "campaigns", "profiles", "queues"], "description": "Which part to list. Default all." },
            "name": { "type": "string", "description": "Optional. Only entries whose name contains this text." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists the contact center campaigns, dialer profiles and queues with the settings that decide how work is offered and dialed: dialing mode, offer timeout, attempts, retry delay, pacing, calling window, routing strategy, reservation timeout, business hours and whether each is turned on. Use it to answer questions about how something is configured. Answer only from the result.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var section = Read(arguments, "section")?.ToLowerInvariant() ?? "all";
        var filter = Read(arguments, "name");
        var names = await ContactCenterLookups.ReadQueueNamesAsync(services, cancellationToken);
        var overseen = await ContactCenterLookups.OverseenAsync(services, user, names.Keys, cancellationToken);
        var calendars = new Dictionary<string, string>(StringComparer.Ordinal);

        if (services.GetService<IBusinessHoursCalendarManager>() is { } calendarManager)
        {
            foreach (var calendar in await calendarManager.GetAllAsync(cancellationToken) ?? [])
            {
                calendars[calendar.ItemId] = calendar.Name;
            }
        }

        var agents = (await services.GetRequiredService<IAgentProfileManager>().GetAllAsync(cancellationToken) ?? []).ToArray();
        object campaigns = null;
        object profiles = null;
        object queues = null;
        var summary = new List<string>();

        if (section is "all" or "campaigns" && services.GetService<ICatalogManager<OmnichannelCampaign>>() is { } campaignManager)
        {
            var rows = new List<object>();

            foreach (var campaign in (await campaignManager.GetAllAsync(cancellationToken) ?? []).Where(campaign => campaign is not null && Matches(campaign.DisplayText, filter)))
            {
                var queueId = ContactCenterConstants.CampaignQueue.CreateId(campaign.ItemId);

                if (!overseen.Contains(queueId))
                {
                    continue;
                }

                var (signedIn, available) = ContactCenterLookups.CountAgents(agents, queueId);

                rows.Add(new
                {
                    id = campaign.ItemId,
                    name = campaign.DisplayText,
                    interactionType = campaign.InteractionType.ToString(),
                    channel = campaign.Channel,
                    signedInAgents = signedIn,
                    availableAgents = available,
                });
            }

            campaigns = Limit(rows);
            summary.Add($"{rows.Count} campaign(s)");
        }

        if (section is "all" or "profiles" && services.GetService<IDialerProfileManager>() is { } profileManager)
        {
            var rows = new List<object>();

            foreach (var profile in (await profileManager.GetAllAsync(cancellationToken) ?? []).Where(profile => profile is not null && Matches(profile.Name, filter)).OrderBy(profile => profile.Name, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new
                {
                    id = profile.ItemId,
                    profile.Name,
                    mode = profile.Mode.ToString(),
                    profile.Enabled,
                    offerTimeoutSeconds = profile.PreviewOfferTimeoutSeconds,
                    offerExtensionSeconds = profile.PreviewExtensionSeconds,
                    maxOfferExtensions = profile.MaxPreviewExtensions,
                    ringTimeoutSeconds = profile.RingTimeoutSeconds,
                    maxAttempts = profile.MaxAttempts,
                    retryDelayMinutes = profile.RetryDelayMinutes,
                    pacing = new
                    {
                        profile.CallsPerAgent,
                        profile.MaxLinesPerAgent,
                        predictiveModel = profile.PredictivePacingModel.ToString(),
                        profile.TargetAbandonmentRatePercent,
                        profile.MaxCallsInFlight,
                    },
                    abandonmentCap = new { enforced = profile.EnforceAbandonmentCap, maxPercent = profile.MaxAbandonmentRatePercent },
                    callingWindow = new
                    {
                        enforced = profile.EnforceCallingWindow,
                        calendar = profile.CallingCalendarId is null ? null : calendars.GetValueOrDefault(profile.CallingCalendarId) ?? profile.CallingCalendarId,
                        regionalCalendars = profile.RegionalCallingCalendarIds?.Count ?? 0,
                    },
                    respectDoNotCall = profile.RespectDoNotCall,
                    answeringMachineDetection = profile.AnsweringMachineDetection.ToString(),
                    provider = profile.ProviderName,
                    note = profile.Enabled ? null : "Turned off: records stamped with this profile are held back.",
                });
            }

            profiles = Limit(rows);
            summary.Add($"{rows.Count} dialer profile(s)");
        }

        if (section is "all" or "queues")
        {
            var rows = new List<object>();

            foreach (var queue in (await services.GetRequiredService<IActivityQueueManager>().GetAllAsync(cancellationToken) ?? []).Where(queue => queue is not null && Matches(queue.Name, filter)).OrderBy(queue => queue.Name, StringComparer.OrdinalIgnoreCase))
            {
                if (!overseen.Contains(queue.ItemId))
                {
                    continue;
                }

                var (signedIn, available) = ContactCenterLookups.CountAgents(agents, queue.ItemId);

                rows.Add(new
                {
                    id = queue.ItemId,
                    queue.Name,
                    queue.Enabled,
                    routingStrategy = queue.RoutingStrategy.ToString(),
                    offerTimeoutSeconds = queue.ReservationTimeoutSeconds,
                    unansweredOfferAction = queue.UnansweredOfferAction.ToString(),
                    stickyAgent = queue.PreferStickyAgent,
                    requiredSkills = queue.RequiredSkills,
                    businessHours = queue.BusinessHoursCalendarId is null ? "always open" : calendars.GetValueOrDefault(queue.BusinessHoursCalendarId) ?? queue.BusinessHoursCalendarId,
                    afterHoursAction = queue.AfterHoursAction.ToString(),
                    maxWaitSeconds = queue.MaxWaitSeconds,
                    maxQueueSize = queue.MaxQueueSize,
                    overflowQueue = queue.OverflowQueueId is null ? null : names.GetValueOrDefault(queue.OverflowQueueId) ?? queue.OverflowQueueId,
                    signedInAgents = signedIn,
                    availableAgents = available,
                    note = queue.Enabled ? null : "Turned off: it hands nobody work.",
                });
            }

            queues = Limit(rows);
            summary.Add($"{rows.Count} queue(s)");
        }

        return new
        {
            generatedUtc = Utc(services.GetRequiredService<IClock>().UtcNow),
            verdict = summary.Count == 0 ? "Nothing matches." : $"Found {string.Join(", ", summary)}.",
            campaigns,
            profiles,
            queues,
            note = section is "all" or "profiles" && services.GetService<IDialerProfileManager>() is null ? "Dialer profiles are not listed because the Outbound Dialer feature is off." : null,
        };
    }

    private static bool Matches(string value, string filter)
        => filter is null || (value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
}
