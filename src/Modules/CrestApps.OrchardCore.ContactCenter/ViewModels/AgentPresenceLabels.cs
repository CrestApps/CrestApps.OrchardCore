using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.Localization;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// How an agent's presence reads on the agent's own screens when they are first rendered. The script in
/// <c>shared/agent-presence.js</c> relabels them on every change from the same labels and the same rules.
/// </summary>
public static class AgentPresenceLabels
{
    private const string ReasonPlaceholder = "{0}";

    /// <summary>
    /// Builds the localized labels, keyed as the script reads them.
    /// </summary>
    /// <param name="T">The localizer of the view rendering them.</param>
    public static IReadOnlyDictionary<string, string> Create(IHtmlLocalizer T)
    {
        ArgumentNullException.ThrowIfNull(T);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["offline"] = T["Offline"].Value,
            ["available"] = T["Available"].Value,
            ["reserved"] = T["Reserved"].Value,
            ["busy"] = T["On a call"].Value,
            ["wrapUp"] = T["Wrap-up"].Value,
            ["break"] = T["Break"].Value,
            ["breakPending"] = T["Break pending"].Value,
            ["requestBreak"] = T["Request break"].Value,

            // Left unformatted: the placeholder is filled with the waiting break's reason, here and by the script.
            ["breakPendingWithReason"] = T["Break pending: {0}"].Value,
            ["away"] = T["Away"].Value,
            ["doNotDisturb"] = T["Do not disturb"].Value,
            ["meeting"] = T["Meeting"].Value,
            ["training"] = T["Training"].Value,
            ["afterHoursUnavailable"] = T["After-hours unavailable"].Value,
        };
    }

    /// <summary>
    /// Describes the agent's current state.
    /// </summary>
    /// <remarks>
    /// Only a not-ready state the agent chose shows its reason. A reason left on the profile while the agent is
    /// reserved, on a call or in wrap-up belongs to a break still waiting to start, and a reason-less break reads as
    /// just "Break", as the audit records it -- never as whichever break reason happens to be listed first.
    /// </remarks>
    /// <param name="status">The current state.</param>
    /// <param name="reason">The reason on the agent's profile.</param>
    /// <param name="labels">The labels from <see cref="Create(IHtmlLocalizer)"/>.</param>
    public static string Describe(AgentPresenceStatus status, string reason, IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        if (ShowsReason(status) && !string.IsNullOrWhiteSpace(reason))
        {
            return reason.Trim();
        }

        return labels[status switch
        {
            AgentPresenceStatus.Available => "available",
            AgentPresenceStatus.Reserved => "reserved",
            AgentPresenceStatus.Busy => "busy",
            AgentPresenceStatus.WrapUp => "wrapUp",
            AgentPresenceStatus.Break => "break",
            AgentPresenceStatus.RequestBreak => "breakPending",
            AgentPresenceStatus.Away => "away",
            AgentPresenceStatus.DoNotDisturb => "doNotDisturb",
            AgentPresenceStatus.Meeting => "meeting",
            AgentPresenceStatus.Training => "training",
            AgentPresenceStatus.AfterHoursUnavailable => "afterHoursUnavailable",
            _ => "offline",
        }];
    }

    /// <summary>
    /// Describes a break waiting for the current work to end, or returns <see langword="null"/> when none is waiting.
    /// </summary>
    /// <param name="status">The current state.</param>
    /// <param name="requestedStatus">The state waiting to take effect.</param>
    /// <param name="reason">The reason on the agent's profile, which is the waiting break's.</param>
    /// <param name="labels">The labels from <see cref="Create(IHtmlLocalizer)"/>.</param>
    public static string DescribePending(
        AgentPresenceStatus status,
        AgentPresenceStatus? requestedStatus,
        string reason,
        IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        if (requestedStatus != AgentPresenceStatus.Break ||
            status is AgentPresenceStatus.Break or AgentPresenceStatus.RequestBreak)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(reason)
            ? labels["breakPending"]
            : labels["breakPendingWithReason"].Replace(ReasonPlaceholder, reason.Trim(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Names the presence menu's break choice: "Break" when picking one starts the break now, "Request break" while
    /// the agent's work holds it until that work ends.
    /// </summary>
    /// <remarks>
    /// The same rule the presence manager applies to a break request: a reserved, busy or wrapping-up agent, or one
    /// holding an offer, gets the break when the work ends; anybody else gets it at once.
    /// </remarks>
    /// <param name="status">The current state.</param>
    /// <param name="hasActiveReservation">Whether the agent holds an offer.</param>
    /// <param name="labels">The labels from <see cref="Create(IHtmlLocalizer)"/>.</param>
    public static string DescribeBreakChoice(
        AgentPresenceStatus status,
        bool hasActiveReservation,
        IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        var waitsForWork = hasActiveReservation ||
            status is AgentPresenceStatus.Reserved or AgentPresenceStatus.Busy or AgentPresenceStatus.WrapUp;

        return labels[waitsForWork ? "requestBreak" : "break"];
    }

    private static bool ShowsReason(AgentPresenceStatus status)
        => status is AgentPresenceStatus.Break
            or AgentPresenceStatus.Away
            or AgentPresenceStatus.DoNotDisturb
            or AgentPresenceStatus.Meeting
            or AgentPresenceStatus.Training
            or AgentPresenceStatus.AfterHoursUnavailable;
}
