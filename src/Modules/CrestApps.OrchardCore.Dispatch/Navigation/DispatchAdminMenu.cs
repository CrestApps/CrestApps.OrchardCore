using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Dispatch.Navigation;

/// <summary>
/// Adds the dispatch board and the visits under Field Service, and the dispatch settings.
/// </summary>
public sealed class DispatchAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DispatchAdminMenu"/> class.
    /// </summary>
    public DispatchAdminMenu(IStringLocalizer<DispatchAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Field Service"], fieldService => fieldService
                .Add(S["Dispatch Board"], S["Dispatch Board"].PrefixPosition("05"), board => board
                    .Id("dispatchBoard")
                    .Action("Index", "DispatchBoard", DispatchConstants.Features.Dispatch)
                    .Permission(DispatchPermissions.ViewDispatch)
                    .LocalNav())
                .Add(S["Visits"], S["Visits"].PrefixPosition("15"), visits => visits
                    .Id("dispatchVisits")
                    .Action("Index", "Visits", DispatchConstants.Features.Dispatch)
                    .Permission(DispatchPermissions.ViewDispatch)
                    .LocalNav()))
            .Add(S["Reports"], "after.40", reports => reports
                .Add(S["Field Service"], S["Field Service"].PrefixPosition(), category => category
                    .AddClass("report-category")
                    .Add(S["KPIs"], S["KPIs"].PrefixPosition("10"), kpis => kpis
                        .Id("fieldServiceKpis")
                        .Action("Index", "FieldServiceKpis", DispatchConstants.Features.Dispatch)
                        .Permission(DispatchPermissions.ViewDispatch)
                        .LocalNav())))
            .Add(S["Settings"], settings => settings
                .Add(S["Dispatch"], S["Dispatch"].PrefixPosition(), dispatch => dispatch
                    .Id("dispatchSettings")
                    .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", DispatchConstants.SettingsGroupId } })
                    .Permission(DispatchPermissions.ManageDispatchSettings)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
