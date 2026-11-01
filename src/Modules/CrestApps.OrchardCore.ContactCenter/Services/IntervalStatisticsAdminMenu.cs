using CrestApps.OrchardCore.ContactCenter.Core;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Adds the interval statistics status page to the Contact Center management menu.
/// </summary>
public sealed class IntervalStatisticsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntervalStatisticsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public IntervalStatisticsAdminMenu(IStringLocalizer<IntervalStatisticsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Interaction Center"], "80", interactionCenter => interactionCenter
                .Add(S["Management"], S["Management"].PrefixPosition(), management => management
                    .Add(S["Interval statistics"], S["Interval statistics"].PrefixPosition(), statistics => statistics
                        .Id("contactCenterIntervalStatistics")
                        .Action("Index", "IntervalStatistics", "CrestApps.OrchardCore.ContactCenter")
                        .Permission(ContactCenterPermissions.ViewReports)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
