using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.ServiceLevels.Navigation;

/// <summary>
/// Adds Service Levels to the Configuration menu.
/// </summary>
public sealed class ServiceLevelsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceLevelsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ServiceLevelsAdminMenu(IStringLocalizer<ServiceLevelsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        // Policies serve several kinds of work (work orders, tickets), so they sit under Tools rather than under any one of
        // the modules that use them; Settings is for site settings.
        builder.Add(S["Tools"], tools => tools
            .Add(S["Service Levels"], S["Service Levels"].PrefixPosition(), levels => levels
                .Id("serviceLevelPolicies")
                .Action("Index", "Policies", ServiceLevelsConstants.Features.ServiceLevels)
                .Permission(ServiceLevelsPermissions.ManageServiceLevelPolicies)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
