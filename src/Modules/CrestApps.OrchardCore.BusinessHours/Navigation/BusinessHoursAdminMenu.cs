using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.BusinessHours.Navigation;

/// <summary>
/// Adds Business Hours to the Configuration menu.
/// </summary>
public sealed class BusinessHoursAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessHoursAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public BusinessHoursAdminMenu(IStringLocalizer<BusinessHoursAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        // Schedules are shared by queues, service levels and automations, so they sit under Configuration rather than
        // under any one of the modules that read them.
        builder.Add(S["Configuration"], configuration => configuration
            .Add(S["Business Hours"], S["Business Hours"].PrefixPosition(), hours => hours
                .Id("businessHoursSchedules")
                .Action("Index", "Schedules", BusinessHoursConstants.Features.BusinessHours)
                .Permission(BusinessHoursPermissions.ManageBusinessHoursSchedules)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
