using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// The Contact Center setup records the generic record tools (<c>listRecords</c>, <c>getRecord</c>, <c>saveRecord</c>
/// and <c>deleteRecord</c>) work on: their kind names, the allow-listed fields each returns, and the rules the admin
/// pages apply that their editors do not. A queue, a calendar and an entry point are read-only kinds: their editors
/// post lists of objects (overflow targets and skill requirements, the weekly schedule, the phone menu) that the
/// editor replay cannot post, so <c>saveContactCenterQueue</c>, <c>saveBusinessHoursCalendar</c> and
/// <c>saveContactCenterEntryPoint</c> write them, and their projections use those tools' argument names.
/// </summary>
internal static class ContactCenterRecordKinds
{
    /// <summary>
    /// The kind of the agent state reason codes.
    /// </summary>
    public const string ReasonCodeKind = "agentStateReasonCode";

    /// <summary>
    /// The kind of the queue groups.
    /// </summary>
    public const string QueueGroupKind = "contactCenterQueueGroup";

    /// <summary>
    /// The kind of the dialer profiles.
    /// </summary>
    public const string DialerProfileKind = "dialerProfile";

    /// <summary>
    /// The kind of the queues, read-only.
    /// </summary>
    public const string QueueKind = "contactCenterQueue";

    /// <summary>
    /// The kind of the business-hours calendars, read-only.
    /// </summary>
    public const string BusinessHoursCalendarKind = "businessHoursCalendar";

    /// <summary>
    /// The kind of the inbound entry points, read-only.
    /// </summary>
    public const string EntryPointKind = "contactCenterEntryPoint";

    // The states the reason code editor offers a reason for; a reason for any other state is never shown to an agent.
    private static readonly AgentPresenceStatus[] _reasonCodeStates =
    [
        AgentPresenceStatus.Break,
        AgentPresenceStatus.Away,
        AgentPresenceStatus.DoNotDisturb,
        AgentPresenceStatus.Meeting,
        AgentPresenceStatus.Training,
        AgentPresenceStatus.AfterHoursUnavailable,
    ];

    /// <summary>
    /// Describes an agent state reason code.
    /// </summary>
    /// <param name="reasonCode">The reason code.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeReasonCode(AgentStateReasonCode reasonCode)
        => new
        {
            id = reasonCode.ItemId,
            name = reasonCode.Name,
            description = reasonCode.Description,
            appliesTo = reasonCode.AppliesTo.ToString(),
            sortOrder = reasonCode.SortOrder,
            enabled = reasonCode.Enabled,
        };

    /// <summary>
    /// Describes a queue group.
    /// </summary>
    /// <param name="group">The queue group.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeQueueGroup(ActivityQueueGroup group)
        => new
        {
            id = group.ItemId,
            name = group.Name,
            description = group.Description,
        };

    /// <summary>
    /// Describes a dialer profile: every setting of its editor, none of which is a secret. What the profile measured
    /// (abandonment and answer rates) is shown on its admin page, not here.
    /// </summary>
    /// <param name="profile">The dialer profile.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeDialerProfile(DialerProfile profile)
        => new
        {
            id = profile.ItemId,
            name = profile.Name,
            description = profile.Description,
            enabled = profile.Enabled,
            mode = profile.Mode.ToString(),
            providerName = profile.ProviderName,
            callsPerAgent = profile.CallsPerAgent,
            maxAttempts = profile.MaxAttempts,
            retryDelayMinutes = profile.RetryDelayMinutes,
            answeringMachineDetection = profile.AnsweringMachineDetection.ToString(),
            ringTimeoutSeconds = profile.RingTimeoutSeconds,
            previewOfferTimeoutSeconds = profile.PreviewOfferTimeoutSeconds,
            callerId = profile.CallerId,
            alwaysUseCallerId = profile.AlwaysUseCallerId,
            defaultRegionCode = profile.DefaultRegionCode,
            respectDoNotCall = profile.RespectDoNotCall,
            enforceCallingWindow = profile.EnforceCallingWindow,
            callingCalendarId = profile.CallingCalendarId,
            regionalCallingCalendarIds = profile.RegionalCallingCalendarIds is { Count: > 0 }
                ? new Dictionary<string, string>(profile.RegionalCallingCalendarIds, StringComparer.OrdinalIgnoreCase)
                : null,
            enforceAbandonmentCap = profile.EnforceAbandonmentCap,
            maxAbandonmentRatePercent = profile.MaxAbandonmentRatePercent,
            abandonmentSampleFloor = profile.AbandonmentSampleFloor,
            safeHarborEnabled = profile.SafeHarborEnabled,
            safeHarborMessage = profile.SafeHarborMessage,
            predictivePacingModel = profile.PredictivePacingModel.ToString(),
            targetAbandonmentRatePercent = profile.TargetAbandonmentRatePercent,
            maxLinesPerAgent = profile.MaxLinesPerAgent,
            maxCallsInFlight = profile.MaxCallsInFlight,
            answerRateSampleFloor = profile.AnswerRateSampleFloor,
            answerRateWindowMinutes = profile.AnswerRateWindowMinutes,
            creditAgentsFreeingUp = profile.CreditAgentsFreeingUp,
            freeUpCreditPercent = profile.FreeUpCreditPercent,
            connectWaitMilliseconds = profile.ConnectWaitMilliseconds,
            abandonedRetryRequiresAgent = profile.AbandonedRetryRequiresAgent,
            showDialProgressToAgent = profile.ShowDialProgressToAgent,
        };

    /// <summary>
    /// Describes a queue in full, with the argument names <c>saveContactCenterQueue</c> takes, so a queue read with
    /// <c>getRecord</c> can be changed with that tool.
    /// </summary>
    /// <param name="queue">The queue.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeQueue(ActivityQueue queue)
    {
        var treatment = queue.Treatment ?? new QueueTreatmentSettings();

        return new
        {
            id = queue.ItemId,
            name = queue.Name,
            description = queue.Description,
            queueGroupId = queue.QueueGroupId,
            enabled = queue.Enabled,
            defaultPriority = queue.DefaultPriority.ToString(),
            routingStrategy = queue.RoutingStrategy.ToString(),
            preferStickyAgent = queue.PreferStickyAgent,
            enableSlaAging = queue.EnableSlaAging,
            slaThresholdSeconds = queue.SlaThresholdSeconds,
            firstResponseTargetSeconds = queue.FirstResponseTargetSeconds,
            reservationTimeoutSeconds = queue.ReservationTimeoutSeconds,
            unansweredOfferAction = queue.UnansweredOfferAction.ToString(),
            businessHoursCalendarId = queue.BusinessHoursCalendarId,
            afterHoursAction = queue.AfterHoursAction.ToString(),
            inboundChannelEndpointId = queue.InboundChannelEndpointId,
            requiredSkills = queue.RequiredSkills?.ToList() ?? [],
            skillRequirements = (queue.SkillRequirements ?? [])
                .Where(requirement => requirement is not null)
                .Select(requirement => new
                {
                    skillId = requirement.SkillId,
                    minimumProficiency = requirement.MinimumProficiency,
                    required = requirement.Required,
                    relaxAfterSeconds = requirement.RelaxAfterSeconds,
                })
                .ToList(),
            overflowQueueId = queue.OverflowQueueId,
            overflowAfterSeconds = queue.OverflowAfterSeconds,
            overflowTargets = (queue.OverflowTargets ?? [])
                .Where(target => target is not null)
                .Select(target => new
                {
                    queueId = target.QueueId,
                    afterSeconds = target.AfterSeconds,
                })
                .ToList(),
            maxWaitSeconds = queue.MaxWaitSeconds,
            maxWaitAction = queue.MaxWaitAction.ToString(),
            maxQueueSize = queue.MaxQueueSize,
            queueFullAction = queue.QueueFullAction.ToString(),
            treatment = new
            {
                welcomeMessage = treatment.WelcomeMessage,
                announcementIntervalSeconds = treatment.AnnouncementIntervalSeconds,
                announcePosition = treatment.AnnouncePosition,
                announceEstimatedWait = treatment.AnnounceEstimatedWait,
                holdMusicMediaId = treatment.HoldMusicMediaId,
                callbackDtmfKey = treatment.CallbackDtmfKey,
                callbackOfferAfterSeconds = treatment.CallbackOfferAfterSeconds,
                minimumEstimateSeconds = treatment.MinimumEstimateSeconds,
                maximumEstimateSeconds = treatment.MaximumEstimateSeconds,
                averageHandleTimeSeconds = treatment.AverageHandleTimeSeconds,
            },
        };
    }

    /// <summary>
    /// Describes a business-hours calendar, a Business Hours schedule, with the argument names
    /// <c>saveBusinessHoursCalendar</c> takes: a weekday with several opening windows is listed once per window, the
    /// holidays are the dates closed all day, and the special days list every holiday and special day in full, with its
    /// hours and name. Its times and dates are wall-clock times and dates of its own time zone,
    /// not instants, so they are given as stored.
    /// </summary>
    /// <param name="calendar">The schedule.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeCalendar(BusinessHoursSchedule calendar)
        => new
        {
            id = calendar.ItemId,
            name = calendar.Name,
            description = calendar.Description,
            timeZoneId = string.IsNullOrWhiteSpace(calendar.Hours?.TimeZoneId) ? "UTC" : calendar.Hours.TimeZoneId,
            enabled = calendar.Enabled,
            weeklySchedule = Enum.GetValues<DayOfWeek>()
                .SelectMany(day => (calendar.Hours?.Windows ?? []).Where(window => window is not null && window.Day == day).OrderBy(window => window.OpenMinute).ToList() is { Count: > 0 } windows
                    ? windows.Select(window => (object)new { day = day.ToString(), open = FormatMinutes(window.OpenMinute), close = FormatMinutes(window.CloseMinute) })
                    : [new { day = day.ToString(), closed = true }])
                .ToList(),
            holidays = (calendar.Hours?.DateExceptions ?? [])
                .Where(exception => exception is not null && exception.Closed)
                .Select(exception => exception.Date)
                .Distinct()
                .Order()
                .Select(date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .ToList(),
            specialDays = (calendar.Hours?.DateExceptions ?? [])
                .Where(exception => exception is not null)
                .GroupBy(exception => exception.Date)
                .Select(group => group.Last())
                .OrderBy(exception => exception.Date)
                .Select(exception => exception.Closed
                    ? (object)new
                    {
                        date = exception.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        closed = true,
                        name = string.IsNullOrWhiteSpace(exception.Name) ? null : exception.Name.Trim(),
                    }
                    : new
                    {
                        date = exception.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        open = FormatMinutes(exception.OpenMinute),
                        close = FormatMinutes(exception.CloseMinute),
                        name = string.IsNullOrWhiteSpace(exception.Name) ? null : exception.Name.Trim(),
                    })
                .ToList(),
        };

    /// <summary>
    /// Writes minutes after midnight as a time of day, with 24:00 for the end of the day.
    /// </summary>
    /// <param name="minutes">The minutes after midnight.</param>
    /// <returns>The time of day, HH:mm.</returns>
    public static string FormatMinutes(int minutes)
    {
        var clamped = Math.Clamp(minutes, 0, 1440);

        return string.Create(CultureInfo.InvariantCulture, $"{clamped / 60:D2}:{clamped % 60:D2}");
    }

    /// <summary>
    /// Describes an inbound entry point in full, phone menu included, with the argument names
    /// <c>saveContactCenterEntryPoint</c> takes. The settings other features keep on it (messaging, the AI agent for
    /// texts or calls) are named, never returned; they are changed on its page.
    /// </summary>
    /// <param name="entryPoint">The entry point.</param>
    /// <returns>The allow-listed fields.</returns>
    public static object DescribeEntryPoint(ContactCenterEntryPoint entryPoint)
        => new
        {
            id = entryPoint.ItemId,
            name = entryPoint.Name,
            description = entryPoint.Description,
            channel = entryPoint.GetChannel(),
            enabled = entryPoint.Enabled,
            addressIds = entryPoint.AddressIds?.ToList() ?? [],
            dialedNumbers = entryPoint.DialedNumbers?.ToList() ?? [],
            targetType = entryPoint.TargetType.ToString(),
            targetQueueId = entryPoint.TargetQueueId,
            targetAgentId = entryPoint.TargetAgentId,
            targetAIProfileId = entryPoint.TargetAIProfileId,
            businessHoursCalendarId = entryPoint.BusinessHoursCalendarId,
            priority = entryPoint.Priority.ToString(),
            closedAction = entryPoint.ClosedAction.ToString(),
            overflowQueueId = entryPoint.OverflowQueueId,
            welcomeMessage = entryPoint.WelcomeMessage,
            closedMessage = entryPoint.ClosedMessage,
            voicemailEnabled = entryPoint.VoicemailEnabled,
            ringTimeoutSeconds = entryPoint.RingTimeoutSeconds,
            voicemailGreetingText = entryPoint.VoicemailGreetingText,
            voicemailDestination = entryPoint.VoicemailDestination.ToString(),
            voicemailRecipientAgentId = entryPoint.VoicemailRecipientAgentId,
            phoneMenu = DescribeMenu(entryPoint.IvrFlow),
            otherSettings = (entryPoint.Properties ?? new Dictionary<string, object>()).Keys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
        };

    /// <summary>
    /// Checks an agent state reason code as its admin page does before saving: the rules of the reason code's handlers,
    /// and, as the editor offers only the states an agent can choose with a reason, that it applies to one of them.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="reasonCode">The reason code, with the posted values bound.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The outcome of the checks.</returns>
    public static async ValueTask<ValidationResultDetails> ValidateReasonCodeAsync(IServiceProvider services, AgentStateReasonCode reasonCode, CancellationToken cancellationToken)
    {
        var result = await services.GetRequiredService<IAgentStateReasonCodeManager>().ValidateAsync(reasonCode, cancellationToken) ?? new ValidationResultDetails();

        if (!_reasonCodeStates.Contains(reasonCode.AppliesTo))
        {
            result.Fail(new ValidationResult(
                $"A reason code applies to one of: {string.Join(", ", _reasonCodeStates)}.",
                [nameof(AgentStateReasonCode.AppliesTo)]));
        }

        return result;
    }

    /// <summary>
    /// Checks a dialer profile as its admin page does before saving: the rules of the profile's handlers, and, as the
    /// editor offers only existing calendars, that a calling calendar it is given exists. A calendar the profile already
    /// had is not checked again, so a profile can still be saved after its calendar was deleted, as on the page. The same
    /// goes for the caller ID: the editor picks it from the business's own phone numbers (the addresses with the phone
    /// capability, the list <see cref="OmnichannelCallerIdOptionsExtensions.GetCallerIdOptionsAsync"/> builds), so a new or
    /// changed caller ID must be one of them; the number people see must be one the business owns. A save
    /// that relaxes a compliance rule is refused: the generic record tools cannot ask for approval, and the calls a
    /// relaxed profile allows leave the site and cannot be undone, so that change stays on the admin page.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="profile">The profile, with the posted values bound.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    /// <returns>The outcome of the checks.</returns>
    public static async ValueTask<ValidationResultDetails> ValidateDialerProfileAsync(IServiceProvider services, DialerProfile profile, CancellationToken cancellationToken)
    {
        var manager = services.GetRequiredService<IDialerProfileManager>();
        var result = await manager.ValidateAsync(profile, cancellationToken) ?? new ValidationResultDetails();

        var stored = string.IsNullOrEmpty(profile.ItemId)
            ? null
            : await manager.FindByIdAsync(profile.ItemId, cancellationToken);

        var relaxed = GetRelaxedCompliance(stored, profile);

        if (relaxed.Count > 0)
        {
            result.Fail(new ValidationResult(
                $"A dialer profile's compliance rules are not relaxed from the chat ({string.Join("; ", relaxed)}): the calls it would allow reach real customers and cannot be taken back, and this save cannot ask anyone to confirm it. A person makes this change on the Dialer profiles admin page; making the rules stricter is allowed here.",
                [nameof(DialerProfile.RespectDoNotCall)]));
        }

        var callerId = profile.CallerId?.Trim();

        if (!string.IsNullOrEmpty(callerId) &&
            (stored is null || !string.Equals(stored.CallerId?.Trim(), callerId, StringComparison.OrdinalIgnoreCase)))
        {
            var addresses = services.GetService<IOmnichannelChannelEndpointManager>();
            var choices = addresses is null
                ? new List<string>()
                : (await addresses.GetCallerIdOptionsAsync(null, cancellationToken)).Select(choice => choice.Value).ToList();

            if (!choices.Contains(callerId, StringComparer.OrdinalIgnoreCase))
            {
                result.Fail(new ValidationResult(
                    choices.Count == 0
                        ? $"The caller ID '{callerId}' is not one of the business's own phone numbers, and none is set up: a caller ID is picked from the addresses with the phone capability."
                        : $"The caller ID '{callerId}' is not one of the business's own phone numbers. Use one of: {string.Join(", ", choices)}.",
                    [nameof(DialerProfile.CallerId)]));
            }
        }

        if (string.IsNullOrEmpty(profile.CallingCalendarId) ||
            (stored is not null && string.Equals(stored.CallingCalendarId, profile.CallingCalendarId, StringComparison.OrdinalIgnoreCase)))
        {
            return result;
        }

        var calendars = services.GetService<INamedCatalogManager<BusinessHoursSchedule>>();

        if (calendars is null || await calendars.FindByIdAsync(profile.CallingCalendarId, cancellationToken) is null)
        {
            result.Fail(new ValidationResult(
                $"No business-hours calendar has the ID '{profile.CallingCalendarId}'. Use listRecords with the kind {BusinessHoursCalendarKind} to find one.",
                [nameof(DialerProfile.CallingCalendarId)]));
        }

        return result;
    }

    /// <summary>
    /// Lists the compliance rules a save relaxes: do-not-call lists no longer respected, the calling window, the
    /// abandonment cap or the abandoned-call message no longer enforced, or a higher abandonment cap. A new profile
    /// relaxes only by ignoring the do-not-call lists, which a new profile respects by default.
    /// </summary>
    /// <param name="before">The stored profile, or <see langword="null"/> for a new one.</param>
    /// <param name="after">The profile being saved.</param>
    /// <returns>What is relaxed; empty when nothing is.</returns>
    internal static List<string> GetRelaxedCompliance(DialerProfile before, DialerProfile after)
    {
        var relaxed = new List<string>();

        if ((before?.RespectDoNotCall ?? true) && !after.RespectDoNotCall)
        {
            relaxed.Add("do-not-call lists no longer respected");
        }

        if (before is null)
        {
            return relaxed;
        }

        if (before.EnforceCallingWindow && !after.EnforceCallingWindow)
        {
            relaxed.Add("the calling window no longer enforced");
        }

        if (before.EnforceAbandonmentCap && !after.EnforceAbandonmentCap)
        {
            relaxed.Add("the abandonment rate cap no longer enforced");
        }
        else if (before.EnforceAbandonmentCap && after.EnforceAbandonmentCap && after.MaxAbandonmentRatePercent > before.MaxAbandonmentRatePercent)
        {
            relaxed.Add("a higher abandonment rate cap");
        }

        if (before.SafeHarborEnabled && !after.SafeHarborEnabled)
        {
            relaxed.Add("the abandoned-call message no longer played");
        }

        return relaxed;
    }

    private static object DescribeMenu(IvrFlow flow)
        => flow is null
            ? null
            : new
            {
                rootNodeId = flow.RootNodeId,
                maxRetries = flow.MaxRetries,
                fallbackAction = DescribeAction(flow.FallbackAction),
                nodes = (flow.Nodes ?? [])
                    .Where(node => node is not null)
                    .Select(node => new
                    {
                        nodeId = node.NodeId,
                        prompt = node.Prompt,
                        promptMediaId = node.PromptMediaId,
                        options = (node.Options ?? [])
                            .Where(option => option is not null)
                            .Select(option => new
                            {
                                digit = option.Digit,
                                action = DescribeAction(option.Action),
                            })
                            .ToList(),
                    })
                    .ToList(),
            };

    private static object DescribeAction(IvrAction action)
        => action is null
            ? null
            : new
            {
                kind = action.Kind.ToString(),
                targetId = action.TargetId,
            };
}
