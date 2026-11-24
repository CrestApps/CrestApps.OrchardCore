using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Dispatch.Navigation;

/// <summary>
/// Adds the field service KPIs to the Reports menu. Registered only while the Reports module is on, since Reports owns
/// that menu and its icon.
/// </summary>
public sealed class FieldServiceReportsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="FieldServiceReportsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public FieldServiceReportsAdminMenu(IStringLocalizer<FieldServiceReportsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Reports"], "after.40", reports => reports
                .Add(S["Field Service"], S["Field Service"].PrefixPosition(), category => category
                    .AddClass("report-category")
                    .Add(S["KPIs"], S["KPIs"].PrefixPosition("10"), kpis => kpis
                        .Id("fieldServiceKpis")
                        .Action("Index", "FieldServiceKpis", DispatchConstants.Features.Dispatch)
                        .Permission(DispatchPermissions.ViewDispatch)
                        .LocalNav())));

        return ValueTask.CompletedTask;
    }
}
