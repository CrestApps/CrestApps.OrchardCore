using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Assets.Navigation;

/// <summary>
/// Adds the asset pages to the admin menu.
/// </summary>
public sealed class AssetsAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AssetsAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public AssetsAdminMenu(IStringLocalizer<AssetsAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Assets"], S["Assets"].PrefixPosition("7"), assets => assets
            .AddClass("assets")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-toolbox")
            .Id("assets")
            .Add(S["Assets"], S["Assets"].PrefixPosition("10"), list => list
                .Id("assetsList")
                .Action("Index", "Assets", AssetsConstants.Features.Assets)
                .Permission(AssetsPermissions.ViewAssets)
                .LocalNav())
            .Add(S["Asset Types"], S["Asset Types"].PrefixPosition("20"), types => types
                .Id("assetTypes")
                .Action("Index", "AssetTypes", AssetsConstants.Features.Assets)
                .Permission(AssetsPermissions.ManageAssets)
                .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
