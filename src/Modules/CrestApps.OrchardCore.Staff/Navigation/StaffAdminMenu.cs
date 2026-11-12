using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Staff.Navigation;

/// <summary>
/// Adds the Staff menu: the staff list and, when it is on, the organization chart.
/// </summary>
public sealed class StaffAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StaffAdminMenu(IStringLocalizer<StaffAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Staff"], S["Staff"].PrefixPosition("5"), staff => staff
            .AddClass("staff")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-id-card-clip")
            .Id("staff")
            .Add(S["All Staff"], S["All Staff"].PrefixPosition("10"), all => all
                .Id("staffAll")
                .Action("Index", "Staff", StaffConstants.Features.Staff)
                .Permission(StaffPermissions.ViewStaff)
                .LocalNav())
            .Add(S["My Profile"], S["My Profile"].PrefixPosition("05"), me => me
                .Id("staffMe")
                .Action("Me", "Staff", StaffConstants.Features.Staff)
                .Permission(StaffPermissions.ViewOwnStaffProfile)
                .LocalNav()));

        builder.Add(S["Configuration"], configuration => configuration
            .Add(S["Settings"], settings => settings
                .Add(S["Staff"], S["Staff"].PrefixPosition(), staff => staff
                    .Id("staffSettings")
                    .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", Drivers.StaffSettingsDisplayDriver.GroupId } })
                    .Permission(StaffPermissions.ManageStaff)
                    .LocalNav())));

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Adds the organization chart to the Staff menu.
/// </summary>
public sealed class StaffOrgChartAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StaffOrgChartAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StaffOrgChartAdminMenu(IStringLocalizer<StaffOrgChartAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Staff"], staff => staff
            .Add(S["Organization Chart"], S["Organization Chart"].PrefixPosition("20"), chart => chart
                .Id("staffOrgChart")
                .Action("OrgChart", "Staff", StaffConstants.Features.Staff)
                .Permission(StaffPermissions.ViewStaff)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
