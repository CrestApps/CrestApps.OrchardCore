using CrestApps.OrchardCore.TimeTracker;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Navigation;

/// <summary>
/// Adds the stages of agents' states to the Time Tracker menu, beside the stages and categories.
/// </summary>
public sealed class ContactCenterTimeStagesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterTimeStagesAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ContactCenterTimeStagesAdminMenu(IStringLocalizer<ContactCenterTimeStagesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Time Tracker"], tracker => tracker
            .Add(S["Contact Center Stages"], S["Contact Center Stages"].PrefixPosition("62"), stages => stages
                .Id("workforceContactCenterTimeStages")
                .Action("Index", "ContactCenterTimeStages", WorkforceContactCenterConstants.Features.ContactCenter)
                .Permission(TimeTrackerPermissions.ManageTimeTrackerSettings)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
