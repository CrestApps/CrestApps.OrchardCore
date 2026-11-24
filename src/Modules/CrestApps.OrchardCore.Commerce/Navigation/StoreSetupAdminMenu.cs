using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Commerce.Navigation;

/// <summary>
/// Adds Commerce › Set Up Your Store, the store's setup guide. The Commerce feature, the store layer, registers it, so a
/// site with billing alone has the Commerce menu without the guide.
/// </summary>
public sealed class StoreSetupAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreSetupAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public StoreSetupAdminMenu(IStringLocalizer<StoreSetupAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder
            .Add(S["Commerce"], "15", commerce => commerce
                .Id("commerce")
                .Add(S["Set Up Your Store"], S["Set Up Your Store"].PrefixPosition("0"), setup => setup
                    .Id("commerceSetup")
                    .Action("Index", "StoreSetup", new { area = CommerceConstants.Features.Area })
                    .Permission(CommercePermissions.SetUpStore)
                    .LocalNav()));

        return ValueTask.CompletedTask;
    }
}
