using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.Navigation;

/// <summary>
/// Adds the Time Off, Time-Off Types, Time-Off Policies, Allotments and Blackout Dates items to the Workforce menu.
/// </summary>
public sealed class TimeOffAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimeOffAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TimeOffAdminMenu(IStringLocalizer<TimeOffAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], workforce => workforce
            .Add(S["Time Off"], S["Time Off"].PrefixPosition("23"), timeOff => timeOff
                .Id("workforceTimeOff")
                .Action("Index", "TimeOff", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ApproveTimeOff)
                .LocalNav())
            .Add(S["Time-Off Types"], S["Time-Off Types"].PrefixPosition("81"), types => types
                .Id("workforceTimeOffTypes")
                .Action("Index", "TimeOffTypes", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTimeOffPolicies)
                .LocalNav())
            .Add(S["Time-Off Policies"], S["Time-Off Policies"].PrefixPosition("82"), policies => policies
                .Id("workforceTimeOffPolicies")
                .Action("Index", "TimeOffPolicies", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTimeOffPolicies)
                .LocalNav())
            .Add(S["Time-Off Allotments"], S["Time-Off Allotments"].PrefixPosition("83"), allotments => allotments
                .Id("workforceTimeOffAllotments")
                .Action("Index", "TimeOffAllotments", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTimeOffPolicies)
                .LocalNav())
            .Add(S["Blackout Dates"], S["Blackout Dates"].PrefixPosition("84"), blackouts => blackouts
                .Id("workforceBlackoutDates")
                .Action("Index", "BlackoutDates", WorkforceConstants.Features.Workforce)
                .Permission(WorkforcePermissions.ManageTimeOffPolicies)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
