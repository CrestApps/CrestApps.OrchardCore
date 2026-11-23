using CrestApps.OrchardCore.Learning.Packages.Drivers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Learning.Packages.Services;

/// <summary>
/// Settings › Learning Packages.
/// </summary>
public sealed class PackagesAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="PackagesAdminMenu"/> class.
    /// </summary>
    public PackagesAdminMenu(IStringLocalizer<PackagesAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Settings"], settings => settings
                .Add(S["Learning Packages"], S["Learning Packages"].PrefixPosition(), packages => packages
                    .Id("learningPackages")
                    .Action("Index", "Admin", new RouteValueDictionary { { "area", "OrchardCore.Settings" }, { "groupId", LearningPackageContentHostSettingsDisplayDriver.GroupId } })
                    .Permission(LearningPackagesPermissions.ManageLearningPackageSettings)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
