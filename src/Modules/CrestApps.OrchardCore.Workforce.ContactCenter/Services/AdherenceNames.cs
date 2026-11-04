using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services;

/// <summary>
/// The display names of activities, agent states and exception reasons.
/// </summary>
public static class AdherenceNames
{
    /// <summary>
    /// Gets the name of a kind of activity.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    public static string Kind(IStringLocalizer S, ActivityKind kind) => kind switch
    {
        ActivityKind.Work => S["Work"],
        ActivityKind.Break => S["Break"],
        ActivityKind.Meal => S["Meal"],
        ActivityKind.Meeting => S["Meeting"],
        ActivityKind.Training => S["Training"],
        ActivityKind.Coaching => S["Coaching"],
        ActivityKind.OfflineWork => S["Offline work"],
        ActivityKind.Travel => S["Travel"],
        ActivityKind.OnCall => S["On call"],
        ActivityKind.TimeOff => S["Time off"],
        _ => S["Other"],
    };

    /// <summary>
    /// Gets the name of an agent state.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <param name="status">The state.</param>
    /// <returns>The name.</returns>
    public static string Status(IStringLocalizer S, AgentPresenceStatus status) => status switch
    {
        AgentPresenceStatus.Offline => S["Signed out"],
        AgentPresenceStatus.Available => S["Available"],
        AgentPresenceStatus.Reserved => S["Offered work"],
        AgentPresenceStatus.Busy => S["Busy"],
        AgentPresenceStatus.WrapUp => S["Wrap-up"],
        AgentPresenceStatus.Break => S["Break"],
        AgentPresenceStatus.RequestBreak => S["Break requested"],
        AgentPresenceStatus.Away => S["Away"],
        AgentPresenceStatus.DoNotDisturb => S["Do not disturb"],
        AgentPresenceStatus.Meeting => S["Meeting"],
        AgentPresenceStatus.Training => S["Training"],
        AgentPresenceStatus.AfterHoursUnavailable => S["After hours"],
        _ => status.ToString(),
    };

    /// <summary>
    /// Gets the name of an exception reason.
    /// </summary>
    /// <param name="S">The localizer.</param>
    /// <param name="reason">The reason.</param>
    /// <returns>The name.</returns>
    public static string Reason(IStringLocalizer S, AdherenceExceptionReason reason) => reason switch
    {
        AdherenceExceptionReason.SystemOutage => S["System outage"],
        AdherenceExceptionReason.Coaching => S["Coaching"],
        AdherenceExceptionReason.ApprovedScheduleChange => S["Approved schedule change"],
        _ => S["Other"],
    };
}
