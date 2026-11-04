using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Navigation;

/// <summary>
/// Adds the contact center agents page to the Workforce menu.
/// </summary>
public sealed class WorkforceContactCenterAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkforceContactCenterAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkforceContactCenterAdminMenu(IStringLocalizer<WorkforceContactCenterAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Workforce"], S["Workforce"].PrefixPosition("6"), workforce => workforce
            .Add(S["Contact Center Agents"], S["Contact Center Agents"].PrefixPosition("12"), agents => agents
                .Id("workforceAgents")
                .Action("Index", "Agents", WorkforceContactCenterConstants.Features.ContactCenter)
                .Permission(WorkforcePermissions.ManageWorkers)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
