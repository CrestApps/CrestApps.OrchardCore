using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// The decisions about a call the soft phone placed itself: a <see cref="TelephonyInteraction"/> with a call id but no
/// provider identity, which the platform never saw and cannot look up. The phone's reports are the only evidence of
/// it -- that it is still up (<see cref="ClientRecordedCallActivity"/>), and that it ended -- so its end is settled
/// either from the phone's report or, once the phone has gone quiet for long enough, from the last report heard.
/// </summary>
internal static class ClientRecordedCallPolicy
{
    /// <summary>
    /// Whether the interaction is one the client recorded itself.
    /// </summary>
    public static bool IsClientRecorded(TelephonyInteraction interaction)
        => interaction is not null &&
            !string.IsNullOrWhiteSpace(interaction.CallId) &&
            string.IsNullOrWhiteSpace(interaction.ProviderName);

    /// <summary>
    /// When the phone was last heard from about the call: its last report, else when the call started.
    /// </summary>
    public static DateTime LastHeardUtc(TelephonyInteraction interaction)
    {
        var lastReportedUtc = ActivityOf(interaction)?.LastReportedUtc;

        return lastReportedUtc.HasValue && lastReportedUtc.Value > interaction.StartedUtc
            ? lastReportedUtc.Value
            : interaction.StartedUtc;
    }

    /// <summary>
    /// Whether the phone has not reported the call, still in progress, for longer than <paramref name="silenceTimeout"/>.
    /// </summary>
    public static bool HasGoneSilent(TelephonyInteraction interaction, DateTime utcNow, TimeSpan silenceTimeout)
        => IsClientRecorded(interaction) &&
            interaction.Outcome == CallOutcome.InProgress &&
            interaction.StartedUtc != default &&
            utcNow - LastHeardUtc(interaction) > silenceTimeout;

    /// <summary>
    /// Records a report that the call is still up, and when it first connected. Returns whether anything changed.
    /// </summary>
    public static bool MarkAlive(TelephonyInteraction interaction, bool connected, DateTime utcNow)
    {
        if (!IsClientRecorded(interaction) || interaction.Outcome != CallOutcome.InProgress)
        {
            return false;
        }

        var activity = ActivityOf(interaction) ?? new ClientRecordedCallActivity();

        activity.LastReportedUtc = utcNow;

        if (connected && activity.ConnectedUtc is null)
        {
            activity.ConnectedUtc = utcNow;
        }

        interaction.Put(activity);

        return true;
    }

    /// <summary>
    /// Settles the call from the phone's report of its end. A call the phone's earlier reports saw connected is
    /// completed even when the end says otherwise: a page that went away mid-call can only guess. Returns whether the
    /// call was still in progress.
    /// </summary>
    public static bool SettleReported(TelephonyInteraction interaction, bool connected, DateTime utcNow)
    {
        if (interaction is null || interaction.Outcome != CallOutcome.InProgress)
        {
            return false;
        }

        Settle(interaction, connected || ActivityOf(interaction)?.ConnectedUtc is not null, utcNow);

        return true;
    }

    /// <summary>
    /// Settles a call the phone stopped reporting, as of the last moment it was heard from. Returns whether the call was
    /// still in progress.
    /// </summary>
    public static bool SettleUnreported(TelephonyInteraction interaction)
    {
        if (interaction is null || interaction.Outcome != CallOutcome.InProgress)
        {
            return false;
        }

        var activity = ActivityOf(interaction) ?? new ClientRecordedCallActivity();

        Settle(interaction, activity.ConnectedUtc is not null, LastHeardUtc(interaction));

        activity.EndedUnreported = true;
        interaction.Put(activity);

        return true;
    }

    private static void Settle(TelephonyInteraction interaction, bool connected, DateTime endedUtc)
    {
        interaction.Outcome = connected ? CallOutcome.Completed : CallOutcome.Canceled;
        interaction.EndedUtc = endedUtc;
        interaction.DurationSeconds = Math.Max(0, (endedUtc - interaction.StartedUtc).TotalSeconds);
    }

    private static ClientRecordedCallActivity ActivityOf(TelephonyInteraction interaction)
        => interaction is not null && interaction.TryGet<ClientRecordedCallActivity>(out var activity) ? activity : null;
}
