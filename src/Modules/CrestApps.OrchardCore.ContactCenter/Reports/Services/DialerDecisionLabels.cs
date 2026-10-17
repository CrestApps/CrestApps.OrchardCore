using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Words the kind and the reason of a dialer decision for the decision log report and its reason filter.
/// </summary>
/// <remarks>
/// A reason is stored as routing or the dialer wrote it: the routing outcome in English, or the suppression reason's
/// name. The filter offers each one under its words in the reader's language while keeping the stored value, so the
/// filter matches the indexed column exactly.
/// </remarks>
internal static class DialerDecisionLabels
{
    /// <summary>
    /// Names the kind of a decision.
    /// </summary>
    /// <param name="eventType">The decision's event type.</param>
    /// <param name="S">The string localizer.</param>
    /// <returns>The words for the kind.</returns>
    public static string Kind(string eventType, IStringLocalizer S)
        => eventType switch
        {
            ContactCenterConstants.Events.RoutingDecisionMade => S["Routing"].Value,
            ContactCenterConstants.Events.DialSuppressed => S["Suppression"].Value,
            _ => eventType ?? string.Empty,
        };

    /// <summary>
    /// Words a stored reason.
    /// </summary>
    /// <param name="reason">The reason as it is stored.</param>
    /// <param name="S">The string localizer.</param>
    /// <returns>The words for the reason, or the stored value when it is not a known reason.</returns>
    public static string Reason(string reason, IStringLocalizer S)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return S["(No reason)"].Value;
        }

        return reason switch
        {
            ActivityRoutingReasons.NoAgentsAvailable => S["No agents available"].Value,
            ActivityRoutingReasons.NoAgentMatchedPolicy => S["No available agent matched the routing policy"].Value,
            ActivityRoutingReasons.ReservationLost => S["The agent or the record was taken before the reservation"].Value,
            ActivityRoutingReasons.Selected => S["Agent selected"].Value,
            _ => Enum.TryParse<DialerSuppressionReason>(reason, ignoreCase: false, out var suppression) && Enum.IsDefined(suppression)
                ? Suppression(suppression, S)
                : reason,
        };
    }

    /// <summary>
    /// Builds the reason filter's options: every routing outcome, then every suppression reason.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    /// <returns>The options, each with the stored reason as its value.</returns>
    public static IList<SelectListItem> Options(IStringLocalizer S)
    {
        var routing = new SelectListGroup { Name = S["Routing"].Value };
        var suppression = new SelectListGroup { Name = S["Suppression"].Value };
        var options = new List<SelectListItem>();

        foreach (var reason in ActivityRoutingReasons.All)
        {
            options.Add(new SelectListItem(Reason(reason, S), reason) { Group = routing });
        }

        options.Add(Option(DialerSuppressionReason.NoDestination, suppression, S));
        options.Add(Option(DialerSuppressionReason.MaxAttemptsReached, suppression, S));
        options.Add(Option(DialerSuppressionReason.RetryCoolDown, suppression, S));
        options.Add(Option(DialerSuppressionReason.DoNotCall, suppression, S));
        options.Add(Option(DialerSuppressionReason.NationalDoNotCallRegistry, suppression, S));
        options.Add(Option(DialerSuppressionReason.OutsideCallingWindow, suppression, S));
        options.Add(Option(DialerSuppressionReason.AbandonmentRateExceeded, suppression, S));
        options.Add(Option(DialerSuppressionReason.ComplianceScreeningUnavailable, suppression, S));
        options.Add(Option(DialerSuppressionReason.NumberNotInService, suppression, S));

        return options;
    }

    private static SelectListItem Option(DialerSuppressionReason reason, SelectListGroup group, IStringLocalizer S)
        => new(Suppression(reason, S), reason.ToString())
        {
            Group = group,
        };

    private static string Suppression(DialerSuppressionReason reason, IStringLocalizer S)
        => reason switch
        {
            DialerSuppressionReason.NoDestination => S["No number to dial"].Value,
            DialerSuppressionReason.MaxAttemptsReached => S["Out of attempts"].Value,
            DialerSuppressionReason.RetryCoolDown => S["Waiting out the retry delay"].Value,
            DialerSuppressionReason.DoNotCall => S["Do not call"].Value,
            DialerSuppressionReason.NationalDoNotCallRegistry => S["On the national do-not-call registry"].Value,
            DialerSuppressionReason.OutsideCallingWindow => S["Outside the calling window"].Value,
            DialerSuppressionReason.AbandonmentRateExceeded => S["Abandonment rate cap reached"].Value,
            DialerSuppressionReason.ComplianceScreeningUnavailable => S["Compliance check unavailable"].Value,
            DialerSuppressionReason.NumberNotInService => S["Number not in service"].Value,
            _ => reason.ToString(),
        };
}
