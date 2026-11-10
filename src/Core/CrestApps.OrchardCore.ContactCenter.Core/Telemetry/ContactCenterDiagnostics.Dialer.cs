using System.Diagnostics;
using System.Diagnostics.Metrics;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.ContactCenter.Core.Telemetry;

/// <summary>
/// The dialer and the provider command pipeline: pacing cycles, attempts and their ends, abandonment, predictive
/// connects, and the durable commands that carry a call to the provider.
/// </summary>
public static partial class ContactCenterDiagnostics
{
    /// <summary>
    /// The name of the span of one dispatch of a provider command. It links to the trace that registered the command.
    /// </summary>
    internal const string ProviderCommandSpanName = "crestapps.contactcenter.provider_command.dispatch";

    /// <summary>
    /// The name of the span of one dialer attempt: deciding whether a record may be dialed, and dialing it.
    /// </summary>
    internal const string DialerAttemptSpanName = "crestapps.contactcenter.dialer.attempt";

    private static readonly Counter<long> _dialerPacingCycles = Meter.CreateCounter<long>(
        "crestapps.contactcenter.dialer.pacing.cycle",
        "{cycle}",
        "The number of dialer pacing cycles, by how each ended and the dialer mode.");

    private static readonly Histogram<double> _dialerPacingCycleDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.dialer.pacing.cycle.duration",
        "The time a dialer pacing cycle that took its queue's pacing lock spent pacing, by outcome and dialer mode.");

    private static readonly Counter<long> _dialerPlaced = Meter.CreateCounter<long>(
        "crestapps.contactcenter.dialer.pacing.placed",
        "{call}",
        "The number of calls dialer pacing cycles placed, by dialer mode.");

    private static readonly Counter<long> _dialerLockStarved = Meter.CreateCounter<long>(
        "crestapps.contactcenter.dialer.pacing.lock_starved",
        "{warning}",
        "The number of times a campaign queue was found not paced because its pacing lock stayed out of reach.");

    private static readonly Counter<long> _dialerAttempts = Meter.CreateCounter<long>(
        "crestapps.contactcenter.dialer.attempt",
        "{attempt}",
        "The number of dialer attempts, by dialer mode, result and, for a suppressed attempt, why.");

    private static readonly Counter<long> _dialerAttemptsCompleted = Meter.CreateCounter<long>(
        "crestapps.contactcenter.dialer.attempt.completed",
        "{attempt}",
        "The number of dialer attempts whose call ended and was dispositioned, by result.");

    private static readonly Counter<long> _dialerAbandoned = Meter.CreateCounter<long>(
        "crestapps.contactcenter.dialer.abandoned",
        "{call}",
        "The number of answered dialer calls abandoned because no agent reached the person in time.");

    private static readonly Histogram<double> _predictiveConnectDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.dialer.connect.duration",
        "The time from when a person answered an over-dialed call to when an agent was connected to them.");

    private static readonly Counter<long> _predictiveOverdial = Meter.CreateCounter<long>(
        "crestapps.contactcenter.dialer.overdial",
        "{call}",
        "The number of calls predictive pacing placed beyond the agents free to take them.");

    private static readonly Counter<long> _dialerEligibility = Meter.CreateCounter<long>(
        "crestapps.contactcenter.dialer.eligibility",
        "{check}",
        "The number of dialer eligibility checks, by result and, for an ineligible record, the reason.");

    private static readonly Histogram<double> _dncLookupDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.dialer.dnc.lookup.duration",
        "The time a do-not-call lookup took on the dial path, by result.");

    private static readonly Histogram<double> _providerCommandDuration = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
        Meter,
        "crestapps.contactcenter.provider_command.dispatch.duration",
        "The time one dispatch of a provider command took, by provider and command type.");

    private static readonly Counter<long> _providerCommandOutcomes = Meter.CreateCounter<long>(
        "crestapps.contactcenter.provider_command.outcome",
        "{command}",
        "The number of provider commands that reached a settled state, by provider, command type and outcome.");

    private static readonly Counter<long> _providerCommandsRecovered = Meter.CreateCounter<long>(
        "crestapps.contactcenter.provider_command.recovered",
        "{command}",
        "The number of provider commands the recovery pass picked up again after their dispatch did not settle.");

    /// <summary>
    /// Records one dialer pacing cycle.
    /// </summary>
    /// <param name="outcome">How the cycle ended.</param>
    /// <param name="mode">The dialer mode of the profile it paced, or <see langword="null"/> when it never read one.</param>
    /// <param name="elapsed">The time it spent pacing under the lock, or <see langword="null"/> when it never took the lock.</param>
    /// <param name="placed">The calls it placed.</param>
    internal static void RecordDialerPacingCycle(DialerPacingCycleOutcome outcome, DialerMode? mode, TimeSpan? elapsed, int placed = 0)
    {
        var modeTag = mode?.ToString() ?? CrestAppsOrchardCoreTelemetry.Unknown;

        Activity.Current?.AddEvent(new ActivityEvent("crestapps.contactcenter.dialer.pacing.cycle", tags: new ActivityTagsCollection
        {
            { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome.ToString() },
            { DialerModeTag, modeTag },
        }));

        if (_dialerPacingCycles.Enabled)
        {
            Add(_dialerPacingCycles, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome.ToString() },
                { DialerModeTag, modeTag },
            });
        }

        if (elapsed is { } duration && _dialerPacingCycleDuration.Enabled)
        {
            Record(_dialerPacingCycleDuration, duration.TotalSeconds, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome.ToString() },
                { DialerModeTag, modeTag },
            });
        }

        if (placed > 0 && _dialerPlaced.Enabled)
        {
            Add(_dialerPlaced, new TagList { { DialerModeTag, modeTag } }, placed);
        }
    }

    /// <summary>
    /// Records that a campaign queue was found not paced because its pacing lock stayed out of reach.
    /// </summary>
    internal static void RecordDialerLockStarved()
    {
        if (_dialerLockStarved.Enabled)
        {
            Add(_dialerLockStarved, default);
        }
    }

    /// <summary>
    /// Starts the span of one dialer attempt. The provider command it registers links back to it.
    /// </summary>
    /// <param name="mode">The dialer mode.</param>
    /// <returns>The span, or <see langword="null"/> when nothing listens.</returns>
    internal static Activity StartDialerAttempt(DialerMode mode)
    {
        var activity = ActivitySource.StartActivity(DialerAttemptSpanName, ActivityKind.Internal);

        activity?.SetTag(DialerModeTag, mode.ToString());

        return activity;
    }

    /// <summary>
    /// Records how a dialer attempt ended.
    /// </summary>
    /// <param name="activity">The span of the attempt, or <see langword="null"/>.</param>
    /// <param name="mode">The dialer mode.</param>
    /// <param name="result"><c>dialed</c>, <c>suppressed</c>, <c>deferred</c> or <c>failed</c>.</param>
    /// <param name="suppressReason">Why it was suppressed, from the bounded set of suppression reasons, or <see langword="null"/>.</param>
    internal static void RecordDialerAttempt(Activity activity, DialerMode mode, string result, string suppressReason = null)
    {
        activity?.SetTag(CrestAppsOrchardCoreTelemetry.ResultTag, result);
        activity?.SetTag(SuppressReasonTag, suppressReason);

        if (!_dialerAttempts.Enabled)
        {
            return;
        }

        Add(_dialerAttempts, new TagList
        {
            { DialerModeTag, mode.ToString() },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
            { SuppressReasonTag, suppressReason ?? "none" },
        });
    }

    /// <summary>
    /// Records that a dialer attempt's call ended and was dispositioned.
    /// </summary>
    /// <param name="result">How the attempt ended, from a bounded set such as the terminal reason codes.</param>
    internal static void RecordDialerAttemptCompleted(string result)
    {
        if (_dialerAttemptsCompleted.Enabled)
        {
            Add(_dialerAttemptsCompleted, new TagList { { CrestAppsOrchardCoreTelemetry.ResultTag, GetReasonCode(result) } });
        }
    }

    /// <summary>
    /// Records that an answered dialer call was abandoned.
    /// </summary>
    /// <param name="reason">Why, one of the dialer abandonment reasons.</param>
    internal static void RecordDialerAbandoned(string reason)
    {
        if (_dialerAbandoned.Enabled)
        {
            Add(_dialerAbandoned, new TagList { { CrestAppsOrchardCoreTelemetry.ReasonTag, GetReasonCode(reason) } });
        }
    }

    /// <summary>
    /// Records how long an answered over-dialed call waited for its agent.
    /// </summary>
    /// <param name="elapsed">The time from the answer to the connect.</param>
    internal static void RecordPredictiveConnect(TimeSpan elapsed)
    {
        if (_predictiveConnectDuration.Enabled)
        {
            Record(_predictiveConnectDuration, Math.Max(0, elapsed.TotalSeconds), default);
        }
    }

    /// <summary>
    /// Records calls placed beyond the agents free to take them.
    /// </summary>
    /// <param name="count">How many.</param>
    internal static void RecordPredictiveOverdial(long count)
    {
        if (count > 0 && _predictiveOverdial.Enabled)
        {
            Add(_predictiveOverdial, default, count);
        }
    }

    /// <summary>
    /// Records one dialer eligibility check.
    /// </summary>
    /// <param name="eligible">Whether the record may be dialed.</param>
    /// <param name="reason">Why not, a <c>DialerSuppressionReason</c> name, or <see langword="null"/>.</param>
    internal static void RecordDialerEligibility(bool eligible, string reason)
    {
        if (_dialerEligibility.Enabled)
        {
            Add(_dialerEligibility, new TagList
            {
                { CrestAppsOrchardCoreTelemetry.ResultTag, eligible ? "eligible" : "ineligible" },
                { CrestAppsOrchardCoreTelemetry.ReasonTag, eligible ? "none" : reason ?? CrestAppsOrchardCoreTelemetry.Unknown },
            });
        }
    }

    /// <summary>
    /// Records how long a do-not-call lookup on the dial path took.
    /// </summary>
    /// <param name="elapsed">The time it took.</param>
    /// <param name="result"><c>listed</c>, <c>clear</c> or <c>error</c>.</param>
    internal static void RecordDncLookup(TimeSpan elapsed, string result)
    {
        if (_dncLookupDuration.Enabled)
        {
            Record(_dncLookupDuration, elapsed.TotalSeconds, new TagList { { CrestAppsOrchardCoreTelemetry.ResultTag, result } });
        }
    }

    /// <summary>
    /// Starts the span of one dispatch of a provider command, linked to the trace that registered it.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <returns>The span, or <see langword="null"/> when nothing listens.</returns>
    internal static Activity StartProviderCommand(ProviderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var activity = TraceContextPropagation.StartLinkedActivity(ActivitySource, ProviderCommandSpanName, command.TraceParent);

        if (activity is not null)
        {
            activity.SetTag(CrestAppsOrchardCoreTelemetry.ProviderTag, GetProvider(command.ProviderName));
            activity.SetTag(CommandTypeTag, command.CommandType.ToString());
            activity.SetTag("crestapps.contactcenter.provider_command.id", command.CommandId);
            activity.SetTag("crestapps.contactcenter.interaction.id", command.InteractionId);
            activity.SetTag(StateTag, command.Status.ToString());
        }

        return activity;
    }

    /// <summary>
    /// Records one dispatch of a provider command: its duration, and the settled state it reached, if it reached one.
    /// </summary>
    /// <param name="activity">The span of the dispatch, or <see langword="null"/>.</param>
    /// <param name="command">The command as it was when the dispatch started.</param>
    /// <param name="startStatus">Its status when the dispatch started.</param>
    /// <param name="endStatus">Its status when the dispatch ended, or <see langword="null"/> when it threw.</param>
    /// <param name="elapsed">The time the dispatch took.</param>
    internal static void RecordProviderCommand(
        Activity activity,
        ProviderCommand command,
        ProviderCommandStatus startStatus,
        ProviderCommandStatus? endStatus,
        TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(command);

        var outcome = endStatus switch
        {
            ProviderCommandStatus.Confirmed => "confirmed",
            ProviderCommandStatus.OutcomeUnknown => "unknown",
            ProviderCommandStatus.Failed => "failed",
            ProviderCommandStatus.Compensated => "compensated",
            ProviderCommandStatus.Paused => "paused",
            null => CrestAppsOrchardCoreTelemetry.OutcomeError,
            _ => null,
        };

        activity?.SetTag(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome ?? endStatus?.ToString());

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ProviderTag, GetProvider(command.ProviderName) },
            { CommandTypeTag, command.CommandType.ToString() },
        };

        if (_providerCommandDuration.Enabled)
        {
            var durationTags = tags;
            durationTags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome ?? "pending");
            Record(_providerCommandDuration, elapsed.TotalSeconds, durationTags);
        }

        // Only a change of state is an outcome: a dispatch that found the command already settled, or left it where it
        // was, is not counted again.
        if (outcome is not null && endStatus != startStatus && _providerCommandOutcomes.Enabled)
        {
            tags.Add(CrestAppsOrchardCoreTelemetry.OutcomeTag, outcome);
            Add(_providerCommandOutcomes, tags);
        }
    }

    /// <summary>
    /// Records provider commands the recovery pass picked up again.
    /// </summary>
    /// <param name="count">How many.</param>
    internal static void RecordProviderCommandsRecovered(long count)
    {
        if (count > 0 && _providerCommandsRecovered.Enabled)
        {
            Add(_providerCommandsRecovered, default, count);
        }
    }
}
