using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Explains one activity: its status, owner or reservation, queue item, dialer profile, destination, why it is or is not
/// dialable and the dialer's latest decisions about it.
/// </summary>
internal sealed class ExplainActivityTool : ContactCenterAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "contactCenterExplainActivity";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "activityId": { "type": "string", "description": "The activity (record) id to explain." }
          },
          "required": ["activityId"],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Explains one contact center activity (a record to call): its status, who owns or has reserved it, its queue item, its dialer profile, its channel and destination, why it is or is not dialable right now, whether an empty channel is covered by the contact's number, and the dialer's last decisions about it. Reads only; never changes the activity. Answer only from the result.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var activityId = Read(arguments, "activityId");

        if (activityId is null)
        {
            return Error("Say which activity to explain: its activity id.");
        }

        var activity = await services.GetRequiredService<IOmnichannelActivityManager>().FindByIdAsync(activityId, cancellationToken);

        if (activity is null)
        {
            return Error($"No activity has the id '{activityId}'.");
        }

        var names = await ContactCenterLookups.ReadQueueNamesAsync(services, cancellationToken);
        var campaignQueueId = string.IsNullOrEmpty(activity.CampaignId) ? null : ContactCenterConstants.CampaignQueue.CreateId(activity.CampaignId);

        if (campaignQueueId is not null && (await ContactCenterLookups.OverseenAsync(services, user, [campaignQueueId], cancellationToken)).Count == 0)
        {
            return Error("This activity belongs to a campaign you don't oversee, so you can't see why it is or is not being dialed.");
        }

        var now = services.GetRequiredService<IClock>().UtcNow;
        var queueItem = await services.GetRequiredService<IQueueItemManager>().FindByActivityIdAsync(activity.ItemId, cancellationToken);
        var reservations = (await services.GetRequiredService<IActivityReservationManager>().GetActiveByActivityAsync(activity.ItemId, cancellationToken) ?? [])
            .Where(reservation => !reservation.IsResolved)
            .Select(reservation => new AgentReservation(reservation.ItemId, reservation.ActivityItemId, reservation.QueueId, reservation.Status.ToString(), reservation.CreatedUtc, reservation.ExpiresUtc))
            .ToArray();
        var interaction = await services.GetRequiredService<IInteractionManager>().FindByActivityIdAsync(activity.ItemId, cancellationToken);
        var isPending = Core.Services.ActivityProgressTally.IsPending(activity.Status);
        var classifier = services.GetRequiredService<DialerRecordClassifier>();
        var profileId = queueItem?.DialerProfileId ?? activity.DialerProfileId;
        var profile = await classifier.ReadProfileAsync(profileId, cancellationToken);

        DispatchBlockerReading blocker = null;

        if (isPending && !string.IsNullOrEmpty(activity.CampaignId))
        {
            var verdicts = await classifier.ClassifyAsync(
                [new DialerRecordInput(activity.ItemId, queueItem is not null, queueItem?.DialerProfileId, activity.DialerProfileId)],
                now,
                cancellationToken);

            if (verdicts.TryGetValue(activity.ItemId, out var verdict))
            {
                blocker = new DispatchBlockerReading(verdict.Blocker, verdict.DueUtc);
            }
        }

        ActivityEligibility eligibility = null;

        // The compliance gate is asked exactly as the dialer asks it. It fills in the contact's current number on the object
        // it is given, in memory only; the number is put back so nothing is changed.
        if (isPending && profile is not null && services.GetService<IDialerEligibilityService>() is { } eligibilityService)
        {
            var storedDestination = activity.PreferredDestination;

            try
            {
                var result = await eligibilityService.EvaluateAsync(new DialerEligibilityContext { Profile = profile, Activity = activity }, cancellationToken);
                var resolved = string.IsNullOrEmpty(storedDestination) && !string.IsNullOrEmpty(activity.PreferredDestination);

                eligibility = new ActivityEligibility(
                    result.IsEligible,
                    result.IsEligible ? null : result.Reason.ToString(),
                    result.Description,
                    resolved,
                    resolved ? Last4(activity.PreferredDestination) : null);
            }
            finally
            {
                activity.PreferredDestination = storedDestination;
            }
        }

        var decisions = (await services.GetRequiredService<IDialerDecisionLog>().GetEntriesAsync(
            new DialerDecisionLogQuery { ActivityItemId = activity.ItemId, FromUtc = now.AddDays(-7) },
            cancellationToken) ?? [])
            .OrderByDescending(entry => entry.LastSeenUtc)
            .Take(20)
            .Select(entry => new ActivityDecision(
                entry.EventType == ContactCenterConstants.Events.DialSuppressed ? "Suppression" : "Routing",
                entry.Reason,
                entry.OccurrenceCount,
                entry.LastSeenUtc))
            .ToArray();

        var facts = new ActivityFacts
        {
            ActivityId = activity.ItemId,
            Status = activity.Status.ToString(),
            IsPending = isPending,
            IsAutomated = activity.InteractionType == ActivityInteractionType.Automated,
            TerminalReasonCode = activity.TerminalReasonCode,
            Channel = activity.Channel,
            HasStoredDestination = !string.IsNullOrEmpty(activity.PreferredDestination),
            StoredDestinationLast4 = Last4(activity.PreferredDestination),
            CampaignId = activity.CampaignId,
            CampaignName = campaignQueueId is null ? null : names.GetValueOrDefault(campaignQueueId),
            DialerProfileId = profileId,
            DialerProfileName = profile?.Name,
            DialerMode = profile?.Mode.ToString(),
            DialerProfileEnabled = profile?.Enabled,
            AssignedToUserName = string.IsNullOrEmpty(activity.AssignedToId) ? null : activity.AssignedToUsername ?? activity.AssignedToId,
            ReservedByUserName = activity.ReservedByUsername,
            ReservationExpiresUtc = activity.ReservationExpiresUtc,
            Attempts = activity.Attempts,
            MaxAttempts = activity.MaxAttempts,
            ScheduledUtc = activity.ScheduledUtc,
            QueueItem = queueItem is null ? null : new ActivityQueueItem(queueItem.ItemId, queueItem.QueueId, queueItem.Status.ToString(), queueItem.DialerProfileId, queueItem.AgentId, queueItem.EnqueuedUtc),
            Reservations = reservations,
            LastInteraction = interaction is null ? null : new ActivityInteraction(
                interaction.ItemId,
                interaction.Status.ToString(),
                interaction.AgentId,
                interaction.CreatedUtc,
                interaction.EndedUtc,
                interaction.IsSettled,
                interaction.WrapUpStartedUtc.HasValue && !interaction.WrapUpCompletedUtc.HasValue),
            Blocker = blocker?.Blocker,
            BlockerDueUtc = blocker?.DueUtc,
            Eligibility = eligibility,
            Decisions = decisions,
            NowUtc = now,
        };

        var analysis = ActivityAnalyzer.Analyze(facts);

        return new
        {
            generatedUtc = Utc(now),
            verdict = analysis.Verdict,
            activity = new
            {
                id = activity.ItemId,
                status = facts.Status,
                interactionType = activity.InteractionType.ToString(),
                channel = string.IsNullOrWhiteSpace(activity.Channel) ? "(empty)" : activity.Channel,
                hasOwnNumber = facts.HasStoredDestination,
                numberEndsWith = facts.StoredDestinationLast4,
                campaign = facts.CampaignName ?? facts.CampaignId,
                scheduledUtc = Utc(activity.ScheduledUtc),
                attempts = activity.Attempts,
                maxAttempts = activity.MaxAttempts,
                assignedTo = facts.AssignedToUserName,
                reservedBy = activity.ReservedByUsername,
                reservationExpiresUtc = Utc(activity.ReservationExpiresUtc),
                terminalReason = activity.TerminalReasonCode,
                createdUtc = Utc(activity.CreatedUtc),
            },
            dialerProfile = profile is null ? null : new { id = profile.ItemId, profile.Name, mode = profile.Mode.ToString(), profile.Enabled },
            queueItem = facts.QueueItem,
            findings = analysis.Findings,
        };
    }

    private static string Last4(string number)
    {
        // Only the tail is shown, so the digits are read through the shared comparison key rather than a second
        // normalization of the number.
        var digits = PhoneNumberComparisonKey.For(default, number);

        return digits.Length >= 4 ? digits[^4..] : null;
    }

    private sealed record DispatchBlockerReading(Core.Models.Dispatch.DispatchBlocker Blocker, DateTime? DueUtc);
}
